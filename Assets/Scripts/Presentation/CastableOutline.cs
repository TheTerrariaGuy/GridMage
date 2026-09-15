using Assets.Scripts;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class CastableOutline : MonoBehaviour
{
    public enum Region { Cast, Move }
    [SerializeField] private Region region;
    [SerializeField] private Color highlightColor = new Color(.35f, .9f, 1f, 1f);
    [SerializeField, Range(0f, 1f)] private float opacity = .5f;
    [SerializeField, Range(0f, .49f), Tooltip("Solid border width as a fraction of a tile.")]
    private float borderWidth = 1f / 16f;
    [SerializeField, Range(0f, .49f), Tooltip("Inward fade width as a fraction of a tile.")]
    private float fadeWidth = .25f;
    [SerializeField, Range(0f, .49f), Tooltip("Inset separates overlapping cast and movement borders.")]
    private float inset;
    [SerializeField, Min(.01f)] private float falloff = 1f;
    private Mesh mesh;
    private MeshRenderer meshRenderer;
    private bool[,] lastGrid;
    private float lastSpacing = 1f, lastOffset;
    private Matrix4x4 lastGridToLocal = Matrix4x4.identity;

    private void OnEnable()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        if (mesh == null)
        {
            mesh = new Mesh { name = "Castable outline mesh", hideFlags = HideFlags.HideAndDontSave };
            mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }
        var game = GameLogic.INSTANCE;
        if (game != null && transform.parent != null && transform.parent.GetComponent<PlayerHandler>() != null)
            Rebuild(game);
        else Rebuild(lastGrid, lastSpacing, lastOffset, lastGridToLocal);
    }

    public void Rebuild(GameLogic game)
    {
        var player = transform.parent.GetComponent<PlayerHandler>();
        // Anchor to the logical cell, not the animated position, so rebuilds during Blink keep following it.
        // GridTranslation cancels between the player's cell and the board-space mesh vertices.
        Vector3 origin = new Vector3(player.c * game.spacing + game.offset, -player.r * game.spacing + game.offset, 0f);
        Matrix4x4 gridToPlayer = Matrix4x4.TRS(origin, player.transform.localRotation, player.transform.localScale).inverse;
        Rebuild(region == Region.Move ? game.moveableGrid : game.castableGrid, game.spacing, game.offset, gridToPlayer);
    }

    public void Rebuild(bool[,] grid, float spacing, float offset, Matrix4x4? gridToLocal = null)
    {
        lastGrid = grid;
        lastSpacing = spacing;
        lastOffset = offset;
        lastGridToLocal = gridToLocal ?? Matrix4x4.identity;
        if (!isActiveAndEnabled || mesh == null) return;
        Color color = highlightColor;
        color.a *= opacity;
        BuildMesh(mesh, grid, spacing, offset, color, borderWidth, fadeWidth, falloff, inset);
        if (lastGridToLocal != Matrix4x4.identity)
        {
            var vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = lastGridToLocal.MultiplyPoint3x4(vertices[i]);
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }
        meshRenderer.enabled = mesh.vertexCount > 0 && color.a > 0f;
    }

    private void OnValidate()
    {
        opacity = Mathf.Clamp01(opacity);
        inset = Mathf.Clamp(inset, 0f, .49f);
        borderWidth = Mathf.Clamp(borderWidth, 0f, .49f - inset);
        fadeWidth = Mathf.Clamp(fadeWidth, 0f, .49f - inset - borderWidth);
        falloff = Mathf.Max(.01f, falloff);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall -= RefreshInEditor;
        UnityEditor.EditorApplication.delayCall += RefreshInEditor;
#endif
    }

#if UNITY_EDITOR
    private void RefreshInEditor()
    {
        if (this != null && isActiveAndEnabled) Rebuild(lastGrid, lastSpacing, lastOffset, lastGridToLocal);
    }
