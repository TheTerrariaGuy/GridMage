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
        [SerializeField] private Dictionary<int, EnemyData> enemyData;
        [SerializeField] private GameObject enemyPrefab;
        private HashSet<MobScript> mobs;
        private NextStep[,] optimalPath;

        void Start()
        {
            if (INSTANCE != null)
            {
                Destroy(this);
                return;
            }
            INSTANCE = this;
        }

        private void Init()
        {
            mobs = new HashSet<MobScript>();
            UpdateBestPath(5, 5);
        }

        public void SummonAt(int r, int c, int type)
        {
            if (!enemyData.ContainsKey(type)) print("Enemy type " + type + " not found in MobHandler");
            GameObject enemy = Instantiate(enemyPrefab, GameLogic.INSTANCE.gridParent);
            MobScript mobScript = enemy.GetComponent<MobScript>();
            mobScript.Init(enemyData[type], GameLogic.INSTANCE.tilesGrid[r,c]);
            mobs.Add(mobScript);
        }

        public NextStep getBestPath(int r, int c)
        {
            return optimalPath[r, c];
        }

        public NextStep getBestPath(int r, int c, float wander)
        {
            NextStep b = optimalPath[r, c];
            float[] weights = new float[4];
            NextStep[] cands = new NextStep[4];
            int count = 0;
            for (int i = -1; i <= 1; i += 2)
            {
                for (int j = -1; j <= 1; j += 2)
                {
                    // check bounds, no backtrack
                    if (r + i >= 0 && r + i < optimalPath.GetLength(0) && c + j >= 0 && c + j < optimalPath.GetLength(1) && optimalPath[r + i, c + j].Equals(new NextStep(-i, -j)))
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
            int[,] walls = ExtractWalls(); // both walls and visited
            Queue<(int, int)> queue = new Queue<(int, int)>();
            optimalPath = new NextStep[walls.GetLength(0), walls.GetLength(1)];
            walls[targetR, targetC] = 1;
            queue.Enqueue((targetR, targetC));
            while (queue.TryDequeue(out var current))
            {
                int r = current.Item1, c = current.Item2;

                List<(int, int)> toAdd = new List<(int, int)>();
                for (int i = -1; i <= 1; i += 2)
                {
                    for (int j = -1; j <= 1; j += 2)
                    {
                        int newR = r + i, newC = c + j;
                        if (newR >= 0 && newR < walls.GetLength(0) && newC >= 0 && newC < walls.GetLength(1) && walls[newR, newC] == 0 && optimalPath[newR, newC] == null)
                        {
                            toAdd.Add((newR, newC));
                        }
                    }
                }

                Shuffle(toAdd);
                foreach ((int newR, int newC) in toAdd)
                {
                    optimalPath[newR, newC] = new NextStep(r - newR, c - newC);
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
        
        public int[,] ExtractWalls()
        {
            int[,] grid = GameLogic.INSTANCE.grid;
            int[,] walls = new int[grid.GetLength(0), grid.GetLength(1)];

            for (int i = 0; i < grid.GetLength(0); i++)
            {
                for (int j = 0; j < grid.GetLength(1); j++)
                {
                    walls[i, j] = grid[i, j] / 100 == 4 ? 1 : 0;
                }
            }

            return walls;
        }

        public Transform GetTileTransform(int r, int c)
        {
            return GameLogic.INSTANCE.tilesGrid[r, c].gameObject.transform;
        }

        public Tile GetTileOn(Vector3 position)
        {
            Vector3Int transformed = Vector3Int.RoundToInt((position - Vector3.one * GameLogic.INSTANCE.offset) / GameLogic.INSTANCE.spacing);
            return GameLogic.INSTANCE.tilesGrid[transformed.x, transformed.y];
        }

        public Transform GetAnchor()
        {
            return GameLogic.INSTANCE.gridParent;
        }

        public class NextStep
        {
            public int r, c;

            public NextStep(int r, int c)
            {
                this.r = r;
                this.c = c;
            }
        }
    }
}
