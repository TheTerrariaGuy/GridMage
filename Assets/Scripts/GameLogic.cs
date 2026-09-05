using System;
using System.Collections.Generic;
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


        //[SerializeField] public GameObject playerPoint;
        
        private int currentSelection;
        public float time;
        public float currMana;
        private const float ProcessSpellsManaCost = 5f;

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
            currMana += Time.deltaTime * manaRegen;
            if (currMana > maxMana) currMana = maxMana;
            if (clock <= 0f) return;
            if (time > clock)
            {
                time -= clock;
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
            if (!GridHelper.IsInBounds(grid, r, c)) return;
            if (grid[r, c] != 0 && grid[r, c] % 100 != 11) return;
            if (!Indexing.INSTANCE.manaCosts.TryGetValue(type, out float manaCost)) return;
            if (currMana - manaCost < 0) return;
            currMana -= manaCost;
            grid[r, c] = type;
            UpdateTile(r, c);
        }

        public void ProcessSpells()
        {
            if (currMana < ProcessSpellsManaCost) return;
            currMana -= ProcessSpellsManaCost;

            grid = HandleFading(grid);
            int[,] beforeRxn = (int[,])grid.Clone();
            grid = HandleSpells(grid);
            grid = HandleOverlap(grid, beforeRxn);
            UpdateTiles();
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
                        Indexing.INSTANCE.ModifyFade(Indexing.INSTANCE.fadeMap[g[i,j]], i, j, g[i,j], g, ref newGrid); // cursed af
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
                                    reaction.Outputs,
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
                                    reaction.Outputs,
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

        private static void QueueChanges(
            SortedSet<Change> changeQueue,
            IReadOnlyCollection<Offset> outputs,
            int originRow,
            int originCol,
            ref int insertionOrder,
            int[,] grid)
        {
            foreach (Offset output in outputs)
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
                    originCol));
            }
        }

        private static void ApplyQueuedChanges(SortedSet<Change> changeQueue, ref int[,] grid)
        {
            int[,] walls = GridHelper.INSTANCE.ExtractWalls(grid);
            while (changeQueue.Count > 0)
            {
                Change change = changeQueue.Min;
                changeQueue.Remove(change);
                if (!GridHelper.INSTANCE.TestForWalls(walls,
                    change.SourceRow, change.SourceCol, change.Row, change.Col)) continue;
                grid[change.Row, change.Col] = change.Type;
            }
        }

        public class Change : IComparable<Change>
        {
            public int Row { get; }
            public int Col { get; }
            public int SourceRow { get; }
            public int SourceCol { get; }
            public int Type { get; }
            public int Priority { get; }
            private int InsertionOrder { get; }

            public Change(int row, int col, int type, int priority, int insertionOrder, int sourceRow, int sourceCol)
            {
                Row = row;
                Col = col;
                SourceRow = sourceRow;
                SourceCol = sourceCol;
                Type = type;
                Priority = priority;
                InsertionOrder = insertionOrder;
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
