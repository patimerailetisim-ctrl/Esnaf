using System.Collections.Generic;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Content;
using Esnaf.Domain.Products;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Content
{
    /// <summary>
    /// Depodaki GERÇEK içerik dosyalarını (Assets/_Project/Content/Data) yükler ve GDD v0.2 Bölüm 3.2 tablosuyla karşılaştırır.
    /// </summary>
    public class PhoneContentFileTests
    {
        // id, ad, marka, segment, çıkış yılı, baz fiyat, baz hafıza, yaş min, yaş max
        private static readonly object[][] ExpectedModels =
        {
            new object[] { "phone.nova_n1_lite", "Nova N1 Lite", "Nova", ProductSegment.Entry, 2023, 4500L, 64, 6, 36 },
            new object[] { "phone.yildiz_y5", "Yıldız Y5", "Yıldız", ProductSegment.Entry, 2022, 6000L, 64, 12, 48 },
            new object[] { "phone.samsun_vega_a3", "Samsun Vega A3", "Samsun", ProductSegment.Mid, 2023, 9500L, 128, 6, 36 },
            new object[] { "phone.nova_n3_pro", "Nova N3 Pro", "Nova", ProductSegment.Mid, 2023, 12500L, 128, 6, 36 },
            new object[] { "phone.zirve_z5", "Zirve Z5", "Zirve", ProductSegment.Mid, 2022, 14000L, 128, 12, 48 },
            new object[] { "phone.yildiz_y8_plus", "Yıldız Y8 Plus", "Yıldız", ProductSegment.Mid, 2023, 17500L, 128, 6, 36 },
            new object[] { "phone.elma_e11", "Elma E11", "Elma", ProductSegment.Mid, 2021, 19000L, 64, 24, 60 },
            new object[] { "phone.samsun_vega_s21", "Samsun Vega S21", "Samsun", ProductSegment.Upper, 2022, 24000L, 128, 12, 48 },
            new object[] { "phone.elma_e13_pro", "Elma E13 Pro", "Elma", ProductSegment.Upper, 2023, 32000L, 128, 6, 36 },
            new object[] { "phone.elma_e14_pro_max", "Elma E14 Pro Max", "Elma", ProductSegment.Upper, 2024, 46000L, 256, 0, 18 }
        };

        private static ContentDatabase LoadReal()
        {
            var source = new DirectoryContentSource(TestPaths.ContentDataDirectory());
            ContentLoadResult result = ContentDatabase.Load(source);
            Assert.IsTrue(result.IsSuccess, "Gerçek içerik yüklenemedi:\n" + result.FormatIssues());
            Assert.AreEqual(0, result.WarningCount, result.FormatIssues());
            return result.Database;
        }

        [Test]
        public void RealContent_LoadsWithoutErrorsOrWarnings()
        {
            ContentDatabase db = LoadReal();

            Assert.AreEqual(10, db.Products.Count);
        }

        [Test]
        public void RealContent_MatchesGddV02ModelTable()
        {
            ContentDatabase db = LoadReal();

            Assert.AreEqual(ExpectedModels.Length, db.Products.Count);
            for (int i = 0; i < ExpectedModels.Length; i++)
            {
                object[] e = ExpectedModels[i];
                ProductDefinition p = db.Products[i];

                Assert.AreEqual((string)e[0], p.Id, "id #" + i);
                Assert.AreEqual("phone", p.Sector, p.Id);
                Assert.AreEqual((string)e[1], p.Name, p.Id);
                Assert.AreEqual((string)e[2], p.Brand, p.Id);
                Assert.AreEqual((ProductSegment)e[3], p.Segment, p.Id);
                Assert.AreEqual((int)e[4], p.ReleaseYear, p.Id);
                Assert.AreEqual(Money.FromTl((long)e[5]), p.BasePrice, p.Id);
                Assert.AreEqual((int)e[6], p.BaseStorageGb, p.Id);
                Assert.AreEqual((int)e[7], p.MinAgeMonths, p.Id);
                Assert.AreEqual((int)e[8], p.MaxAgeMonths, p.Id);
                Assert.IsFalse(p.IsDeprecated, p.Id);
            }
        }

        [Test]
        public void RealContent_E13Pro_StorageMultipliers()
        {
            ProductDefinition e13 = LoadReal().GetProduct("phone.elma_e13_pro");
            double multiplier;

            Assert.IsTrue(e13.TryGetStorageMultiplier(128, out multiplier));
            Assert.AreEqual(1.00, multiplier, 1e-12);
            Assert.IsTrue(e13.TryGetStorageMultiplier(256, out multiplier));
            Assert.AreEqual(1.12, multiplier, 1e-12);
            Assert.IsTrue(e13.TryGetStorageMultiplier(512, out multiplier));
            Assert.AreEqual(1.28, multiplier, 1e-12);
            Assert.IsFalse(e13.TryGetStorageMultiplier(64, out multiplier));
        }

        [Test]
        public void RealContent_AllStorageMultipliersMatchGddV02()
        {
            ContentDatabase db = LoadReal();
            var expected = new Dictionary<string, double[]>
            {
                { "phone.nova_n1_lite", new[] { 64, 1.00, 128, 1.15 } },
                { "phone.yildiz_y5", new[] { 64, 1.00, 128, 1.12 } },
                { "phone.samsun_vega_a3", new[] { 64, 0.90, 128, 1.00, 256, 1.15 } },
                { "phone.nova_n3_pro", new[] { 64, 0.90, 128, 1.00, 256, 1.15 } },
                { "phone.zirve_z5", new[] { 128, 1.00, 256, 1.15 } },
                { "phone.yildiz_y8_plus", new[] { 128, 1.00, 256, 1.15 } },
                { "phone.elma_e11", new[] { 64, 1.00, 128, 1.10, 256, 1.22 } },
                { "phone.samsun_vega_s21", new[] { 128, 1.00, 256, 1.12 } },
                { "phone.elma_e13_pro", new[] { 128, 1.00, 256, 1.12, 512, 1.28 } },
                { "phone.elma_e14_pro_max", new[] { 256, 1.00, 512, 1.15, 1024, 1.30 } }
            };

            foreach (KeyValuePair<string, double[]> pair in expected)
            {
                ProductDefinition p = db.GetProduct(pair.Key);
                Assert.AreEqual(pair.Value.Length / 2, p.StorageOptions.Count, pair.Key);
                for (int i = 0; i < p.StorageOptions.Count; i++)
                {
                    Assert.AreEqual((int)pair.Value[i * 2], p.StorageOptions[i].Gb, pair.Key);
                    Assert.AreEqual(pair.Value[i * 2 + 1], p.StorageOptions[i].Multiplier, 1e-12, pair.Key);
                }
            }
        }

        [Test]
        public void RealContent_SegmentsFollowGddV02PriceBands()
        {
            // v0.2: Giriş < 8.000, Orta 8.000–20.000, Üst > 20.000. (Yalnızca içerik tutarlılık testi; validator bunu dayatmaz.)
            foreach (ProductDefinition p in LoadReal().Products)
            {
                ProductSegment expected = p.BasePrice.Tl < 8000
                    ? ProductSegment.Entry
                    : (p.BasePrice.Tl <= 20000 ? ProductSegment.Mid : ProductSegment.Upper);
                Assert.AreEqual(expected, p.Segment, p.Id);
            }
        }

        [Test]
        public void RealContent_PricesAreRoundedAndIdsAreUnique()
        {
            ContentDatabase db = LoadReal();

            Assert.IsTrue(db.Products.All(p => p.BasePrice.IsRoundedTo10));
            Assert.AreEqual(db.Products.Count, db.Products.Select(p => p.Id).Distinct().Count());
        }

        [Test]
        public void RealContent_TurkishCharactersSurviveTheFile()
        {
            ContentDatabase db = LoadReal();

            Assert.AreEqual("Yıldız Y5", db.GetProduct("phone.yildiz_y5").Name);
            Assert.AreEqual("Yıldız", db.GetProduct("phone.yildiz_y8_plus").Brand);
        }

        [Test]
        public void RealContent_DefaultsIconKeyToId()
        {
            foreach (ProductDefinition p in LoadReal().Products)
            {
                Assert.AreEqual(p.Id, p.IconKey);
            }
        }

        [Test]
        public void DefinitionAndInstance_AreLinkedOnlyById()
        {
            ContentDatabase db = LoadReal();
            var instance = new ProductInstance { InstanceId = 1042, DefinitionId = "phone.elma_e13_pro", StorageGb = 128, AgeMonths = 18 };

            ProductDefinition definition = db.GetProduct(instance.DefinitionId);

            Assert.AreEqual("Elma E13 Pro", definition.Name);
            Assert.AreEqual(Money.FromTl(32000), definition.BasePrice);
            Assert.IsTrue(definition.TryGetStorageMultiplier(instance.StorageGb, out double _));
            Assert.IsTrue(instance.AgeMonths >= definition.MinAgeMonths && instance.AgeMonths <= definition.MaxAgeMonths);
        }
    }
}
