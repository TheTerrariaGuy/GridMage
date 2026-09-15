// Run in Edit Mode with saved scenes; restores the original active scene.
CheckSupport.Require(!Application.isPlaying, "Run asset validation in Edit Mode.");
for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
    CheckSupport.Require(!UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty, "Save scene edits first.");
var failures = new System.Collections.Generic.List<string>();
int objects = 0, refs = 0, prefabs = 0;
System.Action<UnityEngine.Object> check = obj => {
 if (obj == null) return;
 objects++;
 var serialized = new SerializedObject(obj);
 var property = serialized.GetIterator();
 while(property.Next(true)) {
  if(property.propertyType != SerializedPropertyType.ObjectReference) continue;
  refs++;
  if(property.objectReferenceValue == null && property.objectReferenceEntityIdValue != default)
   failures.Add(obj.name + ": " + property.propertyPath);
 }
};
System.Action<GameObject> hierarchy = root => {
 foreach(var t in root.GetComponentsInChildren<Transform>(true)) {
  if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0)
    failures.Add("Missing script: " + t.name);
  foreach(var component in t.GetComponents<Component>()) check(component);
 }
};
foreach(string path in AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/"))) {
 if(path.EndsWith(".prefab")) { hierarchy(AssetDatabase.LoadAssetAtPath<GameObject>(path)); prefabs++; }
 else if(path.EndsWith(".asset") && (path.StartsWith("Assets/Data/") || path.StartsWith("Assets/Levels/") || path.StartsWith("Assets/Rendering/")))
  foreach(var obj in AssetDatabase.LoadAllAssetsAtPath(path)) check(obj);
}
string original = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
try {
 foreach(var path in new[]{"Assets/Scenes/Bootstrap.unity","Assets/Scenes/In Game.unity"}) {
  var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path);
  foreach(var root in scene.GetRootGameObjects()) hierarchy(root);
 }
} finally { UnityEditor.SceneManagement.EditorSceneManager.OpenScene(original); }
CheckSupport.Require(failures.Count == 0, string.Join("\n", failures));
return $"PASS: {objects} objects, {refs} reference properties, {prefabs} prefabs, and both game scenes.";
