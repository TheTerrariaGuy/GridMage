using System;
using UnityEngine;
using System.Collections;

namespace Assets.Scripts
{
    [DefaultExecutionOrder(-50)]
    public class PlayerHandler : MonoBehaviour
    {
        public static PlayerHandler INSTANCE;
        public float hp;
        [Min(0f)] public float blinkCooldown = .25f;
        [Min(1)] public int blinkRange = 3;
        [Min(0)] public int castRange = 3;
        [SerializeField, Min(0f)] private float blinkManaCost = 4f;
        public float BlinkManaCost => blinkManaCost;
        [SerializeField] public int r, c;
        [SerializeField] private SpriteRenderer playerSprite;
        [SerializeField, Min(0f), Tooltip("Blink animation duration in seconds.")]
        private float blinkSpeed = .1f;
        private float blinkReadyAt;
        private bool isBlinking;
        private Vector3 blinkOriginLocal;

        private void Awake()
        {
            if (INSTANCE != null)
            {
                Destroy(this);
                return;
            }
            INSTANCE = this;
        }

        public void ResetForLevel()
        {
            StopAllCoroutines();
            isBlinking = false;
            blinkReadyAt = 0f;
            var game = GameLogic.INSTANCE;
            if (game == null || game.grid == null) return;
            if (game.LevelLayout != null)
            {
                r = game.LevelLayout.player.y;
                c = game.LevelLayout.player.x;
            }
            if (!game.CanWalk(r, c))
                throw new InvalidOperationException($"Player spawn ({r}, {c}) is not a walkable cell.");
            // Player and tiles are direct children of GridParent.
            transform.localPosition = GridHelper.INSTANCE.GetTileTransform(r, c).localPosition;
        }

        public void TakeDamage(float damage)
        {
            hp -= damage;
        }

        public bool IsInBlinkRange(Tile target)
        {
            if (target == null) return false;
            int distance = GridMath.SquareDistance(target.row, target.col, r, c);
            return distance > 0 && distance <= blinkRange;
        }

        public bool BlinkTo(Tile target)
        {
            if (!isActiveAndEnabled || isBlinking || Time.time < blinkReadyAt || !IsInBlinkRange(target) ||
                !GameLogic.INSTANCE.CanBlinkTo(target.row, target.col))
                return false;
            isBlinking = true;
            blinkReadyAt = Time.time + blinkCooldown;
            StartCoroutine(AnimateBlink(target));
            return true;
        }

        private IEnumerator AnimateBlink(Tile target)
        {
            blinkOriginLocal = transform.localPosition;
            Vector3 destination = target.transform.localPosition;
            int row = target.row, col = target.col;
            float duration = blinkSpeed;
            StartCoroutine(Afterimage());
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                float t = .5f - .5f * Mathf.Cos(Mathf.PI * elapsed / duration);
                transform.localPosition = Vector3.Lerp(blinkOriginLocal, destination, t);
                yield return null;
            }
            transform.localPosition = destination;
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
            transform.localPosition = blinkOriginLocal;
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
