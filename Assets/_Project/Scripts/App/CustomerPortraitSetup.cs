#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using Esnaf.App.Ui;
using Esnaf.Presentation;
using UnityEditor;
using UnityEngine;

namespace Esnaf.App
{
    /// <summary>
    /// Esnaf > Setup Customer Portraits: Assets/_Project/Art/Customers altındaki PNG'leri Sprite (2D and UI) olarak içe aktarır
    /// (PhoneSpriteImport ile aynı ayarlar: şeffaflık, mipmap yok, NPOT yok) ve CustomerPortraitCatalog.asset'i klasörü TARAYARAK kurar.
    /// GUID elle yazılmaz. Beklenen 28 portreden eksik olan Console'a yazılır. Tekrar çalıştırılabilir; Setup Day 10 da bunu çağırır.
    /// </summary>
    internal static class CustomerPortraitSetup
    {
        public const string Folder = "Assets/_Project/Art/Customers";
        public const string CatalogPath = Folder + "/CustomerPortraitCatalog.asset";

        [MenuItem("Esnaf/Setup Customer Portraits")]
        private static void Menu()
        {
            CreateOrUpdateCatalog();
        }

        [MenuItem("Esnaf/Apply Customer Portrait Import Settings")]
        private static void ApplyMenu()
        {
            ApplyToExisting();
        }

        internal static void ApplyToExisting()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder }))
            {
                var importer = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter;
                if (importer != null)
                {
                    PhoneSpriteImport.Apply(importer);
                    importer.SaveAndReimport();
                }
            }
        }

        public static CustomerPortraitCatalog CreateOrUpdateCatalog()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                Debug.LogWarning("Esnaf: " + Folder + " klasörü yok; müşteri ekranı silüet gösterecek.");
                return null;
            }

            ApplyToExisting();

            var catalog = AssetDatabase.LoadAssetAtPath<CustomerPortraitCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CustomerPortraitCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var sprites = new List<KeyValuePair<string, Sprite>>();
            var report = new StringBuilder("Esnaf: müşteri portreleri (dosya adı -> Sprite)\n");
            var seenKeys = new HashSet<string>();
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { Folder });
            var paths = new List<string>();
            foreach (string guid in guids)
            {
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }

            paths.Sort(System.StringComparer.Ordinal);
            foreach (string path in paths)
            {
                string name = Path.GetFileNameWithoutExtension(path);
                string key = CustomerPortraitNaming.Key(name);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                {
                    Debug.LogWarning("Esnaf: " + path + " Sprite olarak içe aktarılmamış (Esnaf > Apply Customer Portrait Import Settings).");
                    continue;
                }

                if (key.Length == 0 || !seenKeys.Add(key))
                {
                    Debug.LogWarning("Esnaf: " + path + " için anahtar boş ya da başka bir portreyle aynı; atlandı.");
                    continue;
                }

                sprites.Add(new KeyValuePair<string, Sprite>(name, sprite));
                report.Append("  ").Append(name).Append(" -> ").Append(path).Append('\n');
            }

            var serialized = new SerializedObject(catalog);
            SerializedProperty entries = serialized.FindProperty("_entries");
            entries.arraySize = sprites.Count;
            for (int i = 0; i < sprites.Count; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("_name").stringValue = sprites[i].Key;
                entry.FindPropertyRelative("_sprite").objectReferenceValue = sprites[i].Value;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            var missing = new List<string>();
            foreach (string expected in CustomerPortraitNaming.ExpectedNames)
            {
                if (!seenKeys.Contains(CustomerPortraitNaming.Key(expected)))
                {
                    missing.Add(expected);
                }
            }

            Debug.Log(report + (missing.Count == 0
                ? "Beklenen " + CustomerPortraitNaming.ExpectedNames.Count + " portrenin tamamı katalogda."
                : missing.Count + " portre eksik: " + string.Join(", ", missing)));
            return catalog;
        }
    }
}
#endif
