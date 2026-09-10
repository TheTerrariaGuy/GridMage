using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using static Indexing;

namespace Assets.Scripts
{
    [DefaultExecutionOrder(-100)]
    public class GameLogic : MonoBehaviour
    {
        public static GameLogic INSTANCE;

        [NonSerialized] public int[,] grid;
        [NonSerialized] public Tile[,] tilesGrid;
        [NonSerialized] public HashSet<Tile> tilesSet;
        [SerializeField] private GameObject tile;
        [SerializeField] private int rows, cols;
        [SerializeField] public Transform gridParent;
        [SerializeField] public float spacing, offset;
        [SerializeField] private float clock;
        [SerializeField] private GameObject clockHand;
        [SerializeField] public float maxMana, manaRegen;
        [SerializeField, Min(0f)] private float placementManaRegenMultiplier = 0.5f;
        private bool[,] placeableMap; 


        //[SerializeField] public GameObject playerPoint;
        
        private int currentSelection;
        public float time;
        public float currMana;
        private const float SubmitQueueManaCost = 5f;
        private readonly Dictionary<(int row, int col), (int type, float cost)> queuedSpells = new();
        private readonly List<ParticleVFX.Burst> visuals = new();
        private readonly Dictionary<Vector2Int, ParticleVFX.Burst> visualOwners = new();
        private float reservedMana;
        public bool IsPlacementMode => queuedSpells.Count > 0;
        public float AvailableMana => Mathf.Max(0f, currMana - reservedMana);
        public int CurrentSelection => currentSelection;

        // Use this for initialization

        void Start()
        {
            if (INSTANCE == null)
            {
                INSTANCE = this;
            } else
            {
                Destroy(this);
                return;
            }
            InitializedGrid();
        }

        public void InitializedGrid()
        {
            ParticleVFX.INSTANCE?.ClearBursts();
            if (tilesSet != null)
                foreach (Tile oldTile in tilesSet)
                {
                    oldTile.ReleaseParticles();
                    oldTile.gameObject.SetActive(false);
                    Destroy(oldTile.gameObject);
                }
            queuedSpells.Clear();
            reservedMana = 0f;
            grid = new int[rows, cols];
            tilesGrid = new Tile[rows, cols];
            tilesSet = new HashSet<Tile>();
            for (int i = 0; i < grid.GetLength(0); i++)
            {
                for (int j = 0; j < grid.GetLength(1); j++)
                {
                    grid[i,j] = 0;
                    GameObject t = Instantiate(tile, new Vector3(0, 0, 0), Quaternion.identity);
                    Tile tileScript = t.GetComponent<Tile>();
                    tileScript.Init(i, j, spacing, offset);
                    tilesGrid[i, j] = tileScript;
                    tilesSet.Add(tileScript);
                }
            }
        }

        void Update()
        {
            time += Time.deltaTime;
            currMana += Time.deltaTime * manaRegen * (IsPlacementMode ? placementManaRegenMultiplier : 1f);
            // Queued spells hold their cost separately so spendable mana can still regenerate.
            currMana = Mathf.Min(currMana, maxMana + reservedMana);
            if (clock <= 0f) return;
            while (time >= clock)
            {
                time -= clock;
                TickCombat();
            }
            float angle = time / clock * 360f;
            if (clockHand != null) clockHand.transform.localRotation = Quaternion.Euler(0f, 0f, -angle);
        }

        public ref float GetTime()
        {
            return ref time;
        }

        public void UpdateTiles()
        {
            for (int i = 0; i < grid.GetLength(0); i++)
            {
                for (int j = 0; j < grid.GetLength(1); j++)
                {
                    UpdateTile(i,j);
                }
            }
        }

        public void UpdateTile(int r, int c)
        {
            tilesGrid[r, c].ChangeType(grid[r, c]);
        }
        
        public void UpdateSelection(int newSelection)
        {
            currentSelection = newSelection;
        }

        public void Clicked(Tile t)
        {
            MakeMove(t.row, t.col, currentSelection);
        }


        // type: 100, 200 ,300, 400
        public void MakeMove(int r, int c, int type)
        {
            if (!CanQueueSpell(r, c, type)) return;
            float manaCost = Indexing.INSTANCE.manaCosts[type];
            queuedSpells.Add((r, c), (type, manaCost));
            reservedMana += manaCost;
            tilesGrid[r, c].ShowQueuedSpell(type);
        }

        public bool CanQueueSpell(int r, int c, int type)
        {
            return CanCastAt(r, c) && !queuedSpells.ContainsKey((r, c)) &&
                Indexing.INSTANCE.manaCosts.TryGetValue(type, out float manaCost) &&
                AvailableMana >= manaCost;
        }

