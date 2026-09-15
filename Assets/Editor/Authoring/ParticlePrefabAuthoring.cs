using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Bake ground-contact anchors into particle prefabs during authoring.</summary>
public static class ParticlePrefabAuthoring
{
    public static void Bake(ParticleSystem root, bool damage = false)
    {
        if (root.GetComponentInChildren<SortingGroup>(true) != null) return;
        var systems = root.GetComponentsInChildren<ParticleSystem>(true);
        var pattern = root.GetComponent<ParticlePattern>();
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
                group = WorldSorting.CreateGroup(root.transform, new Vector3(point.x, point.y, 0f), "Y anchor " + point);
                groups.Add(point, group);
            }
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            if (renderer != null) WorldSorting.Include(group, renderer, renderer.sortingOrder);
        }
        if (damage)
        {
            Transform anchor = groups[Vector2.zero].transform;
            var visuals = new GameObject("Visuals").transform;
            visuals.gameObject.layer = root.gameObject.layer;
            visuals.SetParent(anchor, false);
            foreach (var system in systems)
                if (system != root) system.transform.SetParent(visuals, true);
        }
    }
}
