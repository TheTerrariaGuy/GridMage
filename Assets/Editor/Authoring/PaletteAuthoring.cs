using System;
using UnityEditor;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class PaletteAuthoring
{
    public static void Edit(string folder, string name, Action<Tilemap> paint, bool skipExisting = false)
    {
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Levels", "Palettes");
        string path = folder + "/" + name + ".prefab";
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
        if (exists && skipExisting) return;
        if (!exists)
            GridPaletteUtility.CreateNewPalette(folder, name, GridLayout.CellLayout.Rectangle,
                GridPalette.CellSizing.Manual, Vector3.one, GridLayout.CellSwizzle.XYZ);
        var root = PrefabUtility.LoadPrefabContents(path);
        try { paint(root.GetComponentInChildren<Tilemap>()); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
