using System.Collections.Generic;
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

    public static void ConfigureParticles(ParticleSystem root, ParticleSystem[] systems, ParticlePattern pattern)
    {
        var groups = new Dictionary<Vector2, SortingGroup>();
        var anchors = new Dictionary<ParticleSystem, Vector2>();
        if (pattern != null)
            foreach (var part in pattern.parts)
            {
                // Links are authored one grid edge at a time. Sort each edge at its midpoint,
                // and each destination's systems together, independently of the casting origin.
                Vector2 cell = part.link ? ((Vector2)part.from + part.to) * .5f : part.to;
                anchors[part.system] = new Vector2(cell.x, -cell.y);
            }

        foreach (var system in systems)
        {
            if (system == root) continue; // Catalog roots are non-emitting playback containers.
            Vector2 point = anchors.TryGetValue(system, out var cell) ? cell : Vector2.zero;
            if (!groups.TryGetValue(point, out var group))
            {
                group = CreateGroup(root.transform, new Vector3(point.x, point.y, 0f), "Y anchor " + point);
                groups.Add(point, group);
            }
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            if (renderer != null) Include(group, renderer, renderer.sortingOrder);
        }
    }
}
