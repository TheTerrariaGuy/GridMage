// Run with Unity Pipeline eval_file in a fresh SampleScene Play session, then stop Play mode.

if (!Application.isPlaying) throw new System.Exception("Enter Play mode first.");
Time.timeScale = 0f;
var game = Assets.Scripts.GameLogic.INSTANCE;
var player = Assets.Scripts.PlayerHandler.INSTANCE;
var mobs = Assets.Scripts.MobHandler.INSTANCE;
var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var speedField = typeof(Assets.Scripts.PlayerHandler).GetField("blinkSpeed", flags);
float originalSpeed = (float)speedField.GetValue(player);
float originalCooldown = Indexing.INSTANCE.blinkCooldown;
speedField.SetValue(player, 0f);
Indexing.INSTANCE.blinkCooldown = 0f;
System.Action<bool, string> require = (condition, message) =>
{
    if (!condition) throw new System.Exception(message);
};
game.currMana = 30f;
player.r = 5;
player.c = 5;
player.transform.position = game.tilesGrid[5, 5].transform.position;
float hp = player.hp;
float combatTime = game.time;
foreach (var tile in game.tilesGrid)
{
    game.RemoveQueuedSpell(tile.row, tile.col);
    game.grid[tile.row, tile.col] = 0;
}
game.UpdateTiles();
Physics2D.SyncTransforms();
game.MakeCastable();
require(!game.TryCastBlink(game.tilesGrid[5, 5]), "Current tile must be rejected.");
require(!game.TryCastBlink(game.tilesGrid[5, 9]), "Out-of-range tile must be rejected.");
require(!game.TryCastBlink(null), "Null destination must be rejected.");
game.grid[5, 6] = 400;
game.MakeCastable();
require(!game.TryCastBlink(game.tilesGrid[5, 6]), "Wall destination must be rejected.");
require(!game.TryCastBlink(game.tilesGrid[5, 7]), "Wall must block the route.");
require(game.CanBlinkTo(6, 6), "A single corner wall allows Blink.");
game.grid[6, 5] = 400;
game.MakeCastable();
require(!game.TryCastBlink(game.tilesGrid[6, 6]), "Two corner walls must block Blink.");
game.grid[6, 5] = 0;
game.grid[5, 6] = 0;
game.MakeCastable();
mobs.SummonAt(6, 5, 1);
require(!game.TryCastBlink(game.tilesGrid[6, 5]), "Occupied destination must be rejected.");
require(game.currMana == 30f, "Invalid casts must not spend mana.");

game.currMana = 10f;
game.MakeMove(5, 3, 100);
require(game.AvailableMana == 2f, "Queue must reserve its mana.");
require(!game.TryCastBlink(game.tilesGrid[5, 6]), "Blink cannot spend reserved mana.");
game.currMana = 12f;
require(game.TryCastBlink(game.tilesGrid[5, 6]), "Exactly enough available mana must succeed.");
require(player.r == 5 && player.c == 6 && player.transform.position == game.tilesGrid[5, 6].transform.position,
    "Blink must relocate both coordinates and transform.");
require(game.currMana == 8f && game.AvailableMana == 0f && game.IsPlacementMode && game.grid[5, 3] == 0,
    "Blink must preserve the queued spell without submitting it.");
require(mobs.getBestPath(5, 6) == null && mobs.getBestPath(5, 5) != null,
    "Enemy path field must target the new location immediately.");
require(player.hp == hp && game.time == combatTime, "Blink must not advance combat.");
foreach (var sprite in UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
    if (sprite.name == "Blink afterimage")
        require(sprite.sprite.name.StartsWith("Player"), "Afterimages must use player artwork, not the spell selector.");
game.currMana = 13f;
game.SubmitQueuedSpells();
require(game.grid[5, 3] == 100 && game.currMana == 0f && !game.IsPlacementMode,
    "Reserved spell must still submit correctly after Blink.");

game.currMana = 30f;
game.UpdateSelection(100);
var pointer = UnityEngine.Object.FindAnyObjectByType<GridPointer>();
var camera = (Camera)typeof(GridPointer).GetField("viewCamera", flags).GetValue(pointer);
var update = typeof(GridPointer).GetMethod("Update", flags);
var mouse = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
var keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
System.Action advanceInput = () => typeof(UnityEngine.InputSystem.InputSystem).GetMethod("Update", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(UnityEngine.InputSystem.LowLevel.InputUpdateType) }, null).Invoke(null, new object[] { UnityEngine.InputSystem.LowLevel.InputUpdateType.Dynamic });
var inputSettings = UnityEngine.InputSystem.InputSystem.settings;
var previousBackground = inputSettings.backgroundBehavior;
var previousRouting = inputSettings.editorInputBehaviorInPlayMode;
try
{
    inputSettings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
    inputSettings.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    Vector2 screen = camera.WorldToScreenPoint(game.tilesGrid[5, 7].transform.position);
    require(pointer.Pick(screen) == game.tilesGrid[5, 7], "Test target must be visible and pickable.");
    UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = screen });
    UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.LeftShift));
    advanceInput();
    update.Invoke(pointer, null);
    UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = screen }.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));
    advanceInput();
    update.Invoke(pointer, null);
    require(player.c == 7 && game.currMana == 26f && !game.IsPlacementMode,
        $"Shift-click must cast immediately: col={player.c}, mana={game.currMana}, pressed={mouse.leftButton.isPressed}, edge={mouse.leftButton.wasPressedThisFrame}, current={UnityEngine.InputSystem.Mouse.current == mouse}, shift={keyboard.leftShiftKey.isPressed}.");
    screen = camera.WorldToScreenPoint(game.tilesGrid[5, 8].transform.position);
    UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = screen }.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));
    advanceInput();
    update.Invoke(pointer, null);
    require(player.c == 7 && game.currMana == 26f, "Holding the click must not cast again on another tile.");
    UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
    advanceInput();
    update.Invoke(pointer, null);
    require(!game.IsPlacementMode, "Releasing Shift during the click must not place a spell.");
    UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = screen });
    advanceInput();
    update.Invoke(pointer, null);
    UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = screen }.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));
    advanceInput();
    update.Invoke(pointer, null);
    require(game.IsPlacementMode, "A new ordinary click must still queue spells.");
    game.RemoveQueuedSpell(5, 8);
}
finally
{
    UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);
    UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
    inputSettings.backgroundBehavior = previousBackground;
    inputSettings.editorInputBehaviorInPlayMode = previousRouting;
    speedField.SetValue(player, originalSpeed);
    Indexing.INSTANCE.blinkCooldown = originalCooldown;
}
return "Passed Blink integration: mana reservations, validity, relocation, enemy routes, single-click input, modifier release, and ordinary placement.";
