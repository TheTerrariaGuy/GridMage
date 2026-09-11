using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using System.Collections;

namespace Assets.Scripts
{
    [DefaultExecutionOrder(-50)]
    public class PlayerHandler : MonoBehaviour
    {
        public static PlayerHandler INSTANCE;
        public float hp;
        [SerializeField] public int r, c;
        [SerializeField] private SpriteRenderer playerSprite;
        [SerializeField, Min(0f), Tooltip("Blink animation duration in seconds.")]
        private float blinkSpeed = .1f;
        private float blinkReadyAt;
        private bool isBlinking;
        private Vector3 blinkOrigin;

        void Start()
        {
            if (INSTANCE != null)
            {
                Destroy(this);
                return;
            }
            INSTANCE = this;
            Vector3 tilePosition = GridHelper.INSTANCE.GetTileTransform(r, c).position;
            transform.position = tilePosition;
            GameLogic.INSTANCE.MakeCastable();
        }

        public void TakeDamage(float damage)
        {
            hp -= damage;
        }

        public bool IsInBlinkRange(Tile target)
        {
            if (target == null || Indexing.INSTANCE == null) return false;
            int distance = Mathf.Max(Mathf.Abs(target.row - r), Mathf.Abs(target.col - c));
            return distance > 0 && distance <= Indexing.INSTANCE.blinkRange;
        }

        /// <summary>Square range and line of sight against a walls graph, including diagonal corners.</summary>
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

        public bool BlinkTo(Tile target)
        {
            if (!isActiveAndEnabled || isBlinking || Time.time < blinkReadyAt || !IsInBlinkRange(target) ||
                !GameLogic.INSTANCE.Castable(target.row, target.col))
                return false;
            isBlinking = true;
            blinkReadyAt = Time.time + Indexing.INSTANCE.blinkCooldown;
            StartCoroutine(AnimateBlink(target));
            return true;
        }

        private IEnumerator AnimateBlink(Tile target)
        {
            blinkOrigin = transform.position;
            Vector3 destination = target.transform.position;
            int row = target.row, col = target.col;
            float duration = blinkSpeed;
            StartCoroutine(Afterimage());
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                float t = .5f - .5f * Mathf.Cos(Mathf.PI * elapsed / duration);
                transform.position = Vector3.Lerp(blinkOrigin, destination, t);
                yield return null;
            }
            transform.position = destination;
            r = row;
            c = col;
            GameLogic.INSTANCE.MakeCastable();
            isBlinking = false;
            StartCoroutine(Afterimage());
            MobHandler.INSTANCE?.RefreshAfterPlayerMove();
        }

        private void OnDisable()
        {
            if (!isBlinking) return;
            StopAllCoroutines();
            transform.position = blinkOrigin;
            isBlinking = false;
        }

        private IEnumerator Afterimage()
        {
            // The player's children also contain the floating spell selector and its hidden source sprite.
            if (playerSprite == null)
                foreach (SpriteRenderer candidate in GetComponentsInChildren<SpriteRenderer>())
                    if (candidate.enabled && candidate.sprite != null && candidate.GetComponentInParent<Selector>() == null)
                    {
                        playerSprite = candidate;
                        break;
                    }
            SpriteRenderer source = playerSprite;
            if (source == null || source.sprite == null) yield break;
            var effect = new GameObject("Blink afterimage");
            effect.layer = source.gameObject.layer;
            effect.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            effect.transform.localScale = source.transform.lossyScale;
            var sprite = effect.AddComponent<SpriteRenderer>();
            sprite.sprite = source.sprite;
            sprite.sharedMaterial = source.sharedMaterial;
            sprite.flipX = source.flipX;
            sprite.flipY = source.flipY;
            sprite.sortingLayerName = WorldSorting.Foreground;
            sprite.sortingOrder = 1;
            const float duration = .2f;
            Destroy(effect, duration);
            for (float elapsed = 0f; elapsed < duration && sprite != null; elapsed += Time.deltaTime)
            {
                sprite.color = new Color(.45f, .9f, 1f, .65f * (1f - elapsed / duration));
                yield return null;
            }
        }

        private void OnDestroy()
        {
            if (INSTANCE == this) INSTANCE = null;
        }
    }
}
