#if UNITY_EDITOR
using System.Collections.Generic;
using Esnaf.Content;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Esnaf.App
{
    /// <summary>
    /// Esnaf > Setup Day 10: içerik JSON'larını ContentCatalog.asset'e bağlar ve Main sahnesini kurar (kamera + GameBootstrap).
    /// Tekrar çalıştırılabilir (aynı dosyaların üzerine yazar). Arayüz sahnede değil, çalışma anında kodla kurulur.
    /// </summary>
    internal static class Day10Setup
    {
        private const string DataFolder = "Assets/_Project/Content/Data";
        private const string CatalogPath = "Assets/_Project/Content/ContentCatalog.asset";
        private const string ScenePath = "Assets/_Project/Scenes/Main.unity";

        [MenuItem("Esnaf/Setup Day 10 (Catalog + Main Scene)")]
        private static void Setup()
        {
            ContentCatalog catalog = CreateOrUpdateCatalog();
            CreateMainScene(catalog);
            Debug.Log("Esnaf: ContentCatalog ve Main sahnesi hazır. Main sahnesini açıp Play'e basın.");
        }

        private static ContentCatalog CreateOrUpdateCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ContentCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ContentCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var files = new List<TextAsset>();
            foreach (string guid in AssetDatabase.FindAssets("t:TextAsset", new[] { DataFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".json"))
                {
                    files.Add(AssetDatabase.LoadAssetAtPath<TextAsset>(path));
                }
            }

            files.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            var serialized = new SerializedObject(catalog);
            SerializedProperty list = serialized.FindProperty("_contentFiles");
            list.arraySize = files.Count;
            for (int i = 0; i < files.Count; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = files[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        private static void CreateMainScene(ContentCatalog catalog)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.09f, 0.11f, 1f);
            camera.orthographic = true;

            var bootstrapObject = new GameObject("GameBootstrap");
            var bootstrap = bootstrapObject.AddComponent<GameBootstrap>();
            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("_catalog").objectReferenceValue = catalog;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);

            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (existing.path != ScenePath)
                {
                    scenes.Add(existing);
                }
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
#endif
