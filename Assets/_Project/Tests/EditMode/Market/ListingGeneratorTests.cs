using System;
using System.Collections.Generic;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Content;
using Esnaf.Domain.Market;
using Esnaf.Domain.Npc;
using Esnaf.Domain.Phone;
using Esnaf.Domain.Products;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Market
{
    public class ListingGeneratorTests
    {
        // ---------- Gün 1: rehberli ilan (UA7) ----------

        [Test]
        public void Day1_HasThreeListings_OneOfThemGuided()
        {
            var h = new MarketHarness(1UL);

            IReadOnlyList<MarketListing> listings = h.Generate(1);

            Assert.AreEqual(3, listings.Count);
            Assert.AreEqual(1, listings.Count(l => l.IsGuided));
            Assert.IsTrue(listings.All(l => l.SellerNpcId == "npc.ayse"), "Gün 1'de yalnızca Ayşe Hanım satıcıdır");
        }

        [Test]
        public void GuidedListing_MatchesTheGddDayOne_Exactly()
        {
            var h = new MarketHarness(1UL);

            MarketListing g = h.Generate(1).Single(l => l.IsGuided);

            ProductInstance phone = h.InstanceOf(g);
            Assert.AreEqual("phone.yildiz_y5", phone.DefinitionId);
            Assert.AreEqual(64, phone.StorageGb);
            Assert.AreEqual(24, phone.AgeMonths);
            Assert.AreEqual(95, phone.GetNumber(PhoneAttributes.Battery));
            Assert.AreEqual(100, phone.GetNumber(PhoneAttributes.Body));
            Assert.AreEqual("original", phone.GetText(PhoneAttributes.Screen));
            Assert.AreEqual("ok", phone.GetText(PhoneAttributes.Camera));
            Assert.IsFalse(phone.GetFlag(PhoneAttributes.Box));
            Assert.IsFalse(phone.GetFlag(PhoneAttributes.Invoice));
            Assert.AreEqual(Money.FromTl(5280), h.TrueValueOf(g), "v0.2 Gün 1: V ≈ 5.280");
            Assert.AreEqual(Money.FromTl(5280), g.BelievedValue, "Ayşe dürüst: inandığı değer gerçek değer");
            Assert.AreEqual(Money.FromTl(5800), g.AskingPrice, "v0.2 Gün 1: istenen 5.800");
            Assert.AreEqual(Money.FromTl(4750), g.RejectPrice, "v0.2 Gün 1: kolay mod R = 4.750");
            Assert.IsTrue(g.IsOpportunity);
            Assert.IsFalse(g.IsTrap);
            Assert.IsFalse(g.IsJackpot);
            Assert.AreEqual(0, g.Tags.Count);
            Assert.AreEqual(1, g.DayListed);
        }

        [TestCase(9000, true)]
        [TestCase(9010, false)]
        [TestCase(8990, true)]
        public void OpportunityThreshold_IsInclusive(int rejectPrice, bool expected)
        {
            // Fixture'ın rehberli ürünü: V = 10.000 → eşik 0,90 × 10.000 = 9.000 TL.
            ContentDatabase content = MarketHarness.FixtureContent(
                ContentFixtures.EconomyConstantsJson.Replace("\"rejectPrice\": 4750", "\"rejectPrice\": " + rejectPrice));
            var h = new MarketHarness(1UL, content);

            MarketListing guided = h.Generate(1).Single(l => l.IsGuided);

            Assert.AreEqual(Money.FromTl(10000), h.TrueValueOf(guided));
            Assert.AreEqual(expected, guided.IsOpportunity);
        }

        [Test]
        public void JackpotThreshold_IsStrict()
        {
            // Eşik 0,82: Aceleci Test'in ret oranı tam 0,82 → jackpot DEĞİL (P3: "ret oranı < eşik"); Saklayan Test (0,78) jackpot.
            ContentDatabase content = MarketHarness.FixtureContent(
                ContentFixtures.EconomyConstantsJson.Replace("\"rejectRatioBelow\": 0.85", "\"rejectRatioBelow\": 0.82"));
            var h = new MarketHarness(2UL, content);
            var flags = new Dictionary<string, HashSet<bool>>();

            for (int day = 1; day <= 60; day++)
            {
                foreach (MarketListing l in h.Generate(day))
                {
                    if (!flags.ContainsKey(l.SellerNpcId))
                    {
                        flags[l.SellerNpcId] = new HashSet<bool>();
                    }

                    flags[l.SellerNpcId].Add(l.IsJackpot);
                }
            }

            CollectionAssert.AreEqual(new[] { false }, flags["npc.test_hurried"].ToArray());
            CollectionAssert.AreEqual(new[] { true }, flags["npc.test_liar"].ToArray());
            CollectionAssert.AreEqual(new[] { false }, flags["npc.test_honest"].ToArray());
        }

        [Test]
        public void ATrap_IsNeverCountedAsAnOpportunity_EvenWithACheapRejectRatio()
        {
            // Saklayan satıcının ret oranı 0,50: tuzakta bile R ≈ 0,61 × değer (≤ 0,90) olur; yine de fırsat sayılmamalı.
            string npcs = ContentFixtures.NpcProfilesJson.Replace("\"rejectRatio\": 0.78", "\"rejectRatio\": 0.50");
            DictionaryContentSource source = ContentFixtures.ValidSource();
            source.Add(ContentFileNames.NpcProfiles, npcs);
            source.Add(ContentFileNames.EconomyConstants, GenerousJackpots(ContentFixtures.EconomyConstantsJson));
            ContentLoadResult loaded = ContentDatabase.Load(source);
            Assert.IsNotNull(loaded.Database, loaded.FormatIssues());
            var h = new MarketHarness(3UL, loaded.Database);
            int traps = 0;

            for (int day = 1; day <= 100; day++)
            {
                foreach (MarketListing l in h.Generate(day))
                {
                    if (l.IsTrap)
                    {
                        traps++;
                        Assert.Less(l.RejectPrice.Tl, 0.90m * h.TrueValueOf(l).Tl, "senaryo: R gerçekten ≤ 0,90 × değer");
                        Assert.IsFalse(l.IsOpportunity, "tuzak fırsat sayılmaz (gün " + day + ")");
                    }
                }
            }

            Assert.Greater(traps, 20);
        }

        [Test]
        public void GuidedListing_IsOnlyOnDayOne()
        {
            var h = new MarketHarness(1UL);

            for (int day = 2; day <= 30; day++)
            {
                Assert.AreEqual(0, h.Generate(day).Count(l => l.IsGuided), "gün " + day);
            }
        }

        [Test]
        public void GuidedListing_PositionVariesWithTheSeed()
        {
            var positions = new HashSet<int>();
            for (ulong seed = 1; seed <= 40; seed++)
            {
                var h = new MarketHarness(seed);
                IReadOnlyList<MarketListing> listings = h.Generate(1);
                for (int i = 0; i < listings.Count; i++)
                {
                    if (listings[i].IsGuided)
                    {
                        positions.Add(i);
                    }
                }
            }

            CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, positions, "ilanlar karıştırılmalı");
        }

        // ---------- sayılar, ömür, kayıtlar ----------

        [TestCase(2, 5, 5)]
        [TestCase(3, 6, 6)]
        [TestCase(4, 6, 6)]
        public void ListingCount_FollowsTheDayBand(int day, int min, int max)
        {
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var h = new MarketHarness(seed);
                int count = h.Generate(day).Count;
                Assert.GreaterOrEqual(count, min, "seed " + seed);
                Assert.LessOrEqual(count, max, "seed " + seed);
            }
        }

        [Test]
        public void Day5Plus_HasSevenOrEightListings_AndBothOccur()
        {
            var counts = new HashSet<int>();
            for (ulong seed = 1; seed <= 60; seed++)
            {
                var h = new MarketHarness(seed);
                foreach (int day in new[] { 5, 6, 12 })
                {
                    int count = h.Generate(day).Count;
                    Assert.IsTrue(count == 7 || count == 8, "seed " + seed + " gün " + day + ": " + count);
                    counts.Add(count);
                }
            }

            CollectionAssert.AreEquivalent(new[] { 7, 8 }, counts);
        }

        [Test]
        public void Lifetime_IsTwoToFourDays_AndAllValuesOccur()
        {
            var seen = new HashSet<int>();
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var h = new MarketHarness(seed);
                foreach (MarketListing l in h.Generate(6))
                {
                    Assert.GreaterOrEqual(l.RemainingDays, 2);
                    Assert.LessOrEqual(l.RemainingDays, 4);
                    seen.Add(l.RemainingDays);
                }
            }

            CollectionAssert.AreEquivalent(new[] { 2, 3, 4 }, seen);
        }

        [Test]
        public void EveryListing_HasAnInstanceOnTheMarket_LinkedBothWays()
        {
            var h = new MarketHarness(3UL);

            IReadOnlyList<MarketListing> listings = h.Generate(6);

            Assert.AreEqual(listings.Count, h.Store.Count);
            foreach (MarketListing l in listings)
            {
                ProductInstance phone = h.InstanceOf(l);
                Assert.AreEqual(ProductLocation.Market, phone.Location);
                Assert.AreEqual(l.ListingId, phone.ListingId);
                Assert.AreEqual(l.SellerNpcId, phone.SellerNpcId);
                Assert.IsFalse(phone.AcquiredDay.HasValue);
                Assert.AreEqual(6, l.DayListed);
            }
        }

        [Test]
        public void Ids_AreSequentialAndUnique_AndCountersAdvance()
        {
            var h = new MarketHarness(4UL);

            IReadOnlyList<MarketListing> day1 = h.Generate(1);
            IReadOnlyList<MarketListing> day2 = h.Generate(2);

            IList<MarketListing> all = day1.Concat(day2).ToList();
            CollectionAssert.AreEquivalent(Enumerable.Range(1, all.Count).Select(i => (long)i).ToArray(), all.Select(l => l.ListingId).ToArray());
            CollectionAssert.AreEquivalent(Enumerable.Range(1, all.Count).Select(i => (long)i).ToArray(), all.Select(l => l.InstanceId).ToArray());
            Assert.AreEqual(all.Count, h.ListingIds.LastIssued);
            Assert.AreEqual(all.Count, h.InstanceIds.LastIssued);
        }

        [Test]
        public void Tags_ReflectBoxInvoiceAndUrgentLabel()
        {
            var h = new MarketHarness(5UL);
            var urgentSeen = false;
            for (int day = 1; day <= 200; day++)
            {
                foreach (MarketListing l in h.Generate(day))
                {
                    ProductInstance phone = h.InstanceOf(l);
                    NpcDefinition seller = h.Content.GetNpc(l.SellerNpcId);
                    Assert.AreEqual(phone.GetFlag(PhoneAttributes.Box), l.Tags.Contains("box"), "kutu etiketi");
                    Assert.AreEqual(phone.GetFlag(PhoneAttributes.Invoice), l.Tags.Contains("invoice"), "fatura etiketi");
                    bool urgent = seller.Seller.UrgentLabelFromDay.HasValue && day >= seller.Seller.UrgentLabelFromDay.Value;
                    Assert.AreEqual(urgent, l.Tags.Contains("urgent_sale"), l.SellerNpcId + " gün " + day);
                    urgentSeen |= urgent;
                    Assert.AreEqual(l.Tags.Count, l.Tags.Distinct().Count());
                }
            }

            Assert.IsTrue(urgentSeen, "Gün 6+ 'Acil satış' etiketi hiç görülmedi");
        }

        // ---------- GDD kabul ölçütü: 1.000 gün, kotalar (P1, P3, P5) ----------

        private static void AssertDayQuotas(MarketHarness h, int day, IReadOnlyList<MarketListing> listings)
        {
            MarketConstants m = h.Content.MarketConstants;
            string where = " (gün " + day + ")";

            ListingCountBand band = m.CountBandFor(day);
            Assert.GreaterOrEqual(listings.Count, band.Min, "ilan sayısı" + where);
            Assert.LessOrEqual(listings.Count, band.Max, "ilan sayısı" + where);

            int opportunities = 0;
            int traps = 0;
            int jackpots = 0;
            foreach (MarketListing l in listings)
            {
                NpcDefinition seller = h.Content.GetNpc(l.SellerNpcId);
                ProductInstance phone = h.InstanceOf(l);
                ProductDefinition def = h.DefinitionOf(l);
                Money value = h.TrueValueOf(l);

                Assert.LessOrEqual(seller.Seller.AvailableFromDay, day, "satıcı henüz açılmadı" + where);
                Assert.IsTrue(m.IsModelAvailable(def.Id, day), def.Id + " kapalı" + where);
                Assert.GreaterOrEqual(l.RemainingDays, m.LifetimeMinDays, "ömür" + where);
                Assert.LessOrEqual(l.RemainingDays, m.LifetimeMaxDays, "ömür" + where);
                Assert.Less(l.RejectPrice.Tl, l.AskingPrice.Tl, "R < istenen" + where);
                Assert.IsTrue(l.AskingPrice.Tl % m.AskingPriceStep == 0, "adım" + where);
                Assert.IsTrue(l.RejectPrice.IsRoundedTo10, "R 10'un katı" + where);
                Assert.Greater(l.RejectPrice.Tl, 0);
                Assert.AreEqual(ProductLocation.Market, phone.Location);

                // Bayraklar, bağımsız olarak yeniden hesaplanan tanımlarla tutarlı olmalı
                bool jackpot = seller.Seller.RejectRatio < m.JackpotRejectRatioBelow;
                Assert.AreEqual(jackpot, l.IsJackpot, "jackpot bayrağı" + where);
                if (l.IsTrap)
                {
                    Assert.Greater(seller.Seller.ConcealChance, 0.0, "tuzağı saklayan satıcı kurar" + where);
                    Assert.GreaterOrEqual(l.BelievedValue.Tl * 100, value.Tl * 115L, "tuzak: inanılan ≥ 1,15 × değer" + where);
                    Assert.IsFalse(l.IsOpportunity, "tuzak fırsat sayılmaz" + where);
                    Assert.IsTrue(
                        m.HiddenDefects.Any(r => r.IsHidden(phone.GetText(r.Attribute))), "tuzakta gizli kusur olmalı" + where);
                }

                bool opportunity = !l.IsTrap && l.RejectPrice.Tl <= 0.90m * value.Tl;
                Assert.AreEqual(opportunity, l.IsOpportunity, "fırsat bayrağı" + where);

                opportunities += opportunity ? 1 : 0;
                traps += l.IsTrap ? 1 : 0;
                jackpots += jackpot ? 1 : 0;
            }

            if (day == 1)
            {
                Assert.GreaterOrEqual(opportunities, 1, "Gün 1: rehberli fırsat" + where);
            }
            else if (day >= m.OpportunityFromDay)
            {
                Assert.GreaterOrEqual(opportunities, m.MinOpportunitiesPerDay, "P1: ≥ 2 fırsat" + where);
            }

            Assert.LessOrEqual(jackpots, m.JackpotMax(day), "P3: jackpot kotası" + where);

            if (day < m.TrapFromDay)
            {
                Assert.AreEqual(0, traps, "Gün 5'ten önce tuzak yok" + where);
            }
            else
            {
                Assert.GreaterOrEqual(traps, m.TrapMinPerDay, "P5: Gün 5+ ≥ 1 tuzak" + where);
            }

            Assert.LessOrEqual(traps, m.TrapMax(day), "P5: Gün 6+ ≤ 2 tuzak" + where);
        }

        [TestCase(1UL)]
        [TestCase(2UL)]
        [TestCase(3UL)]
        public void ThousandDays_HoldEveryQuota(ulong seed)
        {
            var h = new MarketHarness(seed);

            for (int day = 1; day <= 1000; day++)
            {
                AssertDayQuotas(h, day, h.Generate(day));
            }
        }

        [Test]
        public void ThousandDays_ProduceTheFullVarietyOfSellersAndKinds()
        {
            var h = new MarketHarness(9UL);
            var sellers = new HashSet<string>();
            int trapDays = 0;
            int jackpotDays = 0;
            int exactlyMinOpportunityDays = 0;
            int exactlyMinTrapDays = 0;

            for (int day = 1; day <= 1000; day++)
            {
                IReadOnlyList<MarketListing> listings = h.Generate(day);
                foreach (MarketListing l in listings)
                {
                    sellers.Add(l.SellerNpcId);
                }

                trapDays += listings.Any(l => l.IsTrap) ? 1 : 0;
                if (day >= 2 && listings.Count(l => l.IsOpportunity) == h.Content.MarketConstants.MinOpportunitiesPerDay)
                {
                    exactlyMinOpportunityDays++;
                }

                if (day >= 5 && listings.Count(l => l.IsTrap) == h.Content.MarketConstants.TrapMinPerDay)
                {
                    exactlyMinTrapDays++;
                }
                jackpotDays += listings.Any(l => l.IsJackpot) ? 1 : 0;
            }

            Assert.AreEqual(10, sellers.Count);
            Assert.AreEqual(996, trapDays, "Gün 5–1000: her gün tuzak");
            Assert.Greater(jackpotDays, 200, "jackpot satıcıları (Selin, Cengiz) düzenli görünmeli");
            Assert.Greater(exactlyMinOpportunityDays, 50, "fırsat sayısı alt sınırın ÜSTÜNE zorlanmaz: bazı günler tam 2 fırsat vardır");
            Assert.Greater(exactlyMinTrapDays, 900, "tuzak sayısı alt sınırın ÜSTÜNE zorlanmaz: çoğu gün tam 1 tuzak vardır");
        }

        // ---------- üst sınırlar (fixture'da doğal olarak sık karşılaşılır) ----------

        /// <summary>Tuzak üst sınırı testlerinde jackpot kotası devre dışı kalsın diye cömert tutulur (tuzak satıcısı da jackpot satıcısıdır).</summary>
        private static string GenerousJackpots(string economyJson)
        {
            const string original = "\"maxPerDay\": [ { \"fromDay\": 1, \"max\": 1 }, { \"fromDay\": 6, \"max\": 2 } ]";
            Assert.IsTrue(economyJson.Contains(original));
            return economyJson.Replace(original, "\"maxPerDay\": [ { \"fromDay\": 1, \"max\": 9 } ]");
        }

        private static int[] CountsPerDay(MarketHarness h, int fromDay, int toDay, Func<MarketListing, bool> predicate)
        {
            var counts = new List<int>();
            for (int day = 1; day <= toDay; day++)
            {
                int n = h.Generate(day).Count(predicate);
                if (day >= fromDay)
                {
                    counts.Add(n);
                }
            }

            return counts.ToArray();
        }

        [Test]
        public void TrapCap_IsEnforcedAtTheConfiguredValue()
        {
            // Gün 5+ üst sınır 1: her gün TAM 1 tuzak (alt sınır 1).
            string capOne = ContentFixtures.EconomyConstantsJson.Replace(
                "\"maxPerDay\": [ { \"fromDay\": 6, \"max\": 2 } ], \"valueRatio\"",
                "\"maxPerDay\": [ { \"fromDay\": 5, \"max\": 1 } ], \"valueRatio\"");
            var one = new MarketHarness(3UL, MarketHarness.FixtureContent(GenerousJackpots(capOne)));

            int[] withCapOne = CountsPerDay(one, 5, 300, l => l.IsTrap);

            Assert.IsTrue(withCapOne.All(n => n == 1), "üst sınır 1: " + string.Join(",", withCapOne.Distinct()));
        }

        [Test]
        public void TrapCap_Two_AllowsExactlyTwoFromDaySixAndMoreOnDayFive()
        {
            var h = new MarketHarness(3UL, MarketHarness.FixtureContent(GenerousJackpots(ContentFixtures.EconomyConstantsJson)));

            int[] day6Plus = CountsPerDay(h, 6, 300, l => l.IsTrap);

            Assert.IsTrue(day6Plus.All(n => n >= 1 && n <= 2), "Gün 6+ 1–2 tuzak: " + string.Join(",", day6Plus.Distinct()));
            Assert.IsTrue(day6Plus.Any(n => n == 2), "ikinci tuzak hiç çıkmadı");
            Assert.IsTrue(day6Plus.Any(n => n == 1));
        }

        [Test]
        public void TrapCap_DoesNotApplyBeforeItsFirstBand()
        {
            // Gün 5'te sınır tanımsız (Gün 6'da başlar): bazı tohumlarda Gün 5'te 2+ tuzak görülür.
            bool moreThanOne = false;
            for (ulong seed = 1; seed <= 60 && !moreThanOne; seed++)
            {
                var h = new MarketHarness(seed, MarketHarness.FixtureContent(GenerousJackpots(ContentFixtures.EconomyConstantsJson)));
                for (int day = 1; day <= 4; day++)
                {
                    h.Generate(day);
                }

                moreThanOne = h.Generate(5).Count(l => l.IsTrap) >= 2;
            }

            Assert.IsTrue(moreThanOne);
        }

        [Test]
        public void JackpotCap_IsEnforcedAtTheConfiguredValue()
        {
            string capOne = ContentFixtures.EconomyConstantsJson.Replace(
                "\"maxPerDay\": [ { \"fromDay\": 1, \"max\": 1 }, { \"fromDay\": 6, \"max\": 2 } ]",
                "\"maxPerDay\": [ { \"fromDay\": 1, \"max\": 1 } ]");
            var h = new MarketHarness(3UL, MarketHarness.FixtureContent(capOne));

            int[] perDay = CountsPerDay(h, 1, 300, l => l.IsJackpot);

            Assert.IsTrue(perDay.All(n => n <= 1), "jackpot ≤ 1: " + string.Join(",", perDay.Distinct()));
        }

        [Test]
        public void JackpotCap_Two_FromDaySix_ReachesTwoButNeverThree()
        {
            var h = new MarketHarness(3UL, MarketHarness.FixtureContent());

            int[] before = CountsPerDay(h, 1, 5, l => l.IsJackpot);
            var h2 = new MarketHarness(3UL, MarketHarness.FixtureContent());
            var all = new List<int>();
            for (int day = 1; day <= 300; day++)
            {
                all.Add(h2.Generate(day).Count(l => l.IsJackpot));
            }

            Assert.IsTrue(before.All(n => n <= 1));
            Assert.IsTrue(all.Take(5).All(n => n <= 1), "Gün 1–5 ≤ 1");
            Assert.IsTrue(all.Skip(5).All(n => n <= 2), "Gün 6+ ≤ 2");
            Assert.IsTrue(all.Skip(5).Any(n => n == 2), "Gün 6+ ikinci jackpot hiç çıkmadı");
        }

        [Test]
        public void Day5PlusListings_IncludeTheFlagshipModel_Sometimes()
        {
            var h = new MarketHarness(6UL);
            int flagship = 0;
            for (int day = 5; day <= 300; day++)
            {
                flagship += h.Generate(day).Count(l => h.InstanceOf(l).DefinitionId == "phone.elma_e14_pro_max");
            }

            Assert.Greater(flagship, 50);
        }

        // ---------- determinizm ve rastgelelik yalıtımı ----------

        private static string Fingerprint(MarketHarness h, IReadOnlyList<MarketListing> listings)
        {
            return string.Join(
                "|",
                listings.Select(l =>
                {
                    ProductInstance p = h.InstanceOf(l);
                    return string.Join(
                        ",",
                        l.ListingId, l.InstanceId, l.SellerNpcId, l.AskingPrice.Tl, l.RejectPrice.Tl, l.BelievedValue.Tl, l.RemainingDays,
                        l.IsOpportunity, l.IsTrap, l.IsJackpot, l.IsGuided, string.Join("+", l.Tags),
                        p.DefinitionId, p.StorageGb, p.AgeMonths, p.GetNumber("battery"), p.GetText("screen"), p.GetNumber("body"), p.GetText("camera"));
                }));
        }

        [Test]
        public void SameSeed_GivesTheSameListings()
        {
            var a = new MarketHarness(77UL);
            var b = new MarketHarness(77UL);

            for (int day = 1; day <= 40; day++)
            {
                Assert.AreEqual(Fingerprint(a, a.Generate(day)), Fingerprint(b, b.Generate(day)), "gün " + day);
            }
        }

        [Test]
        public void DifferentSeeds_GiveDifferentListings()
        {
            var a = new MarketHarness(1UL);
            var b = new MarketHarness(2UL);

            Assert.AreNotEqual(Fingerprint(a, a.Generate(6)), Fingerprint(b, b.Generate(6)));
        }

        [Test]
        public void Generation_UsesOnlyTheGivenStream()
        {
            var h = new MarketHarness(8UL);

            h.Generate(1);
            h.Generate(2);

            CollectionAssert.AreEqual(new[] { "market" }, h.Rng.Capture().Keys.ToArray());
        }

        // ---------- hata durumları ----------

        [Test]
        public void Generate_RejectsBadArguments()
        {
            var h = new MarketHarness();

            Assert.Throws<ArgumentNullException>(() => h.Generator.Generate(1, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => h.Generator.Generate(0, h.Rng.Get("market")));
            Assert.Throws<ArgumentOutOfRangeException>(() => h.Generator.Generate(-4, h.Rng.Get("market")));
            Assert.AreEqual(0, h.Store.Count);
        }

        [Test]
        public void Constructor_RejectsNulls()
        {
            ContentDatabase content = MarketHarness.RealContent();

            Assert.Throws<ArgumentNullException>(() => new ListingGenerator(null, new InstanceStore(), new IdGenerator(), new IdGenerator()));
            Assert.Throws<ArgumentNullException>(() => new ListingGenerator(content, null, new IdGenerator(), new IdGenerator()));
            Assert.Throws<ArgumentNullException>(() => new ListingGenerator(content, new InstanceStore(), null, new IdGenerator()));
            Assert.Throws<ArgumentNullException>(() => new ListingGenerator(content, new InstanceStore(), new IdGenerator(), null));
        }

        [Test]
        public void ImpossibleOpportunityQuota_FailsWithoutChangingAnyState()
        {
            // 9 fırsat isteniyor ama Gün 2'de yalnızca 5 ilan var.
            ContentDatabase content = MarketHarness.FixtureContent(
                ContentFixtures.EconomyConstantsJson.Replace("\"minPerDay\": 2", "\"minPerDay\": 9"));
            var h = new MarketHarness(1UL, content);

            Assert.Throws<InvalidOperationException>(() => h.Generate(2));

            Assert.AreEqual(0, h.Store.Count, "depo değişmemeli");
            Assert.AreEqual(0, h.InstanceIds.LastIssued);
            Assert.AreEqual(0, h.ListingIds.LastIssued);
        }

        [Test]
        public void UnreachableOpportunity_GivesUpAfterBoundedAttempts_WithoutChangingState()
        {
            // maxRejectRatio 0,01: hiçbir ilan fırsat olamaz → sınırlı deneme sonrası hata.
            ContentDatabase content = MarketHarness.FixtureContent(
                ContentFixtures.EconomyConstantsJson.Replace("\"maxRejectRatio\": 0.90", "\"maxRejectRatio\": 0.01"));
            var h = new MarketHarness(1UL, content);

            Assert.Throws<InvalidOperationException>(() => h.Generate(2));

            Assert.AreEqual(0, h.Store.Count);
            Assert.AreEqual(0, h.InstanceIds.LastIssued);
            Assert.AreEqual(0, h.ListingIds.LastIssued);
        }

        [Test]
        public void FixtureContent_AlsoHoldsTheQuotas()
        {
            var h = new MarketHarness(4UL, MarketHarness.FixtureContent());

            for (int day = 1; day <= 200; day++)
            {
                IReadOnlyList<MarketListing> listings = h.Generate(day);
                Assert.GreaterOrEqual(listings.Count(l => l.IsOpportunity), day == 1 ? 1 : 2, "gün " + day);
                if (day >= 5)
                {
                    Assert.GreaterOrEqual(listings.Count(l => l.IsTrap), 1, "gün " + day);
                }
            }
        }
    }
}
