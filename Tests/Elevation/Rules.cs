// Run in Edit mode: unity command eval_file --file Tests/Elevation/Rules.cs --json
int checks = 0;
System.Action<bool, string> require = (ok, why) => { checks++; if (!ok) throw new System.Exception(why); };
foreach (float start in new[] { -2f, 0f, 1f, 7.5f })
    foreach (float diff in new[] { -1f, -.5f, -.25f, 0f, .25f, .5f, 1f })
        require(Assets.Scripts.GridHelper.IsElevationDiffOk(start, start + diff) == (diff == 0 || Mathf.Abs(diff) == .5f), "Exact permitted differences");
require(!Assets.Scripts.GridHelper.IsElevationDiffOk(float.NaN, 0), "NaN rejected");
require(!Assets.Scripts.GridHelper.IsElevationDiffOk(float.PositiveInfinity, float.PositiveInfinity), "Infinity rejected");
var ramp = new float[,] { { 1, 1.5f, 2, 2.5f } };
require(Assets.Scripts.GridHelper.TestElevationLine(ramp, 0, 0, 0, 3), "Climbing several half steps");
require(Assets.Scripts.GridHelper.TestElevationLine(ramp, 0, 3, 0, 0), "Descending several half steps");
ramp[0, 1] = 2;
require(!Assets.Scripts.GridHelper.TestElevationLine(ramp, 0, 0, 0, 3), "Intermediate cliff");
ramp[0, 3] = 1;
require(!Assets.Scripts.GridHelper.TestElevationLine(ramp, 0, 0, 0, 3), "Equal endpoints do not bypass cliff");
require(!Assets.Scripts.GridHelper.TestElevationLine(ramp, 0, 0, 1, 0), "Bounds");
require(!Assets.Scripts.GridHelper.TestElevationLine(null, 0, 0, 0, 0), "Missing heights");
var heights = new float[7, 7];
var exists = new bool[7, 7];
for (int r = 0; r < 7; r++) for (int c = 0; c < 7; c++) { heights[r, c] = (r + c) * .5f; exists[r, c] = true; }
require(Assets.Scripts.GridHelper.TestElevationLine(heights, 0, 0, 2, 4), "Ordered shallow ray (not endpoint comparison)");
require(!Assets.Scripts.GridHelper.TestElevationLine(heights, 0, 0, 3, 3), "Diagonal full step");
for (int r = 0; r < 7; r++) for (int c = 0; c < 7; c++) heights[r, c] = Mathf.Max(r, c) * .5f;
require(Assets.Scripts.GridHelper.TestElevationLine(heights, 0, 0, 6, 6), "Diagonal half steps");
exists[3, 3] = false;
require(!Assets.Scripts.GridHelper.TestElevationLine(heights, 0, 0, 6, 6, exists), "Missing intermediate cell");
// Exercise every octant and reversal, including exact corner crossings.
for (int r1 = 0; r1 < 7; r1++) for (int c1 = 0; c1 < 7; c1++)
    for (int r2 = 0; r2 < 7; r2++) for (int c2 = 0; c2 < 7; c2++)
        require(Assets.Scripts.GridHelper.TestElevationLine(heights, r1, c1, r2, c2, exists) ==
            Assets.Scripts.GridHelper.TestElevationLine(heights, r2, c2, r1, c1, exists), "Ray symmetry");
TilemapLevelChecks.Run();
var game = UnityEngine.Object.FindAnyObjectByType<Assets.Scripts.GameLogic>();
var layout = game.Level.Read(game.gridParent);
// Independently read from the glyphs: white 1..4, blue 1..4, white 5..8, blue 5..8.
var expectedSpriteHeights = new float[] { 1, 2, 3, 4, 1.5f, 2.5f, 3.5f, 4.5f, 5, 6, 7, 8, 5.5f, 6.5f, 7.5f, 8.5f };
foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:LevelMarkerTile", new[] { "Assets/Levels/Elevation" }))
{
    var marker = UnityEditor.AssetDatabase.LoadAssetAtPath<Assets.Scripts.LevelMarkerTile>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
    int spriteIndex = int.Parse(marker.sprite.name.Substring("Elevation_".Length));
    require(marker.elevation == expectedSpriteHeights[spriteIndex], "Sprite-to-elevation mapping: " + marker.sprite.name);
    require(marker.name == "Elevation " + marker.elevation.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture), "Asset label matches height");
}
var palette = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Levels/Elevation/Elevation Palette.prefab")
    .GetComponentInChildren<UnityEngine.Tilemaps.Tilemap>();
for (int col = 0; col < 8; col++)
{
    var white = palette.GetTile<Assets.Scripts.LevelMarkerTile>(new Vector3Int(col, 0, 0));
    var blue = palette.GetTile<Assets.Scripts.LevelMarkerTile>(new Vector3Int(col, -1, 0));
    require(white != null && white.elevation == col + 1, "White palette row");
    require(blue != null && blue.elevation == col + 1.5f, "Blue palette row");
}
var source = (Assets.Scripts.LevelMarkerTile)game.Level.logic.GetTile(layout.ToCell(layout.player.y, layout.player.x));
float previous = source.elevation;
try
{
    source.elevation = .25f;
    bool rejected = false;
    try { game.Level.Read(game.gridParent); } catch (System.InvalidOperationException) { rejected = true; }
    require(rejected, "Invalid authored elevation rejected");
}
finally { source.elevation = previous; }
// Both mesh variants stay inside selected cells for all 3x3 masks, including holes.
var mesh = new Mesh();
try
{
    for (int mask = 0; mask < 512; mask++)
        foreach (float inset in new[] { 0f, .14f })
        {
            var region = new bool[3, 3];
            for (int i = 0; i < 9; i++) region[i / 3, i % 3] = (mask & (1 << i)) != 0;
            CastableOutline.BuildMesh(mesh, region, 1, 0, Color.yellow, .0625f, .25f, 1, inset);
            var v = mesh.vertices; var t = mesh.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                require(Vector3.Cross(b - a, c - a).z > 0, "Mesh winding");
                Vector3 center = (a + b + c) / 3f;
                int row = Mathf.FloorToInt(.5f - center.y), col = Mathf.FloorToInt(center.x + .5f);
                require(row >= 0 && row < 3 && col >= 0 && col < 3 && region[row, col], "Border stays in region");
            }
        }
}
finally { UnityEngine.Object.DestroyImmediate(mesh); }
return $"PASS: {checks} elevation, ray, asset, parsing and mesh assertions.";
