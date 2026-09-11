using Assets.Scripts;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using static UnityEngine.Rendering.DebugUI.Table;

public class Selector : MonoBehaviour
{
    public static Selector INSTANCE { get; private set; }
    [SerializeField] private SpriteRenderer spriteRenderer;
    private int currentType;
    private Vector3 spriteSize;
    private float t;
    private Vector3 initPos;

    
    void Start()
    {
        if (INSTANCE != null)
        {
            Destroy(this);
            return;
        }
        initPos = transform.localPosition;
        INSTANCE = this;
        spriteSize = spriteRenderer.sprite.bounds.size;
        // Resize only the artwork; the selector's renderer shares its object with the click collider.
        var artwork = new GameObject("Selection artwork");
        artwork.layer = spriteRenderer.gameObject.layer;
        artwork.transform.SetParent(spriteRenderer.transform, false);
        artwork.transform.localPosition = spriteRenderer.sprite.bounds.center;
        var preview = artwork.AddComponent<SpriteRenderer>();
        preview.sharedMaterial = spriteRenderer.sharedMaterial;
        preview.sortingLayerID = spriteRenderer.sortingLayerID;
        preview.sortingOrder = spriteRenderer.sortingOrder;
        spriteRenderer.enabled = false;
        spriteRenderer = preview;
        currentType = 0;
        ChangeType();
    }
    
    private void ChangeType()
    {
        ChangeType(currentType >= 400 ? 100 : currentType + 100);
    }
    private void ChangeType(int type)
    {
        currentType = type;
        GameLogic.INSTANCE.UpdateSelection(currentType);
        Sprite sprite = TextureHandler.INSTANCE.GetPreviewSprite(currentType);
        spriteRenderer.sprite = sprite;
        spriteRenderer.color = Color.white;
        if (sprite != null)
        {
            spriteRenderer.transform.localScale = new Vector3(
                spriteSize.x / sprite.bounds.size.x, spriteSize.y / sprite.bounds.size.y, 1f);
        }
    }

    void Update()
    {
        HandleAnimations();
        
        if (Keyboard.current == null) return;
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            GameLogic.INSTANCE.SubmitQueuedSpells();
        }

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            ChangeType(100);
        }
        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            ChangeType(200);
        }
        if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            ChangeType(300);
        }
        if (Keyboard.current.digit4Key.wasPressedThisFrame)
        {
            ChangeType(400);
        }
    }
    private void HandleAnimations()
    {
        t += Time.deltaTime;

        if (t > 1f)
        {
            t -= 1f;
        }

        transform.localPosition = initPos + 0.15f * Vector3.up * Mathf.Pow(Floaty(t * 2f - 0.5f), 2);

    }
    private float Floaty(float x)
    {
        return Mathf.Sin(x * Mathf.PI);
    }
}
