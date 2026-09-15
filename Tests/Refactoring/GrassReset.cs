// Run in a fresh authored-level Play session; stop Play afterward.
CheckSupport.Require(Application.isPlaying, "Enter a fresh authored-level Play session.");
var game = Assets.Scripts.GameLogic.INSTANCE;
var wind = UnityEngine.Object.FindAnyObjectByType<Assets.Scripts.GrassWind>();
CheckSupport.Require(wind != null, "The authored level must have grass wind.");
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
var cells = (Vector3Int[])typeof(Assets.Scripts.GrassWind).GetField("grassCells", flags).GetValue(wind);
var heightsField = typeof(Assets.Scripts.GrassWind).GetField("grassElevations", flags);
var background = wind.GetComponent<UnityEngine.Tilemaps.Tilemap>();
int checkedCells = 0;
System.Action check = () => {
 var heights = (float[])heightsField.GetValue(wind);
 for(int i = 0; i < cells.Length; i++) {
  float expected = Assets.Scripts.GridHelper.INSTANCE.TryGetTileOn(background.GetCellCenterWorld(cells[i]), out var tile)
   ? game.elevationGrid[tile.row,tile.col] : 0f;
  CheckSupport.Require(heights[i] == expected, "Grass cache must match the initialized board.");
  checkedCells++;
 }
};
check();
CheckSupport.Require(checkedCells > 0, "The grass cache check must cover painted cells.");
var changedCell = cells.First(c => game.Level.logic.GetTile(c) is Assets.Scripts.LevelMarkerTile);
var original = game.Level.logic.GetTile<Assets.Scripts.LevelMarkerTile>(changedCell);
var replacement = UnityEngine.Object.Instantiate(original);
replacement.elevation += .5f;
try {
 game.Level.logic.SetTile(changedCell, replacement);
 game.InitializeGrid();
 check();
 game.Level.logic.SetTile(changedCell, original);
 game.InitializeGrid();
 check();
} finally {
 game.Level.logic.SetTile(changedCell, original);
 UnityEngine.Object.Destroy(replacement);
}
return $"PASS: {checkedCells} grass elevation-cache assertions across initialization, changed heights, and restoration.";
