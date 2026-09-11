using Assets.Scripts;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public sealed class GridPointer : MonoBehaviour
{
    [SerializeField] private Camera viewCamera;
    [SerializeField] private Sprite borderSprite, teleportSprite;
    [SerializeField, Range(0f, 1f)] private float hoverAlpha;
    [SerializeField] private SpriteRenderer hoverOverlay;
    private Tile hovered;
    private bool consumedLeftPress;

    public Tile Pick(Vector2 screenPosition)
    {
        if (viewCamera == null || !viewCamera.pixelRect.Contains(screenPosition)) return null;
        Ray ray = viewCamera.ScreenPointToRay(screenPosition);
        var hit = Physics2D.GetRayIntersection(ray, Mathf.Infinity, LayerMask.GetMask("Default"));
        return hit.collider != null ? hit.collider.GetComponent<Tile>() : null;
    }

    private void Update()
    {
        var mouse = Mouse.current;
        var keyboard = Keyboard.current;
        bool blinkMode = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
        if (mouse == null || !mouse.leftButton.isPressed) consumedLeftPress = false;
        else if (blinkMode) consumedLeftPress = true;
        Tile next = mouse != null && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
            ? Pick(mouse.position.ReadValue()) : null;
        SetHovered(next, blinkMode);
        if (hovered == null || mouse == null || GameLogic.INSTANCE == null) return;
        if (mouse.rightButton.isPressed) GameLogic.INSTANCE.RemoveQueuedSpell(hovered.row, hovered.col);
        else if (blinkMode)
        {
            if (mouse.leftButton.wasPressedThisFrame) GameLogic.INSTANCE.TryCastBlink(hovered);
        }
        else if (mouse.leftButton.isPressed && !consumedLeftPress) GameLogic.INSTANCE.Clicked(hovered);
    }

    public void SetHovered(Tile tile, bool teleport = false)
    {
        hovered = tile;
        if (hoverOverlay == null) return;
        hoverOverlay.enabled = hovered != null;
        if (hovered == null) return;
        teleport = teleport && PlayerHandler.INSTANCE != null && PlayerHandler.INSTANCE.IsInBlinkRange(hovered) &&
            GameLogic.INSTANCE.Castable(hovered.row, hovered.col);
        TextureHandler.INSTANCE.UpdatePreview(hovered, hoverOverlay,
            teleport ? teleportSprite : borderSprite, hoverAlpha);
    }

    private void OnDisable()
    {
        SetHovered(null);
        consumedLeftPress = false;
    }

}
