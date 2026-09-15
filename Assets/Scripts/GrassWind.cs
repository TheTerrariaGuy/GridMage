using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Assets.Scripts
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Tilemap))]
    public sealed class GrassWind : MonoBehaviour
    {
        [SerializeField, Tooltip("Spring tiles in order: 13, 27, 39, 53, 69. Only cells initially painted with the first tile become grass.")]
        private TileBase[] grassTiles = new TileBase[5];
        [SerializeField] private Vector2 windDirection = Vector2.ClampMagnitude(Vector2.right + Vector2.down, 1f);
        [SerializeField] private float windSpeed = 1.5f;
        [SerializeField] private float noiseScale = 0.18f;
        [SerializeField] private float strength = 1f;
        [SerializeField] private float gradientSampleDistance = 0.2f;
        [SerializeField] private float updateRate = 12f;
        [SerializeField] private float perlinScale = 10f;
        [SerializeField] private float crosswindStretch = 1f;
        [SerializeField] private float elevationDelaySeconds = 0.5f;

        private Tilemap background;
        private Vector3Int[] grassCells;
        private Vector2[] samplePositions;
        private float[] grassElevations;
        private int[] currentTileIndices;
        private Vector2 windOffset;
        private float updateTimer;

        private void Awake()
        {
            background = GetComponent<Tilemap>();
            if (grassTiles == null || grassTiles.Length != 5 ||
                System.Array.Exists(grassTiles, tile => tile == null))
            {
                Debug.LogError("GrassWind needs the five spring tiles: 13, 27, 39, 53, 69.", this);
                enabled = false;
                return;
            }
            CacheGrassCells();
        }

        private void Start()
        {
            // GameLogic initializes the layout in Start with execution order -100.
            CacheGrassElevations();
        }

        private void CacheGrassCells()
        {
            var cells = new List<Vector3Int>();
            foreach (Vector3Int cell in background.cellBounds.allPositionsWithin)
                if (background.GetTile(cell) == grassTiles[0]) cells.Add(cell);

            grassCells = cells.ToArray();
            samplePositions = new Vector2[grassCells.Length];
            grassElevations = new float[grassCells.Length];
            currentTileIndices = new int[grassCells.Length];
            for (int i = 0; i < grassCells.Length; i++)
                samplePositions[i] = background.GetCellCenterWorld(grassCells[i]);
        }

        private void CacheGrassElevations()
        {
            var game = GameLogic.INSTANCE;
            var layout = game != null ? game.LevelLayout : null;
            if (layout == null || game.gridParent == null) return;

            for (int i = 0; i < grassCells.Length; i++)
            {
                // Use the initialized layout's coordinates; no other tilemap is scanned.
                Vector3 local = game.gridParent.InverseTransformPoint(
                    background.GetCellCenterWorld(grassCells[i]));
                int col = Mathf.RoundToInt((local.x - layout.origin.x) / layout.spacing);
                int row = Mathf.RoundToInt((layout.origin.y - local.y) / layout.spacing);
                if (row >= 0 && row < layout.exists.GetLength(0) &&
                    col >= 0 && col < layout.exists.GetLength(1) && layout.exists[row, col])
                    grassElevations[i] = layout.elevations[row, col];
            }
        }

        private void Update()
        {
            windOffset += windDirection.normalized * (windSpeed * perlinScale * Time.deltaTime);
            updateTimer -= Time.deltaTime;
            if (updateTimer > 0f) return;
            updateTimer = 1f / updateRate;
            UpdateGrassTiles();
        }

        private void UpdateGrassTiles()
        {
            Vector2 velocity = windDirection.normalized * (windSpeed * perlinScale);
            for (int i = 0; i < grassCells.Length; i++)
            {
                float delay = grassElevations[i] * elevationDelaySeconds;
                // Positive delay samples an earlier point in the moving noise pattern.
                Vector2 position = (samplePositions[i] - windOffset + velocity * delay) * noiseScale;
                int index = GetTileIndex(SampleGradientMagnitude(position));
                if (index == currentTileIndices[i]) continue;
                background.SetTile(grassCells[i], grassTiles[index]);
                currentTileIndices[i] = index;
            }
        }

        private float SampleGradientMagnitude(Vector2 position)
        {
            position /= perlinScale;
            Vector2 direction = windDirection.normalized;
            if (direction == Vector2.zero) direction = Vector2.right;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            float across = Vector2.Dot(position, perpendicular);
            // Compress sampling coordinates to widen the pattern across the wind.
            position += perpendicular * across * (1f / Mathf.Max(0.01f, crosswindStretch) - 1f);

            float step = gradientSampleDistance;
            float dx = (Mathf.PerlinNoise(position.x + step, position.y) -
                        Mathf.PerlinNoise(position.x - step, position.y)) / (2f * step);
            float dy = (Mathf.PerlinNoise(position.x, position.y + step) -
                        Mathf.PerlinNoise(position.x, position.y - step)) / (2f * step);
            // Differentiate in noise coordinates so gust size and strength tune independently.
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private int GetTileIndex(float magnitude)
        {
            float value = Mathf.Clamp01(magnitude * strength);
            return Mathf.Min(Mathf.FloorToInt(value * grassTiles.Length), grassTiles.Length - 1);
        }

        private void OnDisable()
        {
            if (background == null || grassCells == null) return;
            for (int i = 0; i < grassCells.Length; i++)
            {
                if (currentTileIndices[i] == 0) continue;
                background.SetTile(grassCells[i], grassTiles[0]);
                currentTileIndices[i] = 0;
            }
            updateTimer = 0f;
        }
    }
}
