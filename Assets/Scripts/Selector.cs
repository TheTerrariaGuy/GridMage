using Assets.Scripts;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using static UnityEngine.Rendering.DebugUI.Table;

public class Selector : MonoBehaviour, IPointerClickHandler
{
    public static Selector INSTANCE { get; private set; }
    [SerializeField] private SpriteRenderer spriteRenderer;
    private int currentType;
    
    void Start()
    {
        if (INSTANCE != null)
        {
            Destroy(this);
            return;
        }
        INSTANCE = this;
        currentType = 0;
        ChangeType();
    }
    
    private void ChangeType()
    {
        currentType += 100;
        if (currentType > 400)
        {
            currentType = 100;
        }
        GameLogic.INSTANCE.UpdateSelection(currentType);
        if (Indexing.INSTANCE.colorMap.TryGetValue(currentType, out Color32 color))
        {
            spriteRenderer.color = color;
        }
    }
    private void ChangeType(int type)
    {
        currentType = type;
        GameLogic.INSTANCE.UpdateSelection(currentType);
        if (Indexing.INSTANCE.colorMap.TryGetValue(currentType, out Color32 color))
        {
            spriteRenderer.color = color;
        }
    }

    void Update()
    {
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

    public void OnPointerClick(PointerEventData eventData)
    {
        ChangeType();
    }

}
