using System;
using System.Collections.Generic;

namespace Assets.Scripts
{
    /// <summary>Placement reservation bookkeeping. Mana spending and terrain validity are explicit inputs.</summary>
    public sealed class SpellQueue
    {
        private readonly Dictionary<(int row, int col), (int type, float cost)> spells = new();
        private readonly List<(int row, int col)> invalid = new();
        public IReadOnlyDictionary<(int row, int col), (int type, float cost)> Entries => spells;
        public float ReservedMana { get; private set; }
        public bool HasSpells => spells.Count != 0;
        public bool Contains(int row, int col) => spells.ContainsKey((row, col));
        public void Add(int row, int col, int type, float cost)
        {
            spells.Add((row, col), (type, cost));
            ReservedMana += cost;
        }
        public bool Remove(int row, int col)
        {
            if (!spells.Remove((row, col), out var spell)) return false;
            ReservedMana = spells.Count == 0 ? 0f : ReservedMana - spell.cost;
            return true;
        }
        public void RemoveInvalid(Func<int, int, bool> valid, Action<int, int> removed)
        {
            invalid.Clear();
            foreach (var cell in spells.Keys) if (!valid(cell.row, cell.col)) invalid.Add(cell);
            foreach (var cell in invalid) { Remove(cell.row, cell.col); removed(cell.row, cell.col); }
        }
        public void Clear() { spells.Clear(); ReservedMana = 0f; invalid.Clear(); }
    }
}
