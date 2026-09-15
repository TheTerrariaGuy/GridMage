using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Ground-contact anchors shared by sprites and particle renderers.</summary>
public static class WorldSorting
{
    public const string Ground = "Ground", World = "World", Foreground = "Foreground", UI = "UI";

    public static SortingGroup CreateGroup(Transform parent, Vector3 localAnchor, string name, string layer = World)
    {
        var anchor = new GameObject(name);
        anchor.layer = parent.gameObject.layer;
        anchor.transform.SetParent(parent, false);
        anchor.transform.localPosition = localAnchor;
        var group = anchor.AddComponent<SortingGroup>();
        group.sortingLayerName = layer;
        // All world groups have equal order: the renderer's custom Y axis decides their order.
        group.sortingOrder = 0;
        return group;
    }

    public static void Include(SortingGroup group, Renderer renderer, int order)
    {
        renderer.transform.SetParent(group.transform, true);
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = order;
        if (renderer is ParticleSystemRenderer particle) particle.sortingFudge = 0f;
    }
}
