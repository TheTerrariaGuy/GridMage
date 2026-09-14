using System;
using System.Collections.Generic;
using Assets.Scripts;

static class Program
{
    static void Equal(int expected, int actual, string message)
    {
        if (expected != actual) throw new Exception($"{message}: expected {expected}, got {actual}");
    }

    static int[,] Neighborhood(int bits, int type = 400)
    {
        var grid = new int[3, 3];
        grid[1, 1] = type;
        int[] rows = { 0, 0, 1, 2, 2, 2, 1, 0 };
        int[] cols = { 1, 2, 2, 2, 1, 0, 0, 0 };
        for (int i = 0; i < 8; i++)
            if ((bits & (1 << i)) != 0) grid[rows[i], cols[i]] = type + 1;
        return grid;
    }

    static void Main()
    {
        var variants = new HashSet<int>();
        for (int bits = 0; bits < 256; bits++)
        {
            var grid = Neighborhood(bits);
            int variant = TileSpriteLayout.SurfaceVariant(grid, 1, 1);
            if (variant < 2 || variant > 48) throw new Exception("Surface ID outside reserved range");
            variants.Add(variant);

            // Each diagonal matters exactly when its two cardinal neighbors exist.
            for (int diagonal = 1; diagonal < 8; diagonal += 2)
            {
                int before = (diagonal + 7) % 8, after = (diagonal + 1) % 8;
                bool relevant = (bits & (1 << before)) != 0 && (bits & (1 << after)) != 0;
                int toggled = TileSpriteLayout.SurfaceVariant(Neighborhood(bits ^ (1 << diagonal)), 1, 1);
                if ((variant != toggled) != relevant)
                    throw new Exception($"Incorrect diagonal influence: neighborhood {bits}, diagonal {diagonal}");
            }
        }
        Equal(47, variants.Count, "Distinct surface shapes across all 256 neighborhoods");
        Equal(2, TileSpriteLayout.SurfaceVariant(Neighborhood(0), 1, 1), "Isolated wall");
        Equal(18, TileSpriteLayout.SurfaceVariant(Neighborhood(69), 1, 1), "T joining north/east/west");
        Equal(9, TileSpriteLayout.SurfaceVariant(Neighborhood(20), 1, 1), "Thin L joining east/south");
        Equal(12, TileSpriteLayout.SurfaceVariant(Neighborhood(28), 1, 1), "Filled top-left block corner");
        Equal(23, TileSpriteLayout.SurfaceVariant(Neighborhood(85), 1, 1), "Thin four-way cross");
        Equal(48, TileSpriteLayout.SurfaceVariant(Neighborhood(255), 1, 1), "Solid block interior");

        Equal(2, TileSpriteLayout.SurfaceVariant(new int[,] { { 400 } }, 0, 0), "Single-cell boundary");
        Equal(2, TileSpriteLayout.SurfaceVariant(null, 0, 0), "Uninitialized grid");
        foreach (int type in new[] { 0, 100, 200, 300, 410, 411, 500 })
            if (TileSpriteLayout.IsWall(type)) throw new Exception($"Non-wall type {type} joined");

        foreach (int type in new[] { 100, 200, 300, 400 })
        {
            var shapes = new HashSet<int>();
            for (int bits = 0; bits < 256; bits++)
            {
                int shape = TileSpriteLayout.SurfaceVariant(Neighborhood(bits, type), 1, 1, type);
                shapes.Add(shape);
                bool fillsCorners = type == 200 || type == 400;
                int expected = TileSpriteLayout.SurfaceVariant(Neighborhood(fillsCorners ? bits : bits & 85), 1, 1);
                Equal(expected, shape, $"Element {type}, neighbors {bits}");
                Equal(shape, TileSpriteLayout.SurfaceVariant(Neighborhood(bits, type), 1, 1, type + 1), "Spent stages share connections");
            }
            Equal(type == 200 || type == 400 ? 47 : 16, shapes.Count, $"Shape count for {type}");
            var square = new int[,] { { type, type + 1 }, { type + 1, type } };
            Equal(type == 200 || type == 400 ? 12 : 9, TileSpriteLayout.SurfaceVariant(square, 0, 0, type), "2x2 corner behavior");
            Equal(2, TileSpriteLayout.SurfaceVariant(new int[,] { { type } }, 0, 0, type), "Element boundary");
            foreach (int neighbor in new[] { 0, 100, 200, 300, 400, 110, 111, 210, 211, 310, 311, 410, 411 })
            {
                if (neighbor == type) continue;
                Equal(2, TileSpriteLayout.SurfaceVariant(new int[,] { { type, neighbor } }, 0, 0, type), "Base stages stay separate from reactions and other elements");
            }
        }
        foreach (int type in new[] { 100, 200, 300 })
        {
            for (int stage = 0; stage <= 11; stage++)
            {
                foreach (int reaction in new[] { type + 10, type + 11 })
                {
                    var pair = new int[,] { { type + stage, reaction } };
                    Equal(stage >= 10 ? 4 : 2, TileSpriteLayout.SurfaceVariant(pair, 0, 0, type + stage), "Only reaction stages join reactions to the east");
                    Equal(stage >= 10 ? 15 : 2, TileSpriteLayout.SurfaceVariant(pair, 0, 1, reaction), "Reactions only join reaction stages to the west");
                }
            }
            var square = new int[,] { { type + 10, type + 11 }, { type + 11, type + 10 } };
            Equal(type == 200 ? 12 : 9, TileSpriteLayout.SurfaceVariant(square, 0, 0, type + 10), "Mixed reaction stages preserve corner behavior");
            square[1, 1] = type;
            Equal(9, TileSpriteLayout.SurfaceVariant(square, 0, 0, type + 10), "Base diagonal does not fill a reaction corner");
            foreach (int neighbor in new[] { 110, 111, 210, 211, 310, 311, 410, 411 })
            {
                if (neighbor / 100 == type / 100) continue;
                foreach (int reaction in new[] { type + 10, type + 11 })
                    Equal(2, TileSpriteLayout.SurfaceVariant(new int[,] { { reaction, neighbor } }, 0, 0, reaction), "Reactions of different elements stay separate");
            }
        }
        foreach (int type in new[] { 112, 212, 312, 410, 411 })
            Equal(0, TileSpriteLayout.SurfaceVariant(new int[,] { { type, type / 100 * 100 } }, 0, 0, type), "Excluded stages remain unconnected");
        Console.WriteLine("Passed: 256 neighborhoods per element, water/stone 47 shapes, fire/lightning 16 shapes, 2x2 merging rules, stages, family separation, and boundaries.");
    }
}
