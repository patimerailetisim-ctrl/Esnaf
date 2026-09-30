using System;
using System.Collections.Generic;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Appraisal;
using Esnaf.Domain.Content;
using Esnaf.Domain.Products;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Appraisal
{
    /// <summary>
    /// GDD v0.3 9.4 istatistik testleri (10.000 deneme): S1 kaçırma %30 ± 1,5; yanlış alarm oranları; kapsama S1 %85, S2 %92, S3 %96 (± 3).
    /// Sabit tohumlar kullanılır: sonuçlar deterministiktir, testler kararsız (flaky) olamaz.
    /// </summary>
    public class AppraisalStatisticsTests
    {
        private const int Trials = 10000;

        private static ProductDefinition Def(string id)
        {
            return MarketHarness.RealContent().GetProduct(id);
        }

        private static AppraisalCalculator Calc(AppraisalConfig config)
        {
            return new AppraisalCalculator(new ValueCalculator(MarketHarness.RealContent().ValueTables), config);
        }

        private static double Rate(int hits)
        {
            return hits / (double)Trials;
        }

        // ---------- yakalama ve yanlış alarm (kategorik bulgular) ----------

        private static double DetectRate(string level, string attribute, ulong seed)
        {
            AppraisalConfig config = MarketHarness.RealContent().Appraisal;
            AppraisalCalculator calc = Calc(config);
            ProductDefinition def = Def("phone.elma_e13_pro");
            bool screen = attribute == "screen";
            var phone = ContentFixtures.Instance(
                def, 18, 78, screen ? "replaced_aftermarket" : "original", 85, screen ? "ok" : "faulty", false, false, 128);
            IRandom rng = new PcgRandom(seed, 1UL);
            int found = 0;
            for (int i = 0; i < Trials; i++)
            {
                AppraisalEvaluation e = calc.Evaluate(phone, def, config.GetLevel(level), rng);
                found += e.Findings.Single(f => f.Attribute == attribute).Found ? 1 : 0;
            }

            return Rate(found);
        }

        private static double FalseAlarmRate(string level, string attribute, ulong seed)
        {
            AppraisalConfig config = MarketHarness.RealContent().Appraisal;
            AppraisalCalculator calc = Calc(config);
            ProductDefinition def = Def("phone.elma_e13_pro");
            var clean = ContentFixtures.Instance(def, 18, 78, "original", 85, "ok", false, false, 128);
            IRandom rng = new PcgRandom(seed, 2UL);
            int found = 0;
            for (int i = 0; i < Trials; i++)
            {
                AppraisalEvaluation e = calc.Evaluate(clean, def, config.GetLevel(level), rng);
                AttributeFinding f = e.Findings.Single(x => x.Attribute == attribute);
                found += f.Found ? 1 : 0;
                if (f.Found)
                {
                    Assert.IsTrue(f.IsFalseAlarm);
                }
            }

            return Rate(found);
        }

        [Test]
        public void S1_MissesTheScreenDefect_30Percent_Plus_Minus_1_5()
        {
            double missed = 1.0 - DetectRate("s1", "screen", 11UL);

            Assert.AreEqual(0.30, missed, 0.015, "GDD 9.4: S1 kaçırma oranı 10.000 denemede %30 ±1,5");
        }

        [Test]
        public void DetectionRates_FollowTheTable()
        {
            Assert.AreEqual(0.35, DetectRate("s0", "screen", 21UL), 0.02);
            Assert.AreEqual(0.20, DetectRate("s0", "camera", 22UL), 0.02);
            Assert.AreEqual(0.60, DetectRate("s1", "camera", 23UL), 0.02);
            Assert.AreEqual(0.92, DetectRate("s2", "screen", 24UL), 0.015);
            Assert.AreEqual(0.90, DetectRate("s2", "camera", 25UL), 0.015);
            Assert.AreEqual(0.99, DetectRate("s3", "screen", 26UL), 0.006);
            Assert.AreEqual(0.99, DetectRate("s3", "camera", 27UL), 0.006);
        }

        [Test]
        public void FalseAlarmRates_FollowTheTable()
        {
            Assert.AreEqual(0.05, FalseAlarmRate("s0", "screen", 31UL), 0.01);
            Assert.AreEqual(0.05, FalseAlarmRate("s1", "camera", 32UL), 0.01);
            Assert.AreEqual(0.02, FalseAlarmRate("s2", "screen", 33UL), 0.008);
            Assert.AreEqual(0.005, FalseAlarmRate("s3", "camera", 34UL), 0.004);
        }

        // ---------- kapsama ----------

        /// <summary>Doğal piyasa ürünleri: Gün 5+ (tüm durum profilleri), rastgele modeller.</summary>
        private static double Coverage(string level, ulong seed)
        {
            ContentDatabase content = MarketHarness.RealContent();
            AppraisalConfig config = content.Appraisal;
            AppraisalCalculator calc = Calc(config);
            var valueCalc = new ValueCalculator(content.ValueTables);
            var generator = new InstanceGenerator(content.ConditionProfiles, new IdGenerator());
            IRandom population = new PcgRandom(seed, 3UL);
            IRandom noise = new PcgRandom(seed, 4UL);
            int covered = 0;
            for (int i = 0; i < Trials; i++)
            {
                ProductDefinition def = content.Products[population.NextInt(content.Products.Count)];
                ProductInstance phone = generator.Generate(def, 10, population);
                AppraisalEvaluation e = calc.Evaluate(phone, def, config.GetLevel(level), noise);
                covered += e.ValueRange.Contains(valueCalc.TrueValue(phone, def)) ? 1 : 0;
            }

            return Rate(covered);
        }

        [Test]
        public void ValueRangeCoverage_S1_85Percent_Plus_Minus_3()
        {
            Assert.AreEqual(0.85, Coverage("s1", 41UL), 0.03);
        }

        [Test]
        public void ValueRangeCoverage_S2_92Percent_Plus_Minus_3()
        {
            Assert.AreEqual(0.92, Coverage("s2", 42UL), 0.03);
        }

        [Test]
        public void ValueRangeCoverage_S3_96Percent_Plus_Minus_3()
        {
            Assert.AreEqual(0.96, Coverage("s3", 43UL), 0.03);
        }

        [Test]
        public void HigherLevels_AreStrictlyMoreReliable_AndNarrower()
        {
            double s1 = Coverage("s1", 51UL);
            double s2 = Coverage("s2", 51UL);
            double s3 = Coverage("s3", 51UL);

            Assert.Less(s1, s2, "yüksek seviye daha güvenilir");
            Assert.Less(s2, s3);
        }

        [Test]
        public void Ranges_AlwaysContainTheTrueBatteryAndBody()
        {
            ContentDatabase content = MarketHarness.RealContent();
            AppraisalConfig config = content.Appraisal;
            AppraisalCalculator calc = Calc(config);
            var generator = new InstanceGenerator(content.ConditionProfiles, new IdGenerator());
            IRandom population = new PcgRandom(61UL, 3UL);
            IRandom noise = new PcgRandom(61UL, 4UL);

            for (int i = 0; i < 3000; i++)
            {
                ProductDefinition def = content.Products[population.NextInt(content.Products.Count)];
                ProductInstance phone = generator.Generate(def, 10, population);
                foreach (string id in new[] { "s1", "s2", "s3" })
                {
                    AppraisalEvaluation e = calc.Evaluate(phone, def, config.GetLevel(id), noise);

                    Assert.IsTrue(e.BatteryRange.Contains((int)phone.GetNumber("battery")), id + " pil");
                    Assert.IsTrue(e.BodyRange.Contains((int)phone.GetNumber("body")), id + " kasa");
                }
            }
        }

        [Test]
        public void MeasuredCoverage_IsCloseToTheDeclaredEstimate_ThatTheRiskCardShows()
        {
            // Risk kartının "aralık dışı kalma olasılığı" tahmini (coverageEstimate) ölçülen kapsamayla uyumlu olmalı.
            AppraisalConfig config = MarketHarness.RealContent().Appraisal;
            foreach (string id in new[] { "s1", "s2", "s3" })
            {
                Assert.AreEqual(config.GetLevel(id).CoverageEstimate.Value, Coverage(id, 71UL), 0.03, id);
            }
        }
    }
}
