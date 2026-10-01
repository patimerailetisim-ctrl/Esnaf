using Esnaf.App.Ui;
using Esnaf.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Esnaf.App.Tests
{
    /// <summary>
    /// Unity-only (Test Runner > EditMode): kaydedilmiş PhoneImageCatalog.asset'te 10 modelin 4 açısı da dolu mu.
    /// Önce Esnaf > Setup Day 10 (veya Setup Phone Images) çalıştırılmış olmalıdır. .NET koşucusu bu klasörü derlemez.
    /// </summary>
    public class PhoneImageCatalogAssetTests
    {
        private const string CatalogPath = "Assets/_Project/Art/Phones/PhoneImageCatalog.asset";

        private static readonly PhoneAngle[] RealAngles = { PhoneAngle.Front, PhoneAngle.Back, PhoneAngle.Side, PhoneAngle.CameraClose };

        private static PhoneImageCatalog Load()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PhoneImageCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, CatalogPath + " yok: Esnaf > Setup Day 10 çalıştırın.");
            return catalog;
        }

        [Test]
        public void EveryModel_HasAllFourAngles_Filled()
        {
            PhoneImageCatalog catalog = Load();

            foreach (string id in PhoneModelIds.All)
            {
                foreach (PhoneAngle angle in RealAngles)
                {
                    Assert.IsNotNull(catalog.GetSprite(id, angle), id + " + " + angle + " Sprite'ı boş");
                }
            }
        }

        [Test]
        public void TheFourAngles_OfAModel_AreFourDifferentSprites_SoAllViewsIsNeverUsedInPlace()
        {
            PhoneImageCatalog catalog = Load();

            foreach (string id in PhoneModelIds.All)
            {
                var seen = new System.Collections.Generic.HashSet<Sprite>();
                foreach (PhoneAngle angle in RealAngles)
                {
                    Assert.IsTrue(seen.Add(catalog.GetSprite(id, angle)), id + " + " + angle + " başka bir açıyla aynı Sprite");
                }

                foreach (PhoneAngle angle in RealAngles)
                {
                    StringAssert.DoesNotContain("AllViews", catalog.GetSprite(id, angle).texture.name, id + " + " + angle);
                }
            }
        }

        [Test]
        public void TheSprites_AreImportedForUi_WithTransparencyAndNoMipmaps()
        {
            PhoneImageCatalog catalog = Load();

            foreach (string id in PhoneModelIds.All)
            {
                foreach (PhoneAngle angle in RealAngles)
                {
                    string path = AssetDatabase.GetAssetPath(catalog.GetSprite(id, angle));
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    Assert.AreEqual(TextureImporterType.Sprite, importer.textureType, path);
                    Assert.IsTrue(importer.alphaIsTransparency, path);
                    Assert.IsFalse(importer.mipmapEnabled, path);
                }
            }
        }

        [Test]
        public void AnUnknownModel_HasNoSprite_SoTheMockIsTheFallback()
        {
            Assert.IsNull(Load().GetSprite("phone.does_not_exist", PhoneAngle.Front));
        }
    }
}
