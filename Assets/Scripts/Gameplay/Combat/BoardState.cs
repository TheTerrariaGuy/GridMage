using System;

namespace Assets.Scripts
{
    /// <summary>Authoritative gameplay state; tile components never own a second copy of cell contents.</summary>
    public sealed class BoardState
    {
        private int[,] cells;
        public int Rows => cells.GetLength(0);
        public int Cols => cells.GetLength(1);
        public int[,] Cells => cells;
        public bool[,] Exists { get; }
        public float[,] Elevations { get; }
        private readonly bool[,] walkable, blocksSight, allowsSpells;

        public BoardState(int rows, int cols, bool[,] exists = null, float[,] elevations = null,
            bool[,] walkable = null, bool[,] blocksSight = null, bool[,] allowsSpells = null)
        {
            if (rows <= 0 || cols <= 0) throw new ArgumentOutOfRangeException(nameof(rows));
            cells = new int[rows, cols];
            Exists = exists ?? new bool[rows, cols];
            Elevations = elevations ?? new float[rows, cols];
            this.walkable = walkable; this.blocksSight = blocksSight; this.allowsSpells = allowsSpells;
            if (exists == null)
                for (int r = 0; r < rows; r++) for (int c = 0; c < cols; c++) Exists[r, c] = true;
        }

        public bool HasCell(int r, int c) => GridMath.IsInBounds(cells, r, c) && Exists[r, c];
        public int Get(int r, int c) => cells[r, c];
        public void Set(int r, int c, int type)
        {
            if (!HasCell(r, c)) throw new ArgumentOutOfRangeException(nameof(r), "Cannot write a missing cell.");
            cells[r, c] = type;
        }
        public void Replace(int[,] next)
        {
            if (next == null || next.GetLength(0) != Rows || next.GetLength(1) != Cols)
                throw new ArgumentException("Replacement board dimensions must match.", nameof(next));
            cells = next;
        }
        public bool AllowsSpells(int r, int c) => HasCell(r, c) && (allowsSpells == null || allowsSpells[r, c]);
        public bool CanWalk(int r, int c) => HasCell(r, c) &&
            (walkable == null || walkable[r, c]) && !ElementState.IsWall(cells[r, c]);
        public bool BlocksSight(int r, int c) => !HasCell(r, c) || (blocksSight != null && blocksSight[r, c]);
        public bool CanStep(int r, int c, int toR, int toC) => CanWalk(r, c) && CanWalk(toR, toC) &&
            GridMath.IsElevationDiffOk(Elevations[r, c], Elevations[toR, toC]);
        public bool ElevationLine(int r, int c, int toR, int toC) =>
            GridMath.TestElevationLine(Elevations, r, c, toR, toC, Exists);
        public int[,] SightWalls(int[,] snapshot)
        {
            var walls = new int[Rows, Cols];
            for (int r = 0; r < Rows; r++) for (int c = 0; c < Cols; c++)
                walls[r, c] = BlocksSight(r, c) || ElementState.IsWall(snapshot[r, c]) ? 1 : 0;
            return walls;
        }
    }
}
