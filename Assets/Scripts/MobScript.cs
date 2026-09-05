using Assets.Scripts;
using Assets.Scripts.ScriptableObjects;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor.Rendering;
using UnityEngine;
using static Assets.Scripts.MobHandler;

public class MobScript : MonoBehaviour
{
    private Queue<NextStep> path;
    private Tile lastVisitedTile;
    private Vector3 target; // in localPos
    private Vector3 currVelocity, maxVelocity; // use smoothDamp to move towards target
    private int pathDisagreement;
    private float currentHp, speed, damage, wander, instability, cornerCut, maxAcceleration;


    public void Init(EnemyData data, Tile spawnTile)
    {
        pathDisagreement = 0;

        currentHp = data.maxHp;
        speed = data.speed;
        damage = data.damage;
        wander = data.wander;
        instability = data.instability;
        cornerCut = data.cornerCut;
        maxAcceleration = data.maxAcceleration;
        lastVisitedTile = spawnTile;

        currVelocity = Vector3.zero;
        maxVelocity = Vector3.one * speed;

        path = new Queue<NextStep>();

        RefreshPath();
    }

    void Update()
    {
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
            NextStep next = MobHandler.INSTANCE.getBestPath(currR, currC, wander);
            currR += next.r;
            currC += next.c;
            newPath.Enqueue(next);
        }
        path = newPath;
    }

    public void UpdateTargetComplete()
    {
        NextStep next = path.Dequeue(); // reached target
        next = path.Peek(); // next target

        if (next == null)
        {
            // reached end
            return;
        }

        lastVisitedTile = MobHandler.INSTANCE.GetTileOn(lastVisitedTile.transform.localPosition);
        RefreshPath();
        Tile targetTile = GameLogic.INSTANCE.tilesGrid[lastVisitedTile.row + next.r, lastVisitedTile.col + next.c];
        target = targetTile.transform.localPosition;
        target += new Vector3((UnityEngine.Random.value - 0.5f) * instability, (UnityEngine.Random.value - 0.5f) * instability, 0);

        NextStep next2 = path.ElementAt(1);

        if (next2 == null) return;
        
        if (next.r * next2.r + next.c * next2.c != 0) // dot product: want 90 degree angle
        {
            if (next.r != 0) 
            {
                target += new Vector3(0, -next.r * cornerCut * 0.5f, 0);
            }
            else
            {
                target += new Vector3(next.c * cornerCut * 0.5f, 0, 0);
            }
        }
    }

    public void UpdateTargetIncomplete()
    {
        RefreshPath();
    }

    public void CalculateDisagreement()
    {
        while (path.Count > 0)
        {
            NextStep next = path.Dequeue();
            NextStep best = MobHandler.INSTANCE.getBestPath(lastVisitedTile.row, lastVisitedTile.col, wander);
            if (!next.Equals(best))
            {
                pathDisagreement++;
            }
        }
    }
}