#endif

    private void OnDisable()
    {
        if (meshRenderer != null) meshRenderer.enabled = false;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall -= RefreshInEditor;
#endif
    }

    private void OnDestroy()
    {
        if (mesh == null) return;
        if (Application.isPlaying) Destroy(mesh);
        else DestroyImmediate(mesh);
    }

    private readonly struct Edge
    {
        public readonly Vector2Int from, to;
        public Edge(Vector2Int from, Vector2Int to) { this.from = from; this.to = to; }
    }

    /// <summary>Joined inward bands around exposed cell edges. Distances are fractions of a tile.</summary>
    public static void BuildMesh(Mesh mesh, bool[,] grid, float spacing, float offset,
        Color color, float borderWidth, float fadeWidth, float falloff, float inset = 0f)
    {
        mesh.Clear();
        if (grid == null || spacing <= 0f) return;
        inset = Mathf.Clamp(inset, 0f, .49f);
        borderWidth = Mathf.Clamp(borderWidth, 0f, .49f - inset);
        fadeWidth = Mathf.Clamp(fadeWidth, 0f, .49f - inset - borderWidth);
        falloff = Mathf.Max(.01f, falloff);
        var vertices = new List<Vector3>();
        var colors = new List<Color>();
        var triangles = new List<int>();
        foreach (var corners in TraceLoops(CollectBoundaryEdges(grid)))
        {
            var loop = new Vector2[corners.Count];
            for (int i = 0; i < loop.Length; i++) loop[i] = ToLocal(corners[i], spacing, offset);
            AppendBand(loop, inset * spacing, (inset + borderWidth) * spacing, 1f, 1f, color, vertices, colors, triangles);
            const int fadeSteps = 8;
            for (int step = 0; step < fadeSteps; step++)
            {
                float a = (float)step / fadeSteps, b = (float)(step + 1) / fadeSteps;
                AppendBand(loop, (inset + borderWidth + a * fadeWidth) * spacing,
                    (inset + borderWidth + b * fadeWidth) * spacing, FadeAlpha(a, falloff), FadeAlpha(b, falloff),
                    color, vertices, colors, triangles);
            }
        }
        mesh.indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
    }

    private static bool IsCastable(bool[,] grid, int row, int col) => GridMath.IsInBounds(grid, row, col) && grid[row, col];

    private static List<Edge> CollectBoundaryEdges(bool[,] grid)
    {
        var edges = new List<Edge>();
        for (int row = 0; row < grid.GetLength(0); row++)
            for (int col = 0; col < grid.GetLength(1); col++)
            {
                if (!grid[row, col]) continue;
                var tl = new Vector2Int(col, row);
                var tr = new Vector2Int(col + 1, row);
                var bl = new Vector2Int(col, row + 1);
                var br = new Vector2Int(col + 1, row + 1);
                // In world XY, castable cells are always on the left of a directed edge.
                if (!IsCastable(grid, row - 1, col)) edges.Add(new Edge(tr, tl));
                if (!IsCastable(grid, row, col - 1)) edges.Add(new Edge(tl, bl));
                if (!IsCastable(grid, row + 1, col)) edges.Add(new Edge(bl, br));
                if (!IsCastable(grid, row, col + 1)) edges.Add(new Edge(br, tr));
            }
        return edges;
    }

    private static List<List<Vector2Int>> TraceLoops(List<Edge> edges)
    {
        var outgoing = new Dictionary<Vector2Int, List<int>>();
        for (int i = 0; i < edges.Count; i++)
        {
            if (!outgoing.TryGetValue(edges[i].from, out var list))
                outgoing.Add(edges[i].from, list = new List<int>(2));
            list.Add(i);
        }
        var visited = new bool[edges.Count];
        var loops = new List<List<Vector2Int>>();
        for (int first = 0; first < edges.Count; first++)
        {
            if (visited[first]) continue;
            var loop = new List<Vector2Int>();
            int current = first;
            while (true)
            {
                Edge edge = edges[current];
                visited[current] = true;
                loop.Add(edge.from);
                if (edge.to == edges[first].from) break;
                Vector2Int incoming = edge.to - edge.from;
                int next = -1, bestTurn = -1;
                foreach (int candidate in outgoing[edge.to])
                {
                    if (visited[candidate]) continue;
                    Vector2Int direction = edges[candidate].to - edge.to;
                    // Row coordinates point down: negate the cross product for world-space turns.
                    int cross = incoming.y * direction.x - incoming.x * direction.y;
                    int dot = incoming.x * direction.x + incoming.y * direction.y;
                    int turn = cross > 0 ? 3 : dot > 0 ? 2 : cross < 0 ? 1 : 0;
                    if (turn > bestTurn) { next = candidate; bestTurn = turn; }
                }
                if (next < 0) throw new System.InvalidOperationException("Castable outline boundary is not closed.");
                current = next;
            }
            loops.Add(loop);
        }
        return loops;
    }

    private static Vector2 ToLocal(Vector2Int corner, float spacing, float offset) =>
        new Vector2((corner.x - .5f) * spacing + offset, (.5f - corner.y) * spacing + offset);

    private static Vector2 InsetCorner(Vector2 previous, Vector2 current, Vector2 next, float distance)
    {
        Vector2 incoming = (current - previous).normalized, outgoing = (next - current).normalized;
        Vector2 a = new Vector2(-incoming.y, incoming.x), b = new Vector2(-outgoing.y, outgoing.x);
        return current + (a + b) * (distance / (1f + Vector2.Dot(a, b)));
    }

    private static void AppendBand(Vector2[] loop, float outer, float inner, float outerAlpha, float innerAlpha,
        Color color, List<Vector3> vertices, List<Color> colors, List<int> triangles)
    {
        if (inner <= outer) return;
        Color outerColor = color, innerColor = color;
        outerColor.a *= outerAlpha;
        innerColor.a *= innerAlpha;
        int start = vertices.Count;
        for (int i = 0; i < loop.Length; i++)
        {
            Vector2 previous = loop[(i + loop.Length - 1) % loop.Length], next = loop[(i + 1) % loop.Length];
            vertices.Add(InsetCorner(previous, loop[i], next, outer));
            vertices.Add(InsetCorner(previous, loop[i], next, inner));
            colors.Add(outerColor);
            colors.Add(innerColor);
        }
        for (int i = 0; i < loop.Length; i++)
        {
            int a = start + i * 2, b = start + ((i + 1) % loop.Length) * 2;
            triangles.Add(a); triangles.Add(b); triangles.Add(b + 1);
            triangles.Add(a); triangles.Add(b + 1); triangles.Add(a + 1);
        }
    }

    private static float FadeAlpha(float t, float falloff) =>
        Mathf.Pow(1f - Mathf.SmoothStep(0f, 1f, t), falloff);
}
