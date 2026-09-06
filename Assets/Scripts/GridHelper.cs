using System.Collections;
using UnityEngine;

namespace Assets.Scripts
{
    public class GridHelper : MonoBehaviour
    {

        public static GridHelper INSTANCE { get; private set; }

        private void Awake()
        {
            if (INSTANCE != null && INSTANCE != this)
            {
                Destroy(this);
                return;
            }
            INSTANCE = this;
        }

        private void OnDestroy()
        {
            if (INSTANCE == this) INSTANCE = null;
        }

        public Transform GetTileTransform(int r, int c)
        {
            if (!IsInBounds(GameLogic.INSTANCE.tilesGrid, r, c))
                throw new System.ArgumentOutOfRangeException(nameof(r), "Tile coordinates are outside the initialized grid.");
            return GameLogic.INSTANCE.tilesGrid[r, c].transform;
        }

        public Vector3 GetLocalPosition(int row, int col, float spacing, float offset)
        {
            if (spacing <= 0f || float.IsNaN(spacing) || float.IsInfinity(spacing))
                throw new System.ArgumentOutOfRangeException(nameof(spacing), "Grid spacing must be finite and positive.");
            return new Vector3(col * spacing + offset, -row * spacing + offset, 0f);
        }

        public Transform GetAnchor()
        {
            return GameLogic.INSTANCE.gridParent;
        }

        public static bool IsInBounds<T>(T[,] grid, int row, int col)
        {
            return grid != null && row >= 0 && row < grid.GetLength(0) &&
                   col >= 0 && col < grid.GetLength(1);
        }

        public int[,] ExtractWalls()
        {
            return ExtractWalls(GameLogic.INSTANCE.grid);
        }

        public static bool IsInBounds<T>(T[,,] grid, int channel, int row, int col)
        {
            return grid != null && channel >= 0 && channel < grid.GetLength(0) &&
                row >= 0 && row < grid.GetLength(1) && col >= 0 && col < grid.GetLength(2);
        }

        public int[,] ExtractWalls(int[,] grid)
        {
            int[,] walls = new int[grid.GetLength(0), grid.GetLength(1)];
            for (int i = 0; i < grid.GetLength(0); i++)
            {
                for (int j = 0; j < grid.GetLength(1); j++)
                {
                    walls[i, j] = grid[i, j] / 100 == 4 && grid[i, j] % 100 < 10 ? 1 : 0;
                }
            }
            return walls;
        }

        // The position is in world space; rows increase down the grid.
        public Tile GetTileOn(Vector3 position)
        {
            if (!TryGetTileOn(position, out Tile tile))
                throw new System.ArgumentOutOfRangeException(nameof(position), "Position is outside the initialized grid.");
            return tile;
        }

        public bool TryGetTileOn(Vector3 position, out Tile tile)
        {
            tile = null;
            GameLogic gameLogic = GameLogic.INSTANCE;
            if (gameLogic == null || gameLogic.gridParent == null || gameLogic.spacing <= 0f) return false;
            Vector3 localPosition = GetAnchor().InverseTransformPoint(position);
            if (float.IsNaN(localPosition.x) || float.IsInfinity(localPosition.x) ||
                float.IsNaN(localPosition.y) || float.IsInfinity(localPosition.y)) return false;
            int row = Mathf.RoundToInt((gameLogic.offset - localPosition.y) / gameLogic.spacing);
            int col = Mathf.RoundToInt((localPosition.x - gameLogic.offset) / gameLogic.spacing);
            if (!IsInBounds(gameLogic.tilesGrid, row, col)) return false;
            tile = gameLogic.tilesGrid[row, col];
            return tile != null;
        }

        public bool TestForWalls(int[,] walls, int r1, int c1, int r2, int c2, bool allowTarget = false)
        {
            if (!IsInBounds(walls, r1, c1) || !IsInBounds(walls, r2, c2)) return false;
            int dr = System.Math.Abs(r2 - r1), dc = System.Math.Abs(c2 - c1);
            int stepR = System.Math.Sign(r2 - r1), stepC = System.Math.Sign(c2 - c1);
            long crossedR = 0, crossedC = 0;

            while (r1 != r2 || c1 != c2)
            {
                long rowTime = (2 * crossedR + 1) * dc;
                long colTime = (2 * crossedC + 1) * dr;
                if (rowTime <= colTime)
                {
                    r1 += stepR;
                    crossedR++;
                }
                if (colTime <= rowTime)
                {
                    c1 += stepC;
                    crossedC++;
                }
                if (!IsInBounds(walls, r1, c1) ||
                    (walls[r1, c1] != 0 && !(allowTarget && r1 == r2 && c1 == c2))) return false;
            }
            return true;
        }
    }
}
