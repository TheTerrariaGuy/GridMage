// Run in a fresh SampleScene Play session via Unity Pipeline eval_file; stop Play afterward.
if (!Application.isPlaying) throw new System.Exception("Enter Play mode first.");
Time.timeScale = 0f;
var game = Assets.Scripts.GameLogic.INSTANCE;
var player = Assets.Scripts.PlayerHandler.INSTANCE;
var mobs = Assets.Scripts.MobHandler.INSTANCE;
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
int checks = 0;
System.Action<bool, string> require = (value, message) => { checks++; if (!value) throw new System.Exception(message); };
var level = game.Level;
require(level != null && !level.logic.GetComponent<UnityEngine.Tilemaps.TilemapRenderer>().enabled, "Markers hidden on startup.");
var background = level.transform.Find("Background").GetComponent<UnityEngine.Tilemaps.TilemapRenderer>();
require(background.enabled, "Background visible on startup.");
require(player.r == game.LevelLayout.player.y && player.c == game.LevelLayout.player.x, "Player uses authored spawn.");
require(!game.tilesGrid[player.r, player.c].SurfaceRenderer.enabled, "Runtime floor must not cover the background.");

var floor = ScriptableObject.CreateInstance<Assets.Scripts.LevelMarkerTile>();
var start = ScriptableObject.CreateInstance<Assets.Scripts.LevelMarkerTile>();
start.spawnKind = Assets.Scripts.LevelSpawnKind.Player;
var wall = ScriptableObject.CreateInstance<Assets.Scripts.LevelMarkerTile>();
wall.walkable = false; wall.blocksSight = true; wall.allowsSpells = false;
var noSpell = ScriptableObject.CreateInstance<Assets.Scripts.LevelMarkerTile>();
noSpell.allowsSpells = false;
var enemy = ScriptableObject.CreateInstance<Assets.Scripts.LevelMarkerTile>();
enemy.spawnKind = Assets.Scripts.LevelSpawnKind.Enemy;
enemy.enemy = UnityEditor.AssetDatabase.LoadAssetAtPath<Assets.Scripts.ScriptableObjects.EnemyData>("Assets/Scripts/ScriptableObjects/Enemies/Normal.asset");
enemy.spawnInterval = 1f;
try
{
    level.logic.ClearAllTiles();
    for (int y = -3; y <= 3; y++)
        for (int x = -3; x <= 3; x++) level.logic.SetTile(new Vector3Int(x, y, 0), floor);
    level.logic.SetTile(Vector3Int.zero, start);
    level.logic.SetTile(new Vector3Int(1, 0, 0), null);
    level.logic.SetTile(new Vector3Int(-1, 0, 0), wall);
    level.logic.SetTile(new Vector3Int(0, 1, 0), noSpell);
    level.logic.SetTile(new Vector3Int(0, -2, 0), enemy);
    game.InitializedGrid();
    require(game.tilesSet.Count == 48 && game.tilesGrid[3, 4] == null && !game.HasCell(3, 4), "Only existing cells get runtime objects.");
    require(game.HasCell(3, 2) && !game.CanWalk(3, 2), "Wall is present but not walkable.");
    require(game.CanWalk(2, 3) && !game.Castable(2, 3) && game.CanBlinkTo(2, 3), "Walkability and spell permission are independent.");
    require(!game.Castable(3, 4) && !game.Castable(3, 5) && !game.CanBlinkTo(3, 5), "Hole blocks targeting through it.");
    require(!game.CanWalk(-1, 0) && !game.HasCell(7, 3), "Array bounds remain guarded.");
    foreach (var tile in game.tilesSet)
    {
        Vector3 world = level.logic.GetCellCenterWorld(game.LevelLayout.ToCell(tile.row, tile.col));
        require((tile.transform.position - world).sqrMagnitude < .00001f, "Runtime cell alignment.");
        require(Assets.Scripts.GridHelper.INSTANCE.TryGetTileOn(world, out var found) && found == tile, "World-to-cell round trip.");
    }
    require(!Assets.Scripts.GridHelper.INSTANCE.TryGetTileOn(level.logic.GetCellCenterWorld(new Vector3Int(1, 0, 0)), out _), "World lookup rejects a hole.");
    require(mobs.getBestPath(3, 4) == null && mobs.getBestPath(3, 2) == null && mobs.getBestPath(3, 5) != null,
        "Paths avoid holes/walls and can go around them.");
    var mobSet = (System.Collections.Generic.HashSet<MobScript>)typeof(Assets.Scripts.MobHandler).GetField("mobs", flags).GetValue(mobs);
    require(mobSet.Count == 1, "One authored enemy spawn.");
    mobs.SummonAt(3, 4, enemy.enemy);
    mobs.SummonAt(3, 2, enemy.enemy);
    require(mobSet.Count == 1, "Invalid enemy destinations rejected.");
    var schedules = (System.Collections.Generic.List<(Assets.Scripts.TilemapLevel.Spawn spawn, float next)>)typeof(Assets.Scripts.MobHandler).GetField("spawners", flags).GetValue(mobs);
    require(schedules.Count == 1, "Repeating spawner registered.");
    foreach (var mob in mobSet) mob.transform.position = game.tilesGrid[5, 2].transform.position;
    schedules[0] = (schedules[0].spawn, Time.time - 1f);
    typeof(Assets.Scripts.MobHandler).GetMethod("Update", flags).Invoke(mobs, null);
    require(mobSet.Count == 2 && schedules[0].next > Time.time, "Spawner repeats and advances its timer.");

    foreach (int stage in new[] { 110, 210, 310, 410 })
    {
        System.Array.Clear(game.grid, 0, game.grid.Length);
        game.grid[3, 3] = stage;
        var faded = (int[,])typeof(Assets.Scripts.GameLogic).GetMethod("HandleFading", flags).Invoke(game, new object[] { game.grid });
        require(faded[3, 3] == 0 && faded[3, 4] == 0 && faded[3, 2] == 0 && faded[2, 3] == 0,
            "Fade clears its source without writing to holes or forbidden terrain.");
        require(faded[4, 3] == stage + 1, "Fade still reaches valid floor.");
    }
    // Exercise every element pairing against the hole and authored wall.
    foreach (int a in new[] { 100, 200, 300, 400 })
        foreach (int b in new[] { 100, 200, 300, 400 })
        {
            System.Array.Clear(game.grid, 0, game.grid.Length);
            game.grid[3, 3] = a;
            game.grid[4, 3] = b;
            var reacted = (int[,])typeof(Assets.Scripts.GameLogic).GetMethod("HandleSpells", flags).Invoke(game, new object[] { game.grid });
            var overlap = (int[,])typeof(Assets.Scripts.GameLogic).GetMethod("HandleOverlap", flags).Invoke(game, new object[] { reacted, game.grid });
            require(reacted[3, 4] == 0 && overlap[3, 4] == 0 && overlap[3, 2] == 0 && overlap[2, 3] == 0,
                "Reactions and overlaps respect mask and terrain permissions.");
        }
    System.Array.Clear(game.grid, 0, game.grid.Length);
    game.UpdateTiles();
    game.currMana = 100f;
    game.MakeCastable();
    game.MakeMove(3, 4, 100);
    require(!game.IsPlacementMode, "Cannot queue on a hole.");
    game.MakeMove(4, 3, 100);
    require(game.IsPlacementMode, "Valid cells still accept spells.");
    game.SubmitQueuedSpells();
    require(game.grid[4, 3] == 100 && game.tilesGrid[4, 3].SurfaceRenderer.enabled, "Spells render above authored background.");
    foreach (var mob in mobSet) mob.TakeDamage(); // Adjacent null tiles must be safe.
    game.InitializedGrid();
    require(!game.IsPlacementMode && game.grid[4, 3] == 0 && player.r == 3 && player.c == 3,
        "Reload restores mask/spawns and clears spell state.");
    require(((System.Collections.Generic.HashSet<MobScript>)typeof(Assets.Scripts.MobHandler).GetField("mobs", flags).GetValue(mobs)).Count == 1,
        "Reload replaces enemies instead of accumulating them.");

    level.GetComponent<Grid>().cellSize = new Vector3(2f, 2f, 1f);
    game.InitializedGrid();
    Physics2D.SyncTransforms();
    require(Mathf.Approximately(game.spacing, 2f), "Runtime spacing follows the authoring grid.");
    var scaledTile = game.tilesGrid[4, 3];
    require((scaledTile.transform.position - level.logic.GetCellCenterWorld(game.LevelLayout.ToCell(4, 3))).sqrMagnitude < .00001f,
        "Non-unit cell centers align.");
    require(Mathf.Approximately(scaledTile.GetComponent<BoxCollider2D>().bounds.size.x, 2f * game.gridParent.lossyScale.x),
        "Picking colliders match non-unit cells.");
    var pointer = UnityEngine.Object.FindAnyObjectByType<GridPointer>();
    pointer.SetHovered(scaledTile);
    var hover = (SpriteRenderer)typeof(GridPointer).GetField("hoverOverlay", flags).GetValue(pointer);
    require(Mathf.Abs(hover.bounds.size.x - scaledTile.GetComponent<BoxCollider2D>().bounds.size.x) < .001f,
        "Hover artwork matches scaled cells.");

    typeof(Assets.Scripts.GameLogic).GetField("level", flags).SetValue(game, null);
    game.InitializedGrid();
    require(game.LevelLayout == null && game.tilesSet.Count == 49 && game.HasCell(3, 4), "Unassigned levels retain rectangular fallback.");
    require(game.tilesGrid[3, 3].SurfaceRenderer.enabled && game.GridTranslation == Vector3.zero,
        "Fallback restores floor visuals and the legacy origin.");
    require(player.transform.position == game.tilesGrid[player.r, player.c].transform.position, "Reload places the player on the rebuilt grid.");
}
finally
{
    // Test map references are discarded when Play mode stops.
    foreach (var asset in new UnityEngine.Object[] { floor, start, wall, noSpell, enemy }) UnityEngine.Object.Destroy(asset);
}
return $"PASS: {checks} tilemap runtime checks (mask, coordinates, movement, spawns, spells, visuals, reload).";
