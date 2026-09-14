// Run in a fresh Play session, then stop Play mode to discard the temporary board.
if (!Application.isPlaying) throw new System.Exception("Enter Play mode first.");
Time.timeScale = 0;
var game = Assets.Scripts.GameLogic.INSTANCE;
var player = Assets.Scripts.PlayerHandler.INSTANCE;
var mobs = Assets.Scripts.MobHandler.INSTANCE;
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
int checks = 0;
System.Action<bool, string> require = (ok, why) => { checks++; if (!ok) throw new System.Exception(why); };
var floor = ScriptableObject.CreateInstance<Assets.Scripts.LevelMarkerTile>(); floor.elevation = 1;
var start = ScriptableObject.CreateInstance<Assets.Scripts.LevelMarkerTile>(); start.elevation = 1; start.spawnKind = Assets.Scripts.LevelSpawnKind.Player;
var map = game.Level.logic;
map.ClearAllTiles();
for (int y = -4; y <= 4; y++) for (int x = -4; x <= 4; x++) map.SetTile(new Vector3Int(x, y, 0), floor);
map.SetTile(Vector3Int.zero, start);
Indexing.INSTANCE.castRange = 4; Indexing.INSTANCE.blinkRange = 3;
game.InitializedGrid();
require(player.r == 4 && player.c == 4 && game.elevationGrid[4, 4] == 1, "Load elevation and player");
game.elevationGrid[4, 5] = 1.5f; game.elevationGrid[4, 6] = 2; game.elevationGrid[4, 7] = 2.5f; game.elevationGrid[4, 8] = 3;
game.MakeCastable();
require(game.CanBlinkTo(4, 7) && game.moveableGrid[4, 7] && game.Castable(4, 7), "Blink climbs ramp");
require(!game.CanBlinkTo(4, 8) && game.Castable(4, 8), "Blink range restricts yellow border");
Indexing.INSTANCE.castRange = 2; game.MakeCastable();
require(!game.CanBlinkTo(4, 7), "Existing cast visibility range still limits Blink");
Indexing.INSTANCE.castRange = 4; game.MakeCastable();
require(!game.CanBlinkTo(4, 4) && !game.moveableGrid[4, 4], "Origin is not a Blink destination");
game.elevationGrid[4, 5] = 2;
game.MakeCastable();
require(!game.CanBlinkTo(4, 6) && !game.moveableGrid[4, 6], "Blink cannot cross intermediate cliff");
require(game.Castable(4, 6), "Cast mask ignores elevation");
game.currMana = 100;
game.MakeMove(4, 6, 100); require(game.IsPlacementMode, "Queue spell across cliff");
game.SubmitQueuedSpells(); require(game.grid[4, 6] == 100, "Placement crosses elevation unchanged");
game.grid[4, 6] = 0;
// Reactions: requirements, outputs, overlap path and decay all use ordered elevations.
var checkReq = typeof(Assets.Scripts.GameLogic).GetMethod("CheckReq", flags);
game.grid[4, 6] = 200;
var req = new Indexing.Requirement[] { new Indexing.Requirement(2, 0, 200, false) };
require(!(bool)checkReq.Invoke(game, new object[] { game.grid, req, 4, 4 }), "Ingredient across cliff rejected");
game.elevationGrid[4, 5] = 1.5f;
require((bool)checkReq.Invoke(game, new object[] { game.grid, req, 4, 4 }), "Ingredient up ramp accepted");
var apply = typeof(Assets.Scripts.GameLogic).GetMethod("ApplyQueuedChanges", flags);
System.Func<int, int[,]> output = type => {
    var queue = new System.Collections.Generic.SortedSet<Assets.Scripts.GameLogic.Change>();
    queue.Add(new Assets.Scripts.GameLogic.Change(4, 6, type, 0, 0, 4, 4));
    var args = new object[] { queue, new int[9, 9] }; apply.Invoke(game, args); return (int[,])args[1];
};
require(output(100)[4, 6] == 100, "Output climbs ramp");
game.elevationGrid[4, 5] = 2;
require(output(100)[4, 6] == 0 && output(410)[4, 6] == 0, "Normal/lava output blocked by cliff");
var fading = new int[9, 9]; fading[4, 4] = 110;
var faded = (int[,])typeof(Assets.Scripts.GameLogic).GetMethod("HandleFading", flags).Invoke(game, new object[] { fading });
require(faded[4, 5] == 0 && faded[4, 3] == 111 && faded[4, 4] == 0, "Decay respects cliffs and still clears origin");
// Enemy routes cannot cross a full-height barrier, even if its far side is flat.
for (int r = 0; r < 9; r++) for (int c = 0; c < 9; c++) { game.grid[r, c] = 0; game.elevationGrid[r, c] = c >= 5 ? 2 : 1; }
mobs.UpdateBestPath(4, 4);
require(!mobs.IsReachable(4, 6), "Enemy path blocked by full step");
game.elevationGrid[4, 5] = 1.5f;
mobs.UpdateBestPath(4, 4);
require(mobs.IsReachable(4, 6), "Enemy path finds half-step opening");
for (int i = 0; i < 100; i++)
{
    var step = mobs.getBestPath(3, 4, 100f);
    require(step == null || game.CanStep(3, 4, 3 + step.r, 4 + step.c), "Wandering never crosses cliff");
}
game.MakeCastable();
var enemy = UnityEditor.AssetDatabase.LoadAssetAtPath<Assets.Scripts.ScriptableObjects.EnemyData>("Assets/Scripts/ScriptableObjects/Enemies/Normal.asset");
mobs.SummonAt(4, 6, enemy);
game.MakeCastable();
require(!game.CanBlinkTo(4, 6) && !game.moveableGrid[4, 6], "Occupied cell excluded from yellow border");
var mobSet = (System.Collections.Generic.HashSet<MobScript>)typeof(Assets.Scripts.MobHandler).GetField("mobs", flags).GetValue(mobs);
foreach (var mob in mobSet) mob.transform.position = game.tilesGrid[3, 6].transform.position;
typeof(Assets.Scripts.GameLogic).GetMethod("LateUpdate", flags).Invoke(game, null);
require(game.CanBlinkTo(4, 6) && game.moveableGrid[4, 6], "Border follows moving occupancy between ticks");
// Existing wall corner and straight-ray restrictions remain in force.
game.grid[4, 5] = 400; game.MakeCastable();
require(!game.CanBlinkTo(4, 6) && !game.Castable(4, 6), "Wall still blocks both borders");
require(!player.BlinkTo(game.tilesGrid[4, 6]), "Actual Blink entry rejects blocked ray");
game.grid[4, 5] = 0; game.MakeCastable();
typeof(Assets.Scripts.PlayerHandler).GetField("blinkSpeed", flags).SetValue(player, 0f);
require(game.TryCastBlink(game.tilesGrid[4, 6]) && player.c == 6, "Actual Blink accepts ordered ramp");
require(!game.moveableGrid[4, 6], "Border refreshes around new player origin");
require(game.CanBlinkTo(4, 4), "Descending ramp after Blink");
game.InitializedGrid();
require(game.elevationGrid[4, 5] == 1, "Reload restores authored heights");
// Corner rules must agree between cached Blink destinations and actual reaction writes.
for (int mask = 0; mask < 4; mask++)
{
    bool allowed = mask != 3;
    game.elevationGrid[4, 5] = game.elevationGrid[5, 4] = 1;
    game.grid[4, 5] = (mask & 1) != 0 ? 400 : 0;
    game.grid[5, 4] = (mask & 2) != 0 ? 400 : 0;
    game.MakeCastable();
    require(game.CanBlinkTo(5, 5) == allowed && game.moveableGrid[5, 5] == allowed, "Blink wall corner mask");
    var cornerQueue = new System.Collections.Generic.SortedSet<Assets.Scripts.GameLogic.Change>();
    cornerQueue.Add(new Assets.Scripts.GameLogic.Change(5, 5, 100, 0, 0, 4, 4));
    var cornerArgs = new object[] { cornerQueue, (int[,])game.grid.Clone() };
    apply.Invoke(game, cornerArgs);
    require((((int[,])cornerArgs[1])[5, 5] == 100) == allowed, "Reaction wall corner");
    game.grid[4, 5] = game.grid[5, 4] = 0;
    game.elevationGrid[4, 5] = (mask & 1) != 0 ? 4 : 1;
    game.elevationGrid[5, 4] = (mask & 2) != 0 ? 4 : 1;
    game.MakeCastable();
    require(game.CanBlinkTo(5, 5) == allowed && game.moveableGrid[5, 5] == allowed, "Blink elevation corner mask");
    require(game.Castable(5, 5), "Elevation corners do not restrict placement");
    cornerQueue.Add(new Assets.Scripts.GameLogic.Change(5, 5, 100, 0, 0, 4, 4));
    cornerArgs = new object[] { cornerQueue, (int[,])game.grid.Clone() };
    apply.Invoke(game, cornerArgs);
    require((((int[,])cornerArgs[1])[5, 5] == 100) == allowed, "Reaction elevation corner");
}
UnityEngine.Object.Destroy(floor); UnityEngine.Object.Destroy(start);
return $"PASS: {checks} runtime Blink, placement, reaction, enemy, occupancy and reload assertions.";
