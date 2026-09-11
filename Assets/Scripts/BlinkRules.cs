using System;

/// <summary>Square range and line of sight against a walls graph, including diagonal corners.</summary>
public static class BlinkRules
{
    public static bool CanReach(int[,] grid, int row, int col, int targetRow, int targetCol, int range)
    {
        if (!InBounds(grid, row, col) || !InBounds(grid, targetRow, targetCol)) return false;
        int dr = Math.Abs(targetRow - row), dc = Math.Abs(targetCol - col);
        if (Math.Max(dr, dc) > range || Blocked(grid, targetRow, targetCol)) return false;
        int stepR = Math.Sign(targetRow - row), stepC = Math.Sign(targetCol - col);
        long crossedR = 0, crossedC = 0;
        while (row != targetRow || col != targetCol)
        {
            long rowTime = (2 * crossedR + 1) * dc;
            long colTime = (2 * crossedC + 1) * dr;
            if (rowTime == colTime &&
                (Blocked(grid, row + stepR, col) || Blocked(grid, row, col + stepC))) return false;
            if (rowTime <= colTime) { row += stepR; crossedR++; }
            if (colTime <= rowTime) { col += stepC; crossedC++; }
            if (Blocked(grid, row, col)) return false;
        }
        return true;
    }

    private static bool InBounds(int[,] grid, int row, int col) => grid != null &&
        row >= 0 && col >= 0 && row < grid.GetLength(0) && col < grid.GetLength(1);

    private static bool Blocked(int[,] grid, int row, int col) =>
        !InBounds(grid, row, col) || grid[row, col] != 0;
}
