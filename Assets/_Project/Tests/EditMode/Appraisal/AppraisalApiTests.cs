using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Esnaf.Core;
using Esnaf.Domain.Appraisal;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Game;
using Esnaf.Domain.Market;
using Esnaf.Domain.Products;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Appraisal
{
    /// <summary>Ekspertiz komutu ve sorguları IGameApi üzerinden, gerçek içerik (kalibre edilmiş) ile.</summary>
    public class AppraisalApiTests
    {
        private static GameSession New(ulong seed = 42UL, IEventBus bus = null)
        {
            return GameSession.NewGame(MarketHarness.RealContent(), seed, bus);
        }

        private static long GuidedListingId(GameSession s)
        {
            return s.Market.Listings.Single(l => l.IsGuided).ListingId;
        }

        /// <summary>Bugün ilan edilen, dolayısıyla en az 2 gün pazarda kalacak bir ilan (Gün 1 ilanları Gün 3'e kadar yaşamayabilir).</summary>
        private static long FreshListingId(GameSession s)
        {
            return s.Market.Listings.First(l => l.DayListed == s.Api.GetDay()).ListingId;
        }

        private static Money FeeFor(GameSession s, long listingId, string level)
        {
            MarketListing listing = s.Market.Listings.Single(l => l.ListingId == listingId);
            ProductDefinition def = s.Content.GetProduct(s.Store.Get(listing.InstanceId).DefinitionId);
            return s.Content.Appraisal.GetLevel(level).FeeFor(def.Segment);
        }

        private static void PassDays(GameSession s, int days)
        {
            for (int i = 0; i < days; i++)
            {
                Assert.IsTrue(s.Api.EndDay().IsSuccess);
            }
        }

        // ---------- komut ----------

        [Test]
        public void StartAppraisal_S0_OnDayOne_IsFreeAndReturnsFindingsAndRanges()
        {
            GameSession s = New();
            long listing = GuidedListingId(s);

            Result<AppraisalView> result = s.Api.StartAppraisal(listing, "s0");

            Assert.IsTrue(result.IsSuccess, result.ToString());
            AppraisalView v = result.Value;
            Assert.AreEqual(listing, v.ListingId);
            Assert.AreEqual("s0", v.LevelId);
            Assert.AreEqual(1, v.Day);
            Assert.AreEqual(Money.Zero, v.Fee);
            Assert.AreEqual(2, v.Findings.Count);
            Assert.IsNull(v.ValueRange);
            Assert.IsNull(v.BatteryRange);
            Assert.IsNotNull(v.BodyRange);
            Assert.AreEqual(Money.FromTl(250000), s.Api.GetCash());
        }

        [Test]
        public void StartAppraisal_S1_OnDayThree_ChargesTheSegmentFee()
        {
            GameSession s = New();
            PassDays(s, 2);
            long listing = FreshListingId(s);
            Money fee = FeeFor(s, listing, "s1");
            Money cashBefore = s.Api.GetCash();

            Result<AppraisalView> result = s.Api.StartAppraisal(listing, "s1");

            Assert.IsTrue(result.IsSuccess, result.ToString());
            Assert.IsTrue(fee == Money.FromTl(100) || fee == Money.FromTl(200) || fee == Money.FromTl(300), "S1 ücreti segmente göre 100/200/300");
            Assert.AreEqual(fee, result.Value.Fee);
            Assert.AreEqual(cashBefore - fee, s.Api.GetCash());
            Assert.AreEqual(3, result.Value.Day);
            Assert.IsNotNull(result.Value.ValueRange);
            Assert.IsNotNull(result.Value.BatteryRange);
            Assert.AreEqual(fee, s.EconomyService.PendingAppraisalCost(s.Market.Listings.Single(l => l.ListingId == listing).InstanceId));
        }

        [Test]
        public void StartAppraisal_LockedLevel_And_UnknownListing_AreRejected()
        {
            GameSession s = New();
            long listing = GuidedListingId(s);

            Assert.AreEqual("appraisal.level_locked", s.Api.StartAppraisal(listing, "s1").ErrorCode, "S1 Gün 3'te açılır");
            Assert.AreEqual("appraisal.level_unknown", s.Api.StartAppraisal(listing, "s7").ErrorCode);
            Assert.AreEqual("listing.unknown", s.Api.StartAppraisal(99999, "s0").ErrorCode);
            Assert.AreEqual(Money.FromTl(250000), s.Api.GetCash());
        }

        [Test]
        public void StartAppraisal_S3_NeedsTheTestDevice()
        {
            GameSession s = New();
            long listing = GuidedListingId(s);
            PassDays(s, 5);
            long fresh = s.Market.Listings.First(l => l.DayListed == 6).ListingId;

            Assert.AreEqual("appraisal.equipment_missing", s.Api.StartAppraisal(fresh, "s3").ErrorCode);
            s.Equipment.Grant("test_device");
            Assert.IsTrue(s.Api.StartAppraisal(fresh, "s3").IsSuccess);
            Assert.IsNotNull(listing);
        }

        [Test]
        public void StartAppraisal_Twice_ReturnsTheSameLockedResult()
        {
            GameSession s = New();
            PassDays(s, 2);
            long listing = FreshListingId(s);
            AppraisalView first = s.Api.StartAppraisal(listing, "s1").Value;
            Money cash = s.Api.GetCash();

            AppraisalView again = s.Api.StartAppraisal(listing, "s1").Value;

            Assert.AreEqual(first.ResultId, again.ResultId);
            Assert.AreEqual(cash, s.Api.GetCash(), "ikinci çağrı ücret almaz");
            CollectionAssert.AreEqual(
                first.Findings.Select(f => f.Attribute + f.Found).ToArray(), again.Findings.Select(f => f.Attribute + f.Found).ToArray());
            Assert.AreEqual(first.ValueRange.Min, again.ValueRange.Min);
            Assert.AreEqual(first.ValueRange.Max, again.ValueRange.Max);
        }

        [Test]
        public void StartAppraisal_InsufficientCash_Fails_AndChangesNothing()
        {
            GameSession s = New();
            PassDays(s, 2);
            long listing = FreshListingId(s);
            Assert.IsTrue(s.EconomyService.RecordInvestment(s.Api.GetCash() - Money.FromTl(50), 3).IsSuccess);
            string before = GameStateDigest.Describe(s);

            Result<AppraisalView> result = s.Api.StartAppraisal(listing, "s1");

            Assert.AreEqual("cash.insufficient", result.ErrorCode);
            Assert.AreEqual(before, GameStateDigest.Describe(s));
        }

        // ---------- sorgular ----------

        [Test]
        public void GetAppraisals_ListsTheKnowledgeOfAListing_ByResultId()
        {
            GameSession s = New();
            PassDays(s, 2);
            long listing = FreshListingId(s);
            Assert.AreEqual(0, s.Api.GetAppraisals(listing).Count);
            s.Api.StartAppraisal(listing, "s0");
            s.Api.StartAppraisal(listing, "s1");

            IReadOnlyList<AppraisalView> views = s.Api.GetAppraisals(listing);

            CollectionAssert.AreEqual(new[] { "s0", "s1" }, views.Select(v => v.LevelId).ToArray());
            Assert.IsTrue(views.All(v => v.ListingId == listing));
            Assert.AreEqual(0, s.Api.GetAppraisals(99999).Count, "bilinmeyen ilan: boş liste");
            Assert.IsFalse(views is List<AppraisalView>);
        }

        [Test]
        public void GetRiskCard_UsesTheStoredValueRange()
        {
            GameSession s = New();
            PassDays(s, 2);
            long listing = FreshListingId(s);
            AppraisalView v = s.Api.StartAppraisal(listing, "s1").Value;
            double factor = s.Content.Appraisal.ExpectedSaleFactor;

            Result<RiskCard> result = s.Api.GetRiskCard(v.ResultId, Money.FromTl(5000));

            Assert.IsTrue(result.IsSuccess, result.ToString());
            RiskCard card = result.Value;
            Assert.AreEqual(v.ResultId, card.AppraisalId);
            Assert.AreEqual(v.ValueRange.Min, card.Scenarios[0].TrueValue);
            Assert.AreEqual(v.ValueRange.Max, card.Scenarios[2].TrueValue);
            Assert.AreEqual(Money.FromDoubleRoundedTo10(v.ValueRange.Min.Tl * factor), card.Scenarios[0].ExpectedSale);
            Assert.AreEqual(card.Scenarios[0].ExpectedSale - Money.FromTl(5000), card.Scenarios[0].Profit);
            Assert.AreEqual(0.15, card.MissProbability, 1e-12);
        }

        [Test]
        public void GetRiskCard_Errors()
        {
            GameSession s = New();
            long guided = GuidedListingId(s);
            AppraisalView s0 = s.Api.StartAppraisal(guided, "s0").Value;

            Assert.AreEqual("appraisal.unknown", s.Api.GetRiskCard(999, Money.FromTl(1000)).ErrorCode);
            Assert.AreEqual("appraisal.no_value_range", s.Api.GetRiskCard(s0.ResultId, Money.FromTl(1000)).ErrorCode);
            PassDays(s, 2);
            AppraisalView s1 = s.Api.StartAppraisal(FreshListingId(s), "s1").Value;
            Assert.AreEqual("offer.invalid", s.Api.GetRiskCard(s1.ResultId, Money.FromTl(1005)).ErrorCode);
        }

        [Test]
        public void AppraisalQueries_DoNotChangeTheState()
        {
            GameSession s = New();
            PassDays(s, 2);
            long listing = FreshListingId(s);
            AppraisalView v = s.Api.StartAppraisal(listing, "s1").Value;
            string before = GameStateDigest.Describe(s);

            s.Api.GetAppraisals(listing);
            s.Api.GetRiskCard(v.ResultId, Money.FromTl(5000));
            s.Api.GetRiskCard(999, Money.FromTl(5000));

            Assert.AreEqual(before, GameStateDigest.Describe(s));
        }

        // ---------- gizli bilgi ----------

        [Test]
        public void AppraisalViews_CarryNoHiddenMembers()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    "BatteryRange", "BodyRange", "Cards", "Day", "Fee", "Findings", "InstanceId", "LevelId", "ListingId", "ResultId", "ValueRange"
                },
                PropertyNames(typeof(AppraisalView)));
            CollectionAssert.AreEqual(new[] { "Attribute", "Confidence", "EvidencePower", "Found", "WordingKey" }, PropertyNames(typeof(FindingView)));
            CollectionAssert.AreEqual(new[] { "Attribute", "Confidence", "EvidencePower", "ProblemValue", "WordingKey" }, PropertyNames(typeof(CardView)));
            foreach (Type t in new[] { typeof(AppraisalView), typeof(FindingView), typeof(CardView) })
            {
                Assert.AreEqual(0, t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).Length, t.Name);
            }
        }

        private static string[] PropertyNames(Type type)
        {
            return type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        }

        [Test]
        public void Views_MirrorTheStoredResult_WithoutTheFalseAlarmFlag()
        {
            GameSession s = New(7UL);
            PassDays(s, 4);
            s.Equipment.Grant("test_device");
            PassDays(s, 1);
            foreach (MarketListing listing in s.Market.Listings.Take(6))
            {
                AppraisalView v = s.Api.StartAppraisal(listing.ListingId, "s3").Value;
                AppraisalResult r;
                Assert.IsTrue(s.Knowledge.TryGetById(v.ResultId, out r));

                Assert.AreEqual(r.LevelId, v.LevelId);
                Assert.AreEqual(r.Fee, v.Fee);
                Assert.AreEqual(r.Day, v.Day);
                Assert.AreEqual(r.InstanceId, v.InstanceId);
                Assert.AreEqual(r.Findings.Count, v.Findings.Count);
                for (int i = 0; i < r.Findings.Count; i++)
                {
                    Assert.AreEqual(r.Findings[i].Attribute, v.Findings[i].Attribute);
                    Assert.AreEqual(r.Findings[i].Found, v.Findings[i].Found);
                    Assert.AreEqual(r.Findings[i].Confidence, v.Findings[i].Confidence);
                    Assert.AreEqual(r.Findings[i].WordingKey, v.Findings[i].WordingKey);
                    Assert.AreEqual(r.Findings[i].EvidencePower, v.Findings[i].EvidencePower, 1e-12);
                }

                Assert.AreEqual(r.Cards.Count, v.Cards.Count);
                for (int i = 0; i < r.Cards.Count; i++)
                {
                    Assert.AreEqual(r.Cards[i].Attribute, v.Cards[i].Attribute);
                    Assert.AreEqual(r.Cards[i].ProblemValue, v.Cards[i].ProblemValue);
                    Assert.AreEqual(r.Cards[i].EvidencePower, v.Cards[i].EvidencePower, 1e-12);
                }

                Assert.AreEqual(r.ValueRange.Min, v.ValueRange.Min);
                Assert.AreEqual(r.BatteryRange.Max, v.BatteryRange.Max);
                Assert.AreEqual(r.BodyRange.Min, v.BodyRange.Min);
            }
        }

        [Test]
        public void GetAppraisals_ReturnsOnlyThatListingsResults()
        {
            GameSession s = New(12UL);
            PassDays(s, 2);
            long a = s.Market.Listings.Where(l => l.DayListed == 3).ElementAt(0).ListingId;
            long b = s.Market.Listings.Where(l => l.DayListed == 3).ElementAt(1).ListingId;
            s.Api.StartAppraisal(a, "s1");
            s.Api.StartAppraisal(b, "s0");
            s.Api.StartAppraisal(b, "s1");

            Assert.AreEqual(1, s.Api.GetAppraisals(a).Count);
            Assert.AreEqual(2, s.Api.GetAppraisals(b).Count);
            Assert.IsTrue(s.Api.GetAppraisals(a).All(v => v.ListingId == a));
            Assert.IsTrue(s.Api.GetAppraisals(b).All(v => v.ListingId == b));
        }

        [Test]
        public void Views_CarryTheLevelsEvidencePower_ForLowerLevelsToo()
        {
            GameSession s = New(13UL);
            PassDays(s, 4);
            int checkedCards = 0;
            foreach (MarketListing l in s.Market.Listings.Where(x => x.DayListed == 5))
            {
                foreach (string level in new[] { "s0", "s1", "s2" })
                {
                    AppraisalView v = s.Api.StartAppraisal(l.ListingId, level).Value;
                    double power = s.Content.Appraisal.GetLevel(level).EvidencePower;
                    foreach (CardView c in v.Cards)
                    {
                        Assert.AreEqual(power, c.EvidencePower, 1e-12, level);
                        checkedCards++;
                    }

                    foreach (FindingView f in v.Findings)
                    {
                        Assert.AreEqual(power, f.EvidencePower, 1e-12, level);
                    }
                }
            }

            Assert.Greater(checkedCards, 0);
        }

        [Test]
        public void Results_UseTheSessionsGameSeed()
        {
            GameSession s = New(77UL);
            long listing = GuidedListingId(s);

            AppraisalView v = s.Api.StartAppraisal(listing, "s0").Value;

            AppraisalResult r;
            Assert.IsTrue(s.Knowledge.TryGetById(v.ResultId, out r));
            Assert.AreEqual(AppraisalSeed.Compute(77UL, r.InstanceId, "s0"), r.Seed);
        }

        [Test]
        public void Digest_CarriesTheFee_AndTheHiddenFalseAlarmFlags()
        {
            GameSession s = New(42UL);
            PassDays(s, 2);
            long listing = FreshListingId(s);
            AppraisalView v = s.Api.StartAppraisal(listing, "s1").Value;
            string feeField = Describe(s, "A|").Split('|')[5];
            Assert.AreEqual(v.Fee.Tl.ToString(), feeField, "ücret özette");
            Assert.IsTrue(v.Fee.IsPositive);

            // Gizli yanlış-alarm bayrakları (bulgu ve kart) özete girer: elle bir sonuç eklenir.
            s.Knowledge.Add(new AppraisalResult(
                99, 12345, "phone.x", "s2", 4, Money.Zero, 5UL,
                new[]
                {
                    new AttributeFinding("camera", "w", true, AppraisalConfidence.Medium, 0.7, true),
                    new AttributeFinding("screen", "w", true, AppraisalConfidence.Medium, 0.7, false)
                },
                null, null, null,
                new[] { new TrumpCard("camera", "w", AppraisalConfidence.Medium, 0.7, Money.FromTl(1770), true) }));

            string line = GameStateDigest.Describe(s).Split('\n').Single(l => l.StartsWith("A|99|", StringComparison.Ordinal));
            string[] f = line.Split('|');
            Assert.AreEqual("camera:True:True:Medium;screen:True:False:Medium", f[7], "bulgu bayrakları");
            Assert.AreEqual("camera:1770:True", f[11], "kart bayrağı");
        }

        private static string Describe(GameSession s, string prefix)
        {
            return GameStateDigest.Describe(s).Split('\n').First(l => l.StartsWith(prefix, StringComparison.Ordinal));
        }

        // ---------- determinizm (I6) ve özet ----------

        [Test]
        public void I6_SameSeedSameCommands_SameDigest_WithAppraisals()
        {
            GameSession a = New(2025UL);
            GameSession b = New(2025UL);
            for (int day = 1; day <= 12; day++)
            {
                foreach (GameSession s in new[] { a, b })
                {
                    foreach (MarketListing l in s.Market.Listings.Where(x => x.DayListed == s.Api.GetDay()).Take(2))
                    {
                        s.Api.StartAppraisal(l.ListingId, "s0");
                        s.Api.StartAppraisal(l.ListingId, "s1");
                    }

                    Assert.IsTrue(s.Api.EndDay().IsSuccess);
                }

                Assert.AreEqual(a.Api.GetStateDigest(), b.Api.GetStateDigest(), "gün " + day);
            }
        }

        [Test]
        public void Digest_ContainsTheKnowledge_Counter_AndEquipment()
        {
            GameSession s = New(42UL);
            long listing = GuidedListingId(s);
            string before = GameStateDigest.Compute(s);
            s.Api.StartAppraisal(listing, "s0");
            s.Equipment.Grant("test_device");

            string[] lines = GameStateDigest.Describe(s).Split('\n');

            Assert.AreNotEqual(before, GameStateDigest.Compute(s));
            CollectionAssert.Contains(lines, "ids.appraisal=1");
            CollectionAssert.Contains(lines, "E|test_device");
            string[] a = lines.Single(l => l.StartsWith("A|", StringComparison.Ordinal)).Split('|');
            AppraisalResult r;
            s.Knowledge.TryGetById(1, out r);
            Assert.AreEqual("1", a[1]);
            Assert.AreEqual(r.InstanceId.ToString(), a[2]);
            Assert.AreEqual("s0", a[3]);
            Assert.AreEqual("1", a[4]);
            Assert.AreEqual("0", a[5], "ücret");
            Assert.AreEqual(r.Seed.ToString(), a[6]);
            StringAssert.Contains("screen:", a[7]);
        }

        [Test]
        public void Digest_ChangesWithEveryPartOfTheResult()
        {
            GameSession s = New(42UL);
            PassDays(s, 2);
            s.Api.StartAppraisal(FreshListingId(s), "s1");
            AppraisalResult r;
            s.Knowledge.TryGetById(1, out r);
            string baseline = GameStateDigest.Describe(s);
            string aLine = baseline.Split('\n').Single(l => l.StartsWith("A|", StringComparison.Ordinal));
            string[] f = aLine.Split('|');

            Assert.AreEqual(r.Findings.Count(x => x.Found).ToString(), f[7].Split(';').Count(x => x.Contains(":True")).ToString());
            StringAssert.Contains(r.BatteryRange.Min + ".." + r.BatteryRange.Max, aLine);
            StringAssert.Contains(r.BodyRange.Min + ".." + r.BodyRange.Max, aLine);
            StringAssert.Contains(r.ValueRange.Min.Tl + ".." + r.ValueRange.Max.Tl, aLine);
            foreach (TrumpCard c in r.Cards)
            {
                StringAssert.Contains(c.Attribute + ":" + c.ProblemValue.Tl, aLine);
            }
        }

        // ---------- boşa ekspertiz: ilan kalkınca ----------

        [Test]
        public void ExpiredListing_WritesOffItsAppraisalFee_AsTheDaysLoss()
        {
            GameSession s = New(9UL);
            PassDays(s, 2);
            MarketListing target = s.Market.Listings.Where(l => l.DayListed == 3).First();
            long instanceId = target.InstanceId;
            s.Api.StartAppraisal(target.ListingId, "s1");
            Money fee = s.EconomyService.PendingAppraisalCost(instanceId);
            Assert.IsTrue(fee.IsPositive);
            Money wealthBefore = s.Wealth.Calculate().Total;

            int guard = 0;
            DayEndReport report = null;
            while (s.Market.Listings.Any(l => l.ListingId == target.ListingId) && guard++ < 10)
            {
                report = s.Api.EndDay().Value;
            }

            Assert.AreEqual(Money.Zero, s.EconomyService.PendingAppraisalCost(instanceId), "bekleyen ücret gider yazıldı");
            TransactionRecord wasted = s.EconomyState.Ledger.Records.Single(r => r.TypeId == "wasted_appraisal");
            Assert.AreEqual(report.EndedDay, wasted.Day, "gider, ilanın kalktığı günün defterine yazılır");
            Assert.AreEqual(instanceId, wasted.InstanceId);
            Assert.IsTrue(s.EconomyState.Ledger.IsWrittenOff(wasted.RelatedRecordId.Value));
            Assert.Less(s.Wealth.Calculate().Total.Tl, wealthBefore.Tl, "servet boşa ekspertiz kadar (ve günlük giderler kadar) düştü");
            Assert.IsTrue(report.Summary.NetProfit <= Money.FromTl(-500) - fee, "kalktığı günün net kârı gider + boşa ekspertiz");
            ProductInstance ignored;
            Assert.IsFalse(s.Store.TryGet(instanceId, out ignored), "ürün depodan da silindi");
            AppraisalResult stillKnown;
            Assert.IsTrue(s.Knowledge.TryGet(instanceId, "s1", out stillKnown), "bilgi (KnowledgeState) kalır");
        }

        [Test]
        public void ExpiredListing_OfAnItemTheShopBought_KeepsTheFeeInTheCostBasis()
        {
            GameSession s = New(9UL);
            PassDays(s, 2);
            MarketListing target = s.Market.Listings.Where(l => l.DayListed == 3).First();
            s.Api.StartAppraisal(target.ListingId, "s1");
            Money fee = s.EconomyService.PendingAppraisalCost(target.InstanceId);
            Assert.IsTrue(s.InventoryService.Acquire(target.InstanceId, Money.FromTl(1000), 3).IsSuccess);
            ProductInstance owned = s.Store.Get(target.InstanceId);

            PassDays(s, 5);

            Assert.AreEqual(Money.FromTl(1000) + fee, owned.CostBasis, "alınan ürünün maliyeti alış + ekspertiz");
            Assert.AreEqual(0, s.EconomyState.Ledger.Records.Count(r => r.TypeId == "wasted_appraisal"));
        }

        // ---------- uzun koşu ----------

        [Test]
        public void ThreeHundredDaysOfAppraisals_KeepTheInvariants()
        {
            GameSession s = New(31UL);
            s.Equipment.Grant("test_device");
            var rng = new PcgRandom(31UL, 77UL);
            string[] levels = { "s0", "s1", "s2", "s3" };

            for (int day = 1; day <= 300; day++)
            {
                foreach (MarketListing l in s.Market.Listings.ToList())
                {
                    if (rng.Chance(0.25))
                    {
                        s.Api.StartAppraisal(l.ListingId, levels[rng.NextInt(levels.Length)]);
                    }
                }

                MarketListing pick = s.Market.Listings.Count > 0 ? s.Market.Listings[rng.NextInt(s.Market.Listings.Count)] : null;
                if (pick != null && rng.Chance(0.2) && s.InventoryState.Count < s.InventoryState.Capacity && s.Store.Get(pick.InstanceId).Location == ProductLocation.Market)
                {
                    if (s.EconomyState.Cash >= pick.AskingPrice && s.InventoryService.Acquire(pick.InstanceId, pick.AskingPrice, day).IsSuccess)
                    {
                        s.Market.Remove(pick.ListingId);
                    }
                }

                Assert.IsTrue(s.Api.EndDay().IsSuccess);
                AssertLedgerInvariants(s, day + 1);
            }

            Assert.Greater(s.Knowledge.All.Count, 100);
        }

        private static void AssertLedgerInvariants(GameSession s, int day)
        {
            string where = " (gün " + day + ")";
            Assert.AreEqual(s.EconomyState.Ledger.Records.Sum(r => r.Amount.Tl), s.EconomyState.Cash.Tl, "I1" + where);
            Assert.IsTrue(s.EconomyState.Ledger.Verify().IsSuccess, "defter zinciri" + where);
            long netTotal = 0;
            for (int d = 0; d <= day; d++)
            {
                netTotal += s.Summaries.NetProfit(d).Tl;
            }

            Assert.AreEqual(250000 + netTotal, s.Wealth.Calculate().Total.Tl, "I2 servet kimliği" + where);

            // Her ekspertiz satırı ya bekliyor (ürün hâlâ elimizde/pazarda), ya ürüne eklendi (ürün dükkânda) ya da gider yazıldı.
            foreach (TransactionRecord row in s.EconomyState.Ledger.Records.Where(r => r.TypeId == "appraisal"))
            {
                ProductInstance instance;
                if (!s.Store.TryGet(row.InstanceId.Value, out instance))
                {
                    Assert.IsTrue(s.EconomyState.Ledger.IsWrittenOff(row.Id), "silinmiş ürünün ekspertizi gider yazılmalı #" + row.Id + where);
                }
            }

            foreach (ProductInstance instance in s.Store.All)
            {
                foreach (long recordId in s.EconomyState.GetPendingAppraisalRecordIds(instance.InstanceId))
                {
                    Assert.IsFalse(s.EconomyState.Ledger.IsWrittenOff(recordId), "bekleyen ücret aynı anda gider yazılmış olamaz" + where);
                }
            }
        }
    }
}
