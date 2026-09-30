using System;
using Esnaf.Core;
using Esnaf.Domain.Content;
using Esnaf.Domain.Market;
using Esnaf.Domain.Npc;
using Esnaf.Domain.Products;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Market
{
    /// <summary>
    /// Satıcının inandığı değer, istenen fiyat ve ret fiyatı (UA5). Beklenen sayılar GDD v0.2 senaryolarından ve
    /// bağımsız Python referans modelinden gelir; "rastgele" girdiler ScriptedRandom ile sabitlenir.
    /// </summary>
    public class ListingPricerTests
    {
        private static ListingPricer Pricer()
        {
            return new ListingPricer(new ValueCalculator(MarketHarness.RealContent().ValueTables), MarketHarness.RealContent().MarketConstants);
        }

        private static NpcSellerRole Seller(string id)
        {
            return MarketHarness.RealContent().GetNpc(id).Seller;
        }

        private static ProductDefinition Def(string id)
        {
            return MarketHarness.RealContent().GetProduct(id);
        }

        /// <summary>v0.2 Senaryo 4 telefonu: Yıldız Y8 Plus 128 GB, 20 ay, pil 66, kasa 65, yan sanayi ekran, arızalı kamera (V = 8.850).</summary>
        private static ProductInstance Scenario4Phone()
        {
            return ContentFixtures.Instance(Def("phone.yildiz_y8_plus"), 20, 66, "replaced_aftermarket", 65, "faulty", false, false, 128);
        }

        private static ProductInstance CleanY8()
        {
            return ContentFixtures.Instance(Def("phone.yildiz_y8_plus"), 20, 90, "original", 100, "ok", false, false, 128);
        }

        private static ScriptedRandom Script(bool concealRoll, double u)
        {
            return new ScriptedRandom(new[] { concealRoll }, new[] { u });
        }

        [Test]
        public void Scenario4_ConcealingCengiz_BelievesTheCleanValue()
        {
            SellerValuation v = Pricer().Value(Scenario4Phone(), Def("phone.yildiz_y8_plus"), Seller("npc.cengiz"), Script(true, 0.5));

            Assert.IsTrue(v.Concealed);
            Assert.AreEqual(Money.FromTl(8850), v.TrueValue, "gerçek değer");
            Assert.AreEqual(Money.FromTl(12260), v.BelievedValue, "v0.2 Senaryo 4: ilan değeri 12.260");
            Assert.AreEqual(Money.FromTl(12500), v.AskingPrice, "v0.2 Senaryo 4: istenen 12.500");
            Assert.AreEqual(Money.FromTl(9560), v.RejectPrice, "v0.2 Senaryo 4: R = 9.560");
        }

        [Test]
        public void ConcealRoll_IsIgnoredForTheNoiseWhenTheSellerConceals()
        {
            SellerValuation a = Pricer().Value(Scenario4Phone(), Def("phone.yildiz_y8_plus"), Seller("npc.cengiz"), Script(true, 0.01));
            SellerValuation b = Pricer().Value(Scenario4Phone(), Def("phone.yildiz_y8_plus"), Seller("npc.cengiz"), Script(true, 0.99));

            Assert.AreEqual(a.BelievedValue, b.BelievedValue);
            Assert.AreEqual(a.AskingPrice, b.AskingPrice);
        }

        [Test]
        public void Cengiz_NotConcealing_UsesTheNoisyTrueValue()
        {
            SellerValuation v = Pricer().Value(Scenario4Phone(), Def("phone.yildiz_y8_plus"), Seller("npc.cengiz"), Script(false, 0.5));

            Assert.IsFalse(v.Concealed);
            Assert.AreEqual(Money.FromTl(8850), v.BelievedValue, "u = 0,5 → hata payı 0");
            Assert.AreEqual(Money.FromTl(9000), v.AskingPrice);
            Assert.AreEqual(Money.FromTl(6900), v.RejectPrice);
        }

        [Test]
        public void ConcealRollTrue_ButNothingToHide_IsNotConcealed()
        {
            SellerValuation v = Pricer().Value(CleanY8(), Def("phone.yildiz_y8_plus"), Seller("npc.cengiz"), Script(true, 0.25));

            Assert.IsFalse(v.Concealed, "saklanacak kusur yok");
            Assert.AreEqual(Money.FromTl(15400), v.TrueValue);
            Assert.AreEqual(Money.FromTl(14250), v.BelievedValue, "u = 0,25 → çarpan 1 + 0,15 × (2u − 1) = 0,925");
            Assert.AreEqual(Money.FromTl(14550), v.AskingPrice);
            Assert.AreEqual(Money.FromTl(11110), v.RejectPrice);
        }

        [Test]
        public void SellerWithZeroConcealChance_NeverConceals_EvenWhenTheRollSaysTrue()
        {
            // Ayşe'nin concealChance'i 0: senaryo dışı bir "true" zarı bile sakladırmamalı (zar yine de tüketilir).
            var rng = Script(true, 0.5);
            SellerValuation v = Pricer().Value(Scenario4Phone(), Def("phone.yildiz_y8_plus"), Seller("npc.ayse"), rng);

            Assert.IsFalse(v.Concealed);
            Assert.AreEqual(Money.FromTl(8850), v.BelievedValue);
            Assert.AreEqual(0, rng.Remaining, "her zaman bir Chance ve bir NextDouble tüketilir");
        }

        [Test]
        public void OnlyTheHiddenDefects_AreIgnoredByAConcealingSeller()
        {
            // Yalnızca ekran "değişmiş" (0,88): gerçek değer 13.550, satıcı 15.400'e inanır.
            var phone = ContentFixtures.Instance(Def("phone.yildiz_y8_plus"), 20, 90, "replaced_aftermarket", 100, "ok", false, false, 128);

            SellerValuation v = Pricer().Value(phone, Def("phone.yildiz_y8_plus"), Seller("npc.cengiz"), Script(true, 0.5));

            Assert.IsTrue(v.Concealed);
            Assert.AreEqual(Money.FromTl(13550), v.TrueValue);
            Assert.AreEqual(Money.FromTl(15400), v.BelievedValue);
            Assert.AreEqual(Money.FromTl(15700), v.AskingPrice);
            Assert.AreEqual(Money.FromTl(12010), v.RejectPrice);
        }

        [Test]
        public void ConcealingSeller_KeepsVisibleDefectsInTheirValue()
        {
            // Çizik ekran görünür (0,96); arızalı kamera gizli (0,82). Satıcı yalnızca kamerayı yok sayar.
            var phone = ContentFixtures.Instance(Def("phone.yildiz_y8_plus"), 20, 90, "scratched", 100, "faulty", false, false, 128);

            SellerValuation v = Pricer().Value(phone, Def("phone.yildiz_y8_plus"), Seller("npc.cengiz"), Script(true, 0.5));

            Assert.IsTrue(v.Concealed);
            Assert.AreEqual(Money.FromTl(12120), v.TrueValue);
            Assert.AreEqual(Money.FromTl(14780), v.BelievedValue, "ekran çiziği inancın içinde kalır");
            Assert.AreEqual(Money.FromTl(15100), v.AskingPrice);
            Assert.AreEqual(Money.FromTl(11530), v.RejectPrice);
        }

        [Test]
        public void VisibleDefects_AreNotHidden()
        {
            // Çizik ekran ve lekeli kamera... çizik gizli değil; lekeli kamera gizli. Yalnızca çizik: saklanacak şey yok.
            var phone = ContentFixtures.Instance(Def("phone.yildiz_y8_plus"), 20, 90, "scratched", 100, "ok", false, false, 128);

            SellerValuation v = Pricer().Value(phone, Def("phone.yildiz_y8_plus"), Seller("npc.cengiz"), Script(true, 0.5));

            Assert.IsFalse(v.Concealed);
        }

        [Test]
        public void Hatice_BelievesAbout28PercentLess_Scenario2()
        {
            var phone = ContentFixtures.Instance(Def("phone.nova_n3_pro"), 30, 84, "scratched", 70, "ok", true, false, 128);

            SellerValuation v = Pricer().Value(phone, Def("phone.nova_n3_pro"), Seller("npc.hatice"), Script(false, 0.5));

            Assert.AreEqual(Money.FromTl(8540), v.TrueValue, "v0.2 Senaryo 2: V = 8.540");
            Assert.AreEqual(Money.FromTl(6150), v.BelievedValue, "v0.2 Senaryo 2: V_s = 6.150");
            Assert.AreEqual(Money.FromTl(6750), v.AskingPrice, "v0.2 Senaryo 2: istenen 6.750");
            Assert.AreEqual(Money.FromTl(5660), v.RejectPrice, "v0.2 Senaryo 2: R = 5.660");
        }

        [Test]
        public void Hatice_ErrorStaysWithinSigma_AtBothEnds()
        {
            var phone = ContentFixtures.Instance(Def("phone.nova_n3_pro"), 30, 84, "scratched", 70, "ok", true, false, 128);

            SellerValuation low = Pricer().Value(phone, Def("phone.nova_n3_pro"), Seller("npc.hatice"), Script(false, 0.0));
            SellerValuation high = Pricer().Value(phone, Def("phone.nova_n3_pro"), Seller("npc.hatice"), Script(false, 0.999999));

            Assert.AreEqual(Money.FromTl(5980), low.BelievedValue, "çarpan 0,70 = 1 − σ");
            Assert.AreEqual(Money.FromTl(5500), low.RejectPrice);
            Assert.AreEqual(Money.FromTl(6600), low.AskingPrice);
            Assert.AreEqual(Money.FromTl(6320), high.BelievedValue, "çarpan ≈ 0,74");
            Assert.AreEqual(Money.FromTl(6950), high.AskingPrice);
            Assert.AreEqual(Money.FromTl(5810), high.RejectPrice);
        }

        [Test]
        public void Selin_NoisyBelief_Scenario3Phone()
        {
            var phone = ContentFixtures.Instance(Def("phone.samsun_vega_s21"), 26, 80, "original", 75, "ok", true, true, 128);

            SellerValuation typical = Pricer().Value(phone, Def("phone.samsun_vega_s21"), Seller("npc.selin"), Script(false, 0.5));
            SellerValuation over = Pricer().Value(phone, Def("phone.samsun_vega_s21"), Seller("npc.selin"), Script(false, 0.75));

            Assert.AreEqual(Money.FromTl(17160), typical.TrueValue, "v0.2 Senaryo 3: V = 17.160");
            Assert.AreEqual(Money.FromTl(17160), typical.BelievedValue);
            Assert.AreEqual(Money.FromTl(18000), typical.AskingPrice);
            Assert.AreEqual(Money.FromTl(14070), typical.RejectPrice);
            Assert.AreEqual(Money.FromTl(18190), over.BelievedValue, "u = 0,75 → çarpan 1,06");
            Assert.AreEqual(Money.FromTl(19100), over.AskingPrice);
            Assert.AreEqual(Money.FromTl(14920), over.RejectPrice);
        }

        [Test]
        public void RejectPrice_IsAlwaysBelowTheAskingPrice_ForEveryRealSeller()
        {
            ContentDatabase content = MarketHarness.RealContent();
            ProductDefinition def = Def("phone.elma_e13_pro");
            var phone = ContentFixtures.Instance(def, 18, 80, "original", 90, "ok", false, false, 128);
            foreach (NpcDefinition npc in content.Npcs)
            {
                foreach (double u in new[] { 0.0, 0.5, 0.999999 })
                {
                    SellerValuation v = Pricer().Value(phone, def, npc.Seller, Script(false, u));

                    Assert.Less(v.RejectPrice.Tl, v.AskingPrice.Tl, npc.Id + " u=" + u);
                    Assert.IsTrue(v.RejectPrice.IsRoundedTo10);
                    Assert.IsTrue(v.AskingPrice.Tl % 50 == 0);
                }
            }
        }

        [Test]
        public void AskingPrice_RoundsToTheStep_HalfUp()
        {
            ListingPricer pricer = Pricer();

            // v0.2 senaryolarındaki istenen fiyatlar
            Assert.AreEqual(Money.FromTl(30400), pricer.AskingPrice(25350.0, 1.20));
            Assert.AreEqual(Money.FromTl(18400), pricer.AskingPrice(17500.0, 1.05), "18.375 tam yarıda: yukarı");
            Assert.AreEqual(Money.FromTl(6750), pricer.AskingPrice(6150.0, 1.10));
            Assert.AreEqual(Money.FromTl(12500), pricer.AskingPrice(12260.0, 1.02));
            Assert.AreEqual(Money.FromTl(5800), pricer.AskingPrice(5280.0, 1.10));
        }

        [Test]
        public void RoundToStep_Boundaries()
        {
            Assert.AreEqual(50L, ListingPricer.RoundToStep(74.9, 50));
            Assert.AreEqual(100L, ListingPricer.RoundToStep(75.0, 50));
            Assert.AreEqual(50L, ListingPricer.RoundToStep(25.0, 50), "yarım yukarı");
            Assert.AreEqual(50L, ListingPricer.RoundToStep(10.0, 50), "en az bir adım");
            Assert.AreEqual(50L, ListingPricer.RoundToStep(0.0, 50), "en az bir adım");
            Assert.AreEqual(6750L, ListingPricer.RoundToStep(6765.0, 50));
            Assert.AreEqual(6800L, ListingPricer.RoundToStep(6775.0, 50));
            Assert.AreEqual(100L, ListingPricer.RoundToStep(100.0, 100));
            Assert.AreEqual(200L, ListingPricer.RoundToStep(151.0, 100));
        }

        [Test]
        public void AskingPrice_UsesTheConfiguredStep()
        {
            // Fixture pazar sabitleri de 50 kullanır; adım gerçekten veriden geliyorsa 100 yapınca sonuç değişmeli.
            ContentDatabase content = MarketHarness.FixtureContent(
                ContentFixtures.EconomyConstantsJson.Replace("\"askingPriceStep\": 50", "\"askingPriceStep\": 100"));
            var pricer = new ListingPricer(new ValueCalculator(content.ValueTables), content.MarketConstants);

            Assert.AreEqual(Money.FromTl(18400), pricer.AskingPrice(17500.0, 1.05));
            Assert.AreEqual(Money.FromTl(6800), pricer.AskingPrice(6150.0, 1.10), "6.765 → 100 adımında 6.800");
        }

        [Test]
        public void Constructor_RejectsNulls()
        {
            ContentDatabase content = MarketHarness.RealContent();

            Assert.Throws<ArgumentNullException>(() => new ListingPricer(null, content.MarketConstants));
            Assert.Throws<ArgumentNullException>(() => new ListingPricer(new ValueCalculator(content.ValueTables), null));
        }

        [Test]
        public void Value_RejectsNulls()
        {
            ListingPricer pricer = Pricer();
            ProductInstance phone = CleanY8();
            ProductDefinition def = Def("phone.yildiz_y8_plus");
            NpcSellerRole seller = Seller("npc.ayse");

            Assert.Throws<ArgumentNullException>(() => pricer.Value(null, def, seller, Script(false, 0.5)));
            Assert.Throws<ArgumentNullException>(() => pricer.Value(phone, null, seller, Script(false, 0.5)));
            Assert.Throws<ArgumentNullException>(() => pricer.Value(phone, def, null, Script(false, 0.5)));
            Assert.Throws<ArgumentNullException>(() => pricer.Value(phone, def, seller, null));
        }
    }
}
