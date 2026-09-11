// Run in a fresh Play session. Stop Play mode afterward to discard test state.
if (!Application.isPlaying) throw new System.Exception("Enter Play mode first.");
var game = Assets.Scripts.GameLogic.INSTANCE;
var player = Assets.Scripts.PlayerHandler.INSTANCE;
var index = Indexing.INSTANCE;
var speed = typeof(Assets.Scripts.PlayerHandler).GetField("blinkSpeed",
    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
float oldSpeed = (float)speed.GetValue(player), oldCooldown = index.blinkCooldown;
int oldRange = index.castRange;
System.Action<bool, string> require = (condition, message) =>
{
    if (!condition) throw new System.Exception(message);
};
Time.timeScale = 0f;
try
{
    require(game.castableGrid != null && game.Castable(player.r, player.c), "Player initialization must build castability.");
    index.castRange = 2;
    player.r = 5;
    player.c = 5;
    player.transform.position = game.tilesGrid[5, 5].transform.position;
    game.InitializedGrid();
    require(game.castableGrid.GetLength(0) == game.grid.GetLength(0) &&
        game.castableGrid.GetLength(1) == game.grid.GetLength(1), "Cache must match the board dimensions.");
    for (int row = 0; row < game.grid.GetLength(0); row++)
        for (int col = 0; col < game.grid.GetLength(1); col++)
            require(game.Castable(row, col) == (Mathf.Abs(row - 5) <= 2 && Mathf.Abs(col - 5) <= 2),
                "Range 2 must produce an inclusive 5 by 5 square.");
    require(!game.Castable(-1, 0) && !game.Castable(0, int.MaxValue), "Out-of-bounds queries must return false.");
    index.castRange = 0;
    game.MakeCastable();
    require(game.Castable(5, 5) && !game.Castable(5, 6), "Zero radius must include only the origin.");
    index.castRange = 2;
    player.r = player.c = 0;
    game.MakeCastable();
    int visible = 0;
    foreach (bool cell in game.castableGrid) if (cell) visible++;
    require(visible == 9 && game.Castable(2, 2) && !game.Castable(3, 0), "Range must clip safely at grid edges.");
    player.r = player.c = 5;
    game.MakeCastable();
    game.grid[5, 6] = 400;
    require(game.Castable(5, 7), "Castable must read the cache, not run another ray test.");
    game.MakeCastable();
    require(!game.Castable(5, 6) && !game.Castable(5, 7) && !game.Castable(6, 6),
        "Wall graph must block destinations, tiles behind walls, and diagonal corners.");
    game.currMana = 30f;
    require(!game.CanQueueSpell(5, 7, 100) && !game.TryCastBlink(game.tilesGrid[5, 7]),
        "Normal spells and Blink must both respect cached line of sight.");
    require(player.IsInBlinkRange(game.tilesGrid[5, 8]) && !game.TryCastBlink(game.tilesGrid[5, 8]),
        "Cast range must also restrict otherwise in-range Blinks.");
    game.grid[5, 6] = 0;
    game.MakeCastable();
    game.MakeMove(5, 3, 100);
    require(game.IsPlacementMode && game.AvailableMana == 22f, "In-range preview must reserve mana.");
    speed.SetValue(player, 0f);
    index.blinkCooldown = 0f;
    require(game.TryCastBlink(game.tilesGrid[5, 7]), "Blink inside the visible square must succeed.");
    require(!game.Castable(5, 3) && game.Castable(5, 9), "Blink arrival must rebuild the range at the new position.");
    require(game.IsPlacementMode && game.AvailableMana == 18f && game.grid[5, 3] == 0,
        "Previously queued preview must survive moving out of range.");
    game.SubmitQueuedSpells();
    require(game.grid[5, 3] == 100 && !game.IsPlacementMode && game.currMana == 13f,
        "Out-of-range queued preview must still cast without a range recheck.");
}
finally
{
    index.castRange = oldRange;
    index.blinkCooldown = oldCooldown;
    speed.SetValue(player, oldSpeed);
    game.MakeCastable();
}
return "PASS: initialization, square bounds, walls graph, cached queries, spell/Blink gating, post-Blink rebuild, and retained queued previews";
