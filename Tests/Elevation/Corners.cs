// Edit mode: unity command eval_file --file Tests/Elevation/Corners.cs --json
int checks = 0;
System.Action<bool, string> require = (ok, why) => { checks++; if (!ok) throw new System.Exception(why); };
var helper = UnityEngine.Object.FindAnyObjectByType<Assets.Scripts.GridHelper>();
foreach (int dr in new[] { -1, 1 }) foreach (int dc in new[] { -1, 1 })
    for (int mask = 0; mask < 4; mask++)
    {
        var walls = new int[5, 5];
        walls[2 + dr, 2] = mask & 1; walls[2, 2 + dc] = mask & 2;
        bool allowed = mask != 3;
        require(Assets.Scripts.GridMath.HasLineOfSight(walls, 2, 2, 2 + dr, 2 + dc) == allowed, "Wall corner truth table");
        require(Assets.Scripts.GridMath.HasLineOfSight(walls, 2 + dr, 2 + dc, 2, 2) == allowed, "Reverse wall corner");
        require(Assets.Scripts.GridMath.CanReach(walls, 2, 2, 2 + dr, 2 + dc, 2) == allowed, "Blink corner truth table");
        walls[2 + dr, 2 + dc] = 1;
        require(!Assets.Scripts.GridMath.HasLineOfSight(walls, 2, 2, 2 + dr, 2 + dc), "Destination remains blocked");
        require(Assets.Scripts.GridMath.HasLineOfSight(walls, 2, 2, 2 + dr, 2 + dc, true) == allowed, "Target exception cannot bypass two side walls");
        var heights = new float[5, 5];
        var exists = new bool[5, 5];
        for (int r = 0; r < 5; r++) for (int c = 0; c < 5; c++) { heights[r, c] = 1; exists[r, c] = true; }
        heights[2 + dr, 2 + dc] = 1.5f;
        if ((mask & 1) != 0) heights[2 + dr, 2] = 4;
        if ((mask & 2) != 0) heights[2, 2 + dc] = 4;
        require(Assets.Scripts.GridMath.TestElevationLine(heights, 2, 2, 2 + dr, 2 + dc) == allowed, "Elevation corner truth table");
        require(Assets.Scripts.GridMath.TestElevationLine(heights, 2 + dr, 2 + dc, 2, 2) == allowed, "Reverse elevation corner");
        heights[2 + dr, 2] = 1; heights[2, 2 + dc] = 1;
        exists[2 + dr, 2] = (mask & 1) == 0; exists[2, 2 + dc] = (mask & 2) == 0;
        require(Assets.Scripts.GridMath.TestElevationLine(heights, 2, 2, 2 + dr, 2 + dc, exists) == allowed, "Missing side cells");
    }
// A side must connect both endpoints; approaching from the other end gives the same result.
var split = new float[,] { { 1, .5f }, { 2, 1.5f } };
require(!Assets.Scripts.GridMath.TestElevationLine(split, 0, 0, 1, 1), "Neither side connects both endpoints");
require(!Assets.Scripts.GridMath.TestElevationLine(split, 1, 1, 0, 0), "Symmetric split corner");
split[0, 1] = 1.5f;
require(Assets.Scripts.GridMath.TestElevationLine(split, 0, 0, 1, 1), "One connecting side is sufficient");
split[1, 1] = 2;
require(!Assets.Scripts.GridMath.TestElevationLine(split, 0, 0, 1, 1), "Do not bypass invalid direct diagonal height");
var longWall = new int[4, 4]; longWall[1, 2] = 1; longWall[2, 1] = 1;
require(!Assets.Scripts.GridMath.HasLineOfSight(longWall, 0, 0, 3, 3), "Check later corners along the ray");
longWall[2, 1] = 0;
require(Assets.Scripts.GridMath.HasLineOfSight(longWall, 0, 0, 3, 3), "Long ray with one blocked side");
return $"PASS: {checks} diagonal wall/elevation/Blink assertions.";
