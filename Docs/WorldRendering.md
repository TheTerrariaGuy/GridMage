# World rendering and Y sorting

`Main Camera` renders tiles, walls, characters, particles and the HUD directly at display resolution. Only particles use screen-space pixelation. The old low-resolution camera, render texture, output canvas and `PixelWorldRenderer` have been removed.

`ParticlePixelation` on `Main Camera` controls **Pixel Size**, measured in game pixels (component default **6**, current scene **4**). **Pixels Per Unit** defaults to **16**, matching the 16-pixel artwork fitted to one-unit tiles. A cell's world size is `Pixel Size / Pixels Per Unit`; its displayed size follows the rendering camera's orthographic zoom and target resolution, including Scene View previews. Fractional screen sizes are preserved, with a one-screen-pixel minimum. Set Pixel Size to 1 for one artwork pixel per cell, or disable the component for native-resolution particles. The grid stays screen-aligned while objects rotate or move. Sprites and the HUD retain their normal shaders and resolution. `GridPointer` raycasts through this same camera for tile hover and placement; its `Physics2DRaycaster` handles HUD sprites.

The effects use solid, planar XY meshes. Their shader expands each triangle's raster coverage to whole screen cells, tests the original triangle at each cell center, and interpolates color/alpha at that point. A half-open edge rule prevents double blending on internal mesh edges. This pixelates silhouettes as well as interiors, without a separate particle pass or render target. Unity retains control of sorting and blending. A particle smaller than one cell can disappear when it misses all cell centers, as with point-sampled pixelation.

This implementation uses a geometry shader and targets the project's Windows Direct3D configuration. Geometry-shader support is required; Metal and WebGL require a different mesh-expansion implementation. See [Unity shader target requirements](https://docs.unity3d.com/Manual/SL-ShaderCompileTargets.html). The current URP asset uses render scale 1 and no MSAA. Cell size uses the shader's render-target dimensions and projection, so it follows target resolution automatically. Projection and depth handling assume the existing orthographic camera and planar XY effects.

[Validated scene with the HUD](Rendering/WorldWithHUD.png)

## Sorting rules

The URP `Renderer2D` asset uses **Custom Axis**, `(0, 1, 0)`. Lower world Y appears in front. All World sorting groups use Order in Layer zero so Unity compares their anchor positions, without quantizing world coordinates into integer sorting orders.

Sorting layers, back to front:

| Layer | Contents |
| --- | --- |
| Ground | Tile bases and flat surfaces |
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
- `ParticlePixelation`: shared game-pixel cell size for the particle shader.
- `GridPointer`: tile picking and pointer interaction.
- `TextureHandler`: artwork placement and ground/wall classification.

`Tools > Grid Mage > Rendering > Configure Y-sorted world` configures the saved sample scene, mob prefab, tile Z offsets and renderer settings. The checked-in assets are already configured.

`ParticlePixelationChecks.Run` checks GPU output for full cells at game-pixel sizes 1, 4, 6 and 9, two rotations and zoom levels, and three resolutions (including a doubled resolution and fractional cell sizes). Its 48 grid/alpha cases also check alpha seams; additional checks cover disabled pixelation, native sprite edges and both sprite/particle sorting orders. Captures and results go to `Temp/ParticlePixelationChecks`.

[Rotated particle on the 6-pixel grid](Rendering/Particle6px.png)

For CLI validation, use an isolated copy of the project:

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Unity.exe' -batchmode -projectPath '<isolated-project>' -executeMethod WorldRenderingChecks.Run -logFile '<log-path>'
```

The checks open `Assets/Scenes/SampleScene.unity`; use `WorldRenderingSetup.ConfigureAndCheck` to configure it first as well. Omit `-quit`: the checks enter Play Mode and exit batch Unity when finished (an interactive editor returns to Edit Mode). They cover both rendered particle/sprite occlusion orders, catalog anchors at all four rotations, wall transitions, effect continuity and reuse, camera zoom/resize, tile picking and grid reset. Images and results are saved in `Temp/WorldRenderingChecks`.
