using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>Renders the sorted world to one point-filtered texture before drawing the HUD.</summary>
[DefaultExecutionOrder(1000)]
[RequireComponent(typeof(Camera))]
public sealed class PixelWorldRenderer : MonoBehaviour
{
    [SerializeField] private Camera worldCamera;
    [SerializeField] private RawImage output;
    [SerializeField, Min(1)] private int pixelHeight = 400;
    private Camera viewCamera;
    private RenderTexture texture;
    public Camera WorldCamera => worldCamera;
    public RenderTexture Texture => texture;

    private void OnEnable()
    {
        viewCamera = GetComponent<Camera>();
        if (worldCamera != null) worldCamera.enabled = true;
        RenderPipelineManager.beginCameraRendering += BeforeCamera;
        Synchronize();
    }

    private void LateUpdate() => Synchronize();

    private void BeforeCamera(ScriptableRenderContext context, Camera camera)
    {
        if (camera == worldCamera) Synchronize();
    }

    public void Synchronize()
    {
        if (viewCamera == null) viewCamera = GetComponent<Camera>();
        if (worldCamera == null || output == null) return;
        int height = Mathf.Max(1, pixelHeight);
        int width = Mathf.Max(1, Mathf.RoundToInt(height * viewCamera.aspect));
        if (texture == null || texture.width != width || texture.height != height)
        {
            ReleaseTexture();
            texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "Pixel World (runtime)", filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp, antiAliasing = 1, useMipMap = false
            };
            texture.Create();
            worldCamera.targetTexture = texture;
            output.texture = texture;
        }
        worldCamera.transform.SetPositionAndRotation(viewCamera.transform.position, viewCamera.transform.rotation);
        worldCamera.orthographic = viewCamera.orthographic;
        worldCamera.orthographicSize = viewCamera.orthographicSize;
        worldCamera.nearClipPlane = viewCamera.nearClipPlane;
        worldCamera.farClipPlane = viewCamera.farClipPlane;
        worldCamera.worldToCameraMatrix = viewCamera.worldToCameraMatrix;
        worldCamera.projectionMatrix = viewCamera.projectionMatrix;
        worldCamera.backgroundColor = viewCamera.backgroundColor;
        worldCamera.depth = viewCamera.depth - 1f;
        worldCamera.transparencySortMode = TransparencySortMode.CustomAxis;
        worldCamera.transparencySortAxis = Vector3.up;
        output.raycastTarget = false;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeforeCamera;
        if (worldCamera != null) worldCamera.enabled = false;
        ReleaseTexture();
    }

    private void ReleaseTexture()
    {
        if (texture == null) return;
        if (worldCamera != null && worldCamera.targetTexture == texture) worldCamera.targetTexture = null;
        if (output != null && output.texture == texture) output.texture = null;
        texture.Release();
        Destroy(texture);
        texture = null;
    }
}
