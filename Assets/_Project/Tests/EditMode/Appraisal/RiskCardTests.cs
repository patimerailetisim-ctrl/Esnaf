using System;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Appraisal;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Appraisal
{
    /// <summary>v0.2 5.5: risk kartı. Örnek: değer aralığı 28.000–31.000, teklif 29.000.</summary>
    public class RiskCardTests
    {
        private static AppraisalResult ResultWith(string level, MoneyRange range)
        {
            return new AppraisalResult(
                7, 3, "phone.x", level, 5, Money.FromTl(600), 1UL, new AttributeFinding[0],
                new NumericRange(70, 80), new NumericRange(80, 90), range, new TrumpCard[0]);
        }

        private static RiskCardBuilder Builder(AppraisalConfig config = null)
        {
            return new RiskCardBuilder(config ?? ContentFixtures.Appraisal());
        }

        [Test]
        public void GddExample_ThreeScenarios()
        {
            AppraisalResult r = ResultWith("s2", new MoneyRange(Money.FromTl(28000), Money.FromTl(31000)));

            Result<RiskCard> result = Builder().Build(r, Money.FromTl(29000));

            Assert.IsTrue(result.IsSuccess, result.ToString());
            RiskCard card = result.Value;
            Assert.AreEqual(7, card.AppraisalId);
            Assert.AreEqual("s2", card.LevelId);
            Assert.AreEqual(Money.FromTl(29000), card.Offer);
            Assert.AreEqual(3, card.Scenarios.Count);
            RiskScenario bad = card.Scenarios[0];
            RiskScenario mid = card.Scenarios[1];
            RiskScenario good = card.Scenarios[2];
            Assert.AreEqual("bad", bad.Name);
            Assert.AreEqual(Money.FromTl(28000), bad.TrueValue);
            Assert.AreEqual(Money.FromTl(28560), bad.ExpectedSale);
            Assert.AreEqual(Money.FromTl(-440), bad.Profit, "v0.2 5.5: −440");
            Assert.AreEqual("mid", mid.Name);
            Assert.AreEqual(Money.FromTl(29500), mid.TrueValue);
            Assert.AreEqual(Money.FromTl(30090), mid.ExpectedSale);
            Assert.AreEqual(Money.FromTl(1090), mid.Profit, "v0.2 5.5: +1.090");
            Assert.AreEqual("good", good.Name);
            Assert.AreEqual(Money.FromTl(31000), good.TrueValue);
            Assert.AreEqual(Money.FromTl(31620), good.ExpectedSale);
            Assert.AreEqual(Money.FromTl(2620), good.Profit, "v0.2 5.5: +2.620");
        }

        [Test]
        public void MissProbability_IsOneMinusTheLevelCoverage()
        {
            RiskCard s2 = Builder().Build(ResultWith("s2", new MoneyRange(Money.FromTl(1000), Money.FromTl(2000))), Money.FromTl(1000)).Value;
            RiskCard s1 = Builder().Build(ResultWith("s1", new MoneyRange(Money.FromTl(1000), Money.FromTl(2000))), Money.FromTl(1000)).Value;
            RiskCard s3 = Builder().Build(ResultWith("s3", new MoneyRange(Money.FromTl(1000), Money.FromTl(2000))), Money.FromTl(1000)).Value;

            Assert.AreEqual(0.08, s2.MissProbability, 1e-12, "v0.2 5.5: ≈ %8 (S2)");
            Assert.AreEqual(0.15, s1.MissProbability, 1e-12);
            Assert.AreEqual(0.04, s3.MissProbability, 1e-12);
        }

        [Test]
        public void ExpectedSaleFactor_ComesFromTheData()
        {
            string json = JsonEditForFactor("1.10");
            var issues = new System.Collections.Generic.List<Esnaf.Domain.Content.ContentIssue>();
            AppraisalConfig config = Esnaf.Domain.Content.ContentParser.ParseAppraisal("appraisal_levels.json", json, issues);

            RiskCard card = Builder(config).Build(ResultWith("s2", new MoneyRange(Money.FromTl(10000), Money.FromTl(20000))), Money.FromTl(10000)).Value;

            Assert.AreEqual(Money.FromTl(11000), card.Scenarios[0].ExpectedSale);
            Assert.AreEqual(Money.FromTl(1000), card.Scenarios[0].Profit);
        }

        private static string JsonEditForFactor(string factor)
        {
            return JsonEdit.Set(ContentFixtures.AppraisalLevelsJson, "expectedSaleFactor", factor, "\"riskCard\"");
        }

        [Test]
        public void ExpectedSale_IsRoundedTo10()
        {
            RiskCard card = Builder().Build(ResultWith("s2", new MoneyRange(Money.FromTl(10050), Money.FromTl(10250))), Money.FromTl(10000)).Value;

            Assert.AreEqual(Money.FromTl(10250), card.Scenarios[0].ExpectedSale, "10.050 × 1,02 = 10.251 → 10.250");
            Assert.IsTrue(card.Scenarios.All(s => s.ExpectedSale.IsRoundedTo10));
        }

        [Test]
        public void MidScenario_UsesTheRangeMidpoint()
        {
            RiskCard card = Builder().Build(ResultWith("s1", new MoneyRange(Money.FromTl(21680), Money.FromTl(26490))), Money.FromTl(23000)).Value;

            Assert.AreEqual(Money.FromTl(24090), card.Scenarios[1].TrueValue, "(21.680 + 26.490) / 2 = 24.085 → 24.090");
        }

        [Test]
        public void S0_HasNoValueRange_SoNoRiskCard()
        {
            var r = new AppraisalResult(
                1, 3, "phone.x", "s0", 1, Money.Zero, 1UL, new AttributeFinding[0], null, new NumericRange(70, 100), null, new TrumpCard[0]);

            Result<RiskCard> result = Builder().Build(r, Money.FromTl(1000));

            Assert.IsTrue(result.IsFailure);
            Assert.AreEqual("appraisal.no_value_range", result.ErrorCode);
        }

        [TestCase(0L)]
        [TestCase(-1000L)]
        [TestCase(1005L)]
        public void InvalidOffers_AreRejected(long offer)
        {
            AppraisalResult r = ResultWith("s2", new MoneyRange(Money.FromTl(28000), Money.FromTl(31000)));

            Result<RiskCard> result = Builder().Build(r, Money.FromTl(offer));

            Assert.IsTrue(result.IsFailure);
            Assert.AreEqual("offer.invalid", result.ErrorCode);
        }

        [Test]
        public void UnknownLevel_IsRejected()
        {
            AppraisalResult r = ResultWith("s9", new MoneyRange(Money.FromTl(28000), Money.FromTl(31000)));

            Assert.AreEqual("appraisal.level_unknown", Builder().Build(r, Money.FromTl(29000)).ErrorCode);
        }

        [Test]
        public void Scenarios_AreAReadOnlyList()
        {
            RiskCard card = Builder().Build(ResultWith("s2", new MoneyRange(Money.FromTl(28000), Money.FromTl(31000))), Money.FromTl(29000)).Value;

            Assert.IsFalse(card.Scenarios is System.Collections.Generic.List<RiskScenario>);
        }

        [Test]
        public void Build_RejectsNulls()
        {
            Assert.Throws<ArgumentNullException>(() => new RiskCardBuilder(null));
            Assert.Throws<ArgumentNullException>(() => Builder().Build(null, Money.FromTl(1000)));
        }
    }
}
