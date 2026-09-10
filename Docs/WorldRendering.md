# World rendering and Y sorting

Tiles, walls, characters and particles render together through `PixelWorldCamera` into a point-filtered texture. `PixelWorldRenderer` on `Main Camera` owns this runtime texture and synchronizes the world camera with the displayed view. The texture is 400 pixels high; its width follows the view aspect ratio. Camera movement and zoom use the same projection for rendering and input. Point filtering preserves the pixelated appearance; noninteger display scaling can produce uneven screen-pixel widths.

`Main Camera` renders the world image on a screen-space-camera canvas, followed by the HUD on the UI layer. It does not render world objects a second time. The world image does not receive pointer events. `GridPointer` raycasts through the full-resolution view for tile hover and placement, while the main camera's `Physics2DRaycaster` handles HUD sprites. The world camera has no input raycaster.

[Validated scene with the HUD](Rendering/WorldWithHUD.png)

## Sorting rules

The URP `Renderer2D` asset uses **Custom Axis**, `(0, 1, 0)`. Lower world Y appears in front. All World sorting groups use Order in Layer zero so Unity compares their anchor positions, without quantizing world coordinates into integer sorting orders.

Sorting layers, back to front:

| Layer | Contents |
| --- | --- |
| Ground | Tile bases and flat surfaces; the world-image canvas is behind these within this layer |
| Default | Internal renderer order inside a sorting group |
| World | Walls, characters and particle effects |
| Foreground | Tile hover and queued-spell indicators |
| UI | HUD sprites and text, rendered at display resolution |

Wall tops and fronts share a `SortingGroup` anchored at the tile's bottom edge. Changing a wall back to a flat tile moves its group to Ground. Artwork height does not change its sorting anchor. Characters have a `Feet Y anchor` child at the bottom of their sprite. Gameplay transforms remain at the tile center; they no longer use Z offsets to force draw order.

`ParticlePrefabAuthoring.Bake` creates anchors in the editor and saves them into particle prefabs. Playback uses the authored hierarchy directly. A tile effect's systems share the tile-center anchor. A reaction's destination systems share an anchor at that destination; each link sorts at its midpoint. The existing reaction links are already split into one-cell edges, including diagonal edges. Rotation and grid scale apply to these anchors along with the effect. Internal prefab sorting orders preserve the arrangement of flame bodies, cores and sparks.

A sorting group is one drawable unit relative to other groups: individual particles inside it do not interleave with characters. Author separate systems/parts for portions that require different ground anchors, and split new long links into individual grid edges. Effects at exactly the same anchor Y have no guaranteed relative group order; use a deliberate anchor offset if their overlap needs a specific result.

The two fire/water geyser reactions enable `ParticlePattern.keepEmittersUpright`. Their destination positions and sorting anchors rotate with the cast, while jets, droplets and steam retain their upward orientation. Orientation is reapplied after placement on every playback, including pooled reuse.

Enemy damage bursts follow their victim while remaining independently pooled. Their anchor sits just in front of the victim's feet in world Y, keeping the hit visible on the enemy while allowing foreground world objects to occlude it. The particles finish at the last known position after a lethal hit.

## Responsibilities and validation

- `ParticleVFX`: effect playback, stage changes, lifetime and pooling.
- `WorldSorting`: creation and membership of ground-contact sorting groups.
- `PixelWorldRenderer`: render target lifetime and camera/view synchronization.
- `GridPointer`: tile picking and pointer interaction.
- `TextureHandler`: artwork placement and ground/wall classification.

`Tools > Grid Mage > Rendering > Configure Y-sorted pixel world` migrates the saved sample scene, mob prefab, tile Z offsets and renderer settings. The checked-in assets are already configured.

For CLI validation, use an isolated copy of the project:

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Unity.exe' -batchmode -projectPath '<isolated-project>' -executeMethod WorldRenderingChecks.Run -logFile '<log-path>'
```

The checks open `Assets/Scenes/SampleScene.unity`; use `WorldRenderingSetup.ConfigureAndCheck` to migrate it first as well. Omit `-quit`: the checks enter Play Mode and exit Unity when finished. They cover both rendered particle/sprite occlusion orders, catalog anchors at all four rotations, wall transitions, effect continuity and reuse, camera zoom/resize, tile picking and grid reset. Images and results are saved in the copy's `WorldRenderingChecks` directory.
