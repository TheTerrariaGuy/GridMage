using System;
using System.Collections.Generic;
using Assets.Scripts.ScriptableObjects;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Assets.Scripts
{
    [DisallowMultipleComponent]
    public sealed class TilemapLevel : MonoBehaviour
    {
        public Tilemap logic;

        public sealed class Layout
        {
            public BoundsInt bounds;
            public bool[,] exists, walkable, blocksSight, allowsSpells;
            public float[,] elevations;
            public Vector2Int player; // x = column, y = row
            public readonly List<Spawn> enemies = new();
            public Vector3 origin;
            public float spacing;
            public Vector3Int ToCell(int row, int col) =>
                new Vector3Int(bounds.xMin + col, bounds.yMax - 1 - row, 0);
            public Vector2Int ToIndex(Vector3Int cell) =>
                new Vector2Int(cell.x - bounds.xMin, bounds.yMax - 1 - cell.y);
        }

        public readonly struct Spawn
        {
            public readonly int row, col;
            public readonly EnemyData enemy;
            public readonly float interval;
            public Spawn(int row, int col, EnemyData enemy, float interval)
            { this.row = row; this.col = col; this.enemy = enemy; this.interval = interval; }
        }

        // Parsing never changes the painted map or its shared tile assets.
        public Layout Read(Transform runtimeAnchor)
        {
            if (logic == null || runtimeAnchor == null)
                throw new InvalidOperationException("Assign the Logic tilemap and runtime grid parent.");
            if (logic.layoutGrid == null || logic.layoutGrid.cellLayout != GridLayout.CellLayout.Rectangle)
                throw new InvalidOperationException("Levels require a rectangular Unity Grid.");
            if (logic.GetComponent<TilemapCollider2D>() != null)
                throw new InvalidOperationException("Remove the collider from Logic; runtime cells handle picking.");

            var markers = new List<(Vector3Int cell, LevelMarkerTile marker)>();
            Vector3Int min = new Vector3Int(int.MaxValue, int.MaxValue, 0);
            Vector3Int max = new Vector3Int(int.MinValue, int.MinValue, 1);
            foreach (Vector3Int cell in logic.cellBounds.allPositionsWithin)
            {
                TileBase tile = logic.GetTile(cell);
                if (tile == null) continue;
                if (cell.z != 0 || !(tile is LevelMarkerTile marker))
                    throw new InvalidOperationException($"Logic cell {cell} must contain a LevelMarkerTile at Z = 0.");
                markers.Add((cell, marker));
                min = Vector3Int.Min(min, cell);
                max = Vector3Int.Max(max, cell + Vector3Int.one);
            }
            if (markers.Count == 0) throw new InvalidOperationException("Logic has no painted cells.");
            var layout = new Layout { bounds = new BoundsInt(min, max - min) };
            int rows = layout.bounds.size.y, cols = layout.bounds.size.x;
            layout.exists = new bool[rows, cols];
            layout.walkable = new bool[rows, cols];
            layout.blocksSight = new bool[rows, cols];
            layout.allowsSpells = new bool[rows, cols];
            layout.elevations = new float[rows, cols];
            int players = 0;
            foreach (var entry in markers)
            {
                var marker = entry.marker;
                Vector2Int index = layout.ToIndex(entry.cell);
                int r = index.y, c = index.x;
                if (float.IsNaN(marker.elevation) || float.IsInfinity(marker.elevation) ||
                    marker.elevation * 2f != Mathf.Round(marker.elevation * 2f))
                    throw new InvalidOperationException($"Elevation at {entry.cell} must be a finite multiple of 0.5.");
                layout.elevations[r, c] = marker.elevation;
                layout.exists[r, c] = true;
                layout.walkable[r, c] = marker.walkable;
                layout.blocksSight[r, c] = marker.blocksSight;
                layout.allowsSpells[r, c] = marker.allowsSpells;
                if (marker.spawnKind != LevelSpawnKind.None && !marker.walkable)
                    throw new InvalidOperationException($"Spawn at {entry.cell} must be walkable.");
                if (marker.spawnKind == LevelSpawnKind.Player) { layout.player = index; players++; }
                if (marker.spawnKind == LevelSpawnKind.Enemy)
                {
                    if (marker.enemy == null || float.IsNaN(marker.spawnInterval) ||
                        float.IsInfinity(marker.spawnInterval) || marker.spawnInterval < 0f)
                        throw new InvalidOperationException($"Enemy spawn at {entry.cell} needs EnemyData and a finite, nonnegative interval.");
                    layout.enemies.Add(new Spawn(r, c, marker.enemy, marker.spawnInterval));
                }
            }
            if (players != 1) throw new InvalidOperationException($"Logic needs exactly one Player marker; found {players}.");

            Vector3Int first = layout.ToCell(0, 0);
            layout.origin = runtimeAnchor.InverseTransformPoint(logic.GetCellCenterWorld(first));
            Vector3 right = runtimeAnchor.InverseTransformPoint(logic.GetCellCenterWorld(first + Vector3Int.right)) - layout.origin;
            Vector3 up = runtimeAnchor.InverseTransformPoint(logic.GetCellCenterWorld(first + Vector3Int.up)) - layout.origin;
            layout.spacing = right.x;
            if (!(layout.spacing > 0f) || !Close(right, Vector3.right * layout.spacing) ||
                !Close(up, Vector3.up * layout.spacing))
                throw new InvalidOperationException("Logic cells must be square and aligned with the runtime grid parent, with no cell gap.");
            Vector3 cellSize = runtimeAnchor.InverseTransformVector(logic.transform.TransformVector(logic.layoutGrid.cellSize));
            if (Mathf.Abs(cellSize.x - layout.spacing) > .001f || Mathf.Abs(cellSize.y - layout.spacing) > .001f)
                throw new InvalidOperationException("Logic cells must have no gap and matching X/Y size.");
            return layout;
        }

        public void HideMarkers()
        {
            var renderer = logic.GetComponent<TilemapRenderer>();
            if (renderer != null) renderer.enabled = false;
        }

        private static bool Close(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < .000001f;
    }
}
