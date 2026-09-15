using System;
using System.Collections.Generic;

namespace Assets.Scripts
{
    /// <summary>Shared defaults for rectangular fallback levels and their one-time tilemap conversion.</summary>
    public static class LegacyLevelDefaults
    {
        public static IReadOnlyList<(int row, int col)> EnemyCells { get; } = Array.AsReadOnly(new[]
        {
            (10, 10), (15, 10), (10, 15), (13, 10), (10, 13)
        });
    }
}
