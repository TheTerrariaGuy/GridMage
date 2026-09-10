using System;
using System.IO;
using System.Linq;
using Assets.Scripts;
using Assets.Scripts.ScriptableObjects;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class EnemyDamageChecks
{
    private static MobScript follower, victim;
    private static ParticleSystem followingHit, lethalHit, naturalHit;
    private static Vector3 deathPosition;
    private static float readyTime;
    public static bool Ready => Time.time >= readyTime;

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static ParticleSystem[] Hits() => ParticleVFX.INSTANCE.GetComponentsInChildren<ParticleSystem>()
        .Where(p => p.transform.parent == ParticleVFX.INSTANCE.transform && p.name.StartsWith("Damage_")).ToArray();

    public static void Begin()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<ParticleCatalog>("Assets/Rendering/Particles/ParticleCatalog.asset");
        Require(catalog.damage.Length == 4 && catalog.damage.Select(e => e.element).Distinct().Count() == 4,
            "Damage catalog needs one effect per element.");
        follower = Spawn(1000);
        int[] types = { 100, 200, 300, 410 };
        string[] names = { "Fire", "Water", "Electricity", "Lava" };
        for (int i = 0; i < types.Length; i++)
        {
            SetTile(9, 9, types[i]);
            follower.TakeDamage();
            var hits = Hits();
            Require(hits.Length == 1 && hits[0].name.StartsWith("Damage_" + names[i]), "Damage must select its source element.");
            var hit = hits[0];
            Require(hit.transform.position == follower.transform.position, "Damage must spawn on the enemy.");
            var feet = follower.GetComponentInChildren<SortingGroup>();
            var anchor = hit.GetComponentInChildren<SortingGroup>();
            Require(anchor.sortingLayerName == WorldSorting.World && anchor.transform.position.y < feet.transform.position.y &&
                feet.transform.position.y - anchor.transform.position.y < .01f, "Damage must sort immediately in front of the enemy at its feet.");
            hit.Simulate(.1f, true, true);
            Require(hit.GetComponentsInChildren<ParticleSystem>().Sum(p => p.particleCount) > 0, "Damage effects must emit visible particles.");
            hit.Simulate(2, true, true);
            Require(hit.GetComponentsInChildren<ParticleSystem>().All(p => !p.main.loop && p.particleCount == 0),
                hit.name + " must finish its particles without looping.");
            var emitters = hit.GetComponentsInChildren<ParticleSystem>().Where(p => p != hit).ToArray();
            var positions = emitters.Select(p => p.transform.position).ToArray();
            ParticleVFX.INSTANCE.ClearBursts();
            Vector3 feetPosition = feet.transform.localPosition;
            feet.transform.localPosition += new Vector3(0, -.2f, 0);
            follower.TakeDamage();
            Require(Hits().Single() == hit, "Completed damage effects must be reusable from the pool.");
            Require(Mathf.Abs(anchor.transform.position.y - feet.transform.position.y + .001f) < .0001f,
                "Reusing damage effects must reposition their feet anchor.");
            for (int j = 0; j < emitters.Length; j++)
                Require(Vector3.Distance(emitters[j].transform.position, positions[j]) < .0001f,
                    "Changing the victim's feet offset must not shift damage emitters.");
            feet.transform.localPosition = feetPosition;
            ParticleVFX.INSTANCE.ClearBursts();
        }
        SetTile(9, 9, 0);
        follower.TakeDamage();
        Require(Hits().Length == 0, "Grass must not produce enemy damage effects.");
        SetTile(9, 9, 400);
        follower.TakeDamage();
        Require(Hits().Length == 0, "Ordinary stone must not invent damage.");
        SetTile(9, 9, 100);
        SetTile(8, 9, 300);
        SetTile(9, 8, 301);
        follower.TakeDamage();
        Require(Hits().Length == 2 && Hits().Count(p => p.name.StartsWith("Damage_Electricity")) == 1,
            "Overlapping electrical damage must produce one electric hit while retaining the fire hit.");
        ParticleVFX.INSTANCE.ClearBursts();
        SetTile(8, 9, 0);
        SetTile(9, 8, 0);

        follower.TakeDamage();
        followingHit = Hits().Single();
        Hold(followingHit);
        follower.transform.position += new Vector3(.25f, .15f, 0);
        SetTile(9, 9, 410);
        victim = Spawn(1);
        deathPosition = victim.transform.position;
        victim.TakeDamage();
        lethalHit = Hits().Single(p => p.name.StartsWith("Damage_Lava"));
        Hold(lethalHit);
        SetTile(9, 9, 0);
        ParticleVFX.INSTANCE.PlayDamage(follower.transform, 2);
        naturalHit = Hits().Single(p => p.name.StartsWith("Damage_Water"));
        readyTime = Time.time + 2f;
    }

    public static void Finish(string output)
    {
        Require(!naturalHit.gameObject.activeInHierarchy, "Finished damage effects must automatically return to the pool.");
        Require(followingHit.transform.position == follower.transform.position, "Damage particles must follow a moving enemy.");
        Require(victim == null && lethalHit != null && lethalHit.gameObject.activeInHierarchy,
            "A lethal hit must survive destruction of the enemy.");
        Require(lethalHit.transform.position == deathPosition, "A lethal hit must remain at the enemy's final position.");
        ParticleVFX.INSTANCE.ClearBursts();
        Require(Hits().Length == 0, "Reset must clear damage effects.");
        Object.Destroy(follower.gameObject);
        Capture(output);
        Debug.Log("ENEMY DAMAGE CHECKS PASSED: all elements, deduplication, pooling, following, lethal hits and cleanup.");
    }

    private static void Hold(ParticleSystem root)
    {
        // Extend only these test instances so movement/death can be checked after several frames.
        foreach (var p in root.GetComponentsInChildren<ParticleSystem>())
        {
            if (p == root) continue;
            var main = p.main;
            main.startLifetime = 5;
        }
        root.Simulate(.05f, true, true);
        root.Play(true);
    }

    private static MobScript Spawn(float hp)
    {
        var data = ScriptableObject.CreateInstance<EnemyData>();
        data.maxHp = hp;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mob.prefab");
        var mob = Object.Instantiate(prefab, GameLogic.INSTANCE.gridParent).GetComponent<MobScript>();
        mob.Init(data, GameLogic.INSTANCE.tilesGrid[9, 9]);
        mob.enabled = false;
        Object.Destroy(data);
        return mob;
    }

    private static void SetTile(int row, int col, int type)
    {
        GameLogic.INSTANCE.grid[row, col] = type;
        GameLogic.INSTANCE.UpdateTile(row, col);
    }

    private static void Capture(string output)
    {
        var view = Camera.main;
        var pipeline = view.GetComponent<PixelWorldRenderer>();
        Vector3 position = view.transform.position;
        float size = view.orthographicSize, aspect = view.aspect;
        var previews = new GameObject("Damage previews");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mob.prefab");
        view.transform.position = new Vector3(100, 100, -10);
        view.orthographicSize = 1.1f;
        view.aspect = 4;
        for (int i = 0; i < 4; i++)
        {
            var enemy = Object.Instantiate(prefab, previews.transform);
            enemy.GetComponent<MobScript>().enabled = false;
            enemy.transform.position = new Vector3(97 + i * 2, 100, 0);
            ParticleVFX.INSTANCE.PlayDamage(enemy.transform, i + 1);
        }
        foreach (var hit in Hits()) hit.Simulate(.12f, true, true);
        pipeline.Synchronize();
        RenderPipeline.SubmitRenderRequest(pipeline.WorldCamera,
            new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = pipeline.Texture });
        var previous = RenderTexture.active;
        RenderTexture.active = pipeline.Texture;
        var image = new Texture2D(pipeline.Texture.width, pipeline.Texture.height, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, image.width, image.height), 0, 0);
        image.Apply();
        File.WriteAllBytes(Path.Combine(output, "EnemyDamage.png"), image.EncodeToPNG());
        RenderTexture.active = previous;
        Object.Destroy(image);
        ParticleVFX.INSTANCE.ClearBursts();
        Object.Destroy(previews);
        view.transform.position = position;
        view.orthographicSize = size;
        view.aspect = aspect;
        pipeline.Synchronize();
    }
}
