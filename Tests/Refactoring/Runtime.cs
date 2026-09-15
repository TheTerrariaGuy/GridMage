// Run in a fresh rectangular ValidationFixture Play session; stop Play afterward.
CheckSupport.Require(Application.isPlaying, "Enter fixture Play mode first.");
var game = Assets.Scripts.GameLogic.INSTANCE;
var mobs = Assets.Scripts.MobHandler.INSTANCE;
var player = Assets.Scripts.PlayerHandler.INSTANCE;
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
int checks = 0;
System.Action<bool, string> require = (ok, why) => { checks++; CheckSupport.Require(ok, why); };
float savedTimeScale = Time.timeScale;
try
{
    Time.timeScale = 0;
    game.InitializeGrid();
    var presentation = new Assets.Scripts.TilePresentation();
    presentation.Refresh(game.tilesGrid, false);
    require(presentation.LastSpriteRefreshCount == 0, "An unchanged board performs no sprite refreshes.");
    game.Board.Set(3, 3, 100);
    require(game.tilesGrid[3, 3].type == 100, "Tile type reads the authoritative board before presentation.");
    presentation.Refresh(game.tilesGrid, false);
    require(presentation.LastSpriteRefreshCount == 9, "One interior change refreshes its 3x3 neighborhood once.");
    game.Board.Set(3, 3, 200); game.Board.Set(3, 4, 200);
    presentation.Refresh(game.tilesGrid, false);
    require(presentation.LastSpriteRefreshCount == 12, "Adjacent changes share six neighbor refreshes.");
    presentation.Refresh(game.tilesGrid, false);
    require(presentation.LastSpriteRefreshCount == 0, "The completed batch leaves no stale dirty state.");
    game.Board.Set(0, 0, 300);
    presentation.Refresh(game.tilesGrid, false);
    require(presentation.LastSpriteRefreshCount == 4, "Corner batching stays inside the board.");

    object paths = typeof(Assets.Scripts.MobHandler).GetField("optimalPath", flags).GetValue(mobs);
    object visited = typeof(Assets.Scripts.MobHandler).GetField("visited", flags).GetValue(mobs);
    mobs.UpdateBestPath(player.r, player.c);
    require(ReferenceEquals(paths, typeof(Assets.Scripts.MobHandler).GetField("optimalPath", flags).GetValue(mobs)), "Path buffers are reused for unchanged dimensions.");
    require(ReferenceEquals(visited, typeof(Assets.Scripts.MobHandler).GetField("visited", flags).GetValue(mobs)), "Visited storage is reused across path updates.");
    require(mobs.IsReachable(3, 3), "Reused buffers still produce reachable routes.");

    foreach (var cell in Assets.Scripts.LegacyLevelDefaults.EnemyCells)
        require(mobs.IsOccupied(cell.row, cell.col), "Fallback spawns match the shared level defaults.");
    var set = (System.Collections.Generic.HashSet<MobScript>)typeof(Assets.Scripts.MobHandler).GetField("mobs", flags).GetValue(mobs);
    var mob = set.First();
    Assets.Scripts.GridHelper.INSTANCE.TryGetTileOn(mob.transform.position, out var previous);
    mob.transform.position = game.tilesGrid[2, 2].transform.position;
    require(mobs.IsOccupied(2, 2) && !mobs.IsOccupied(previous.row, previous.col), "Spawn checks observe enemy movement immediately.");
    int count = set.Count;
    mobs.SummonAt(2, 2, 1);
    require(set.Count == count, "Spawning on another enemy remains blocked.");
    mobs.SummonAt(player.r, player.c, 1);
    require(set.Count == count, "Spawning on the player remains blocked.");
    mob.enabled = false;
    require(!mobs.IsOccupied(2, 2), "Disabled enemies stop occupying spawn cells immediately.");
    mob.enabled = true;

    game.InitializeGrid();
    require(game.tilesGrid[3, 3].type == 0 && game.tilesGrid[0, 0].type == 0, "Reload replaces authoritative state and presentation.");
    require(!mobs.IsOccupied(2, 2), "Reload discards previous occupancy.");
    presentation.Refresh(game.tilesGrid, false);
    require(presentation.LastSpriteRefreshCount == 0, "Reload finishes a complete presentation batch.");
}
finally { Time.timeScale = savedTimeScale; }
return $"PASS: {checks} board ownership, sprite batching, buffer reuse, occupancy and reset assertions.";
