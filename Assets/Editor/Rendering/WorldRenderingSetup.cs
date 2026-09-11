using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using ComponentUtility = UnityEditorInternal.ComponentUtility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class WorldRenderingSetup
{
    public static void ConfigureHoverOverlay(GridPointer pointer, Transform gridParent)
    {
        var hover = gridParent.Find("Hover");
        if (hover == null)
        {
            hover = new GameObject("Hover", typeof(SpriteRenderer)).transform;
            hover.SetParent(gridParent, false);
        }
        var overlay = hover.GetComponent<SpriteRenderer>();
        var tile = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tile.prefab").GetComponent<Tile>();
        overlay.sharedMaterial = tile.SurfaceRenderer.sharedMaterial;
        overlay.sortingLayerName = WorldSorting.Foreground;
        overlay.sortingOrder = 1;
        overlay.enabled = false;
        var data = new SerializedObject(pointer);
        overlay.sprite = (Sprite)data.FindProperty("borderSprite").objectReferenceValue;
        overlay.color = new Color(1f, 1f, 1f, data.FindProperty("hoverAlpha").floatValue);
        data.FindProperty("hoverOverlay").objectReferenceValue = overlay;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    [MenuItem("Tools/Grid Mage/Rendering/Configure Y-sorted world")]
    public static void Configure()
    {
        ConfigureLayers();
        var renderer = new SerializedObject(AssetDatabase.LoadAssetAtPath<Renderer2DData>("Assets/Settings/Renderer2D.asset"));
        renderer.FindProperty("m_TransparencySortMode").intValue = (int)TransparencySortMode.CustomAxis;
        renderer.FindProperty("m_TransparencySortAxis").vector3Value = Vector3.up;
        renderer.ApplyModifiedPropertiesWithoutUndo();

        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        Camera view = Camera.main;
        view.cullingMask = LayerMask.GetMask("Default", "TransparentFX", "PixelVFX", "Lighting", "UI");
        view.targetTexture = null;
        view.eventMask = LayerMask.GetMask("UI");
        view.GetComponent<Physics2DRaycaster>().eventMask = LayerMask.GetMask("UI");
        view.transparencySortMode = TransparencySortMode.CustomAxis;
        view.transparencySortAxis = Vector3.up;
        if (view.GetComponent<ParticlePixelation>() == null) view.gameObject.AddComponent<ParticlePixelation>();
        var game = Object.FindAnyObjectByType<Assets.Scripts.GameLogic>();
        var pointer = game.GetComponent<GridPointer>() ?? game.gameObject.AddComponent<GridPointer>();
        var pointerData = new SerializedObject(pointer);
        pointerData.FindProperty("viewCamera").objectReferenceValue = view;
        pointerData.FindProperty("borderSprite").objectReferenceValue = AssetDatabase.LoadAllAssetsAtPath("Assets/Textures/__Player/Border.png").OfType<Sprite>().First();
        pointerData.FindProperty("teleportSprite").objectReferenceValue = AssetDatabase.LoadAllAssetsAtPath("Assets/Textures/__Player/TeleportTo.png").OfType<Sprite>().First();
        pointerData.ApplyModifiedPropertiesWithoutUndo();
        ConfigureHoverOverlay(pointer, game.gridParent);

        foreach (string name in new[] { "ManaBar", "Button", "Clock", "Mana Label" })
        {
            var root = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == name)?.gameObject;
            if (root == null) continue;
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = LayerMask.NameToLayer("UI");
            foreach (var sprite in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                sprite.sortingLayerName = WorldSorting.UI;
                sprite.sortingOrder = Mathf.RoundToInt(-sprite.transform.position.z * 100f);
            }
            foreach (var hudCanvas in root.GetComponentsInChildren<Canvas>(true))
            {
                hudCanvas.sortingLayerName = WorldSorting.UI;
                hudCanvas.sortingOrder = 500;
            }
        }
        ConfigureActor(Object.FindAnyObjectByType<Assets.Scripts.PlayerHandler>().gameObject);
        foreach (var light in Object.FindObjectsByType<Light2D>())
        {
            var lightData = new SerializedObject(light);
            var layers = lightData.FindProperty("m_ApplyToSortingLayers");
            layers.arraySize = SortingLayer.layers.Length;
            for (int i = 0; i < layers.arraySize; i++) layers.GetArrayElementAtIndex(i).intValue = SortingLayer.layers[i].id;
            lightData.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorSceneManager.SaveScene(scene);

        const string mobPath = "Assets/Prefabs/Mob.prefab";
        var mob = PrefabUtility.LoadPrefabContents(mobPath);
        ConfigureActor(mob);
        PrefabUtility.SaveAsPrefabAsset(mob, mobPath);
        PrefabUtility.UnloadPrefabContents(mob);

        const string tilePath = "Assets/Prefabs/Tile.prefab";
        var tile = PrefabUtility.LoadPrefabContents(tilePath);
        foreach (var t in tile.GetComponentsInChildren<Transform>(true))
            t.localPosition = new Vector3(t.localPosition.x, t.localPosition.y, 0f);
        PrefabUtility.SaveAsPrefabAsset(tile, tilePath);
        PrefabUtility.UnloadPrefabContents(tile);
        AssetDatabase.SaveAssets();
        Debug.Log("World rendering configured: native-resolution world, particle-only screen grid, Y anchors and view-space picking.");
    }

    private static void ConfigureActor(GameObject actor)
    {
        actor.transform.localPosition = new Vector3(actor.transform.localPosition.x, actor.transform.localPosition.y, 0f);
        var original = actor.GetComponent<SpriteRenderer>();
        if (original == null) return; // Already migrated.
        float feet = original.sprite.bounds.min.y;
        var group = WorldSorting.CreateGroup(actor.transform, new Vector3(0, feet, 0), "Feet Y anchor");
        var visual = new GameObject("Sprite");
        visual.layer = actor.layer;
        visual.transform.SetParent(actor.transform, false);
        ComponentUtility.CopyComponent(original);
        ComponentUtility.PasteComponentAsNew(visual);
        WorldSorting.Include(group, visual.GetComponent<SpriteRenderer>(), 0);
        Object.DestroyImmediate(original);
    }

    private static void ConfigureLayers()
    {
        var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tags.FindProperty("m_SortingLayers");
        string[] names = { WorldSorting.Ground, WorldSorting.World, WorldSorting.Foreground, WorldSorting.UI };
        for (int i = 0; i < names.Length; i++)
        {
            bool exists = false;
            for (int j = 0; j < layers.arraySize; j++)
                exists |= layers.GetArrayElementAtIndex(j).FindPropertyRelative("name").stringValue == names[i];
            if (exists) continue;
            int index = i == 0 ? 0 : layers.arraySize;
            layers.InsertArrayElementAtIndex(index);
            var layer = layers.GetArrayElementAtIndex(index);
            layer.FindPropertyRelative("name").stringValue = names[i];
            layer.FindPropertyRelative("uniqueID").intValue = 1800100 + i;
            layer.FindPropertyRelative("locked").boolValue = false;
        }
        tags.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void ConfigureAndCheck()
    {
        try
        {
            Configure();
            WorldRenderingChecks.Run();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }
}
