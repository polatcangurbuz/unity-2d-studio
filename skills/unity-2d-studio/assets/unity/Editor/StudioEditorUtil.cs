using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Studio.EditorTools
{
    // Building blocks for idempotent scene/prefab builders when there is no MCP bridge. Keep builders thin: call these.
    public static class StudioEditorUtil
    {
        private const int FirstUserLayer = 8;

        // Sets [SerializeField] private fields by name; arrays take Object[]. Unknown field or type → LogError, others still applied.
        public static void Wire(Object target, params (string field, object value)[] values)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in values)
            {
                var p = so.FindProperty(field);
                if (p == null)
                {
                    Debug.LogError($"[Wire] {target.GetType().Name}.{field} not found (private fields need [SerializeField])", target);
                    continue;
                }

                if (!Assign(p, value)) Debug.LogError($"[Wire] {target.GetType().Name}.{field}: can't assign {value?.GetType().Name ?? "null"} to {p.propertyType}", target);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool Assign(SerializedProperty p, object value)
        {
            switch (value)
            {
                case null when p.propertyType == SerializedPropertyType.ObjectReference: p.objectReferenceValue = null; return true;
                case Object o when p.propertyType == SerializedPropertyType.ObjectReference: p.objectReferenceValue = o; return true;
                case Object[] items when p.isArray:
                    p.arraySize = items.Length;
                    for (var i = 0; i < items.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
                    return true;
                case System.Enum e when p.propertyType == SerializedPropertyType.Enum: p.intValue = System.Convert.ToInt32(e); return true;
                case int i when p.propertyType == SerializedPropertyType.Enum
                                || p.propertyType == SerializedPropertyType.LayerMask
                                || p.propertyType == SerializedPropertyType.Integer: p.intValue = i; return true;
                case int i when p.propertyType == SerializedPropertyType.Float: p.floatValue = i; return true;
                case float f when p.propertyType == SerializedPropertyType.Float: p.floatValue = f; return true;
                case bool b when p.propertyType == SerializedPropertyType.Boolean: p.boolValue = b; return true;
                case Vector2 v when p.propertyType == SerializedPropertyType.Vector2: p.vector2Value = v; return true;
                case Vector3 v when p.propertyType == SerializedPropertyType.Vector3: p.vector3Value = v; return true;
                case Color c when p.propertyType == SerializedPropertyType.Color: p.colorValue = c; return true;
                case string s when p.propertyType == SerializedPropertyType.String: p.stringValue = s; return true;
                default: return false;
            }
        }

        public static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        public static void EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // Returns the layer index; adds the name to the first empty user layer (8-31) if it is missing.
        public static int EnsureLayer(string name)
        {
            var existing = LayerMask.NameToLayer(name);
            if (existing >= 0) return existing;
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (var i = FirstUserLayer; i < layers.arraySize; i++)
            {
                var slot = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue)) continue;
                slot.stringValue = name;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                return i;
            }

            Debug.LogError($"[EnsureLayer] no free user layer for '{name}'");
            return 0;
        }

        // index -1 appends; an existing entry moves to index (or stays if index is -1).
        public static void AddToBuildSettings(string scenePath, int index = -1)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            var at = scenes.FindIndex(s => s.path == scenePath);
            if (at >= 0 && (index < 0 || at == index)) return;
            if (at >= 0) scenes.RemoveAt(at);
            var entry = new EditorBuildSettingsScene(scenePath, true);
            if (index < 0 || index > scenes.Count) scenes.Add(entry);
            else scenes.Insert(index, entry);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // Hierarchy separator in the active scene: EditorOnly, so it is stripped from builds.
        public static GameObject Separator(string name)
        {
            var found = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(go => go.name == name);
            return found != null ? found : new GameObject(name) { tag = "EditorOnly" };
        }

        // Root = physics/logic, child "Visual" = renderer, so squash/flip/flash never touch colliders.
        public static GameObject NewActor(string name, Sprite sprite, int sortingOrder, int layer, out SpriteRenderer visual)
        {
            var root = new GameObject(name) { layer = layer };
            var child = new GameObject("Visual") { layer = layer };
            child.transform.SetParent(root.transform, false);
            visual = child.AddComponent<SpriteRenderer>();
            visual.sprite = sprite;
            visual.sortingOrder = sortingOrder;
            return root;
        }

        // Saves the scene object as a prefab asset and destroys the scene copy; returns the asset.
        public static GameObject SavePrefab(GameObject go, string path)
        {
            EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        // Open/create the scene FIRST, then make assets: NewScene(Single) unloads unreferenced assets created before it.
        public static Scene OpenOrCreateScene(string path)
        {
            if (File.Exists(path)) return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, path);
            return scene;
        }
    }
}
