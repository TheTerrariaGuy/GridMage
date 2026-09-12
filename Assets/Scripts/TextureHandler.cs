using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts
{
    public class TextureHandler : MonoBehaviour
    {
        public static TextureHandler INSTANCE { get; private set; }

        [Tooltip("Key = tile type * 100 + variant. 0: default surface; 1: default front; 2-48: neighbor shapes (2 = isolated preview); 49-52: stone fronts. See Docs/ElementSpriteMap.md.")]
        [SerializeField] public Dictionary<int, Sprite> spriteMap = new();
        [SerializeField] private Sprite fallbackSprite;
        [Tooltip("Height of the wall front in tile-local units.")]
        [SerializeField, Min(0f)] private float wallHeight = 0.25f;

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

        public void UpdateTexture(Tile tile)
        {
            ApplyTexture(tile);

            // Corners and front endpoints can change in any of the eight neighbors.
            Tile[,] tiles = tile.gameLogic != null ? tile.gameLogic.tilesGrid : null;
            for (int dr = -1; dr <= 1; dr++)
            {
                for (int dc = -1; dc <= 1; dc++)
                {
                    if (dr == 0 && dc == 0) continue;
                    int row = tile.row + dr, col = tile.col + dc;
                    if (!GridHelper.IsInBounds(tiles, row, col)) continue;
                    Tile neighbor = tiles[row, col];
                    if (neighbor != null) ApplyTexture(neighbor);
                }
            }
        }

        private void ApplyTexture(Tile tile)
        {
            int[,] grid = tile.gameLogic != null ? tile.gameLogic.grid : null;
            // Combat replaces the grid before refreshing Tile.type on each cell.
            // Read the current grid so neighbor refreshes never use a stale type.
            int type = GridHelper.IsInBounds(grid, tile.row, tile.col) ? grid[tile.row, tile.col] : tile.type;
            tile.SurfaceRenderer.enabled = type != 0 || tile.gameLogic == null || !tile.gameLogic.HasBackground;
            bool wall = TileSpriteLayout.IsWall(type);
            float height = wall ? Mathf.Max(0f, wallHeight) : 0f;
            tile.SetWallSorting(wall);
            int surfaceVariant = TileSpriteLayout.SurfaceVariant(grid, tile.row, tile.col, type);
            tile.SurfaceRenderer.color = Indexing.INSTANCE.colorMap.TryGetValue(type, out Color32 color) ? color : Color.white;
            SetSprite(tile, tile.SurfaceRenderer, GetSprite(type, surfaceVariant), 1f, 1f, height);

            SpriteRenderer front = tile.WallFrontRenderer;
            if (front == null) return;
            bool wallBelow = TileSpriteLayout.HasWall(grid, tile.row + 1, tile.col);
            front.enabled = wall && height > 0f && !wallBelow;
            if (!front.enabled) return;
            front.color = tile.SurfaceRenderer.color;
            SetSprite(tile, front, GetSprite(type, TileSpriteLayout.FrontVariant(grid, tile.row, tile.col)), 1f, height,
                -0.5f + height * 0.5f);
        }

        public Sprite GetPreviewSprite(int type) => GetSprite(type, 2);

        public void UpdatePreview(Tile tile, SpriteRenderer renderer, int type, float alpha)
        {
            UpdatePreview(tile, renderer, GetPreviewSprite(type), alpha);
        }

        public void UpdatePreview(Tile tile, SpriteRenderer renderer, Sprite sprite, float alpha)
        {
            SetSprite(tile, renderer, sprite, 1f, 1f, 0f);
            renderer.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
        }

        private Sprite GetSprite(int type, int variant)
        {
            if (spriteMap.TryGetValue(type * 100 + variant, out Sprite sprite) && sprite != null)
                return sprite;
            // Base/spent/fading stages share shapes unless a stage has an explicit override.
            if (TileSpriteLayout.Connects(type) &&
                spriteMap.TryGetValue(type / 100 * 10000 + variant, out sprite) && sprite != null)
                return sprite;
            if (variant >= 49 && spriteMap.TryGetValue(type * 100 + 1, out sprite) && sprite != null)
                return sprite;
            if (variant != 0 && spriteMap.TryGetValue(type * 100, out sprite) && sprite != null)
                return sprite;
            return fallbackSprite;
        }

        private static void SetSprite(Tile tile, SpriteRenderer renderer, Sprite sprite,
            float width, float height, float y)
        {
            if (renderer == null) return;
            renderer.sprite = sprite;
            if (sprite == null) return;
            // Fit the artwork without scaling the tile's collider, preview, or particles.
            Vector3 size = sprite.bounds.size;
            Vector3 scale = new Vector3(width / size.x, height / size.y, 1f);
            Vector3 parentScale = renderer.transform.parent != null ? renderer.transform.parent.lossyScale : Vector3.one;
            Vector3 tileScale = tile.transform.lossyScale;
            renderer.transform.localScale = Vector3.Scale(scale,
                new Vector3(tileScale.x / parentScale.x, tileScale.y / parentScale.y, tileScale.z / parentScale.z));
            Vector3 tilePosition = new Vector3(0f, y, 0f) - Vector3.Scale(sprite.bounds.center, scale);
            renderer.transform.position = tile.transform.TransformPoint(tilePosition);
        }
    }
}
