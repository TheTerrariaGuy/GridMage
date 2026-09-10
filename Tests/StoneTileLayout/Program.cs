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

        var front = new int[2, 3];
        front[0, 1] = 400;
        Equal(49, TileSpriteLayout.FrontVariant(front, 0, 1), "Isolated front");
        front[0, 0] = 401;
        Equal(50, TileSpriteLayout.FrontVariant(front, 0, 1), "Front joins left across stone types");
        front[0, 2] = 400;
        Equal(52, TileSpriteLayout.FrontVariant(front, 0, 1), "Middle front");
        front[1, 0] = 400;
        Equal(51, TileSpriteLayout.FrontVariant(front, 0, 1), "Covered left neighbor creates front endpoint");
        front[1, 1] = 401;
        if (TileSpriteLayout.HasFront(front, 0, 1)) throw new Exception("Internal front must be hidden");
        front[1, 1] = 410;
        if (!TileSpriteLayout.HasFront(front, 0, 1)) throw new Exception("Lava must expose front");

        Equal(2, TileSpriteLayout.SurfaceVariant(new int[,] { { 400 } }, 0, 0), "Single-cell boundary");
        Equal(2, TileSpriteLayout.SurfaceVariant(null, 0, 0), "Uninitialized grid");
        if (TileSpriteLayout.HasWall(front, -1, 0) || TileSpriteLayout.HasWall(front, 0, 3))
            throw new Exception("Out-of-bounds cells must not join");
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
            foreach (int neighbor in new[] { 0, 100, 200, 300, 400, 110, 210, 310, 410 })
            {
                if (neighbor == type) continue;
                Equal(2, TileSpriteLayout.SurfaceVariant(new int[,] { { type, neighbor } }, 0, 0, type), "Different elements and reactions do not connect");
            }
        }
        Console.WriteLine("Passed: 256 neighborhoods per element, water/stone 47 shapes, fire/lightning 16 shapes, 2x2 merging rules, stages, family separation, wall fronts, and boundaries.");
    }
}
