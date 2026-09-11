using UnityEngine;
using UnityEngine.Rendering;

/// <summary>A screen-aligned particle grid sized in game pixels (16 per world unit).</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Camera))]
public sealed class ParticlePixelation : MonoBehaviour
{
    [SerializeField, Min(1), Tooltip("Particle cell size in game pixels. Scales with camera zoom and resolution. Disable this component for native resolution.")]
    private int pixelSize = 6;
    [SerializeField, Min(1), Tooltip("Game pixels per world unit. Tile artwork uses 16 pixels across a one-unit tile.")]
    private int pixelsPerUnit = 16;
    private static readonly int PixelSizeId = Shader.PropertyToID("_ParticlePixelWorldSize");

    public int PixelSize
    {
        get => pixelSize;
        set { pixelSize = Mathf.Max(1, value); Apply(); }
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += BeforeCamera;
        Apply();
    }

    private void OnValidate()
    {
        pixelSize = Mathf.Max(1, pixelSize);
        pixelsPerUnit = Mathf.Max(1, pixelsPerUnit);
        Apply();
    }

    private void BeforeCamera(ScriptableRenderContext context, Camera camera)
    {
        // A camera's own settings take priority over the shared Scene View fallback.
        if (camera.TryGetComponent<ParticlePixelation>(out var settings))
            settings.Apply();
        else
            Apply();
    }

    private void Apply() => Shader.SetGlobalFloat(PixelSizeId,
        isActiveAndEnabled ? (float)pixelSize / Mathf.Max(1, pixelsPerUnit) : 0f);

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeforeCamera;
        Shader.SetGlobalFloat(PixelSizeId, 0f);
    }
}
