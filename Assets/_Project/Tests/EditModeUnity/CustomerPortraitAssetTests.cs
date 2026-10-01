using System.Collections.Generic;
using Esnaf.App.Ui;
using Esnaf.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Esnaf.App.Tests
{
    /// <summary>
    /// Unity-only (Test Runner > EditMode): kaydedilmiş CustomerPortraitCatalog.asset ve CustomerPortraitView.
    /// Önce Esnaf > Setup Customer Portraits çalıştırılmış olmalıdır.
    /// </summary>
    public class CustomerPortraitAssetTests
    {
        private const string CatalogPath = "Assets/_Project/Art/Customers/CustomerPortraitCatalog.asset";

        private static CustomerPortraitCatalog Load()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CustomerPortraitCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, CatalogPath + " yok: Esnaf > Setup Customer Portraits çalıştırın.");
            return catalog;
        }

        [Test]
        public void AllTwentyEightPortraits_AreInTheCatalog_WithFilledSprites()
        {
            CustomerPortraitCatalog catalog = Load();

            Assert.AreEqual(28, CustomerPortraitNaming.ExpectedNames.Count);
            foreach (string name in CustomerPortraitNaming.ExpectedNames)
            {
                Assert.IsNotNull(catalog.GetSprite(name), name + ": Sprite boş / katalogda yok");
            }
        }

        [Test]
        public void NoTwoPortraits_ShareTheSameSprite()
        {
            CustomerPortraitCatalog catalog = Load();
            var seen = new HashSet<Sprite>();

            foreach (string name in CustomerPortraitNaming.ExpectedNames)
            {
                Assert.IsTrue(seen.Add(catalog.GetSprite(name)), name + ": başka bir portreyle aynı Sprite");
            }
        }

        [Test]
        public void TheSprite_IsFoundFromTheCustomersDisplayName_AndTheTexturesAreNamedLikeIt()
        {
            CustomerPortraitCatalog catalog = Load();

            Assert.AreSame(catalog.GetSprite("Murat"), catalog.GetSprite("Dr. Murat"));
            Assert.AreEqual("Oğuz", catalog.GetSprite("oğuz").texture.name);
            Assert.AreEqual("İrem", catalog.GetSprite("IREM").texture.name);
            Assert.AreNotSame(catalog.GetSprite("Selin"), catalog.GetSprite("Berk"));
        }

        [Test]
        public void AnUnknownName_HasNoSprite()
        {
            CustomerPortraitCatalog catalog = Load();

            Assert.IsNull(catalog.GetSprite("Bu Isim Yok"));
            Assert.IsNull(catalog.GetSprite(null));
            Assert.IsNull(catalog.GetSprite(""));
        }

        private static RectTransform NewHost()
        {
            var go = new GameObject("Host", typeof(RectTransform));
            return go.GetComponent<RectTransform>();
        }

        [Test]
        public void ThePortraitView_ShowsTheRealSprite_ForAKnownName_AndNoPlaceholder()
        {
            CustomerPortraitCatalog catalog = Load();
            CustomerPortraits.Provider = catalog.GetSprite;
            RectTransform host = NewHost();
            try
            {
                CustomerPortraitView.Draw(host, "Selin", "npc.selin");

                Transform portrait = host.Find("Portrait");
                Assert.IsNotNull(portrait, "gerçek portre çizilmedi");
                var image = portrait.GetComponent<Image>();
                Assert.AreSame(catalog.GetSprite("Selin"), image.sprite);
                Assert.IsTrue(image.preserveAspect, "yüz ezilmemeli");
                Assert.IsNull(host.Find("Silhouette"), "gerçek portre varken silüet çizilmemeli");
            }
            finally
            {
                CustomerPortraits.Provider = null;
                Object.DestroyImmediate(host.gameObject);
            }
        }

        [Test]
        public void TheName_Decides_TheSprite_AndRedrawingSwapsIt()
        {
            CustomerPortraitCatalog catalog = Load();
            CustomerPortraits.Provider = catalog.GetSprite;
            RectTransform host = NewHost();
            try
            {
                CustomerPortraitView.Draw(host, "Selin", "npc.selin");
                Sprite first = host.Find("Portrait").GetComponent<Image>().sprite;

                CustomerPortraitView.Draw(host, "Berk", "npc.berk");
                Sprite second = host.Find("Portrait").GetComponent<Image>().sprite;

                Assert.AreSame(catalog.GetSprite("Selin"), first);
                Assert.AreSame(catalog.GetSprite("Berk"), second);
                Assert.AreNotSame(first, second);
            }
            finally
            {
                CustomerPortraits.Provider = null;
                Object.DestroyImmediate(host.gameObject);
            }
        }

        [Test]
        public void ThePortraitView_FallsBackToTheSilhouette_ForAnUnknownName()
        {
            CustomerPortraitCatalog catalog = Load();
            CustomerPortraits.Provider = catalog.GetSprite;
            RectTransform host = NewHost();
            try
            {
                CustomerPortraitView.Draw(host, "Bu Isim Yok", "npc.kemal");

                Assert.IsNull(host.Find("Portrait"));
                Assert.IsNotNull(host.Find("Silhouette"), "silüet fallback çizilmedi");
            }
            finally
            {
                CustomerPortraits.Provider = null;
                Object.DestroyImmediate(host.gameObject);
            }
        }

        [Test]
        public void WithoutAProvider_TheViewDrawsTheSilhouette_AndDoesNotThrow()
        {
            CustomerPortraits.Provider = null;
            RectTransform host = NewHost();
            try
            {
                Assert.DoesNotThrow(() => CustomerPortraitView.Draw(host, "Selin", "npc.selin"));
                Assert.IsNotNull(host.Find("Silhouette"));
            }
            finally
            {
                Object.DestroyImmediate(host.gameObject);
            }
        }
    }
}
