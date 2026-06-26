using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Gerty
{
    /// <summary>
    /// Standard library of helper functions for AI-generated code.
    /// Provides simplified access to common Unity Editor operations.
    /// </summary>
    public static class AIHelper
    {
        #region Scene Operations

        /// <summary>
        /// Find a component of type T in the current scene by GameObject name.
        /// </summary>
        public static T FindInScene<T>(string name) where T : Component
        {
            var go = FindGameObject(name);
            return go != null ? go.GetComponent<T>() : null;
        }

        /// <summary>
        /// Find a GameObject in the current scene by name (supports partial match).
        /// </summary>
        public static GameObject FindGameObject(string name)
        {
            // First try exact match
            var go = GameObject.Find(name);
            if (go != null) return go;

            // Try case-insensitive search
            var allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);

            // Exact match (case-insensitive)
            go = allObjects.FirstOrDefault(o =>
                string.Equals(o.name, name, StringComparison.OrdinalIgnoreCase));
            if (go != null) return go;

            // Partial match (contains)
            go = allObjects.FirstOrDefault(o =>
                o.name.Contains(name, StringComparison.OrdinalIgnoreCase));

            return go;
        }

        /// <summary>
        /// Find all GameObjects in the scene matching a predicate.
        /// </summary>
        public static GameObject[] FindAllInScene(Func<GameObject, bool> predicate)
        {
            return Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
                .Where(predicate)
                .ToArray();
        }

        /// <summary>
        /// Find all GameObjects with a specific component type.
        /// </summary>
        public static GameObject[] FindAllWithComponent<T>() where T : Component
        {
            return Object.FindObjectsByType<T>(FindObjectsSortMode.None)
                .Select(c => c.gameObject)
                .ToArray();
        }

        /// <summary>
        /// Find all GameObjects whose name contains the search string.
        /// </summary>
        public static GameObject[] FindAllByName(string nameContains)
        {
            return Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
                .Where(go => go.name.Contains(nameContains, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        /// <summary>
        /// Find all GameObjects with a specific tag.
        /// </summary>
        public static GameObject[] FindAllByTag(string tag)
        {
            try
            {
                return GameObject.FindGameObjectsWithTag(tag);
            }
            catch
            {
                return Array.Empty<GameObject>();
            }
        }

        /// <summary>
        /// Find all GameObjects on a specific layer.
        /// </summary>
        public static GameObject[] FindAllByLayer(string layerName)
        {
            var layer = LayerMask.NameToLayer(layerName);
            if (layer < 0) return Array.Empty<GameObject>();

            return Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
                .Where(go => go.layer == layer)
                .ToArray();
        }

        /// <summary>
        /// Get the currently selected GameObjects in the Editor.
        /// </summary>
        public static GameObject[] GetSelectedObjects()
        {
            return Selection.gameObjects;
        }

        /// <summary>
        /// Set the Editor selection to the specified objects.
        /// </summary>
        public static void SetSelection(params GameObject[] objects)
        {
            Selection.objects = objects;
        }

        /// <summary>
        /// Set the Editor selection and frame (focus) on them.
        /// </summary>
        public static void SelectAndFrame(params GameObject[] objects)
        {
            Selection.objects = objects;
            if (objects.Length > 0 && SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }
        }

        /// <summary>
        /// Get all root GameObjects in the active scene.
        /// </summary>
        public static GameObject[] GetSceneRootObjects()
        {
            return SceneManager.GetActiveScene().GetRootGameObjects();
        }

        #endregion

        #region Asset Operations

        /// <summary>
        /// Load an asset by path.
        /// </summary>
        public static T LoadAsset<T>(string path) where T : Object
        {
            // Ensure path starts with Assets/
            if (!path.StartsWith("Assets/") && !path.StartsWith("Packages/"))
            {
                path = "Assets/" + path;
            }

            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        /// <summary>
        /// Find assets by name and type.
        /// </summary>
        public static T[] FindAssets<T>(string nameFilter = "") where T : Object
        {
            var typeName = typeof(T).Name;
            var filter = string.IsNullOrEmpty(nameFilter)
                ? $"t:{typeName}"
                : $"{nameFilter} t:{typeName}";

            var guids = AssetDatabase.FindAssets(filter);
            var assets = new List<T>();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                {
                    assets.Add(asset);
                }
            }

            return assets.ToArray();
        }

        /// <summary>
        /// Find assets in a specific folder.
        /// </summary>
        public static string[] FindAssetPaths(string filter, params string[] searchFolders)
        {
            var guids = AssetDatabase.FindAssets(filter, searchFolders);
            return guids.Select(AssetDatabase.GUIDToAssetPath).ToArray();
        }

        /// <summary>
        /// Get the path of an asset.
        /// </summary>
        public static string GetAssetPath(Object asset)
        {
            return AssetDatabase.GetAssetPath(asset);
        }

        #endregion

        #region Prefab Operations

        /// <summary>
        /// Instantiate a prefab by path at a position.
        /// </summary>
        public static GameObject InstantiatePrefab(string prefabPath, Vector3 position)
        {
            return InstantiatePrefab(prefabPath, position, Quaternion.identity);
        }

        /// <summary>
        /// Instantiate a prefab by path at a position with rotation.
        /// </summary>
        public static GameObject InstantiatePrefab(string prefabPath, Vector3 position, Quaternion rotation)
        {
            var prefab = LoadAsset<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"Could not find prefab at path: {prefabPath}");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = position;
            instance.transform.rotation = rotation;

            return instance;
        }

        /// <summary>
        /// Instantiate a prefab as a child of a parent transform.
        /// </summary>
        public static GameObject InstantiatePrefab(string prefabPath, Transform parent)
        {
            var prefab = LoadAsset<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"Could not find prefab at path: {prefabPath}");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            return instance;
        }

        /// <summary>
        /// Check if a GameObject is a prefab instance.
        /// </summary>
        public static bool IsPrefabInstance(GameObject go)
        {
            return PrefabUtility.IsPartOfPrefabInstance(go);
        }

        /// <summary>
        /// Get the prefab asset path for a prefab instance.
        /// </summary>
        public static string GetPrefabPath(GameObject prefabInstance)
        {
            var prefab = PrefabUtility.GetCorrespondingObjectFromSource(prefabInstance);
            return prefab != null ? AssetDatabase.GetAssetPath(prefab) : null;
        }

        #endregion

        #region Creation Helpers

        /// <summary>
        /// Create an empty GameObject.
        /// </summary>
        public static GameObject CreateEmpty(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }
            return go;
        }

        /// <summary>
        /// Create a primitive GameObject.
        /// </summary>
        public static GameObject CreatePrimitive(PrimitiveType type, string name, Vector3 position)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.position = position;
            return go;
        }

        /// <summary>
        /// Create a new material with a color.
        /// </summary>
        public static Material CreateMaterial(Color color, string name = null)
        {
            var shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("HDRP/Lit");

            var mat = new Material(shader);
            mat.color = color;
            if (!string.IsNullOrEmpty(name))
            {
                mat.name = name;
            }
            return mat;
        }

        #endregion

        #region Transform Utilities

        /// <summary>
        /// Get all children of a transform.
        /// </summary>
        public static Transform[] GetChildren(Transform parent)
        {
            var children = new Transform[parent.childCount];
            for (int i = 0; i < parent.childCount; i++)
            {
                children[i] = parent.GetChild(i);
            }
            return children;
        }

        /// <summary>
        /// Get all descendants of a transform (recursive).
        /// </summary>
        public static Transform[] GetDescendants(Transform parent)
        {
            var descendants = new List<Transform>();
            GetDescendantsRecursive(parent, descendants);
            return descendants.ToArray();
        }

        private static void GetDescendantsRecursive(Transform parent, List<Transform> list)
        {
            foreach (Transform child in parent)
            {
                list.Add(child);
                GetDescendantsRecursive(child, list);
            }
        }

        /// <summary>
        /// Set the world position of a transform, handling various scenarios.
        /// </summary>
        public static void SetWorldPosition(Transform t, Vector3 position)
        {
            t.position = position;
        }

        /// <summary>
        /// Set the local position of a transform.
        /// </summary>
        public static void SetLocalPosition(Transform t, Vector3 position)
        {
            t.localPosition = position;
        }

        #endregion

        #region Logging Utilities

        /// <summary>
        /// Log a message (wrapper for Debug.Log).
        /// </summary>
        public static void Log(string message)
        {
            Debug.Log(message);
        }

        /// <summary>
        /// Log an object as formatted JSON.
        /// </summary>
        public static void LogJson(object obj)
        {
            Debug.Log(JsonUtility.ToJson(obj, true));
        }

        /// <summary>
        /// Log a collection of items.
        /// </summary>
        public static void LogList<T>(IEnumerable<T> items, string header = null)
        {
            if (!string.IsNullOrEmpty(header))
            {
                Debug.Log(header);
            }

            var list = items.ToList();
            Debug.Log($"Count: {list.Count}");

            foreach (var item in list)
            {
                Debug.Log($"  - {item}");
            }
        }

        /// <summary>
        /// Log information about a GameObject.
        /// </summary>
        public static void LogGameObject(GameObject go)
        {
            if (go == null)
            {
                Debug.Log("GameObject is null");
                return;
            }

            var components = go.GetComponents<Component>();
            var componentNames = string.Join(", ", components.Select(c => c.GetType().Name));

            Debug.Log($"GameObject: {go.name}");
            Debug.Log($"  Path: {GetGameObjectPath(go)}");
            Debug.Log($"  Position: {go.transform.position}");
            Debug.Log($"  Active: {go.activeInHierarchy}");
            Debug.Log($"  Layer: {LayerMask.LayerToName(go.layer)}");
            Debug.Log($"  Tag: {go.tag}");
            Debug.Log($"  Components: {componentNames}");
        }

        /// <summary>
        /// Get the full hierarchy path of a GameObject.
        /// </summary>
        public static string GetGameObjectPath(GameObject go)
        {
            var path = go.name;
            var parent = go.transform.parent;

            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return "/" + path;
        }

        #endregion

        #region Scene Management

        /// <summary>
        /// Mark the current scene as dirty (needs saving).
        /// </summary>
        public static void MarkSceneDirty()
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        /// <summary>
        /// Save all open scenes.
        /// </summary>
        public static void SaveAllScenes()
        {
            EditorSceneManager.SaveOpenScenes();
        }

        /// <summary>
        /// Get the name of the active scene.
        /// </summary>
        public static string GetActiveSceneName()
        {
            return SceneManager.GetActiveScene().name;
        }

        /// <summary>
        /// Get the path of the active scene.
        /// </summary>
        public static string GetActiveScenePath()
        {
            return SceneManager.GetActiveScene().path;
        }

        #endregion

        #region Editor Utilities

        /// <summary>
        /// Refresh the AssetDatabase.
        /// </summary>
        public static void RefreshAssets()
        {
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Focus the Scene View on a position.
        /// </summary>
        public static void FocusSceneView(Vector3 position, float size = 10f)
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                sceneView.LookAt(position, sceneView.rotation, size);
            }
        }

        /// <summary>
        /// Repaint all Scene Views.
        /// </summary>
        public static void RepaintSceneViews()
        {
            SceneView.RepaintAll();
        }

        #endregion
    }
}
