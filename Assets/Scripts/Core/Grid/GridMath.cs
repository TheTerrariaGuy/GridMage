using System;
using System.Collections.Generic;

namespace Assets.Scripts
{
    /// <summary>Grid coordinates use rows down, columns right. No Unity or scene dependencies.</summary>
    public static class GridMath
    {
        public static readonly IReadOnlyList<(int row, int col)> Cardinal =
            Array.AsReadOnly(new[] { (1, 0), (-1, 0), (0, 1), (0, -1) });

        public static bool IsInBounds<T>(T[,] grid, int row, int col) =>
            grid != null && row >= 0 && col >= 0 && row < grid.GetLength(0) && col < grid.GetLength(1);

        public static bool IsInBounds<T>(T[,,] grid, int channel, int row, int col) =>
            grid != null && channel >= 0 && channel < grid.GetLength(0) &&
            row >= 0 && col >= 0 && row < grid.GetLength(1) && col < grid.GetLength(2);

        public static int SquareDistance(int row, int col, int targetRow, int targetCol) =>
            Math.Max(Math.Abs(targetRow - row), Math.Abs(targetCol - col));

        public static (int x, int y) Rotate(int x, int y, int turns)
        {
            turns = (turns % 4 + 4) % 4;
            for (int i = 0; i < turns; i++) (x, y) = (-y, x);
            return (x, y);
        }

        /// <summary>Allocation-free center-to-center traversal, reporting exact corner crossings.</summary>
        public struct Line
        {
            private readonly int targetRow, targetCol, dr, dc, stepRow, stepCol;
            private long crossedRows, crossedCols;
            public int Row { get; private set; }
            public int Col { get; private set; }
            public int PreviousRow { get; private set; }
            public int PreviousCol { get; private set; }
            public bool CrossesCorner { get; private set; }

            public Line(int row, int col, int targetRow, int targetCol)
            {
                this = default;
                Row = row; Col = col;
                this.targetRow = targetRow; this.targetCol = targetCol;
                dr = Math.Abs(targetRow - row); dc = Math.Abs(targetCol - col);
                stepRow = Math.Sign(targetRow - row); stepCol = Math.Sign(targetCol - col);
            }

            public bool MoveNext()
            {
                if (Row == targetRow && Col == targetCol) return false;
                PreviousRow = Row; PreviousCol = Col;
                long rowTime = (2 * crossedRows + 1) * dc;
                long colTime = (2 * crossedCols + 1) * dr;
                CrossesCorner = rowTime == colTime;
                if (rowTime <= colTime) { Row += stepRow; crossedRows++; }
                if (colTime <= rowTime) { Col += stepCol; crossedCols++; }
                return true;
            }
        }

        public static bool HasLineOfSight(int[,] walls, int row, int col, int targetRow, int targetCol,
            bool allowTarget = false)
        {
            if (!IsInBounds(walls, row, col) || !IsInBounds(walls, targetRow, targetCol)) return false;
            bool Blocked(int r, int c) => !IsInBounds(walls, r, c) || walls[r, c] != 0;
            var line = new Line(row, col, targetRow, targetCol);
            while (line.MoveNext())
            {
                if (line.CrossesCorner && Blocked(line.Row, line.PreviousCol) &&
                    Blocked(line.PreviousRow, line.Col)) return false;
                if (Blocked(line.Row, line.Col) && !(allowTarget && line.Row == targetRow && line.Col == targetCol))
                    return false;
            }
            return true;
        }

        public static bool CanReach(int[,] walls, int row, int col, int targetRow, int targetCol, int range) =>
            IsInBounds(walls, targetRow, targetCol) && walls[targetRow, targetCol] == 0 &&
            SquareDistance(row, col, targetRow, targetCol) <= range &&
            HasLineOfSight(walls, row, col, targetRow, targetCol);

        public static bool IsElevationDiffOk(float from, float to)
        {
            float diff = to - from;
            return diff == 0f || diff == .5f || diff == -.5f;
        }

        public static bool TestElevationLine(float[,] heights, int row, int col, int targetRow, int targetCol,
            bool[,] exists = null)
        {
            bool HasHeight(int r, int c) => IsInBounds(heights, r, c) &&
                (exists == null || (IsInBounds(exists, r, c) && exists[r, c])) &&
                !float.IsNaN(heights[r, c]) && !float.IsInfinity(heights[r, c]);
            bool SideOpen(float from, float to, int r, int c) => HasHeight(r, c) &&
                IsElevationDiffOk(from, heights[r, c]) && IsElevationDiffOk(heights[r, c], to);
            if (!HasHeight(row, col) || !HasHeight(targetRow, targetCol)) return false;
            var line = new Line(row, col, targetRow, targetCol);
            while (line.MoveNext())
            {
                if (!HasHeight(line.Row, line.Col)) return false;
                float previous = heights[line.PreviousRow, line.PreviousCol], next = heights[line.Row, line.Col];
                if (line.CrossesCorner && !SideOpen(previous, next, line.Row, line.PreviousCol) &&
                    !SideOpen(previous, next, line.PreviousRow, line.Col)) return false;
                if (!IsElevationDiffOk(previous, next)) return false;
            }
            return true;
        }
    }
}
