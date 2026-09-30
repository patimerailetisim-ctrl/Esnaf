using System;
using System.Collections.Generic;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Appraisal;
using Esnaf.Domain.Content;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Products;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Appraisal
{
    public class AppraisalServiceTests
    {
        private static string Fingerprint(AppraisalResult r)
        {
            return string.Join(
                "|",
                r.LevelId, r.Day, r.Fee.Tl, r.Seed,
                string.Join(",", r.Findings.Select(f => f.Attribute + ":" + f.Found + ":" + f.IsFalseAlarm + ":" + f.Confidence)),
                r.BatteryRange == null ? "-" : r.BatteryRange.Min + ".." + r.BatteryRange.Max,
                r.BodyRange == null ? "-" : r.BodyRange.Min + ".." + r.BodyRange.Max,
                r.ValueRange == null ? "-" : r.ValueRange.Min.Tl + ".." + r.ValueRange.Max.Tl,
                string.Join(",", r.Cards.Select(c => c.Attribute + ":" + c.ProblemValue.Tl + ":" + c.IsFalseAlarm)));
        }

        // ---------- tohum ----------

        [Test]
        public void Seed_IsTheFnv1a64OfTheDocumentedText_ForEachMasterSeedInstanceAndLevel()
        {
            Assert.AreEqual(13480834060610774364UL, AppraisalSeed.Compute(42UL, 7, "s2"), "Python referans modeli");
            Assert.AreEqual(5949498457534864408UL, AppraisalSeed.Compute(1UL, 1, "s1"));
            Assert.AreNotEqual(AppraisalSeed.Compute(42UL, 7, "s2"), AppraisalSeed.Compute(43UL, 7, "s2"));
            Assert.AreNotEqual(AppraisalSeed.Compute(42UL, 7, "s2"), AppraisalSeed.Compute(42UL, 8, "s2"));
            Assert.AreNotEqual(AppraisalSeed.Compute(42UL, 7, "s2"), AppraisalSeed.Compute(42UL, 7, "s3"));
            Assert.AreEqual(AppraisalSeed.Compute(42UL, 7, "s2"), AppraisalSeed.Compute(42UL, 7, "s2"));
        }

        // ---------- seed'li golden'lar (Python referansı, sıfır sapma/gürültü) ----------

        [Test]
        public void SeededGolden_S1Detected()
        {
            var rig = new AppraisalRig(42UL);
            ProductInstance phone = rig.Listing();
            Assert.AreEqual(1, phone.InstanceId);

            AppraisalResult r = rig.Service.Appraise(phone.InstanceId, "s1", 3).Value;

            Assert.IsTrue(r.Findings[0].Found);
            Assert.IsFalse(r.Findings[1].Found);
            Assert.AreEqual(Money.FromTl(7700), r.ValueRange.Min);
            Assert.AreEqual(Money.FromTl(9410), r.ValueRange.Max);
            Assert.AreEqual(68, r.BatteryRange.Min);
            Assert.AreEqual(88, r.BatteryRange.Max);
            Assert.AreEqual(75, r.BodyRange.Min);
            Assert.AreEqual(95, r.BodyRange.Max);
        }

        [Test]
        public void SeededGolden_S1MissedTheDefect()
        {
            var rig = new AppraisalRig(7UL);
            rig.Listing();
            rig.Listing();
            ProductInstance phone = rig.Listing();
            Assert.AreEqual(3, phone.InstanceId);

            AppraisalResult r = rig.Service.Appraise(3, "s1", 3).Value;

            Assert.IsFalse(r.Findings[0].Found, "ekran değişmiş ama kaçırıldı");
            Assert.AreEqual(Money.FromTl(8100), r.ValueRange.Min);
            Assert.AreEqual(Money.FromTl(9900), r.ValueRange.Max);
            Assert.AreEqual(0, r.Cards.Count);
        }

        [Test]
        public void SeededGolden_S1FalseAlarmOnTheCamera()
        {
            var rig = new AppraisalRig(7UL);
            for (int i = 0; i < 6; i++)
            {
                rig.Listing();
            }

            AppraisalResult r = rig.Service.Appraise(6, "s1", 3).Value;

            Assert.IsTrue(r.Findings[0].Found);
            Assert.IsTrue(r.Findings[1].Found);
            Assert.IsTrue(r.Findings[1].IsFalseAlarm);
            Assert.AreEqual(Money.FromTl(7480), r.ValueRange.Min);
            Assert.AreEqual(Money.FromTl(9140), r.ValueRange.Max);
            Assert.AreEqual(2, r.Cards.Count);
        }

        // ---------- ücret, defter, servet (T2/T3) ----------

        [Test]
        public void S0_IsFree_LeavesNoLedgerRow()
        {
            var rig = new AppraisalRig();
            ProductInstance phone = rig.Listing();

            Result<AppraisalResult> result = rig.Service.Appraise(phone.InstanceId, "s0", 1);

            Assert.IsTrue(result.IsSuccess, result.ToString());
            Assert.AreEqual(Money.Zero, result.Value.Fee);
            Assert.AreEqual(1, rig.Economy.State.Ledger.Count, "yalnızca başlangıç sermayesi");
            Assert.AreEqual(Money.FromTl(250000), rig.Economy.Cash);
            Assert.AreEqual(Money.Zero, rig.Economy.Economy.PendingAppraisalCost(phone.InstanceId));
        }

        [Test]
        public void PaidLevel_ChargesTheSegmentFee_AsAPendingAppraisal()
        {
            var rig = new AppraisalRig();
            ProductInstance phone = rig.Listing();

            AppraisalResult r = rig.Service.Appraise(phone.InstanceId, "s1", 3).Value;

            Assert.AreEqual(Money.FromTl(200), r.Fee, "Orta segment S1 = 200 TL");
            Assert.AreEqual(Money.FromTl(249800), rig.Economy.Cash, "ücret anında ödenir");
            Assert.AreEqual(Money.FromTl(200), rig.Economy.Economy.PendingAppraisalCost(phone.InstanceId));
            TransactionRecord row = rig.Economy.State.Ledger.Records.Last();
            Assert.AreEqual("appraisal", row.TypeId);
            Assert.AreEqual(Money.FromTl(-200), row.Amount);
            Assert.AreEqual(phone.InstanceId, row.InstanceId);
            Assert.AreEqual(3, row.Day);
            Assert.AreEqual(Money.FromTl(250000), rig.Economy.TotalWealth(), "bekleyen ücret varlıktır: servet değişmez");
        }

        private static readonly object[][] FeeTable =
        {
            new object[] { "s1", "phone.test_one", 200L },
            new object[] { "s2", "phone.test_one", 600L },
            new object[] { "s3", "phone.test_one", 1200L }
        };

        [TestCaseSource(nameof(FeeTable))]
        public void Fee_FollowsTheLevelAndSegment(string level, string definition, long expected)
        {
            var rig = new AppraisalRig();
            rig.Equipment.Grant("test_device");
            ProductInstance phone = rig.Listing(definition);

            AppraisalResult r = rig.Service.Appraise(phone.InstanceId, level, 10).Value;

            Assert.AreEqual(Money.FromTl(expected), r.Fee);
        }

        [Test]
        public void Fee_OfEverySegment_MatchesTheGddTable()
        {
            ContentDatabase real = MarketHarness.RealContent();
            var rig = new AppraisalRig(1UL, real);
            rig.Equipment.Grant("test_device");
            var expected = new Dictionary<string, long[]>
            {
                { "s1", new long[] { 100, 200, 300 } },
                { "s2", new long[] { 350, 600, 1000 } },
                { "s3", new long[] { 700, 1200, 2000 } }
            };
            string[] definitions = { "phone.yildiz_y5", "phone.nova_n3_pro", "phone.elma_e13_pro" };
            for (int segment = 0; segment < 3; segment++)
            {
                foreach (string level in new[] { "s1", "s2", "s3" })
                {
                    ProductInstance phone = rig.Listing(definitions[segment]);

                    AppraisalResult r = rig.Service.Appraise(phone.InstanceId, level, 10).Value;

                    Assert.AreEqual(Money.FromTl(expected[level][segment]), r.Fee, level + " " + definitions[segment]);
                }
            }
        }

        [Test]
        public void WastedAppraisal_CostsTheFee_AsTheDaysNetLoss_T3()
        {
            var rig = new AppraisalRig();
            ProductInstance phone = rig.Listing("phone.test_one");
            rig.Service.Appraise(phone.InstanceId, "s1", 3);
            Money fee = rig.Economy.Economy.PendingAppraisalCost(phone.InstanceId);

            Result<Money> wasted = rig.Economy.Economy.WriteOffAppraisals(phone.InstanceId, 3);

            Assert.IsTrue(wasted.IsSuccess);
            Assert.AreEqual(fee, wasted.Value);
            Assert.AreEqual(Money.FromTl(249800), rig.Economy.Cash);
            Assert.AreEqual(Money.FromTl(-200), rig.Economy.Summaries.NetProfit(3), "boşa ekspertiz günün net kârını düşürür");
            Assert.AreEqual(Money.FromTl(249800), rig.Economy.TotalWealth());
        }

        [Test]
        public void BoughtAfterAppraisal_TheFeeJoinsTheCostBasis_T2()
        {
            var rig = new AppraisalRig();
            ProductInstance phone = rig.Listing("phone.test_one");
            rig.Service.Appraise(phone.InstanceId, "s2", 5);

            Result bought = rig.Economy.Shop.Acquire(phone.InstanceId, Money.FromTl(7000), 5);

            Assert.IsTrue(bought.IsSuccess, bought.ToString());
            Assert.AreEqual(Money.FromTl(7600), phone.CostBasis, "alış 7.000 + ekspertiz 600");
            Assert.AreEqual(Money.FromTl(250000 - 600 - 7000), rig.Economy.Cash);
            Assert.AreEqual(Money.Zero, rig.Economy.Economy.PendingAppraisalCost(phone.InstanceId));
        }

        [Test]
        public void InsufficientCash_FailsWithoutAnyChange()
        {
            var rig = new AppraisalRig();
            ProductInstance phone = rig.Listing();
            Assert.IsTrue(rig.Economy.Economy.RecordInvestment(Money.FromTl(249900), 1).IsSuccess); // nakit 100 TL kaldı
            int ledgerRows = rig.Economy.State.Ledger.Count;

            Result<AppraisalResult> result = rig.Service.Appraise(phone.InstanceId, "s1", 3);

            Assert.IsTrue(result.IsFailure);
            Assert.AreEqual("cash.insufficient", result.ErrorCode);
            Assert.AreEqual(ledgerRows, rig.Economy.State.Ledger.Count);
            Assert.AreEqual(0, rig.Knowledge.All.Count, "başarısız ekspertiz sonuç bırakmaz");
            Assert.AreEqual(0, rig.ResultIds.LastIssued, "kimlik tüketilmez");
            AppraisalResult ignored;
            Assert.IsFalse(rig.Knowledge.TryGet(phone.InstanceId, "s1", out ignored));
        }

        // ---------- hata durumları ----------

        [Test]
        public void UnknownInstance_IsRejected()
        {
            var rig = new AppraisalRig();

            Result<AppraisalResult> result = rig.Service.Appraise(999, "s0", 1);

            Assert.AreEqual("instance.unknown", result.ErrorCode);
        }

        [Test]
        public void InstanceNotOnTheMarket_IsRejected()
        {
            var rig = new AppraisalRig();
            ProductInstance phone = rig.Listing();
            Assert.IsTrue(rig.Economy.Shop.Acquire(phone.InstanceId, Money.FromTl(1000), 1).IsSuccess);

            Result<AppraisalResult> result = rig.Service.Appraise(phone.InstanceId, "s0", 1);

            Assert.AreEqual("appraisal.not_on_market", result.ErrorCode);
            Assert.AreEqual(0, rig.Knowledge.All.Count);
        }

        [Test]
        public void UnknownLevel_IsRejected()
        {
            var rig = new AppraisalRig();
            ProductInstance phone = rig.Listing();

            Assert.AreEqual("appraisal.level_unknown", rig.Service.Appraise(phone.InstanceId, "s9", 5).ErrorCode);
            Assert.AreEqual("appraisal.level_unknown", rig.Service.Appraise(phone.InstanceId, null, 5).ErrorCode);
        }

        [TestCase("s0", 1, true)]
        [TestCase("s1", 2, false)]
        [TestCase("s1", 3, true)]
        [TestCase("s2", 4, false)]
        [TestCase("s2", 5, true)]
        public void Levels_UnlockOnTheirDay(string level, int day, bool allowed)
        {
            var rig = new AppraisalRig();
            ProductInstance phone = rig.Listing();

            Result<AppraisalResult> result = rig.Service.Appraise(phone.InstanceId, level, day);

            Assert.AreEqual(allowed, result.IsSuccess, result.ToString());
            if (!allowed)
            {
                Assert.AreEqual("appraisal.level_locked", result.ErrorCode);
                Assert.AreEqual(1, rig.Economy.State.Ledger.Count, "kilitli seviye ücret almaz");
            }
        }

        [Test]
        public void S3_NeedsTheTestDevice_AndTheDayToo()
        {
            var rig = new AppraisalRig();
            ProductInstance phone = rig.Listing();

            Assert.AreEqual("appraisal.equipment_missing", rig.Service.Appraise(phone.InstanceId, "s3", 6).ErrorCode);
            rig.Equipment.Grant("test_device");
            Assert.AreEqual("appraisal.level_locked", rig.Service.Appraise(phone.InstanceId, "s3", 5).ErrorCode, "ekipman olsa da Gün 6'dan önce açık değil");
            Assert.IsTrue(rig.Service.Appraise(phone.InstanceId, "s3", 6).IsSuccess);
        }

        [Test]
        public void DayBelowOne_IsRejected()
        {
            var rig = new AppraisalRig();
            ProductInstance phone = rig.Listing();

            Assert.AreEqual("day.invalid", rig.Service.Appraise(phone.InstanceId, "s0", 0).ErrorCode);
        }

        // ---------- I5: kilitli sonuç ----------

        [Test]
        public void I5_SameInstanceAndLevel_ReturnsTheSameLockedResult_WithoutAnotherFee()
        {
            var rig = new AppraisalRig();
            ProductInstance phone = rig.Listing();
            AppraisalResult first = rig.Service.Appraise(phone.InstanceId, "s1", 3).Value;
            Money cashAfterFirst = rig.Economy.Cash;
            int rows = rig.Economy.State.Ledger.Count;
            int events = 0;
            rig.Bus.Subscribe<AppraisalCompleted>(e => events++);

            Result<AppraisalResult> again = rig.Service.Appraise(phone.InstanceId, "s1", 4);

            Assert.IsTrue(again.IsSuccess);
            Assert.AreSame(first, again.Value, "yeniden deneyerek şansı zorlamak yok");
            Assert.AreEqual(cashAfterFirst, rig.Economy.Cash);
            Assert.AreEqual(rows, rig.Economy.State.Ledger.Count);
            Assert.AreEqual(1, rig.ResultIds.LastIssued);
            Assert.AreEqual(0, events, "tekrar çağrı olay yayınlamaz");
            Assert.AreEqual(3, again.Value.Day, "sonuç ilk yapıldığı günü korur");
        }

        [Test]
        public void I5_TheResultDependsOnlyOnSeedInstanceAndLevel_NotOnHistory()
        {
            var a = new AppraisalRig(99UL);
            var b = new AppraisalRig(99UL);
            ProductInstance pa = a.Listing();
            ProductInstance pb = b.Listing();
            b.Service.Appraise(pb.InstanceId, "s0", 1); // b'de önce başka bir seviye yapıldı

            AppraisalResult ra = a.Service.Appraise(pa.InstanceId, "s2", 5).Value;
            AppraisalResult rb = b.Service.Appraise(pb.InstanceId, "s2", 5).Value;

            Assert.AreEqual(Fingerprint(ra), Fingerprint(rb));
            Assert.AreEqual(AppraisalSeed.Compute(99UL, pa.InstanceId, "s2"), ra.Seed);
        }

        [Test]
        public void DifferentMasterSeeds_GiveDifferentOutcomes_SomewhereAcrossManyInstances()
        {
            var a = new AppraisalRig(1UL);
            var b = new AppraisalRig(2UL);
            bool differs = false;
            for (int i = 0; i < 40 && !differs; i++)
            {
                ProductInstance pa = a.Listing();
                ProductInstance pb = b.Listing();
                differs = Fingerprint(a.Service.Appraise(pa.InstanceId, "s1", 3).Value) != Fingerprint(b.Service.Appraise(pb.InstanceId, "s1", 3).Value);
            }

            Assert.IsTrue(differs);
        }

        [Test]
        public void AHigherLevel_IsANewResult_WithItsOwnFee()
        {
            var rig = new AppraisalRig();
            ProductInstance phone = rig.Listing();

            AppraisalResult s1 = rig.Service.Appraise(phone.InstanceId, "s1", 5).Value;
            AppraisalResult s2 = rig.Service.Appraise(phone.InstanceId, "s2", 5).Value;

            Assert.AreNotSame(s1, s2);
            Assert.AreEqual(1, s1.ResultId);
            Assert.AreEqual(2, s2.ResultId);
            Assert.AreEqual(Money.FromTl(250000 - 200 - 600), rig.Economy.Cash);
            Assert.AreEqual(Money.FromTl(800), rig.Economy.Economy.PendingAppraisalCost(phone.InstanceId));
            CollectionAssert.AreEqual(new long[] { 1, 2 }, rig.Knowledge.ForInstance(phone.InstanceId).Select(r => r.ResultId).ToArray());
        }

        // ---------- sonuç içeriği ----------

        [Test]
        public void Result_CarriesTheContext_AndNeverTheTrueValue()
        {
            var rig = new AppraisalRig(5UL);
            ProductInstance phone = rig.Listing();

            AppraisalResult r = rig.Service.Appraise(phone.InstanceId, "s2", 5).Value;

            Assert.AreEqual(1, r.ResultId);
            Assert.AreEqual(phone.InstanceId, r.InstanceId);
            Assert.AreEqual("phone.test_one", r.DefinitionId);
            Assert.AreEqual("s2", r.LevelId);
            Assert.AreEqual(5, r.Day);
            Assert.AreEqual(AppraisalSeed.Compute(5UL, phone.InstanceId, "s2"), r.Seed);
            Assert.AreEqual(2, r.Findings.Count, "her kontrol edilen nitelik için bir satır");
            CollectionAssert.AreEqual(new[] { "screen", "camera" }, r.Findings.Select(f => f.Attribute).ToArray());
            Assert.AreEqual(r.Findings.Count(f => f.Found), r.Cards.Count);
            Assert.IsFalse(r.Findings is List<AttributeFinding>);
            Assert.IsFalse(r.Cards is List<TrumpCard>);
        }

        [Test]
        public void S0_HasNoValueRange_AndNoBatteryRange()
        {
            var rig = new AppraisalRig();
            ProductInstance phone = rig.Listing();

            AppraisalResult r = rig.Service.Appraise(phone.InstanceId, "s0", 1).Value;

            Assert.IsNull(r.ValueRange);
            Assert.IsNull(r.BatteryRange);
            Assert.IsNotNull(r.BodyRange);
        }

        [Test]
        public void Appraisal_DoesNotChangeTheInstance_OrTheStock()
        {
            var rig = new AppraisalRig(3UL);
            ProductInstance phone = rig.Listing();
            string before = phone.AgeMonths + "|" + phone.GetNumber("battery") + "|" + phone.GetText("screen") + "|" + phone.Location + "|" + phone.CostBasis.Tl;

            rig.Service.Appraise(phone.InstanceId, "s2", 5);

            Assert.AreEqual(before, phone.AgeMonths + "|" + phone.GetNumber("battery") + "|" + phone.GetText("screen") + "|" + phone.Location + "|" + phone.CostBasis.Tl);
            Assert.AreEqual(0, rig.Economy.Inventory.Count);
        }

        // ---------- olay ----------

        [Test]
        public void AppraisalCompleted_IsPublishedAfterTheStateChange()
        {
            var rig = new AppraisalRig(8UL);
            ProductInstance phone = rig.Listing();
            var seen = new List<string>();
            rig.Bus.Subscribe<AppraisalCompleted>(e =>
            {
                AppraisalResult stored;
                bool known = rig.Knowledge.TryGet(e.InstanceId, e.LevelId, out stored);
                seen.Add(e.ResultId + ":" + e.InstanceId + ":" + e.LevelId + ":" + e.Day + ":" + e.Fee.Tl + ":known=" + known
                         + ":pending=" + rig.Economy.Economy.PendingAppraisalCost(e.InstanceId).Tl);
            });

            rig.Service.Appraise(phone.InstanceId, "s1", 4);

            CollectionAssert.AreEqual(new[] { "1:" + phone.InstanceId + ":s1:4:200:known=True:pending=200" }, seen);
        }

        [Test]
        public void FailedAppraisals_PublishNothing()
        {
            var rig = new AppraisalRig();
            int events = 0;
            rig.Bus.Subscribe<AppraisalCompleted>(e => events++);

            rig.Service.Appraise(999, "s0", 1);
            rig.Service.Appraise(rig.Listing().InstanceId, "s1", 1);

            Assert.AreEqual(0, events);
        }

        [Test]
        public void Works_WithoutAnEventBus()
        {
            var economy = new EconomyHarness();
            var content = MarketHarness.FixtureContent();
            var service = new AppraisalService(content, new KnowledgeState(), new EquipmentState(), economy.Economy, economy.Store, new IdGenerator(), null, 1UL);
            ProductInstance phone = economy.NewMarketInstance("phone.test_one");
            phone.Attributes["battery"] = AttributeValue.FromNumber(90);
            phone.Attributes["screen"] = AttributeValue.FromText("original");
            phone.Attributes["body"] = AttributeValue.FromNumber(90);
            phone.Attributes["camera"] = AttributeValue.FromText("ok");
            phone.Attributes["box"] = AttributeValue.FromFlag(false);
            phone.Attributes["invoice"] = AttributeValue.FromFlag(false);

            Assert.IsTrue(service.Appraise(phone.InstanceId, "s0", 1).IsSuccess);
        }

        [Test]
        public void Constructor_RejectsNulls()
        {
            var e = new EconomyHarness();
            var content = MarketHarness.FixtureContent();
            var k = new KnowledgeState();
            var q = new EquipmentState();
            var ids = new IdGenerator();

            Assert.Throws<ArgumentNullException>(() => new AppraisalService(null, k, q, e.Economy, e.Store, ids, null, 1UL));
            Assert.Throws<ArgumentNullException>(() => new AppraisalService(content, null, q, e.Economy, e.Store, ids, null, 1UL));
            Assert.Throws<ArgumentNullException>(() => new AppraisalService(content, k, null, e.Economy, e.Store, ids, null, 1UL));
            Assert.Throws<ArgumentNullException>(() => new AppraisalService(content, k, q, null, e.Store, ids, null, 1UL));
            Assert.Throws<ArgumentNullException>(() => new AppraisalService(content, k, q, e.Economy, null, ids, null, 1UL));
            Assert.Throws<ArgumentNullException>(() => new AppraisalService(content, k, q, e.Economy, e.Store, null, null, 1UL));
        }
    }
}
