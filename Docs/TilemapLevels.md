# Painting levels

SampleScene is configured with `GridParent/Level/Background` and `GridParent/Level/Logic`.
Open Unity's Tile Palette window and select **Level Palette**. The palette contains,
left to right: Floor, Wall, Player Spawn, Enemy Spawn, Enemy Spawner, Background Floor.
The corresponding assets live in `Assets/Levels`.

1. Select **Background** as the active tilemap and paint the visible artwork. The supplied
   Background Floor tile uses the existing floor sprite; other regular tile assets work too.
2. Select **Logic** and paint Floor everywhere gameplay should exist. Erase Logic tiles
   to create holes or irregular borders. Background art alone does not create game cells.
3. Replace one Floor tile with **Player Spawn**. There must be exactly one.
4. Paint **Enemy Spawn** for an enemy present at load, or **Enemy Spawner** for an enemy
   at load and subsequent attempts every five seconds. Both markers also define walkable floor.
5. Paint **Wall** for an existing cell that blocks movement, sight, and spells. Paint its
   appearance separately on Background: the marker itself is hidden in the game.
6. Save the scene and enter Play mode. Logic's renderer is hidden, its data is retained,
   and the parsed mask determines where runtime cells are created.

For another existing grid scene, use **Tools > Grid Mage > Levels > Create tilemaps from
current grid**. It creates and assigns aligned maps, fills the current rectangular board,
and copies the player coordinates and valid legacy enemy positions. It does not overwrite
an already assigned level. Save the scene afterward. Scenes without an assigned Level on
GameLogic retain the old rectangular initialization.

## Marker properties

Create variants with **Assets > Create > Grid Mage > Level Marker**, or duplicate an
existing marker asset. Add them to the palette as needed. Each painted marker creates an
existing cell and independently specifies:

- **Walkable:** whether actors may occupy it, subject to runtime stone walls.
- **Blocks Sight:** whether targeting and reaction rays can pass through it.
- **Allows Spells:** whether spells may be placed or written into it.
- **Spawn Kind:** none, player, or enemy.
- **Enemy:** the EnemyData asset for that spawn, independent of the legacy enemy type list.
- **Spawn Interval:** zero for a single attempt at load, positive seconds for repeated attempts.

A spawn must be walkable. Repeating spawners skip attempts while occupied, blocked by a
runtime wall, occupied by the player, or disconnected from the player. They retry at the
next interval, with no accumulated backlog. Timers use scaled game time.

Marker assets are shared definitions. Parsing copies their flags and spawn settings into
runtime data. Gameplay spell state stays in `GameLogic.grid`; it never modifies the assets.
One coordinate holds one marker, so a spawn marker includes its terrain properties.

## Coordinates and runtime data

`GameLogic.cellExists[row, col]` masks the existing `int[,] grid` and `Tile[,] tilesGrid`.
Absent cells have no Tile object and are blocked for movement, sight, and spell writes.
Use `HasCell`, `CanWalk`, or `AllowsSpells` for gameplay checks; array bounds alone do not
prove that a cell exists. Permanent terrain flags stay separate from changing elemental state.

Array bounds come from occupied Logic cells, including walls and spawns, with the top-left
cell at `[0, 0]`. Negative tilemap coordinates are supported. `LevelLayout.ToCell` and
`ToIndex` convert between coordinates, and `GridHelper.TryGetTileOn` resolves world positions.
Rows increase downward; tilemap Y increases upward. Do not hardcode array indices as saved
level positions: adding cells above or to the left changes the array origin.

Use square rectangular cells at Z = 0 with no gap. Keep the maps aligned with each other
and with GridParent's local axes. GridParent itself can be translated and uniformly scaled.
The supplied palette artwork fits unit cells; use a cell size of 1 and scale GridParent to
resize the whole level. The loader rejects empty maps, unknown Logic tile types, invalid
spawns, mismatched map centers, and unsupported grid geometry.

Runtime Tile objects retain colliders, elemental artwork, queued previews, and particles.
Their normal floor artwork is suppressed when Background is assigned. Authored static
walls are permanent terrain; they do not become elemental stone and do not participate in
stone reactions. Missing cells block sight as well as movement in this implementation.

`GameLogic.InitializedGrid()` rereads the retained Logic map, rebuilds masks/cells, clears
queued spells, and restores authored player/enemy spawns. It is a board reload, not a full
reset of health, mana, or the combat clock. Editing the mask directly at runtime is not an
authoring workflow; edit the Logic map and reload the board.

## Checks

- **Tools > Grid Mage > Levels > Check tilemap parsing** runs temporary Edit mode checks.
- In a fresh SampleScene Play session, run
  `unity command eval_file --file Tests/TilemapLevel/Runtime.cs --json`, then stop Play mode.
  The runtime check temporarily replaces the board to exercise holes, negative coordinates,
  spawns, pathfinding, spell propagation, rendering flags, and reloads.
- Existing Blink checks remain under `Tests/Blink`.
