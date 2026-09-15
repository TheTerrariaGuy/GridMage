# Migration record

## Asset references

313 assets were moved with their original .meta files. The move manifest records old/new paths. One of those assets, the inactive water-diagonal effect prefab, was subsequently retired. All 312 retained moved assets were verified against their original GUIDs. Empty former folders were removed.

New assets include the shared sprite catalog and reaction TextAsset. Their references were assigned in In Game.unity. Existing player ability values were copied to PlayerHandler. The Bootstrap scene and the build-scene selection were not repurposed.

The snapshot comparisons were captured from commit e38d584 before editing the combat implementation. The baseline is checked in under Tests/Refactoring/CombatBaseline.json and should not be regenerated from the implementation under test.

## API changes

| Before | Now |
|---|---|
| GameLogic.InitializedGrid | GameLogic.InitializeGrid |
| GameLogic.GetTime | Removed; existing time value remains available |
| GameLogic's reaction internals | ReactionResolver.Resolve with phase callback |
| GameLogic.Change | Private resolver implementation detail |
| Indexing.Reaction / Requirement / Offset | Assets.Scripts.Reaction / Requirement / Offset |
| Indexing.CastingInfo | Assets/Data/Elements/Reactions.txt |
| Indexing color/damage/mana/fade dictionaries | ElementDefinitions plus shared cardinal geometry |
| Indexing blink/cast settings | PlayerHandler settings |
| Mutable Tile.type | Board-backed read-only property |
| Tile spacing/offset fields, GoToPosition | Initialization inputs and grid coordinate adapter |
| GameLogic.tilesSet | Iterate tilesGrid, filtering missing cells |
| GridHelper bounds/elevation static helpers | GridMath |
| PlayerHandler.CanReach | GridMath.CanReach |
| GridHelper.TestForWalls | GridMath.HasLineOfSight |
| GridHelper.GetTileOn | Removed; use TryGetTileOn |
| TextureHandler.spriteMap | SpriteCatalog asset |
| TextureHandler.UpdateTexture | Batched GameLogic.UpdateTiles or immediate UpdateTile |
| ParticleVFX.Burst | ReactionVisual |
| MobHandler.getBestPath | MobHandler.GetBestPath |
| Nullable NextStep class | Immutable NextStep struct; use IsValid |
| Selector.currentType and singleton | GameLogic selection state and SelectionChanged |

Diagnostic scripts were updated to exercise public resolver behavior and phase callbacks instead of invoking removed GameLogic internals. Sprite/rendering tests follow configured opacity and current selector wiring rather than requiring retired click-collider setup.

## Removed redundancy

- Three copies of grid stepping became one Line traversal with separate policies.
- Four fade-pattern maps became shared cardinal geometry.
- Full-board presentation no longer repeatedly applies every neighbor sprite.
- Sprite tint has one owner.
- Tile objects no longer store a second authoritative type.
- Queue reservation accounting moved into SpellQueue.
- Queue inspection no longer drains/copies the inspected path.
- Pathfinding buffers and visited state are reused.
- Occupancy uses a cached cell set with invalidation.
- Runtime state-reset sequencing moved into InitializeGrid.
- Repeated palette and rendering-test infrastructure moved into shared helpers.
- Rectangular fallback and conversion tools share LegacyLevelDefaults enemy coordinates.
- Unused imports, wrappers, parameters, and redundant ref modifiers were removed.
- Unused shader grass-sway support and its unreferenced assets were retired.
- The inactive Water_Water_Diagonal_Splash catalog entry/prefab was removed.

## Deliberate limits

This refactor does not change spell balance, phase order, path-channel randomization cadence, camera composition, or the existing half-step elevation rule.

Stage definitions for dormant water states remain because they are useful compatibility data; the refactor does not automatically remove every unreachable state.

Board arrays remain exposed for existing diagnostics. Production mutations should use BoardState.Set/Replace, followed by the coordinator's refresh path. Full-board scans still detect changed tile stages; event-only dirty tracking can follow a stricter board API later.

Existing asset names and script namespaces were retained where renaming would provide little value and create additional serialization migration work. The organizational change is primarily folder ownership and extracted responsibilities.

No attempt was made to deduplicate vendor readmes or distinct Unity tile assets. Those files are not duplicated game logic.
