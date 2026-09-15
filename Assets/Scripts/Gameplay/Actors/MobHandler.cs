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
        private readonly List<(TilemapLevel.Spawn spawn, float next)> spawners = new();
        private readonly Queue<(int row, int col)> frontier = new();
        private readonly List<(int, int)> neighbors = new(4);
        private int[,] visited;
        private int visitVersion;
        private readonly float[] weights = new float[4];
        private readonly NextStep[] candidates = new NextStep[4];

        private void Awake()
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
        }

        public void ResetForLevel()
        {
            if (mobs != null)
                foreach (MobScript mob in mobs)
                    if (mob != null) { mob.gameObject.SetActive(false); Destroy(mob.gameObject); }
            mobs = new HashSet<MobScript>();
            spawners.Clear();
            var game = GameLogic.INSTANCE;
            if (game == null || game.grid == null || PlayerHandler.INSTANCE == null) return;
            UpdateBestPath(PlayerHandler.INSTANCE.r, PlayerHandler.INSTANCE.c);
            if (game.LevelLayout != null)
            {
                foreach (var spawn in game.LevelLayout.enemies)
                {
                    SummonAt(spawn.row, spawn.col, spawn.enemy);
                    if (spawn.interval > 0f) spawners.Add((spawn, Time.time + spawn.interval));
                }
                return;
            }
            foreach (var cell in LegacyLevelDefaults.EnemyCells) SummonAt(cell.row, cell.col, 1);
        }

        private void Update()
        {
            for (int i = 0; i < spawners.Count; i++)
            {
                var entry = spawners[i];
                if (Time.time < entry.next) continue;
                SummonAt(entry.spawn.row, entry.spawn.col, entry.spawn.enemy);
                spawners[i] = (entry.spawn, Time.time + entry.spawn.interval);
            }
        }

        public void HandleUpdate()
        {
            if (mobs == null || PlayerHandler.INSTANCE == null) return;
            UpdateBestPath(PlayerHandler.INSTANCE.r, PlayerHandler.INSTANCE.c);
            mobs.RemoveWhere(m => m == null);
            foreach (MobScript m in mobs)
            {
                if (m == null) continue;
                m.TakeDamage();
                m.UpdateTargetIncomplete();
            }
        }

        // Spawners query live positions only when attempting a spawn.
        public bool IsOccupied(int row, int col)
        {
            if (mobs == null) return false;
            foreach (var mob in mobs)
                if (mob != null && mob.isActiveAndEnabled &&
                    GridHelper.INSTANCE.TryGetTileOn(mob.transform.position, out var tile) &&
                    tile.row == row && tile.col == col) return true;
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
            if (enemyData.TryGetValue(type, out EnemyData data)) SummonAt(r, c, data, pathChannel);
        }

        public void SummonAt(int r, int c, EnemyData data, int pathChannel = -1)
        {
            if (data == null || mobs == null || GameLogic.INSTANCE == null ||
                !GameLogic.INSTANCE.CanWalk(r, c) || IsOccupied(r, c) ||
                (PlayerHandler.INSTANCE != null && PlayerHandler.INSTANCE.r == r && PlayerHandler.INSTANCE.c == c)) return;
            if (pathChannel < 0)
            {
                pathChannel = (int)(UnityEngine.Random.value * pathChannelCount);
                
            }
            pathChannel = Mathf.Clamp(pathChannel, 0, Math.Max(1, pathChannelCount) - 1);

            if (!IsReachable(r, c, pathChannel)) return;
            GameObject enemy = Instantiate(enemyPrefab, GridHelper.INSTANCE.GetAnchor());
            MobScript mobScript = enemy.GetComponent<MobScript>();
            mobScript.Init(data, GameLogic.INSTANCE.tilesGrid[r,c], pathChannel);
            mobs.Add(mobScript);
        }

        public NextStep GetBestPath(int r, int c, int pathChannel = 0)
        {
            if (!GridMath.IsInBounds(optimalPath, pathChannel, r, c)) return default;
            return optimalPath[pathChannel, r, c];
        }

        public bool IsReachable(int r, int c, int pathChannel = 0)
        {
            return GridMath.IsInBounds(optimalPath, pathChannel, r, c) &&
                (optimalPath[pathChannel, r, c].IsValid ||
                 (r == PlayerHandler.INSTANCE.r && c == PlayerHandler.INSTANCE.c));
        }

        public NextStep GetBestPath(int r, int c, float wander, int pathChannel = 0)
        {
            NextStep b = GetBestPath(r, c, pathChannel);
            if (!b.IsValid) return default;
            Array.Clear(weights, 0, weights.Length);
            int count = 0;
            foreach ((int i, int j) in GridMath.Cardinal)
            {
                if (!GameLogic.INSTANCE.CanStep(r, c, r + i, c + j)) continue;
                // check bounds, no backtrack
                NextStep neighbor = GetBestPath(r + i, c + j, pathChannel);
                bool isBest = b.r == i && b.c == j;
                if (isBest || (neighbor.IsValid && !neighbor.Equals(new NextStep(-i, -j))))
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
                    candidates[count] = cand;
                    count++;
                }
            }

            float rand = UnityEngine.Random.value * weights.Sum();

            for (int i = 0; i < count; i++)
            {
                rand -= weights[i];
                if (rand <= 0)
                {
                    return candidates[i];
                }
            }

            return b;
        }

        public void UpdateBestPath(int targetR, int targetC)
        {
            var board = GameLogic.INSTANCE.Board;
            int channels = Math.Max(1, pathChannelCount);
            if (optimalPath == null || optimalPath.GetLength(0) != channels ||
                optimalPath.GetLength(1) != board.Rows || optimalPath.GetLength(2) != board.Cols)
            {
                optimalPath = new NextStep[channels, board.Rows, board.Cols];
                visited = new int[board.Rows, board.Cols];
                visitVersion = 0;
            }
            else Array.Clear(optimalPath, 0, optimalPath.Length);
            if (!board.CanWalk(targetR, targetC)) return;
            for (int channel = 0; channel < channels; channel++) BuildChannel(channel, targetR, targetC);
        }

        private void BuildChannel(int channel, int targetR, int targetC)
        {
            if (visitVersion == int.MaxValue) { Array.Clear(visited, 0, visited.Length); visitVersion = 0; }
            int version = ++visitVersion;
            frontier.Clear();
            visited[targetR, targetC] = version;
            frontier.Enqueue((targetR, targetC));
            while (frontier.TryDequeue(out var current))
            {
                int r = current.row, c = current.col;
                neighbors.Clear();
                foreach (var direction in GridMath.Cardinal)
                {
                    int newR = r + direction.row, newC = c + direction.col;
                    if (GameLogic.INSTANCE.CanStep(r, c, newR, newC) && visited[newR, newC] != version)
                        neighbors.Add((newR, newC));
                }
                Shuffle(neighbors);
                foreach (var (newR, newC) in neighbors)
                {
                    optimalPath[channel, newR, newC] = new NextStep(r - newR, c - newC);
                    visited[newR, newC] = version;
                    frontier.Enqueue((newR, newC));
                }
            }
        }

        private static void Shuffle(List<(int, int)> values)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int swapIndex = UnityEngine.Random.Range(0, i + 1);
                (values[i], values[swapIndex]) = (values[swapIndex], values[i]);
            }

        }

        private void OnDestroy()
        {
            if (INSTANCE == this) INSTANCE = null;
        }
        
        public readonly struct NextStep : IEquatable<NextStep>
        {
            public readonly int r, c;
            public bool IsValid => r != 0 || c != 0;
            public NextStep(int r, int c) { this.r = r; this.c = c; }
            public bool Equals(NextStep other) => other.r == r && other.c == c;
            public override bool Equals(object other) => other is NextStep step && Equals(step);
            public override int GetHashCode() => HashCode.Combine(r, c);
        }
    }
}
