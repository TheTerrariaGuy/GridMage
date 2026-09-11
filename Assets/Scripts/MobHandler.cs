using Assets.Scripts.ScriptableObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.Scripts
{
    public class MobHandler : MonoBehaviour
    {
        public static MobHandler INSTANCE;
        [SerializeField] private EnemyData[] enemyTypes = Array.Empty<EnemyData>();
        private readonly Dictionary<int, EnemyData> enemyData = new Dictionary<int, EnemyData>();
        [SerializeField] private GameObject enemyPrefab;
        private HashSet<MobScript> mobs;
        [SerializeField] private int pathChannelCount;
        private NextStep[,,] optimalPath; // [channel, row, col]
        private static readonly (int r, int c)[] Directions =
        {
            (1, 0), (-1, 0), (0, 1), (0, -1)
        };

        void Start()
        {
            if (INSTANCE != null)
            {
                Destroy(this);
                return;
            }
            INSTANCE = this;
            Init();
        }

        private void Init()
        {
            enemyData.Clear();
            foreach (EnemyData data in enemyTypes)
            {
                if (data == null) continue;
                if (enemyData.ContainsKey(data.type))
                {
                    continue;
                }
                enemyData.Add(data.type, data);
            }
            mobs = new HashSet<MobScript>();
            UpdateBestPath(PlayerHandler.INSTANCE.r, PlayerHandler.INSTANCE.c);
            SummonAt(10, 10, 1);
            SummonAt(15, 10, 1);
            SummonAt(10, 15, 1);
            SummonAt(13, 10, 1);
            SummonAt(10, 13, 1);
        }

        public void HandleUpdate()
        {
            UpdateBestPath(PlayerHandler.INSTANCE.r, PlayerHandler.INSTANCE.c);
            mobs.RemoveWhere(m => m == null);
            foreach (MobScript m in mobs)
            {
                if (m == null) continue;
                m.TakeDamage();
                m.UpdateTargetIncomplete();
            }
        }

        public bool IsOccupied(int row, int col)
        {
            if (mobs == null) return false;
            foreach (MobScript mob in mobs)
                if (mob != null && mob.isActiveAndEnabled &&
                    GridHelper.INSTANCE.TryGetTileOn(mob.transform.position, out Tile occupied) &&
                    occupied.row == row && occupied.col == col) return true;
            return false;
        }

        public void RefreshAfterPlayerMove()
        {
            UpdateBestPath(PlayerHandler.INSTANCE.r, PlayerHandler.INSTANCE.c);
            if (mobs == null) return;
            foreach (MobScript mob in mobs)
                if (mob != null && mob.isActiveAndEnabled) mob.RetargetAfterPlayerMove();
        }

        public void SummonAt(int r, int c, int type, int pathChannel = -1)
        {
            if (pathChannel < 0)
            {
                pathChannel = (int)(UnityEngine.Random.value * pathChannelCount);
                
            }
            pathChannel = Math.Min(pathChannel, pathChannelCount - 1);

            if (!GridHelper.IsInBounds(GameLogic.INSTANCE.tilesGrid, r, c)) return;
            GameObject enemy = Instantiate(enemyPrefab, GridHelper.INSTANCE.GetAnchor());
            MobScript mobScript = enemy.GetComponent<MobScript>();
            EnemyData data = enemyData[type];
            mobScript.Init(data, GameLogic.INSTANCE.tilesGrid[r,c], pathChannel);
            mobs.Add(mobScript);
        }

        public NextStep getBestPath(int r, int c, int pathChannel = 0)
        {
            if (!GridHelper.IsInBounds(optimalPath, pathChannel, r, c)) return null;
            return optimalPath[pathChannel, r, c];
        }

        public bool IsReachable(int r, int c, int pathChannel = 0)
        {
            return GridHelper.IsInBounds(optimalPath, pathChannel, r, c) &&
                (optimalPath[pathChannel, r, c] != null ||
                 (r == PlayerHandler.INSTANCE.r && c == PlayerHandler.INSTANCE.c));
        }

        public NextStep getBestPath(int r, int c, float wander, int pathChannel = 0)
        {
            NextStep b = getBestPath(r, c, pathChannel);
            if (b == null) return null;
            float[] weights = new float[4];
            NextStep[] cands = new NextStep[4];
            int count = 0;
            foreach ((int i, int j) in Directions)
            {
                // check bounds, no backtrack
                NextStep neighbor = getBestPath(r + i, c + j, pathChannel);
                bool isBest = b.r == i && b.c == j;
                if (isBest || (neighbor != null && !neighbor.Equals(new NextStep(-i, -j))))
                {
                    NextStep cand = new NextStep(i, j);
                    if (b.Equals(cand))
                    {
                        weights[count] = 1;
                    }
                    else
                    {
                        weights[count] = UnityEngine.Random.value * wander / 4f;
                    }
                    cands[count] = cand;
                    count++;
                }
            }

            float rand = UnityEngine.Random.value * weights.Sum();

            for (int i = 0; i < count; i++)
            {
                rand -= weights[i];
                if (rand <= 0)
                {
                    return cands[i];
                }
            }

            return b;
        }

        public void UpdateBestPath(int targetR, int targetC)
        {
            int[,] walls = GridHelper.INSTANCE.ExtractWalls();
            optimalPath = new NextStep[Math.Max(1, pathChannelCount), walls.GetLength(0), walls.GetLength(1)];
            for (int channel = 0; channel < optimalPath.GetLength(0); channel++)
                BuildChannel(channel, targetR, targetC, (int[,])walls.Clone());
        }

        private void BuildChannel(int channel, int targetR, int targetC, int[,] walls)
        {
            Queue<(int, int)> queue = new Queue<(int, int)>();
            walls[targetR, targetC] = 1;
            queue.Enqueue((targetR, targetC));
            while (queue.TryDequeue(out var current))
            {
                int r = current.Item1, c = current.Item2;

                List<(int, int)> toAdd = new List<(int, int)>();
                foreach ((int i, int j) in Directions)
                {
                    int newR = r + i, newC = c + j;
                    if (GridHelper.IsInBounds(walls, newR, newC) && walls[newR, newC] == 0 && optimalPath[channel, newR, newC] == null)
                    {
                        toAdd.Add((newR, newC));
                    }
                }

                Shuffle(toAdd);
                foreach ((int newR, int newC) in toAdd)
                {
                    optimalPath[channel, newR, newC] = new NextStep(r - newR, c - newC);
                    walls[newR, newC] = 1; // mark as visited
                    queue.Enqueue((newR, newC));
                }
            }
        }

        public static List<(int, int)> Shuffle(List<(int, int)> values)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int swapIndex = UnityEngine.Random.Range(0, i + 1);
                (values[i], values[swapIndex]) = (values[swapIndex], values[i]);
            }

            return values;
        }
        
        public class NextStep
        {
            public int r, c;

            public NextStep(int r, int c)
            {
                this.r = r;
                this.c = c;
            }
            public bool Equals(NextStep other)
            {
                return other != null && other.r == this.r && other.c == this.c;
            }
        }
    }
}
