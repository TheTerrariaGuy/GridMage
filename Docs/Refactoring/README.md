# Assets refactoring guide

This directory documents the September 2026 Assets refactor. It describes the implemented architecture, the authoring workflow, preserved gameplay rules, migrations, and verification.

## Start here

- [Architecture and ownership](Architecture.md): runtime components, initialization, data flow, coordinates, and extension points.
- [Combat rules and invariants](Combat.md): phases, priority, placement reservations, movement, and visual ownership.
- [Authoring and asset organization](Authoring.md): adding stages/reactions/artwork/enemies/levels and using editor tools.
- [Validation](Validation.md): regression suites, fixtures, commands, outputs, and failure diagnosis.
- [Migration record](Migration.md): removed APIs/assets, asset moves, compatibility details, and deliberate limits.
- [Asset move manifest](AssetMoves.json): every moved asset's original and current path.
- [Verification results](Verification.md): results from this implementation session.

Existing art references remain in [ElementSpriteMap](../ElementSpriteMap.md), [StoneSpriteMap](../StoneSpriteMap.md), [WorldRendering](../WorldRendering.md), [TilemapLevels](../TilemapLevels.md), and [Particles](../Particles/README.md).

## Design goals

1. One authoritative board, with tile objects acting as views.
2. One grid traversal implementation with separate visibility and elevation policies.
3. Explicit combat phases that preserve the original ordering.
4. Shared element definitions and reusable presentation catalogs.
5. Sprite work deduplicated per batch, with enemy occupancy queried only for spawn attempts.
6. Editor tooling that validates prerequisites and tests an isolated fixture.
7. Asset organization that preserves Unity GUID references.

This is an incremental refactor. Scene adapters retain their existing MonoBehaviour types, namespaces, and GUIDs. They coordinate the extracted logic; no dependency-injection framework or generic singleton hierarchy was introduced.
