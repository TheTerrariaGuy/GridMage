Run range and line-of-sight checks with:

```powershell
unity command eval_file --file Tests/Blink/Program.cs --json
```

The checks run through Unity so they exercise the shared `GridMath` methods directly. For integration checks, run `ValidationFixture.Open()` in Edit Mode through MCP execute_code, then start a fresh Play session. The fixture supplies a flat rectangular board independent of production level edits. Run:

```powershell
unity command eval_file --file Tests/Blink/Castable.cs --json
unity command eval_file --file Tests/Blink/Integration.cs --json
unity command eval_file --file Tests/Blink/Animation.cs --json
# Wait for Temp/BlinkAnimationResult.txt to report PASS, then:
unity command editor_stop --json
```

The integration script tests cast/input behavior with animation duration and cooldown temporarily set to zero. The animation script then checks sinusoidal movement over multiple frames, cooldown rejection and expiry, cancellation, and the out-of-range border. It also verifies that both player-local range meshes follow Blink while the board moves/scales and the meshes rebuild or re-enable, and that their vertices align with board cells after arrival and cancellation. Both change runtime state; always stop Play mode afterward to discard it. The input script removes its synthetic devices and restores input settings even on failure.

Checks cover cached castability and wall occlusion, queued previews remaining usable after moving out of range, reserved mana, invalid destinations, blinking onto enemies (including the movement region and teleport marker), enemy retargeting, afterimage artwork, Shift-click versus held input, releasing Shift, and ordinary spell placement.
