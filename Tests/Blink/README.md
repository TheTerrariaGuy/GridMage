Run range and line-of-sight checks with:

```powershell
unity command eval_file --file Tests/Blink/Program.cs --json
```

The checks run through Unity so they exercise the methods in `PlayerHandler` directly. With SampleScene open, compile the scripts and start a fresh Play session, then run:

```powershell
unity command eval_file --file Tests/Blink/Castable.cs --json
unity command eval_file --file Tests/Blink/Integration.cs --json
unity command eval_file --file Tests/Blink/Animation.cs --json
# Wait for Temp/BlinkAnimationResult.txt to report PASS, then:
unity command editor_stop --json
```

The integration script tests cast/input behavior with animation duration and cooldown temporarily set to zero. The animation script then checks sinusoidal movement over multiple frames, cooldown rejection and expiry, cancellation, and the out-of-range border. Both change runtime state; always stop Play mode afterward to discard it. The input script removes its synthetic devices and restores input settings even on failure.

Checks cover cached castability and wall occlusion, queued previews remaining usable after moving out of range, reserved mana, invalid destinations, enemy occupancy and retargeting, afterimage artwork, Shift-click versus held input, releasing Shift, and ordinary spell placement.
