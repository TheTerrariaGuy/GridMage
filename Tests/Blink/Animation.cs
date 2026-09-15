// Evaluate in Play mode. Completion is reported in Temp/BlinkAnimationResult.txt.
if (!Application.isPlaying) throw new System.Exception("Enter Play mode first.");
System.Collections.IEnumerator CheckBlinkAnimation()
{
    var game = Assets.Scripts.GameLogic.INSTANCE;
    var player = Assets.Scripts.PlayerHandler.INSTANCE;
    var pointer = UnityEngine.Object.FindAnyObjectByType<GridPointer>();
    var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
    var speed = typeof(Assets.Scripts.PlayerHandler).GetField("blinkSpeed", flags);
    float oldSpeed = (float)speed.GetValue(player), oldCooldown = Assets.Scripts.PlayerHandler.INSTANCE.blinkCooldown;
    float oldCapture = Time.captureDeltaTime, oldTimeScale = Time.timeScale;
    bool oldGameEnabled = game.enabled, oldPointerEnabled = pointer.enabled;
    bool oldBackground = Application.runInBackground;
    var gridParent = game.gridParent;
    Vector3 oldGridPosition = gridParent.position, oldGridScale = gridParent.localScale;
    var mobs = UnityEngine.Object.FindObjectsByType<MobScript>(FindObjectsSortMode.None);
    var activeMobs = mobs.Where(m => m.enabled).ToArray();
    bool passed = false;
    System.Action<bool, string> require = (condition, message) =>
    {
        if (!condition) throw new System.Exception(message);
    };
    try
    {
        require(Mathf.Approximately(oldSpeed, .1f), "Default animation duration must be 0.1 seconds.");
        require(player.transform.parent == gridParent, "Player must share the tiles' grid parent.");
        Time.captureDeltaTime = .02f;
        Application.runInBackground = true;
        Time.timeScale = 1f;
        game.enabled = false;
        pointer.enabled = false;
        foreach (var mob in activeMobs) mob.enabled = false;
        // Longer test duration provides enough frames to measure the easing curve.
        speed.SetValue(player, .4f);
        Assets.Scripts.PlayerHandler.INSTANCE.blinkCooldown = .9f;
        player.r = 5;
        player.c = 5;
        player.transform.position = game.tilesGrid[5, 5].transform.position;
        game.grid[5, 6] = game.grid[5, 7] = game.grid[5, 8] = 0;
        game.MakeCastable();
        CastableOutlineChecks.CheckPlayerAlignment();
        var outlines = player.GetComponentsInChildren<CastableOutline>();
        require(outlines.Length == 2, "Both range meshes must follow Player.");
        var relativeVertices = outlines.Select(outline => gridParent.InverseTransformPoint(
            outline.transform.TransformPoint(outline.GetComponent<MeshFilter>().sharedMesh.vertices[0])) -
            player.transform.localPosition).ToArray();
        game.currMana = 30f;
        var target = game.tilesGrid[5, 7];
        var overlay = (SpriteRenderer)new UnityEditor.SerializedObject(pointer).FindProperty("hoverOverlay").objectReferenceValue;
        var border = (Sprite)new UnityEditor.SerializedObject(pointer).FindProperty("borderSprite").objectReferenceValue;
        var teleport = (Sprite)new UnityEditor.SerializedObject(pointer).FindProperty("teleportSprite").objectReferenceValue;
        pointer.SetHovered(game.tilesGrid[5, 9], true);
        require(overlay.sprite == border, "Shift outside range must show the normal border.");
        pointer.SetHovered(target, true);
        require(overlay.sprite == teleport, "Shift in range must show the teleport marker.");
        Vector3 origin = player.transform.localPosition, destination = target.transform.localPosition;
        float started = Time.time, previousProgress = 0f;
        require(game.TryCastBlink(target), "Blink must begin when ready.");
        require(player.transform.localPosition == origin && player.c == 5, "Animation must begin at the origin.");
        require(!game.TryCastBlink(game.tilesGrid[5, 6]) && game.currMana == 26f,
            "Recasts during animation must not spend mana.");
        int samples = 0;
        bool movedGrid = false;
        while (Time.time - started < .4f)
        {
            yield return null;
            float elapsed = Time.time - started;
            if (!movedGrid && elapsed >= .1f)
            {
                gridParent.position += new Vector3(3f, -2f, 0f);
                gridParent.localScale *= 1.5f;
                movedGrid = true;
            }
            float progress = Vector3.Dot(player.transform.localPosition - origin, destination - origin) /
                (destination - origin).sqrMagnitude;
            float expected = .5f - .5f * Mathf.Cos(Mathf.PI * Mathf.Clamp01(elapsed / .4f));
            float previous = .5f - .5f * Mathf.Cos(Mathf.PI * Mathf.Clamp01((elapsed - Time.deltaTime) / .4f));
            require(progress >= previousProgress - .001f && progress <= 1.001f, "Animation must advance without overshoot.");
            require(Mathf.Min(Mathf.Abs(progress - expected), Mathf.Abs(progress - previous)) < .015f,
                "Animation must follow sinusoidal easing.");
            if (progress > .02f && progress < .98f) samples++;
            if (player.c == 5)
            {
                // Combat and Inspector refreshes may rebuild ranges while the player is in flight.
                game.MakeCastable();
                for (int i = 0; i < outlines.Length; i++)
                {
                    outlines[i].enabled = false;
                    outlines[i].enabled = true;
                    Vector3 actual = outlines[i].transform.TransformPoint(outlines[i].GetComponent<MeshFilter>().sharedMesh.vertices[0]);
                    Vector3 expectedVertex = gridParent.TransformPoint(player.transform.localPosition + relativeVertices[i]);
                    require(Vector3.Distance(actual, expectedVertex) < .001f,
                        "Range meshes must follow every Blink sample, including rebuilds and re-enabling during flight.");
                }
            }
            previousProgress = progress;
        }
        yield return null;
        require(samples >= 5, "Animation needs intermediate positions, not an instant jump.");
        require(movedGrid, "The board must move and scale during the animation.");
        require(player.r == 5 && player.c == 7 && player.transform.localPosition == destination,
            "Animation must finish at the exact destination and update grid coordinates.");
        require(Vector3.Distance(player.transform.position, target.transform.position) < .001f,
            "Arrival must match the target in world space after the board moves and scales.");
        CastableOutlineChecks.CheckPlayerAlignment();
        require(!Assets.Scripts.MobHandler.INSTANCE.GetBestPath(5, 7).IsValid,
            "Enemy path field must target the destination on arrival.");
        require(!game.TryCastBlink(game.tilesGrid[5, 8]) && game.currMana == 26f,
            "Cooldown must remain active after the animation without charging mana.");
        while (Time.time - started < .94f) yield return null;
        require(game.TryCastBlink(game.tilesGrid[5, 8]), "Blink must unlock after cooldown.");
        gridParent.position += Vector3.right;
        player.enabled = false;
        require(player.transform.localPosition == destination && player.c == 7 &&
            Vector3.Distance(player.transform.position, target.transform.position) < .001f,
            "Disabling during Blink must restore the last occupied tile.");
        player.enabled = true;
        CastableOutlineChecks.CheckPlayerAlignment();
        passed = true;
    }
    finally
    {
        speed.SetValue(player, oldSpeed);
        Assets.Scripts.PlayerHandler.INSTANCE.blinkCooldown = oldCooldown;
        Time.captureDeltaTime = oldCapture;
        Time.timeScale = oldTimeScale;
        Application.runInBackground = oldBackground;
        gridParent.position = oldGridPosition;
        gridParent.localScale = oldGridScale;
        game.enabled = oldGameEnabled;
        pointer.enabled = oldPointerEnabled;
        foreach (var mob in activeMobs) if (mob != null) mob.enabled = true;
        System.IO.File.WriteAllText("Temp/BlinkAnimationResult.txt", passed ? "PASS: player-local range meshes during Blink/rebuild/re-enable, moving/scaling board, cooldown and cancellation" : "FAIL: see Unity console");
    }
}
System.IO.File.WriteAllText("Temp/BlinkAnimationResult.txt", "RUNNING");
Assets.Scripts.PlayerHandler.INSTANCE.StartCoroutine(CheckBlinkAnimation());
return "Animation checks started";
