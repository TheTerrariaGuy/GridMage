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
            if (!GameLogic.INSTANCE.HasCell(r, c) || GameLogic.INSTANCE.tilesGrid[r, c] == null)
                throw new System.ArgumentOutOfRangeException(nameof(r), "Tile coordinates are outside the initialized grid.");
            return GameLogic.INSTANCE.tilesGrid[r, c].transform;
        }

        public Vector3 GetLocalPosition(int row, int col, float spacing, float offset)
        {
            if (spacing <= 0f || float.IsNaN(spacing) || float.IsInfinity(spacing))
                throw new System.ArgumentOutOfRangeException(nameof(spacing), "Grid spacing must be finite and positive.");
            return new Vector3(col * spacing + offset, -row * spacing + offset, 0f) +
                (GameLogic.INSTANCE != null ? GameLogic.INSTANCE.GridTranslation : Vector3.zero);
        }

        public Transform GetAnchor()
        {
            return GameLogic.INSTANCE.gridParent;
        }

        public int[,] ExtractWalls() => GameLogic.INSTANCE.Board.SightWalls(GameLogic.INSTANCE.grid);

        public bool TryGetTileOn(Vector3 position, out Tile tile)
        {
            tile = null;
            GameLogic gameLogic = GameLogic.INSTANCE;
            if (gameLogic == null || gameLogic.gridParent == null || gameLogic.spacing <= 0f) return false;
            if (float.IsNaN(position.x) || float.IsInfinity(position.x) ||
                float.IsNaN(position.y) || float.IsInfinity(position.y)) return false;
            if (gameLogic.LevelLayout != null)
            {
                Vector3Int cell = gameLogic.Level.logic.WorldToCell(position);
                Vector2Int index = gameLogic.LevelLayout.ToIndex(cell);
                if (cell.z != 0 || !gameLogic.HasCell(index.y, index.x)) return false;
                tile = gameLogic.tilesGrid[index.y, index.x];
                return tile != null;
            }
            Vector3 localPosition = GetAnchor().InverseTransformPoint(position) - gameLogic.GridTranslation;
            if (float.IsNaN(localPosition.x) || float.IsInfinity(localPosition.x) ||
                float.IsNaN(localPosition.y) || float.IsInfinity(localPosition.y)) return false;
            int row = Mathf.RoundToInt((gameLogic.offset - localPosition.y) / gameLogic.spacing);
            int col = Mathf.RoundToInt((localPosition.x - gameLogic.offset) / gameLogic.spacing);
            if (!gameLogic.HasCell(row, col)) return false;
            tile = gameLogic.tilesGrid[row, col];
            return tile != null;
        }

        public bool TestElevationLine(int r1, int c1, int r2, int c2) =>
            GameLogic.INSTANCE != null && GameLogic.INSTANCE.Board.ElevationLine(r1, c1, r2, c2);
    }
}
