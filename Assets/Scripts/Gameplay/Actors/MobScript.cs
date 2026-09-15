using Assets.Scripts;
using Assets.Scripts.ScriptableObjects;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using static Assets.Scripts.MobHandler;

public class MobScript : MonoBehaviour
{
    private Queue<NextStep> path;
    private Tile lastVisitedTile;
    private Vector3 target; // in localPos
    private bool hasTarget;
    private int pathChannel;
    private Vector3 currVelocity;
    private float maxVelocity;
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
        maxVelocity = Mathf.Sqrt(3f) * speed; // Preserve the existing velocity cap.

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
        currVelocity = Vector3.ClampMagnitude(currVelocity, maxVelocity);
        transform.localPosition = Vector3.MoveTowards(transform.localPosition, target, currVelocity.magnitude * Time.deltaTime);
    }

    public void TakeDamage()
    {
        if (!GridHelper.INSTANCE.TryGetTileOn(transform.position, out Tile currentTile)) return;
        int damagedElements = 0;
        for (int i = 0; i < 3; i++) {
            for (int j = 0; j < 3; j++) {
                int row = currentTile.row + i - 1, col = currentTile.col + j - 1;
                if (!GameLogic.INSTANCE.HasCell(row, col)) continue;
                Tile tile = GameLogic.INSTANCE.tilesGrid[row, col];
                if ((i == 1 && j == 1 || tile.type/100 == 3) &&
                    ElementDefinitions.Damage(tile.type) > 0f)
                {
                    float tileDamage = ElementDefinitions.Damage(tile.type);
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

    private void RefreshPath(bool force = false, int maxSteps = 2)
    {
        int initDisagreement = pathDisagreement;
        CalculateDisagreement();
        int currR = lastVisitedTile.row, currC = lastVisitedTile.col;
        Queue<NextStep> newPath = new Queue<NextStep>();
        if (initDisagreement >= pathDisagreement && !force) // not heading in totally wrong dir
        { // trust old path
            foreach (NextStep s in path)
            {
                currR += s.r;
                currC += s.c;
                newPath.Enqueue(s);
            }
        }
        
        while (newPath.Count < maxSteps)
        {
            NextStep next = MobHandler.INSTANCE.GetBestPath(currR, currC, wander, pathChannel);
            if (!next.IsValid) break;
            currR += next.r;
            currC += next.c;
            newPath.Enqueue(next);
        }
        path = newPath;
    }

    private void UpdateTargetComplete()
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
        if (!next.IsValid) return;
        if (!GameLogic.INSTANCE.CanStep(lastVisitedTile.row, lastVisitedTile.col,
            lastVisitedTile.row + next.r, lastVisitedTile.col + next.c)) return;
        Tile targetTile = GameLogic.INSTANCE.tilesGrid[lastVisitedTile.row + next.r, lastVisitedTile.col + next.c];
        target = targetTile.transform.localPosition;
        target.z = transform.localPosition.z;
        hasTarget = true;
        bool nearBoundary = false;
        for (int dr = -1; dr <= 1; dr++)
            for (int dc = -1; dc <= 1; dc++)
                if (!GameLogic.INSTANCE.CanStep(targetTile.row, targetTile.col,
                    targetTile.row + dr, targetTile.col + dc)) nearBoundary = true;
        if (!nearBoundary)
        {
            float jitter = Mathf.Clamp(instability, 0f, .9f) * GameLogic.INSTANCE.spacing;
            target += new Vector3((UnityEngine.Random.value - 0.5f) * jitter, (UnityEngine.Random.value - 0.5f) * jitter, 0);
        }

        if (path.Count < 2) return;
        NextStep next2 = path.ElementAt(1);

        if (!next2.IsValid) return;
        if (next.r * next2.r + next.c * next2.c != 0) return; // dot product: want 90 degree angle
        // Cutting this corner would cross the fourth cell of the 2x2 square.
        if (!GameLogic.INSTANCE.CanWalk(lastVisitedTile.row + next2.r, lastVisitedTile.col + next2.c)) return;
        // Smooth motion must not skip an intermediate half-step or cut across a cliff.
        int endR = targetTile.row + next2.r, endC = targetTile.col + next2.c;
        int sideR = lastVisitedTile.row + next2.r, sideC = lastVisitedTile.col + next2.c;
        if (!GameLogic.INSTANCE.CanStep(targetTile.row, targetTile.col, endR, endC) ||
            !GameLogic.INSTANCE.CanStep(lastVisitedTile.row, lastVisitedTile.col, sideR, sideC) ||
            !GameLogic.INSTANCE.CanStep(sideR, sideC, endR, endC) ||
            !GridHelper.INSTANCE.TestElevationLine(lastVisitedTile.row, lastVisitedTile.col, endR, endC)) return;

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

    public void RetargetAfterPlayerMove()
    {
        if (path == null || !GridHelper.INSTANCE.TryGetTileOn(transform.position, out Tile currentTile)) return;
        lastVisitedTile = currentTile;
        RefreshPath(force: true);
        SetTargetFromPath();
    }

    private void CalculateDisagreement()
    {
        int currR = lastVisitedTile.row, currC = lastVisitedTile.col;
        foreach (NextStep next in path)
        {
            if (!next.IsValid)
            {
                pathDisagreement++;
                break;
            }
            NextStep best = MobHandler.INSTANCE.GetBestPath(currR, currC, wander, pathChannel);
            if (!next.Equals(best))
            {
                pathDisagreement++;
            }
            currR += next.r;
            currC += next.c;
        }
    }
}
