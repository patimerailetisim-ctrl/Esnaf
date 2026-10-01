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
    /// Assets/_Project/Art/Phones altındaki PNG'leri oyun içi UI için içe aktarır: Sprite (2D and UI), tek sprite, şeffaflık (alpha is transparency),
    /// mipmap yok, Clamp, bilinear, NPOT ölçekleme yok (en-boy oranı bozulmaz). Yeni dosya eklenince otomatik; mevcutlar için
    /// Esnaf > Apply Phone Sprite Import Settings. Ayrıca PhoneImageCatalog'u kurar (Esnaf > Setup Phone Images ve Setup Day 10).
    /// </summary>
    internal sealed class PhoneSpriteImport : AssetPostprocessor
    {
        public const string ArtFolder = "Assets/_Project/Art/Phones";
        public const string CatalogPath = ArtFolder + "/PhoneImageCatalog.asset";

        private void OnPreprocessTexture()
        {
            if (assetPath.StartsWith(ArtFolder + "/"))
            {
                Apply((TextureImporter)assetImporter);
            }
        }

        private static void Apply(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.sRGBTexture = true;
            importer.maxTextureSize = 2048;
            importer.spritePixelsPerUnit = 100f;
        }

        [MenuItem("Esnaf/Apply Phone Sprite Import Settings")]
        private static void ApplyToExisting()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    Apply(importer);
                    importer.SaveAndReimport();
                }
            }

            Debug.Log("Esnaf: telefon görselleri Sprite (2D and UI) olarak yeniden içe aktarıldı.");
        }

        [MenuItem("Esnaf/Setup Phone Images")]
        private static void SetupMenu()
        {
            CreateOrUpdateCatalog();
            Debug.Log("Esnaf: PhoneImageCatalog hazır. GameBootstrap'in 'Phone Images' alanına atanmış olmalı (Setup Day 10 bunu yapar).");
        }

        /// <summary>
        /// 10 modelin girdilerini Art/Phones altındaki PNG'leri TARAYARAK doldurur (dosya adı: "ModelAdı_Front/Back/Side/Camera/AllViews.png",
        /// PhoneAssetNaming). Eşleşme tablosu Console'a yazılır; eksik açı uyarı verir (ekran mock'a düşer, hata vermez).
        /// AllViews yalnızca referans alanına gider, hiçbir açının yerine geçmez.
        /// </summary>
        public static PhoneImageCatalog CreateOrUpdateCatalog()
        {
            ApplyToExisting();

            var catalog = AssetDatabase.LoadAssetAtPath<PhoneImageCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<PhoneImageCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            // modelAnahtarı -> tür -> Sprite yolu
            var found = new Dictionary<string, Dictionary<PhoneAssetKind, string>>();
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string folder = Path.GetFileName(Path.GetDirectoryName(path));
                string key;
                PhoneAssetKind kind = PhoneAssetNaming.Parse(Path.GetFileName(path), folder, out key);
                if (kind == PhoneAssetKind.Unknown)
                {
                    continue;
                }

                Dictionary<PhoneAssetKind, string> byKind;
                if (!found.TryGetValue(key, out byKind))
                {
                    byKind = new Dictionary<PhoneAssetKind, string>();
                    found.Add(key, byKind);
                }

                if (byKind.ContainsKey(kind))
                {
                    Debug.LogWarning("Esnaf: aynı model/tür için birden çok dosya var; ilki kullanıldı, yok sayılan: " + path);
                    continue;
                }

                byKind.Add(kind, path);
            }

            var serialized = new SerializedObject(catalog);
            SerializedProperty entries = serialized.FindProperty("_entries");
            IReadOnlyList<string> ids = PhoneModelIds.All;
            entries.arraySize = ids.Count;
            var report = new StringBuilder("Esnaf: telefon görseli eşleşmeleri (model + açı -> dosya)\n");
            int missing = 0;
            for (int i = 0; i < ids.Count; i++)
            {
                Dictionary<PhoneAssetKind, string> byKind;
                found.TryGetValue(PhoneAssetNaming.ModelKey(ids[i]), out byKind);

                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("_definitionId").stringValue = ids[i];
                missing += Assign(entry, "_front", ids[i], PhoneAssetKind.Front, byKind, report);
                missing += Assign(entry, "_back", ids[i], PhoneAssetKind.Back, byKind, report);
                missing += Assign(entry, "_side", ids[i], PhoneAssetKind.Side, byKind, report);
                missing += Assign(entry, "_camera", ids[i], PhoneAssetKind.Camera, byKind, report);
                Assign(entry, "_allViews", ids[i], PhoneAssetKind.AllViews, byKind, report);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log(report.ToString() + (missing == 0 ? "Tüm modeller x 4 açı dolu." : missing + " açı için Sprite bulunamadı (mock'a düşecek)."));
            return catalog;
        }

        /// <returns>Eksik açı için 1 (AllViews eksikse 0: zorunlu değildir).</returns>
        private static int Assign(SerializedProperty entry, string field, string id, PhoneAssetKind kind, Dictionary<PhoneAssetKind, string> byKind, StringBuilder report)
        {
            string path;
            Sprite sprite = null;
            if (byKind != null && byKind.TryGetValue(kind, out path))
            {
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                {
                    Debug.LogWarning("Esnaf: " + path + " Sprite olarak içe aktarılmamış (Esnaf > Apply Phone Sprite Import Settings).");
                }
                else
                {
                    report.Append("  ").Append(id).Append(" + ").Append(kind).Append(" -> ").Append(path).Append('\n');
                }
            }

            entry.FindPropertyRelative(field).objectReferenceValue = sprite;
            if (sprite == null && kind != PhoneAssetKind.AllViews)
            {
                report.Append("  ").Append(id).Append(" + ").Append(kind).Append(" -> EKSİK\n");
                return 1;
            }

            return 0;
        }
    }
}
#endif
