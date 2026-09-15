using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts
{
    /// <summary>Updates changed stages and each affected neighbor sprite once against the final board.</summary>
    public sealed class TilePresentation
    {
        private readonly HashSet<Vector2Int> dirty = new();
        public int LastSpriteRefreshCount { get; private set; }
        public void Reset(Tile[,] tiles)
        {
            dirty.Clear();
            foreach (var tile in tiles) if (tile != null) tile.InvalidatePresentation();
        }
        public void Refresh(Tile[,] tiles, bool force)
        {
            dirty.Clear();
            foreach (var tile in tiles)
                if (tile != null && (force || tile.NeedsPresentation))
                {
                    tile.RefreshStage();
                    MarkNeighbors(tiles, tile.row, tile.col);
                }
            Flush(tiles);
        }
        public void RefreshCell(Tile[,] tiles, int row, int col)
        {
            dirty.Clear();
            if (tiles[row, col] == null) return;
            tiles[row, col].RefreshStage();
            MarkNeighbors(tiles, row, col);
            Flush(tiles);
        }
        private void MarkNeighbors(Tile[,] tiles, int row, int col)
        {
            for (int dr = -1; dr <= 1; dr++) for (int dc = -1; dc <= 1; dc++)
                if (GridMath.IsInBounds(tiles, row + dr, col + dc) && tiles[row + dr, col + dc] != null)
                    dirty.Add(new Vector2Int(col + dc, row + dr));
        }
        private void Flush(Tile[,] tiles)
        {
            LastSpriteRefreshCount = dirty.Count;
            foreach (var cell in dirty) TextureHandler.INSTANCE?.ApplyTexture(tiles[cell.y, cell.x]);
        }
    }
}
