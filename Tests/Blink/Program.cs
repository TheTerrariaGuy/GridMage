int checks = 0;
void Require(bool condition, string message)
{
    checks++;
    if (!condition) throw new System.Exception(message);
}

var grid = new int[9, 9];
Require(!Assets.Scripts.PlayerHandler.CanReach(null, 0, 0, 1, 1, 3), "Uninitialized grid");
Require(!Assets.Scripts.PlayerHandler.CanReach(grid, 4, 4, -1, 4, 3), "Outside grid");
Require(!Assets.Scripts.PlayerHandler.CanReach(grid, -1, 4, 1, 4, 3), "Invalid origin");
for (int row = 0; row < 9; row++)
    for (int col = 0; col < 9; col++)
    {
        int distance = Math.Max(Math.Abs(row - 4), Math.Abs(col - 4));
        Require(Assets.Scripts.PlayerHandler.CanReach(grid, 4, 4, row, col, 3) == (distance <= 3),
            "Square cast range must include diagonals and the current tile");
    }
foreach (int wall in new[] { 1 })
{
    grid[4, 5] = wall;
    Require(!Assets.Scripts.PlayerHandler.CanReach(grid, 4, 4, 4, 5, 3), "Wall destination");
    Require(!Assets.Scripts.PlayerHandler.CanReach(grid, 4, 4, 4, 7, 3), "Wall along path");
    Require(Assets.Scripts.PlayerHandler.CanReach(grid, 4, 4, 5, 5, 3), "One wall touching diagonal corner allows passage");
    grid[5, 4] = wall;
    Require(!Assets.Scripts.PlayerHandler.CanReach(grid, 4, 4, 5, 5, 3), "Two walls touching diagonal corner block passage");
    grid[5, 4] = 0;
}
foreach (int floor in new[] { 0 })
{
    grid[4, 5] = floor;
    Require(Assets.Scripts.PlayerHandler.CanReach(grid, 4, 4, 4, 5, 3), "Non-wall tile must remain traversable");
}
grid[4, 5] = 0;
// The same blocked line must behave identically in both directions.
for (int wallRow = 1; wallRow < 8; wallRow++)
    for (int wallCol = 1; wallCol < 8; wallCol++)
    {
        grid[wallRow, wallCol] = 1;
        for (int row = 1; row < 8; row++)
            for (int col = 1; col < 8; col++)
            {
                if (grid[4, 4] != 0 || grid[row, col] != 0) continue;
                Require(Assets.Scripts.PlayerHandler.CanReach(grid, 4, 4, row, col, 3) ==
                    Assets.Scripts.PlayerHandler.CanReach(grid, row, col, 4, 4, 3), "Line of sight must be symmetric");
            }
        grid[wallRow, wallCol] = 0;
    }
return $"Passed {checks} Blink checks.";
