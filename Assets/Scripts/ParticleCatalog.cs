using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Grid Mage/Particle Catalog")]
public class ParticleCatalog : ScriptableObject
{
    [Serializable] public struct TileEntry { public int type; public ParticleSystem prefab; }
    [Serializable] public struct ReactionEntry { public string id; public ParticleSystem prefab; }
    [Serializable] public struct DamageEntry { public int element; public ParticleSystem prefab; }
    public TileEntry[] tiles;
    public ReactionEntry[] reactions;
    [Tooltip("Element families: 1 fire, 2 water, 3 electricity, 4 lava (stone family).")]
    public DamageEntry[] damage = Array.Empty<DamageEntry>();
}
