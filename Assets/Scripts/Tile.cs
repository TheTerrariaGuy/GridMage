using Assets.Scripts;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class Tile : MonoBehaviour
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
        transform.parent = GridHelper.INSTANCE.GetAnchor();
        transform.localScale = new Vector3(1,1,1) * 0.975f;
        row = r; col = c;
        GoToPosition();
        gameLogic = GameLogic.INSTANCE;
        ChangeType(0);
    }

    public void GoToPosition()
    {
        transform.localPosition = GridHelper.INSTANCE.GetLocalPosition(row, col, spacing, offset);
    }

    public void ChangeType(int t)
    {
        type = t;
        if (Indexing.INSTANCE.colorMap.TryGetValue(t, out Color32 color))
        {
            spriteRenderer.color = color;
        }
    }

    public void OnMouseDown()
    {
        gameLogic.Clicked(this);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
