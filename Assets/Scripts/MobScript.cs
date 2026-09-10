using Assets.Scripts;
using Assets.Scripts.ScriptableObjects;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using static Assets.Scripts.MobHandler;

public class MobScript : MonoBehaviour
{
    private Queue<NextStep> path;
    private Tile lastVisitedTile;
    private Vector3 target; // in localPos
    private bool hasTarget;
    private int pathChannel;
    private Vector3 currVelocity, maxVelocity; // use smoothDamp to move towards target
    private int pathDisagreement;
    private float currentHp, speed, damage, wander, instability, cornerCut, maxAcceleration;
    public void Init(EnemyData data, Tile spawnTile, int pathChannel = 0)
    {
        this.pathChannel = pathChannel;
        pathDisagreement = 0;

        currentHp = data.maxHp;
        speed = data.speed;
        damage = data.damage;
        wander = data.wander;
        instability = data.instability;
        cornerCut = data.cornerCut;
        maxAcceleration = data.maxAcceleration;
        lastVisitedTile = spawnTile;

        transform.position = lastVisitedTile.transform.position;

        currVelocity = Vector3.zero;
        maxVelocity = Vector3.one * speed;

        path = new Queue<NextStep>();

        UpdateTargetIncomplete();
    }

    void Update()
    {
        if (!hasTarget) return;
        Move();
        if (Vector3.Magnitude(target - transform.localPosition) <= 0.05f)
        {
            UpdateTargetComplete();
        }
    }

    private void Move()
    {
        Vector3 optimalVel = (target - transform.localPosition).normalized * speed;
        Vector3 diff = optimalVel - currVelocity;
        currVelocity += diff * Time.deltaTime * maxAcceleration;
        currVelocity = Vector3.ClampMagnitude(currVelocity, maxVelocity.magnitude);
        transform.localPosition = Vector3.MoveTowards(transform.localPosition, target, currVelocity.magnitude * Time.deltaTime);
    }

    public void TakeDamage()
    {
        if (!GridHelper.INSTANCE.TryGetTileOn(transform.position, out Tile currentTile)) return;
        int damagedElements = 0;
        for (int i = 0; i < 3; i++) {
            for (int j = 0; j < 3; j++) {
                int row = currentTile.row + i - 1, col = currentTile.col + j - 1;
                if (!GridHelper.IsInBounds(GameLogic.INSTANCE.tilesGrid, row, col)) continue;
                Tile tile = GameLogic.INSTANCE.tilesGrid[row, col];
                if ((i == 1 && j == 1 || tile.type/100 == 3) &&
                    Indexing.INSTANCE.damageMap.TryGetValue(tile.type, out float tileDamage))
                {
                    currentHp -= tileDamage;
                    int element = tile.type / 100;
                    if (tileDamage > 0f && element >= 1 && element <= 4) damagedElements |= 1 << element;
                }
            }
        }
        for (int element = 1; element <= 4; element++)
            if ((damagedElements & (1 << element)) != 0) ParticleVFX.INSTANCE?.PlayDamage(transform, element);
        if (currentHp < 0f) Destroy(gameObject);
    }

    public void RefreshPath(bool force = false, int maxSteps = 2)
    {
        int initDisagreement = pathDisagreement;
        CalculateDisagreement();
        int currR = lastVisitedTile.row, currC = lastVisitedTile.col;
        Queue<NextStep> newPath = new Queue<NextStep>();
        if (initDisagreement >= pathDisagreement && !force) // not heading in totally wrong dir
        { // trust old path
            while (path.Count > 0)
            {
                NextStep s = path.Dequeue();
                currR += s.r;
                currC += s.c;
                newPath.Enqueue(s);
            }
        }
        
        while (newPath.Count < maxSteps)
        {
            NextStep next = MobHandler.INSTANCE.getBestPath(currR, currC, wander, pathChannel);
            if (next == null) break;
            currR += next.r;
            currC += next.c;
            newPath.Enqueue(next);
        }
        path = newPath;
    }

    public void UpdateTargetComplete()
    {
        hasTarget = false;
        if (path == null || path.Count == 0) return;
        path.Dequeue(); // reached target
        if (!GridHelper.INSTANCE.TryGetTileOn(transform.position, out Tile currentTile)) return;
        lastVisitedTile = currentTile;
        if (currentTile.row == PlayerHandler.INSTANCE.r && currentTile.col == PlayerHandler.INSTANCE.c)
        {
            // reached end
            PlayerHandler.INSTANCE.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        RefreshPath();
        SetTargetFromPath();
    }

    private void SetTargetFromPath()
    {
        hasTarget = false;
        if (path == null || path.Count == 0) return;
        NextStep next = path.Peek();
        if (next == null) return;
        if (!GridHelper.IsInBounds(GameLogic.INSTANCE.tilesGrid,
            lastVisitedTile.row + next.r, lastVisitedTile.col + next.c)) return;
        Tile targetTile = GameLogic.INSTANCE.tilesGrid[lastVisitedTile.row + next.r, lastVisitedTile.col + next.c];
        target = targetTile.transform.localPosition;
        target.z = transform.localPosition.z;
        hasTarget = true;
        target += new Vector3((UnityEngine.Random.value - 0.5f) * instability, (UnityEngine.Random.value - 0.5f) * instability, 0);

        if (path.Count < 2) return;
        NextStep next2 = path.ElementAt(1);

        if (next2 == null) return;
        if (next.r * next2.r + next.c * next2.c != 0) return; // dot product: want 90 degree angle

        Vector3 incoming = new Vector3(next.c, -next.r, 0f);
        Vector3 outgoing = new Vector3(next2.c, -next2.r, 0f);
        target += (outgoing - incoming) * GameLogic.INSTANCE.spacing * cornerCut * 0.5f;
    }

    public void UpdateTargetIncomplete()
    {
        if (path == null) return;
        if (!GridHelper.INSTANCE.TryGetTileOn(transform.position, out Tile currentTile) ||
            !MobHandler.INSTANCE.IsReachable(currentTile.row, currentTile.col, pathChannel))
        {
            // TODO: Define behavior for mobs with no route to the player.
            hasTarget = false;
            enabled = false;
            Destroy(gameObject);
            return;
        }
        RefreshPath();
        SetTargetFromPath();
    }

    public void CalculateDisagreement()
    {
        Queue<NextStep> start = new Queue<NextStep>(path);
        int currR = lastVisitedTile.row, currC = lastVisitedTile.col;
        while (path.Count > 0)
        {
            NextStep next = path.Dequeue();
            if (next == null)
            {
                pathDisagreement++;
                break;
            }
            NextStep best = MobHandler.INSTANCE.getBestPath(currR, currC, wander, pathChannel);
            if (!next.Equals(best))
            {
                pathDisagreement++;
            }
            currR += next.r;
            currC += next.c;
        }
        path = start;
    }
}