        private bool CanCastAt(int r, int c)
        {
            //bool hasWater = false;
            //for (int i = -1; i <= 1; i++)
            //{
            //    for (int j = -1; j <= 1; j ++)
            //    {
            //        if (i == 0 && j == 0 || ! GridHelper.IsInBounds(grid, r + i, c + j))
            //        {
            //            continue;
            //        }
                    
            //    }
            //}
            return GridHelper.IsInBounds(grid, r, c) &&
                (grid[r, c] == 0 || grid[r, c] % 100 == 11);
        }

        public void RemoveQueuedSpell(int r, int c)
        {
            if (!queuedSpells.TryGetValue((r, c), out var spell)) return;
            queuedSpells.Remove((r, c));
            reservedMana = queuedSpells.Count == 0 ? 0f : reservedMana - spell.cost;
            currMana = Mathf.Min(currMana, maxMana + reservedMana);
            tilesGrid[r, c].ClearQueuedSpell();
        }

        private void RemoveInvalidQueuedSpells()
        {
            var invalidCells = new List<(int row, int col)>();
            foreach (var cell in queuedSpells.Keys)
            {
                if (!CanCastAt(cell.row, cell.col)) invalidCells.Add(cell);
            }
            foreach (var cell in invalidCells) RemoveQueuedSpell(cell.row, cell.col);
        }

        public void SubmitQueuedSpells()
        {
            RemoveInvalidQueuedSpells();
            if (!IsPlacementMode || AvailableMana < SubmitQueueManaCost) return;
            currMana -= reservedMana + SubmitQueueManaCost;
            foreach (var entry in queuedSpells)
            {
                var cell = entry.Key;
                grid[cell.row, cell.col] = entry.Value.type;
                tilesGrid[cell.row, cell.col].ClearQueuedSpell();
                UpdateTile(cell.row, cell.col);
            }
            queuedSpells.Clear();
            reservedMana = 0f;
        }

        private void TickCombat()
        {
            visuals.Clear();
            visualOwners.Clear();
            grid = HandleFading(grid);
            RemoveInvalidQueuedSpells();
            int[,] beforeRxn = (int[,])grid.Clone();
            grid = HandleSpells(grid);
            RemoveInvalidQueuedSpells();
            grid = HandleOverlap(grid, beforeRxn);
            RemoveInvalidQueuedSpells();
            UpdateTiles();
            foreach (var visual in visuals) ParticleVFX.INSTANCE?.Play(visual, clock);
            if (MobHandler.INSTANCE != null) MobHandler.INSTANCE.HandleUpdate();
        }

        private int[,] HandleFading(int[,] g)
        {
            int[,] newGrid = (int[,])g.Clone();
            // Clear the old spent effects before any fading tile writes new effects.
            for (int i = 0; i < g.GetLength(0); i++)
            {
                for (int j = 0; j < g.GetLength(1); j++)
                {
                    if (g[i, j] >= 100 && g[i, j] % 100 == 11) newGrid[i, j] = 0;
                }
            }
            
            for (int i = 0; i < g.GetLength(0); i++)
            {
                for (int j = 0; j < g.GetLength(1); j++)
                {
                    if (g[i,j] < 100) continue; // paranoia

                    // Normal stuff
                    if (g[i,j] % 100 == 10) // Is fading block
                    {
                        string id = g[i,j] switch { 110 => "Fire_Flare_Decay", 210 => "Water_Steam_Decay",
                            310 => "Electricity_Charge_Decay", 410 => "Lava_Cooling_Decay", _ => null };
                        var visual = NewVisual(id, i, j);
                        Indexing.INSTANCE.ModifyFade(Indexing.INSTANCE.fadeMap[g[i,j]], i, j, g[i,j], g, ref newGrid,
                            (r, c) => TrackVisual(r, c, visual));
                    }
                }
            }
            return newGrid;
        }

        private int[,] HandleSpells(int[,] g)
        {
            int[,] newGrid = (int[,])g.Clone();
            SortedSet<Change> changeQueue = new SortedSet<Change>();
            int insertionOrder = 0;

            for (int i = 0; i < g.GetLength(0); i++)
            {
                for (int j = 0; j < g.GetLength(1); j++)
                {
                    if (g[i, j] < 100) continue;
                    if (g[i, j] % 100 != 11 && (g[i,j] % 100)/10 == 0) // indices 0-10 are the rxn-able ids
                    {
                        int elementType = g[i, j] - g[i, j] % 100;
                        IReadOnlyList<Reaction> possible = Indexing.INSTANCE.GetReactions(elementType);

                        foreach (Reaction reaction in possible)
                        {
                            if (CheckReq(g, reaction.Requirements, i, j))
                            {
                                QueueChanges(
                                    changeQueue,
                                    reaction,
                                    i,
                                    j,
                                    ref insertionOrder,
                                    g);
                            }
                        }
                    }
                }
            }

            ApplyQueuedChanges(changeQueue, ref newGrid);
            return newGrid;
        }

