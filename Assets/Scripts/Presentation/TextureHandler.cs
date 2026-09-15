using UnityEngine;

namespace Assets.Scripts
{
    public class TextureHandler : MonoBehaviour
    {
        public static TextureHandler INSTANCE { get; private set; }

        [SerializeField] private SpriteCatalog spriteCatalog;
        [SerializeField] private Sprite fallbackSprite;
        [Tooltip("Vertical offset of wall artwork in tile-local units.")]
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

        public void ApplyTexture(Tile tile)
        {
            int[,] grid = tile.gameLogic != null ? tile.gameLogic.grid : null;
            int type = tile.type;
            tile.SurfaceRenderer.enabled = type != 0 || tile.gameLogic == null || tile.gameLogic.Level == null;
            bool wall = TileSpriteLayout.IsWall(type);
            float height = wall ? Mathf.Max(0f, wallHeight) : 0f;
            tile.SetWallSorting(wall);
            int surfaceVariant = TileSpriteLayout.SurfaceVariant(grid, tile.row, tile.col, type);
            tile.SurfaceRenderer.color = new Color32(255, 255, 255, ElementDefinitions.Alpha(type));
            SetSprite(tile, tile.SurfaceRenderer, GetSprite(type, surfaceVariant), height);
        }

        public Sprite GetPreviewSprite(int type) => GetSprite(type, 2);

        public void UpdatePreview(Tile tile, SpriteRenderer renderer, int type, float alpha)
        {
            UpdatePreview(tile, renderer, GetPreviewSprite(type), alpha);
        }

        public void UpdatePreview(Tile tile, SpriteRenderer renderer, Sprite sprite, float alpha)
        {
            SetSprite(tile, renderer, sprite, 0f);
            renderer.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
        }

        private Sprite GetSprite(int type, int variant)
        {
            if (spriteCatalog.TryGet(type * 100 + variant, out Sprite sprite) && sprite != null)
                return sprite;
            // Base/spent/fading stages share shapes unless a stage has an explicit override.
            if (TileSpriteLayout.Connects(type) &&
                spriteCatalog.TryGet(type / 100 * 10000 + variant, out sprite) && sprite != null)
                return sprite;
            if (variant != 0 && spriteCatalog.TryGet(type * 100, out sprite) && sprite != null)
                return sprite;
            return fallbackSprite;
        }

        private static void SetSprite(Tile tile, SpriteRenderer renderer, Sprite sprite,
            float y)
        {
            if (renderer == null) return;
            renderer.sprite = sprite;
            if (sprite == null) return;
            // Fit the artwork without scaling the tile's collider, preview, or particles.
            Vector3 size = sprite.bounds.size;
            Vector3 scale = new Vector3(1f / size.x, 1f / size.y, 1f);
            Vector3 parentScale = renderer.transform.parent != null ? renderer.transform.parent.lossyScale : Vector3.one;
            Vector3 tileScale = tile.transform.lossyScale;
            renderer.transform.localScale = Vector3.Scale(scale,
                new Vector3(tileScale.x / parentScale.x, tileScale.y / parentScale.y, tileScale.z / parentScale.z));
            Vector3 tilePosition = new Vector3(0f, y, 0f) - Vector3.Scale(sprite.bounds.center, scale);
            renderer.transform.position = tile.transform.TransformPoint(tilePosition);
        }
    }
}
