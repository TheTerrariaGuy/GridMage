using System;

namespace Assets.Scripts
{
    // Clockwise neighbor bits: N, NE, E, SE, S, SW, W, NW.
    // The explicit order keeps serialized sprite IDs stable.
    public static class TileSpriteLayout
    {
        private static readonly int[] surfaceMasks =
        {
            0, 1, 4, 5, 7, 16, 17, 20, 21, 23, 28, 29, 31, 64, 65, 68,
            69, 71, 80, 81, 84, 85, 87, 92, 93, 95, 112, 113, 116, 117,
            119, 124, 125, 127, 193, 197, 199, 209, 213, 215, 221, 223,
            241, 245, 247, 253, 255
        };

        public static bool IsWall(int type) => type / 100 == 4 && type % 100 < 10;

        public static bool Connects(int type) => type >= 100 && type < 410 && type % 100 < 10;

        private static bool HasElement(int[,] grid, int row, int col, int type) =>
            grid != null && row >= 0 && col >= 0 &&
            row < grid.GetLength(0) && col < grid.GetLength(1) &&
            Connects(grid[row, col]) && grid[row, col] / 100 == type / 100;

        public static bool HasWall(int[,] grid, int row, int col) =>
            grid != null && row >= 0 && col >= 0 &&
            row < grid.GetLength(0) && col < grid.GetLength(1) && IsWall(grid[row, col]);

        public static int SurfaceVariant(int[,] grid, int row, int col, int type = 400)
        {
            if (!Connects(type)) return 0;
            bool n = HasElement(grid, row - 1, col, type);
            bool e = HasElement(grid, row, col + 1, type);
            bool s = HasElement(grid, row + 1, col, type);
            bool w = HasElement(grid, row, col - 1, type);
            int mask = (n ? 1 : 0) | (e ? 4 : 0) | (s ? 16 : 0) | (w ? 64 : 0);
            // Water and stone fill joined corners. Fire/lightning retain gaps in 2x2 blocks.
            if (type / 100 == 2 || type / 100 == 4)
            {
                if (n && e && HasElement(grid, row - 1, col + 1, type)) mask |= 2;
                if (e && s && HasElement(grid, row + 1, col + 1, type)) mask |= 8;
                if (s && w && HasElement(grid, row + 1, col - 1, type)) mask |= 32;
                if (w && n && HasElement(grid, row - 1, col - 1, type)) mask |= 128;
            }
            return 2 + Array.BinarySearch(surfaceMasks, mask);
        }

        public static bool HasFront(int[,] grid, int row, int col) =>
            HasWall(grid, row, col) && !HasWall(grid, row + 1, col);

        public static int FrontVariant(int[,] grid, int row, int col)
        {
            // Only exposed fronts join; a covered neighboring wall ends this face.
            bool left = HasFront(grid, row, col - 1);
            bool right = HasFront(grid, row, col + 1);
            return 49 + (left ? 1 : 0) + (right ? 2 : 0);
        }
    }
}
