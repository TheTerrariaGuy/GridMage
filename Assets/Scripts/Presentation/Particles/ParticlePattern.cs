using System;
using Assets.Scripts;
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

    public bool Apply(ReactionVisual burst)
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

    private static bool Contains(Vector2Int cell, ReactionVisual burst)
    {
        var rotated = GridMath.Rotate(cell.x, cell.y, burst.direction);
        cell = new Vector2Int(rotated.x, rotated.y);
        return burst.cells.Contains(cell + new Vector2Int(burst.col, burst.row));
    }
}
