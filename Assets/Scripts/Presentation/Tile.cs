using Assets.Scripts;
using UnityEngine;
using UnityEngine.Rendering;

public class Tile : MonoBehaviour
{
    public int type => gameLogic != null && gameLogic.HasCell(row, col) ? gameLogic.Board.Get(row, col) : 0;
    public int row { get; private set; }
    public int col { get; private set; }
    public GameLogic gameLogic { get; private set; }
    private int lastRenderedType = int.MinValue;
    public bool NeedsPresentation => lastRenderedType != type;
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

    public void Init(GameLogic game, int r, int c)
    {
        gameLogic = game;
        transform.parent = GridHelper.INSTANCE.GetAnchor();
        transform.localScale = new Vector3(game.spacing, game.spacing, 1f);
        row = r; col = c;
        transform.localPosition = GridHelper.INSTANCE.GetLocalPosition(row, col, game.spacing, game.offset);
        // Keep colliders, spell artwork, and previews; the authored map supplies the floor.
        if (gameLogic.Level != null)
            foreach (var renderer in GetComponentsInChildren<SpriteRenderer>())
                if (renderer != spriteRenderer && renderer != overlay)
                    renderer.enabled = false;
        InvalidatePresentation();
        ClearQueuedSpell();
    }

    public void InvalidatePresentation() => lastRenderedType = int.MinValue;
    public void RefreshStage()
    {
        if (!NeedsPresentation) return;
        ParticleVFX.INSTANCE?.SetTile(transform, type, ref particles);
        lastRenderedType = type;
    }

    public void ReleaseParticles()
    {
        ParticleVFX.INSTANCE?.Release(particles);
        particles = null;
    }

    private void OnEnable()
    {
        InvalidatePresentation();
        if (gameLogic != null) { RefreshStage(); TextureHandler.INSTANCE?.ApplyTexture(this); }
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
