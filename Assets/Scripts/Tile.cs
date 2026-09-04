using Assets.Scripts;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class Tile : MonoBehaviour, IPointerClickHandler
{
    public int type;
    public float spacing, offset;
    public int row, col;
    public GameLogic gameLogic;
    [SerializeField] private SpriteRenderer spriteRenderer;

    public void Init(int r, int c, float s, float o)
    {
        spacing = s;
        offset = o;
        transform.parent = GameLogic.INSTANCE.gridParent;
        transform.localScale = new Vector3(1,1,1) * 0.9f;
        row = r; col = c;
        GoToPosition();
        gameLogic = GameLogic.INSTANCE;
        ChangeType(0);
    }

    public void GoToPosition()
    {
        // Bottom left aligned
        float xTarget, yTarget;
        xTarget = col * spacing + offset;
        yTarget = row * -spacing + offset;
        transform.localPosition = new Vector3(xTarget, yTarget, 0);
    }

    public void ChangeType(int t)
    {
        type = t;
        if (Indexing.INSTANCE.colorMap.TryGetValue(t, out Color32 color))
        {
            spriteRenderer.color = color;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        gameLogic.Clicked(this);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
