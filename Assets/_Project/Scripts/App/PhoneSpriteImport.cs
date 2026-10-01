#if UNITY_EDITOR
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
        public const string ElmaFolder = ArtFolder + "/ElmaE13Pro";
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

        /// <summary>Elma E13 Pro girdisini PNG'lerden doldurur; eksik dosya boş bırakılır (ekran mock'a düşer, hata vermez).</summary>
        public static PhoneImageCatalog CreateOrUpdateCatalog()
        {
            ApplyToExisting();

            var catalog = AssetDatabase.LoadAssetAtPath<PhoneImageCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<PhoneImageCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var serialized = new SerializedObject(catalog);
            SerializedProperty entries = serialized.FindProperty("_entries");
            entries.arraySize = 1;
            SerializedProperty entry = entries.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("_definitionId").stringValue = PhoneModelIds.ElmaE13Pro;
            entry.FindPropertyRelative("_front").objectReferenceValue = Load("ElmaE13Pro_Front.png");
            entry.FindPropertyRelative("_back").objectReferenceValue = Load("ElmaE13Pro_Back.png");
            entry.FindPropertyRelative("_side").objectReferenceValue = Load("ElmaE13Pro_Side.png");
            entry.FindPropertyRelative("_camera").objectReferenceValue = Load("ElmaE13Pro_Camera.png");
            entry.FindPropertyRelative("_allViews").objectReferenceValue = Load("ElmaE13Pro_AllViews.png");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        private static Sprite Load(string fileName)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ElmaFolder + "/" + fileName);
            if (sprite == null)
            {
                Debug.LogWarning("Esnaf: " + ElmaFolder + "/" + fileName + " bulunamadı veya Sprite değil; bu açı için mock çizilecek.");
            }

            return sprite;
        }
    }
}
#endif
