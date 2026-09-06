using Assets.Scripts;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>Pick tiles through the full-resolution view, independently of render-target dimensions.</summary>
public sealed class GridPointer : MonoBehaviour
{
    [SerializeField] private Camera viewCamera;
    private Tile hovered;

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
        Tile next = mouse != null && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
            ? Pick(mouse.position.ReadValue()) : null;
        if (next != hovered)
        {
            if (hovered != null) hovered.SetHovered(false);
            hovered = next;
            if (hovered != null) hovered.SetHovered(true);
        }
        if (hovered == null || mouse == null || GameLogic.INSTANCE == null) return;
        if (mouse.rightButton.isPressed) GameLogic.INSTANCE.RemoveQueuedSpell(hovered.row, hovered.col);
        else if (mouse.leftButton.isPressed) GameLogic.INSTANCE.Clicked(hovered);
    }

    private void OnDisable()
    {
        if (hovered != null) hovered.SetHovered(false);
        hovered = null;
    }
}
