using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts
{
    /// <summary>Accepted visual outputs, independent of playback, pooling, or scene objects.</summary>
    public sealed class ReactionVisual
    {
        public readonly string id;
        public readonly int row, col, direction;
        public readonly HashSet<Vector2Int> cells = new();
        public ReactionVisual(string id, int row, int col, int direction = 0)
        { this.id = id; this.row = row; this.col = col; this.direction = direction; }
    }
}
