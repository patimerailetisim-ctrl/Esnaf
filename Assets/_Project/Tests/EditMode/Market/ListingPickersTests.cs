using System;
using System.Collections.Generic;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Content;
using Esnaf.Domain.Npc;
using Esnaf.Domain.Products;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Market
{
    /// <summary>Satıcı seçimi (UA4 açılış günleri + P2 %45 öğrenme dostu payı) ve model seçimi (segment ağırlıkları, model kapısı).</summary>
    public class ListingPickersTests
    {
        private const int Draws = 40000;

        private static Dictionary<string, double> SellerShares(MarketHarness h, int day, ulong seed = 7)
        {
            IRandom rng = new RngStreams(seed).Get("pick");
            var counts = new Dictionary<string, int>();
            for (int i = 0; i < Draws; i++)
            {
                string id = h.Generator.PickSeller(day, rng).Id;
                int n;
                counts.TryGetValue(id, out n);
                counts[id] = n + 1;
            }

            return counts.ToDictionary(p => p.Key, p => (double)p.Value / Draws);
        }

        private static Dictionary<string, double> ModelShares(MarketHarness h, int day, ulong seed = 7)
        {
            IRandom rng = new RngStreams(seed).Get("pick");
            var counts = new Dictionary<string, int>();
            for (int i = 0; i < Draws; i++)
            {
                string id = h.Generator.PickModel(day, rng).Id;
                int n;
                counts.TryGetValue(id, out n);
                counts[id] = n + 1;
            }

            return counts.ToDictionary(p => p.Key, p => (double)p.Value / Draws);
        }

        private static double Share(Dictionary<string, double> shares, string id)
        {
            double v;
            return shares.TryGetValue(id, out v) ? v : 0.0;
        }

        // ---------- satıcı seçimi ----------

        [Test]
        public void Day1_OnlyAyseIsAvailable()
        {
            Dictionary<string, double> shares = SellerShares(new MarketHarness(), 1);

            CollectionAssert.AreEqual(new[] { "npc.ayse" }, shares.Keys.ToArray());
        }

        [Test]
        public void Day2_LearningFriendlySelin_GetsTheReservedShare()
        {
            // Gün 2 satıcıları: Ayşe (Gün 1'den beri), Kemal, Selin, Ozan. Öğrenme dostu: Ayşe, Selin → %45; diğerleri %55.
            Dictionary<string, double> s = SellerShares(new MarketHarness(), 2);

            Assert.AreEqual(4, s.Count);
            Assert.AreEqual(0.225, Share(s, "npc.ayse"), 0.012);
            Assert.AreEqual(0.225, Share(s, "npc.selin"), 0.012);
            Assert.AreEqual(0.275, Share(s, "npc.kemal"), 0.012);
            Assert.AreEqual(0.275, Share(s, "npc.ozan"), 0.012);
        }

        [Test]
        public void Day3_FriendlyGroupHas45Percent()
        {
            Dictionary<string, double> s = SellerShares(new MarketHarness(), 3);

            double friendly = Share(s, "npc.ayse") + Share(s, "npc.selin") + Share(s, "npc.hatice");
            Assert.AreEqual(0.45, friendly, 0.012, "P2: %45");
            Assert.AreEqual(0.15, Share(s, "npc.hatice"), 0.012);
            Assert.AreEqual(0.275, Share(s, "npc.kemal"), 0.012);
            Assert.AreEqual(0.275, Share(s, "npc.ozan"), 0.012);
            Assert.AreEqual(0.0, Share(s, "npc.murat"), "Dr. Murat Gün 4'te açılır");
        }

        [Test]
        public void Day4_LastDayOfTheLearningWeightBias()
        {
            Dictionary<string, double> s = SellerShares(new MarketHarness(), 4);

            double friendly = Share(s, "npc.ayse") + Share(s, "npc.selin") + Share(s, "npc.hatice");
            Assert.AreEqual(0.45, friendly, 0.012);
            Assert.AreEqual(0.1375, Share(s, "npc.murat"), 0.01);
            Assert.AreEqual(0.1375, Share(s, "npc.riza"), 0.01);
            Assert.AreEqual(0.0, Share(s, "npc.cengiz"), "Cengiz Gün 5'te açılır");
        }

        [Test]
        public void Day5_NoMoreGroupBias_EveryAvailableSellerIsEquallyLikely()
        {
            Dictionary<string, double> s = SellerShares(new MarketHarness(), 5);

            Assert.AreEqual(8, s.Count, "Berk ve Nermin Gün 6'da açılır");
            foreach (KeyValuePair<string, double> pair in s)
            {
                Assert.AreEqual(1.0 / 8.0, pair.Value, 0.012, pair.Key);
            }
        }

        [Test]
        public void Day6_AllTenSellersAreAvailable()
        {
            Dictionary<string, double> s = SellerShares(new MarketHarness(), 6);

            Assert.AreEqual(10, s.Count);
            Assert.IsTrue(s.ContainsKey("npc.berk"));
            Assert.IsTrue(s.ContainsKey("npc.nermin"));
        }

        [Test]
        public void SellerOpeningDays_MatchTheDataForEveryDay()
        {
            var h = new MarketHarness();
            IRandom rng = new RngStreams(3).Get("pick");
            for (int day = 1; day <= 8; day++)
            {
                for (int i = 0; i < 500; i++)
                {
                    NpcDefinition seller = h.Generator.PickSeller(day, rng);

                    Assert.LessOrEqual(seller.Seller.AvailableFromDay, day, seller.Id + " gün " + day);
                }
            }
        }

        [Test]
        public void EmptyGroup_FallsBackToTheOtherGroup()
        {
            // Fixture, Gün 1'de yalnızca öğrenme dostu Ayşe'ye sahiptir; diğer grup boş → hep Ayşe (paylar 0,45/0,55 uygulanmaz).
            var h = new MarketHarness(1UL, MarketHarness.FixtureContent());
            IRandom rng = new RngStreams(5).Get("pick");

            for (int i = 0; i < 300; i++)
            {
                Assert.AreEqual("npc.test_honest", h.Generator.PickSeller(1, rng).Id);
            }
        }

        [Test]
        public void SellerPick_IsDeterministicForTheSameStream()
        {
            var h = new MarketHarness();
            IRandom a = new RngStreams(11).Get("pick");
            IRandom b = new RngStreams(11).Get("pick");

            for (int i = 0; i < 200; i++)
            {
                Assert.AreEqual(h.Generator.PickSeller(4, a).Id, h.Generator.PickSeller(4, b).Id);
            }
        }

        // ---------- model seçimi ----------

        [Test]
        public void Days1To4_SegmentWeights_30_40_15()
        {
            Dictionary<string, double> m = ModelShares(new MarketHarness(), 3);

            // Giriş 2 model, Orta 5 model, Üst 2 model (Model 10 kapalı). Toplam ağırlık 85.
            double entry = Share(m, "phone.nova_n1_lite") + Share(m, "phone.yildiz_y5");
            double mid = Share(m, "phone.samsun_vega_a3") + Share(m, "phone.nova_n3_pro") + Share(m, "phone.zirve_z5")
                         + Share(m, "phone.yildiz_y8_plus") + Share(m, "phone.elma_e11");
            double upper = Share(m, "phone.samsun_vega_s21") + Share(m, "phone.elma_e13_pro");
            Assert.AreEqual(30.0 / 85.0, entry, 0.012);
            Assert.AreEqual(40.0 / 85.0, mid, 0.012);
            Assert.AreEqual(15.0 / 85.0, upper, 0.012);
            Assert.AreEqual(1.0, entry + mid + upper, 1e-9);
        }

        [Test]
        public void Days1To4_FlagshipModelNeverAppears()
        {
            var h = new MarketHarness();
            for (int day = 1; day <= 4; day++)
            {
                Assert.AreEqual(0.0, Share(ModelShares(h, day, (ulong)day), "phone.elma_e14_pro_max"), "gün " + day);
            }
        }

        [Test]
        public void Day5_SegmentWeights_30_40_30_AndFlagshipAppears()
        {
            Dictionary<string, double> m = ModelShares(new MarketHarness(), 5);

            double entry = Share(m, "phone.nova_n1_lite") + Share(m, "phone.yildiz_y5");
            double mid = Share(m, "phone.samsun_vega_a3") + Share(m, "phone.nova_n3_pro") + Share(m, "phone.zirve_z5")
                         + Share(m, "phone.yildiz_y8_plus") + Share(m, "phone.elma_e11");
            double upper = Share(m, "phone.samsun_vega_s21") + Share(m, "phone.elma_e13_pro") + Share(m, "phone.elma_e14_pro_max");
            Assert.AreEqual(0.30, entry, 0.012);
            Assert.AreEqual(0.40, mid, 0.012);
            Assert.AreEqual(0.30, upper, 0.012);
            Assert.AreEqual(0.10, Share(m, "phone.elma_e14_pro_max"), 0.01, "Üst segmentin üç modelinden biri");
        }

        [Test]
        public void ModelsWithinASegment_AreEquallyLikely()
        {
            Dictionary<string, double> m = ModelShares(new MarketHarness(), 5);

            Assert.AreEqual(0.15, Share(m, "phone.nova_n1_lite"), 0.01);
            Assert.AreEqual(0.15, Share(m, "phone.yildiz_y5"), 0.01);
            Assert.AreEqual(0.08, Share(m, "phone.zirve_z5"), 0.01);
            Assert.AreEqual(0.08, Share(m, "phone.elma_e11"), 0.01);
        }

        [Test]
        public void SegmentWithoutAvailableModels_IsSkipped()
        {
            // Fixture'da yalnızca Orta segment modelleri var: Giriş/Üst ağırlıkları olsa da hep Orta çıkar.
            var h = new MarketHarness(1UL, MarketHarness.FixtureContent());
            IRandom rng = new RngStreams(5).Get("pick");

            for (int i = 0; i < 300; i++)
            {
                Assert.AreEqual("phone.test_one", h.Generator.PickModel(2, rng).Id, "test_two Gün 5'ten önce kapalı");
            }
        }

        [Test]
        public void FixtureFlagship_OpensExactlyOnItsDay()
        {
            var h = new MarketHarness(1UL, MarketHarness.FixtureContent());
            IRandom rng = new RngStreams(5).Get("pick");
            var seen = new HashSet<string>();

            for (int i = 0; i < 300; i++)
            {
                seen.Add(h.Generator.PickModel(5, rng).Id);
            }

            CollectionAssert.AreEquivalent(new[] { "phone.test_one", "phone.test_two" }, seen);
        }

        [Test]
        public void ModelPick_IsDeterministicForTheSameStream()
        {
            var h = new MarketHarness();
            IRandom a = new RngStreams(11).Get("pick");
            IRandom b = new RngStreams(11).Get("pick");

            for (int i = 0; i < 200; i++)
            {
                Assert.AreEqual(h.Generator.PickModel(6, a).Id, h.Generator.PickModel(6, b).Id);
            }
        }

        [Test]
        public void DeprecatedModels_NeverAppearInNewListings()
        {
            string deprecated = ContentFixtures.ModelWithId("phone.test_two").Replace("\"ageMonths\"", "\"deprecated\": true, \"ageMonths\"");
            DictionaryContentSource source = ContentFixtures.ValidSource();
            source.Add(ContentFileNames.PhoneModels, ContentFixtures.ModelsFile(ContentFixtures.ValidModelJson, deprecated));
            ContentLoadResult loaded = ContentDatabase.Load(source);
            Assert.IsNotNull(loaded.Database, loaded.FormatIssues());
            var h = new MarketHarness(1UL, loaded.Database);
            IRandom rng = new RngStreams(5).Get("pick");

            for (int i = 0; i < 400; i++)
            {
                Assert.AreEqual("phone.test_one", h.Generator.PickModel(6, rng).Id, "kullanımdan kalkan model Gün 5'ten sonra da çıkmamalı");
            }
        }

        [Test]
        public void ConcealingSeller_IsAlwaysCengiz_InTheRealData()
        {
            var h = new MarketHarness();
            IRandom rng = new RngStreams(5).Get("pick");

            for (int day = 5; day <= 12; day++)
            {
                for (int i = 0; i < 100; i++)
                {
                    Assert.AreEqual("npc.cengiz", h.Generator.PickConcealingSeller(day, rng).Id);
                }
            }
        }

        [Test]
        public void ConcealingSeller_BeforeAnyoneCanConceal_IsAnError()
        {
            var h = new MarketHarness();
            IRandom rng = new RngStreams(5).Get("pick");

            Assert.Throws<InvalidOperationException>(() => h.Generator.PickConcealingSeller(4, rng));
        }

        [Test]
        public void ConcealingSeller_OpensOnItsDay_InTheFixture()
        {
            var h = new MarketHarness(1UL, MarketHarness.FixtureContent());
            IRandom rng = new RngStreams(5).Get("pick");

            Assert.Throws<InvalidOperationException>(() => h.Generator.PickConcealingSeller(2, rng));
            Assert.AreEqual("npc.test_liar", h.Generator.PickConcealingSeller(3, rng).Id);
        }

        [Test]
        public void Pickers_RejectBadArguments()
        {
            var h = new MarketHarness();
            IRandom rng = new RngStreams(1).Get("pick");

            Assert.Throws<ArgumentNullException>(() => h.Generator.PickSeller(1, null));
            Assert.Throws<ArgumentNullException>(() => h.Generator.PickModel(1, null));
            Assert.Throws<ArgumentNullException>(() => h.Generator.PickConcealingSeller(5, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => h.Generator.PickConcealingSeller(0, rng));
            Assert.Throws<ArgumentOutOfRangeException>(() => h.Generator.PickSeller(0, rng));
            Assert.Throws<ArgumentOutOfRangeException>(() => h.Generator.PickModel(0, rng));
        }
    }
}
