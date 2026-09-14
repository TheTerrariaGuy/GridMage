using Assets.Scripts;
using UnityEngine;
using UnityEngine.Rendering;

public class Tile : MonoBehaviour
{
    public int type;
    public float spacing, offset;
    public int row, col;
    public GameLogic gameLogic;
    [SerializeField] private SpriteRenderer spriteRenderer;
    public SpriteRenderer SurfaceRenderer => spriteRenderer;
    [SerializeField] private SpriteRenderer overlay;
    [SerializeField] private float queuedAlpha;
    private int queuedSpellType;
    private ParticleVFX.Effect particles;
    private SortingGroup surfaceGroup;

    private void Awake()
    {
        surfaceGroup = WorldSorting.CreateGroup(transform, new Vector3(0f, -.5f, 0f), "Surface Y anchor", WorldSorting.Ground);
        WorldSorting.Include(surfaceGroup, spriteRenderer, 0);
        overlay.sortingLayerName = WorldSorting.Foreground;
        overlay.sortingOrder = 0;
        foreach (var renderer in GetComponentsInChildren<SpriteRenderer>())
            if (renderer != spriteRenderer && renderer != overlay)
            {
                renderer.sortingLayerName = WorldSorting.Ground;
                renderer.sortingOrder = -1;
            }
    }

    public void SetWallSorting(bool wall)
    {
        if (surfaceGroup != null) surfaceGroup.sortingLayerName = wall ? WorldSorting.World : WorldSorting.Ground;
    }

    public void Init(int r, int c, float s, float o)
    {
        spacing = s;
        offset = o;
        transform.parent = GridHelper.INSTANCE.GetAnchor();
        transform.localScale = new Vector3(s, s, 1f);
        row = r; col = c;
        GoToPosition();
        gameLogic = GameLogic.INSTANCE;
        // Keep colliders, spell artwork, and previews; the authored map supplies the floor.
        if (gameLogic.Level != null)
            foreach (var renderer in GetComponentsInChildren<SpriteRenderer>())
                if (renderer != spriteRenderer && renderer != overlay)
                    renderer.enabled = false;
        ChangeType(0);
        ClearQueuedSpell();
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
        TextureHandler.INSTANCE?.UpdateTexture(this);
        ParticleVFX.INSTANCE?.SetTile(transform, t, ref particles);
    }

    public void ReleaseParticles()
    {
        ParticleVFX.INSTANCE?.Release(particles);
        particles = null;
    }

    private void OnEnable()
    {
        if (gameLogic != null) ChangeType(type);
    }

    public void ShowQueuedSpell(int spellType)
    {
        queuedSpellType = spellType;
        RefreshOverlay();
    }

    public void ClearQueuedSpell()
    {
        queuedSpellType = 0;
        RefreshOverlay();
    }

    private void RefreshOverlay()
    {
        if (overlay == null) return;
        if (queuedSpellType != 0 && TextureHandler.INSTANCE != null)
        {
            TextureHandler.INSTANCE.UpdatePreview(this, overlay, queuedSpellType, queuedAlpha);
        }
        else overlay.color = new Color32(255, 255, 255, 0);

    }
}
