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
        [NonSerialized] public bool[,] cellExists;
        [SerializeField] private TilemapLevel level;
        public TilemapLevel Level => level;
        public TilemapLevel.Layout LevelLayout { get; private set; }
        public Vector3 GridTranslation { get; private set; }
        public bool HasBackground => level != null && level.background != null;
        [SerializeField] private GameObject tile;
        [SerializeField] private int rows, cols;
        [SerializeField] public Transform gridParent;
        [SerializeField] public float spacing, offset;
        [SerializeField] private float clock;
        [SerializeField] private GameObject clockHand;
        [SerializeField] public float maxMana, manaRegen;
        [SerializeField, Min(0f)] private float blinkManaCost = 4f;
        [SerializeField, Min(0f)] private float placementManaRegenMultiplier = 0.5f;
        [NonSerialized] public bool[,] castableGrid;
        private bool[,] visibleGrid;
        [SerializeField] private CastableOutline castableOutline;


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
            // Validate before disposing the previous board.
            TilemapLevel.Layout layout = level != null ? level.Read(gridParent) : null;
            ParticleVFX.INSTANCE?.ClearBursts();
            if (tilesSet != null)
                foreach (Tile oldTile in tilesSet)
                {
                    if (oldTile == null) continue;
                    oldTile.ReleaseParticles();
                    oldTile.gameObject.SetActive(false);
                    Destroy(oldTile.gameObject);
                }
            queuedSpells.Clear();
            reservedMana = 0f;
            LevelLayout = layout;
            GridTranslation = Vector3.zero;
            if (layout != null)
            {
                rows = layout.exists.GetLength(0);
                cols = layout.exists.GetLength(1);
                spacing = layout.spacing;
                GridTranslation = layout.origin - new Vector3(offset, offset, 0f);
            }
            grid = new int[rows, cols];
            cellExists = layout != null ? layout.exists : new bool[rows, cols];
            castableGrid = new bool[rows, cols];
            visibleGrid = new bool[rows, cols];
            tilesGrid = new Tile[rows, cols];
            tilesSet = new HashSet<Tile>();
            for (int i = 0; i < grid.GetLength(0); i++)
            {
                for (int j = 0; j < grid.GetLength(1); j++)
                {
                    if (layout == null) cellExists[i, j] = true;
                    if (!cellExists[i, j]) continue;
                    grid[i,j] = 0;
                    GameObject t = Instantiate(tile, new Vector3(0, 0, 0), Quaternion.identity);
                    Tile tileScript = t.GetComponent<Tile>();
                    tileScript.Init(i, j, spacing, offset);
                    tilesGrid[i, j] = tileScript;
                    tilesSet.Add(tileScript);
                }
            }
            if (castableOutline != null) castableOutline.transform.localPosition = GridTranslation;
            if (layout != null) level.HideMarkers();
            if (PlayerHandler.INSTANCE != null) PlayerHandler.INSTANCE.ResetForLevel();
            if (MobHandler.INSTANCE != null) MobHandler.INSTANCE.ResetForLevel();
            MakeCastable();
        }

        public bool HasCell(int r, int c) => GridHelper.IsInBounds(grid, r, c) &&
            (cellExists == null || cellExists[r, c]);

        public bool AllowsSpells(int r, int c) => HasCell(r, c) &&
            (LevelLayout == null || LevelLayout.allowsSpells[r, c]);

        public bool CanWalk(int r, int c) => HasCell(r, c) &&
            (LevelLayout == null || LevelLayout.walkable[r, c]) && !TileSpriteLayout.IsWall(grid[r, c]);

        public bool BlocksSight(int r, int c) => !HasCell(r, c) ||
            (LevelLayout != null && LevelLayout.blocksSight[r, c]);

        public bool Castable(int r, int c) =>
            AllowsSpells(r, c) && GridHelper.IsInBounds(castableGrid, r, c) && castableGrid[r, c];

        public bool CanBlinkTo(int r, int c)
        {
            return CanWalk(r, c) && GridHelper.IsInBounds(visibleGrid, r, c) && visibleGrid[r, c];
        }

        public void MakeCastable()
        {
            if (castableGrid != null)
            {
                Array.Clear(castableGrid, 0, castableGrid.Length);
                Array.Clear(visibleGrid, 0, visibleGrid.Length);
                PlayerHandler player = PlayerHandler.INSTANCE;
                if (player != null && Indexing.INSTANCE != null && HasCell(player.r, player.c))
                {
                    int range = Mathf.Max(0, Indexing.INSTANCE.castRange);
                    int[,] walls = GridHelper.INSTANCE.ExtractWalls();
                    for (int row = Mathf.Max(0, player.r - range); row <= Mathf.Min(rows - 1, player.r + range); row++)
                        for (int col = Mathf.Max(0, player.c - range); col <= Mathf.Min(cols - 1, player.c + range); col++)
                        {
                            visibleGrid[row, col] = HasCell(row, col) && PlayerHandler.CanReach(walls, player.r, player.c, row, col, range);
                            castableGrid[row, col] = AllowsSpells(row, col) && visibleGrid[row, col];
                        }
                }
            }
            castableOutline?.Rebuild(castableGrid, spacing, offset);
        }

        void Update()
        {
            if (grid == null) return;
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
            if (HasCell(r, c) && tilesGrid[r, c] != null) tilesGrid[r, c].ChangeType(grid[r, c]);
        }
        
        public void UpdateSelection(int newSelection)
        {
            currentSelection = newSelection;
        }

        public void Clicked(Tile t)
        {
            MakeMove(t.row, t.col, currentSelection);
        }

        public bool TryCastBlink(Tile target)
        {
            PlayerHandler player = PlayerHandler.INSTANCE;
            if (player == null || !player.isActiveAndEnabled || target == null ||
                !CanWalk(target.row, target.col) ||
                tilesGrid[target.row, target.col] != target ||
                AvailableMana < blinkManaCost ||
                (MobHandler.INSTANCE != null && MobHandler.INSTANCE.IsOccupied(target.row, target.col))) return false;
            if (!player.BlinkTo(target)) return false;
            currMana -= blinkManaCost;
            return true;
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
            return Castable(r, c) && CanCastAt(r, c) && !queuedSpells.ContainsKey((r, c)) &&
                Indexing.INSTANCE.manaCosts.TryGetValue(type, out float manaCost) &&
                AvailableMana >= manaCost;
        }

        private bool CanCastAt(int r, int c)
        {
            // Queued spells only recheck terrain, not the player's current range or sight.
            return AllowsSpells(r, c) &&
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
            // Clear the old spent effects before any fading tile writes new effects
            for (int i = 0; i < g.GetLength(0); i++)
            {
                for (int j = 0; j < g.GetLength(1); j++)
                {
                    if (HasCell(i, j) && g[i, j] >= 100 && g[i, j] % 100 == 11) newGrid[i, j] = 0;
                }
            }
            
            for (int i = 0; i < g.GetLength(0); i++)
            {
                for (int j = 0; j < g.GetLength(1); j++)
                {
                    if (!AllowsSpells(i, j) || g[i,j] < 100) continue;

                    // Normal stuff
                    if (g[i,j] % 100 == 10) // Is fading block
                    {
                        string id = g[i,j] switch { 110 => "Fire_Flare_Decay", 210 => "Water_Steam_Decay",
                            310 => "Electricity_Charge_Decay", 410 => "Lava_Cooling_Decay", _ => null };
                        var visual = NewVisual(id, i, j);
                        Indexing.INSTANCE.ModifyFade(Indexing.INSTANCE.fadeMap[g[i,j]], i, j, g[i,j], g, ref newGrid,
                            (r, c) => TrackVisual(r, c, visual), AllowsSpells);
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
                    if (!AllowsSpells(i, j) || g[i, j] < 100) continue;
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
                    if (AllowsSpells(i, j) && g[i,j] % 100 < 10 && before[i,j] % 100 < 10 && g[i,j] / 100 != before[i,j] / 100) // explodable
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

                if (!HasCell(targetRow, targetCol) ||
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

                if (!AllowsSpells(targetRow, targetCol))
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
                if (!AllowsSpells(change.Row, change.Col)) continue;
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

        private void OnDestroy()
        {
            if (INSTANCE == this) INSTANCE = null;
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
