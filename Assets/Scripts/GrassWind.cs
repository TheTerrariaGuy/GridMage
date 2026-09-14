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
        [SerializeField] private float strength = 1.2f;
        [SerializeField] private float gradientSampleDistance = 0.02f;
        [SerializeField] private float updateRate = 12f;
        [SerializeField] private float perlinScale = 10f;

        private Tilemap background;
        private Vector3Int[] grassCells;
        private Vector2[] samplePositions;
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

        private void CacheGrassCells()
        {
            var cells = new List<Vector3Int>();
            foreach (Vector3Int cell in background.cellBounds.allPositionsWithin)
                if (background.GetTile(cell) == grassTiles[0]) cells.Add(cell);

            grassCells = cells.ToArray();
            samplePositions = new Vector2[grassCells.Length];
            currentTileIndices = new int[grassCells.Length];
            for (int i = 0; i < grassCells.Length; i++)
                samplePositions[i] = background.GetCellCenterWorld(grassCells[i]);
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
            for (int i = 0; i < grassCells.Length; i++)
            {
                // Subtract displacement so features travel along the wind direction.
                Vector2 position = (samplePositions[i] - windOffset) * noiseScale;
                int index = GetTileIndex(SampleGradientMagnitude(position));
                if (index == currentTileIndices[i]) continue;
                background.SetTile(grassCells[i], grassTiles[index]);
                currentTileIndices[i] = index;
            }
        }

        private float SampleGradientMagnitude(Vector2 position)
        {
            position /= perlinScale;
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
