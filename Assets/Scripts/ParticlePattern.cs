using System;
using System.Collections.Generic;
using UnityEngine;

public class ParticlePattern : MonoBehaviour
{
    [Serializable]
    public class Part
    {
        public ParticleSystem system;
        public Vector2Int from, to;
        public bool link;
    }

    public List<Part> parts = new();
    [Tooltip("Keep emitters facing world up while their cell layout rotates with the cast.")]
    public bool keepEmittersUpright;

    public bool Apply(ParticleVFX.Burst burst)
    {
        bool any = false;
        foreach (Part part in parts)
        {
            if (keepEmittersUpright) part.system.transform.rotation = Quaternion.identity;
            bool visible = Contains(part.to, burst) &&
                (!part.link || part.from == Vector2Int.zero || Contains(part.from, burst));
            part.system.gameObject.SetActive(visible);
            any |= visible;
        }
        return any;
    }

    private static bool Contains(Vector2Int cell, ParticleVFX.Burst burst)
    {
        for (int i = 0; i < burst.direction; i++) cell = new Vector2Int(-cell.y, cell.x);
        return burst.cells.Contains(cell + new Vector2Int(burst.col, burst.row));
    }
}