        private int[,] HandleOverlap(int[,] g, int[,] before)
        {
            int[,] newGrid = (int[,])g.Clone();
            SortedSet<Change> changeQueue = new SortedSet<Change>();
            int insertionOrder = 0;

            for (int i = 0; i < g.GetLength(0); i++)
            {
                for (int j = 0; j < g.GetLength(1); j++)
                {
                    if (g[i,j] % 100 < 10 && before[i,j] % 100 < 10 && g[i,j] / 100 != before[i,j] / 100) // explodable
                    {
                        int previousElementType = before[i, j] - before[i, j] % 100;
                        int incomingType = g[i, j];
                        IReadOnlyList<Reaction> possible = Indexing.INSTANCE.GetReactions(previousElementType);

                        foreach (Reaction reaction in possible)
                        {
                            if (IsOriginReaction(reaction, incomingType))
                            {
                                QueueChanges(
                                    changeQueue,
                                    reaction,
                                    i,
                                    j,
                                    ref insertionOrder,
                                    g);
                            }
                        }
                    }
                }
            }

            ApplyQueuedChanges(changeQueue, ref newGrid);
            return newGrid;
        }

        private bool CheckReq(int[,] g, IReadOnlyCollection<Requirement> req, int r, int c) 
        {
            foreach (Requirement requirement in req)
            {
                int targetRow = r + requirement.y;
                int targetCol = c + requirement.x;

                if (!GridHelper.IsInBounds(g, targetRow, targetCol) ||
                    !requirement.Matches(g[targetRow, targetCol]))
                {
                    return false;
                } 
            }
            return true;
        }

        private static bool IsOriginReaction(Reaction reaction, int type)
        {
            if (reaction.Requirements.Count != 1)
            {
                return false;
            }

            foreach (Requirement requirement in reaction.Requirements)
            {
                return requirement.x == 0 &&
                       requirement.y == 0 &&
                       requirement.Matches(type);
            }

            return false;
        }

        private void QueueChanges(
            SortedSet<Change> changeQueue,
            Reaction reaction,
            int originRow,
            int originCol,
            ref int insertionOrder,
            int[,] grid)
        {
            var visual = NewVisual(reaction.Effect, originRow, originCol, reaction.Direction);
            foreach (Offset output in reaction.Outputs)
            {
                int targetRow = originRow + output.y;
                int targetCol = originCol + output.x;

                if (!GridHelper.IsInBounds(grid, targetRow, targetCol))
                {
                    continue;
                }

                changeQueue.Add(new Change(
                    targetRow,
                    targetCol,
                    output.type,
                    output.priority,
                    insertionOrder++,
                    originRow,
                    originCol, visual));
            }
        }

        private void ApplyQueuedChanges(SortedSet<Change> changeQueue, ref int[,] grid)
        {
            int[,] walls = GridHelper.INSTANCE.ExtractWalls(grid);
            while (changeQueue.Count > 0)
            {
                Change change = changeQueue.Min;
                changeQueue.Remove(change);
                if (!GridHelper.INSTANCE.TestForWalls(walls,
                    change.SourceRow, change.SourceCol, change.Row, change.Col, change.Type == 410)) continue;
                grid[change.Row, change.Col] = change.Type;
                TrackVisual(change.Row, change.Col, change.Visual);
            }
        }

        private ParticleVFX.Burst NewVisual(string id, int row, int col, int direction = 0)
        {
            if (id == null) return null;
            var visual = new ParticleVFX.Burst(id, row, col, direction);
            visuals.Add(visual);
            return visual;
        }

        private void TrackVisual(int row, int col, ParticleVFX.Burst visual)
        {
            var cell = new Vector2Int(col, row);
            if (visualOwners.Remove(cell, out var previous)) previous.cells.Remove(cell);
            if (visual == null) return;
            visualOwners[cell] = visual;
            visual.cells.Add(cell);
        }

        public class Change : IComparable<Change>
        {
            public int Row { get; }
            public int Col { get; }
            public int SourceRow { get; }
            public int SourceCol { get; }
            public int Type { get; }
            public int Priority { get; }
            public ParticleVFX.Burst Visual { get; }
            private int InsertionOrder { get; }

            public Change(int row, int col, int type, int priority, int insertionOrder, int sourceRow, int sourceCol,
                ParticleVFX.Burst visual = null)
            {
                Row = row;
                Col = col;
                SourceRow = sourceRow;
                SourceCol = sourceCol;
                Type = type;
                Priority = priority;
                InsertionOrder = insertionOrder;
                Visual = visual;
            }

            public int CompareTo(Change other)
            {
                if (other == null)
                {
                    return 1;
                }

                int priorityComparison = Priority.CompareTo(other.Priority);
                return priorityComparison != 0
                    ? priorityComparison
                    : InsertionOrder.CompareTo(other.InsertionOrder); // ultra deterministic
            }
        }
    }
}
