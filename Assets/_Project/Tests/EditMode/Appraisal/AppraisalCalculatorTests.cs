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
    /// Ekspertiz hesabı (UA9/UA10). Beklenen sayılar GDD v0.2 5.4 örneğinden ve bağımsız Python referans modelinden gelir;
    /// "rastgele" girdiler ScriptedRandom ile sabitlenir (sıfır sapma, u = 0,5).
    /// </summary>
    public class AppraisalCalculatorTests
    {
        private static AppraisalCalculator Calculator(AppraisalConfig config = null)
        {
            return new AppraisalCalculator(new ValueCalculator(MarketHarness.RealContent().ValueTables), config ?? ContentFixtures.Appraisal());
        }

        private static ProductDefinition E13()
        {
            return MarketHarness.RealContent().GetProduct("phone.elma_e13_pro");
        }

        /// <summary>v0.2 5.4: E13 Pro, 18 ay, pil 78, ekran yan sanayi (gizli), kasa 85, kamera sağlam, paket yok (V = 22.310).</summary>
        private static ProductInstance ExamplePhone()
        {
            return ContentFixtures.Instance(E13(), 18, 78, "replaced_aftermarket", 85, "ok", false, false, 128);
        }

        private static ScriptedRandom Script(bool screenRoll, bool cameraRoll, double battery = 0.5, double body = 0.5, double value = 0.5)
        {
            return new ScriptedRandom(new[] { screenRoll, cameraRoll }, new[] { battery, body, value });
        }

        private static AppraisalEvaluation Run(ProductInstance phone, string level, ScriptedRandom rng, AppraisalConfig config = null)
        {
            AppraisalConfig c = config ?? ContentFixtures.Appraisal();
            return Calculator(c).Evaluate(phone, E13(), c.GetLevel(level), rng);
        }

        // ---------- v0.2 5.4 tablosu ----------

        [Test]
        public void ExamplePhone_TrueValue_IsTheGddValue()
        {
            var calc = new ValueCalculator(MarketHarness.RealContent().ValueTables);

            Assert.AreEqual(Money.FromTl(22310), calc.TrueValue(ExamplePhone(), E13()));
        }

        [Test]
        public void S1_Detected_MatchesTheGddRow()
        {
            AppraisalEvaluation e = Run(ExamplePhone(), "s1", Script(true, false));

            Assert.AreEqual(2, e.Findings.Count);
            AttributeFinding screen = e.Findings[0];
            Assert.AreEqual("screen", screen.Attribute);
            Assert.IsTrue(screen.Found);
            Assert.AreEqual(AppraisalConfidence.Low, screen.Confidence);
            Assert.IsFalse(screen.IsFalseAlarm);
            Assert.AreEqual("appraisal.finding.screen_replaced", screen.WordingKey);
            Assert.IsFalse(e.Findings[1].Found, "Kamera: sorun görünmüyor");
            Assert.AreEqual(68, e.BatteryRange.Min);
            Assert.AreEqual(88, e.BatteryRange.Max);
            Assert.AreEqual(75, e.BodyRange.Min);
            Assert.AreEqual(95, e.BodyRange.Max);
            Assert.AreEqual(Money.FromTl(21680), e.ValueRange.Min);
            Assert.AreEqual(Money.FromTl(26490), e.ValueRange.Max);
            AssertWithin(21700, e.ValueRange.Min, "v0.2 5.4: 21.700");
            AssertWithin(26500, e.ValueRange.Max, "v0.2 5.4: 26.500");
            Assert.IsTrue(e.ValueRange.Contains(Money.FromTl(22310)), "gerçek değer aralıkta ('Evet, ama çok geniş')");
        }

        [Test]
        public void S1_Missed_MatchesTheGddRow_AndTheTrueValueFallsOutside()
        {
            AppraisalEvaluation e = Run(ExamplePhone(), "s1", Script(false, false));

            Assert.IsFalse(e.Findings[0].Found, "Ekran: sorun görünmüyor");
            Assert.AreEqual(68, e.BatteryRange.Min);
            Assert.AreEqual(88, e.BatteryRange.Max);
            Assert.AreEqual(75, e.BodyRange.Min);
            Assert.AreEqual(95, e.BodyRange.Max);
            Assert.AreEqual(Money.FromTl(22810), e.ValueRange.Min);
            Assert.AreEqual(Money.FromTl(27880), e.ValueRange.Max);
            AssertWithin(22800, e.ValueRange.Min, "v0.2 5.4: 22.800");
            AssertWithin(27900, e.ValueRange.Max, "v0.2 5.4: 27.900");
            Assert.IsFalse(e.ValueRange.Contains(Money.FromTl(22310)), "v0.2 5.4: 'Hayır!' gerçek değer aralığın altında kalır");
            Assert.AreEqual(0, e.Cards.Count, "bulgu yok → koz kartı yok");
        }

        [Test]
        public void S2_MatchesTheGddRow()
        {
            AppraisalEvaluation e = Run(ExamplePhone(), "s2", Script(true, false));

            Assert.AreEqual(AppraisalConfidence.Medium, e.Findings[0].Confidence);
            Assert.AreEqual(73, e.BatteryRange.Min);
            Assert.AreEqual(83, e.BatteryRange.Max);
            Assert.AreEqual(80, e.BodyRange.Min);
            Assert.AreEqual(90, e.BodyRange.Max);
            Assert.AreEqual(Money.FromTl(22020), e.ValueRange.Min);
            Assert.AreEqual(Money.FromTl(24340), e.ValueRange.Max);
            AssertWithin(22000, e.ValueRange.Min, "v0.2 5.4: 22.000");
            AssertWithin(24300, e.ValueRange.Max, "v0.2 5.4: 24.300");
            Assert.IsTrue(e.ValueRange.Contains(Money.FromTl(22310)));
        }

        [Test]
        public void S3_MatchesTheGddRow()
        {
            AppraisalEvaluation e = Run(ExamplePhone(), "s3", Script(true, false));

            Assert.AreEqual(AppraisalConfidence.Certain, e.Findings[0].Confidence);
            Assert.AreEqual(76, e.BatteryRange.Min);
            Assert.AreEqual(80, e.BatteryRange.Max);
            Assert.AreEqual(82, e.BodyRange.Min);
            Assert.AreEqual(88, e.BodyRange.Max);
            Assert.AreEqual(Money.FromTl(21750), e.ValueRange.Min);
            Assert.AreEqual(Money.FromTl(22860), e.ValueRange.Max);
            AssertWithin(21800, e.ValueRange.Min, "v0.2 5.4: 21.800");
            AssertWithin(22900, e.ValueRange.Max, "v0.2 5.4: 22.900");
            Assert.IsTrue(e.ValueRange.Contains(Money.FromTl(22310)), "'Evet, dar'");
            Assert.Less(e.ValueRange.Max.Tl - e.ValueRange.Min.Tl, 1200);
        }

        [Test]
        public void S0_GivesOnlyHints_NoValueRange_NoBatteryRange()
        {
            AppraisalEvaluation e = Run(ExamplePhone(), "s0", Script(true, false));

            Assert.AreEqual(AppraisalConfidence.Hint, e.Findings[0].Confidence);
            Assert.IsNull(e.BatteryRange, "S0: pil beyan");
            Assert.AreEqual(70, e.BodyRange.Min);
            Assert.AreEqual(100, e.BodyRange.Max);
            Assert.IsNull(e.ValueRange, "S0: değer aralığı yok");
            Assert.AreEqual(1, e.Cards.Count, "ipucu da koz kartı olur (güç 0,2)");
            Assert.AreEqual(0.2, e.Cards[0].EvidencePower, 1e-12);
        }

        private static void AssertWithin(long expected, Money actual, string message)
        {
            Assert.LessOrEqual(Math.Abs(actual.Tl - expected), 100, message + " (bulunan " + actual.Tl + ")");
        }

        // ---------- koz kartları ----------

        [Test]
        public void TrumpCard_ProblemValue_IsTheValueTheDefectRemoves()
        {
            AppraisalEvaluation e = Run(ExamplePhone(), "s2", Script(true, false));

            Assert.AreEqual(1, e.Cards.Count);
            TrumpCard card = e.Cards[0];
            Assert.AreEqual("screen", card.Attribute);
            Assert.AreEqual(Money.FromTl(3040), card.ProblemValue, "v0.2 7.4: 25.350 − 22.310 = 3.040");
            Assert.AreEqual(0.7, card.EvidencePower, 1e-12);
            Assert.AreEqual(AppraisalConfidence.Medium, card.Confidence);
            Assert.IsFalse(card.IsFalseAlarm);
            Assert.AreEqual("appraisal.finding.screen_replaced", card.WordingKey);
        }

        [Test]
        public void TrumpCard_PowerFollowsTheLevel()
        {
            foreach (var pair in new[] { new KeyValuePair<string, double>("s1", 0.4), new KeyValuePair<string, double>("s2", 0.7), new KeyValuePair<string, double>("s3", 1.0) })
            {
                AppraisalEvaluation e = Run(ExamplePhone(), pair.Key, Script(true, false));

                Assert.AreEqual(pair.Value, e.Cards.Single().EvidencePower, 1e-12, pair.Key);
                Assert.AreEqual(Money.FromTl(3040), e.Cards.Single().ProblemValue, pair.Key);
            }
        }

        [Test]
        public void FalseAlarm_OnASoundPart_IsFoundLikeARealFinding_ButFlaggedHidden()
        {
            var clean = ContentFixtures.Instance(E13(), 18, 78, "original", 85, "ok", false, false, 128);

            AppraisalEvaluation e = Run(clean, "s2", Script(false, true));

            AttributeFinding camera = e.Findings[1];
            Assert.IsTrue(camera.Found);
            Assert.IsTrue(camera.IsFalseAlarm, "sağlam parçada bulgu = yanlış alarm (gizli bayrak)");
            Assert.AreEqual(AppraisalConfidence.Medium, camera.Confidence);
            Assert.AreEqual(Money.FromTl(22890), e.ValueRange.Min, "yanlış alarm gözlenen değere katılır (0,93 ^ 0,7)");
            Assert.AreEqual(Money.FromTl(25300), e.ValueRange.Max);
            TrumpCard card = e.Cards.Single();
            Assert.IsTrue(card.IsFalseAlarm);
            Assert.AreEqual("camera", card.Attribute);
            Assert.AreEqual(Money.FromTl(1770), card.ProblemValue, "kartta sanki kusur varmış gibi TL değeri");
        }

        [Test]
        public void TwoRealDefects_S3_BothFoundAndBothBecomeCards()
        {
            var phone = ContentFixtures.Instance(E13(), 18, 78, "replaced_aftermarket", 85, "faulty", false, false, 128);

            AppraisalEvaluation e = Run(phone, "s3", Script(true, true));

            Assert.IsTrue(e.Findings.All(f => f.Found && !f.IsFalseAlarm));
            Assert.AreEqual(2, e.Cards.Count);
            Assert.AreEqual("screen", e.Cards[0].Attribute);
            Assert.AreEqual(Money.FromTl(2490), e.Cards[0].ProblemValue, "kamera arızalıyken ekranın götürdüğü değer");
            Assert.AreEqual("camera", e.Cards[1].Attribute);
            Assert.AreEqual(Money.FromTl(4020), e.Cards[1].ProblemValue, "ekran değişmişken kameranın götürdüğü değer");
            Assert.AreEqual(Money.FromTl(17830), e.ValueRange.Min);
            Assert.AreEqual(Money.FromTl(18750), e.ValueRange.Max);
        }

        [Test]
        public void VisibleDefects_AreNeverHidden_NorFindings()
        {
            // Çizik ekran görünür: gerçek değere olduğu gibi girer. Ekran bulgusu yoksa, zar false ise bulgu da yok.
            var phone = ContentFixtures.Instance(E13(), 18, 90, "scratched", 100, "ok", true, true, 128);
            var calc = new ValueCalculator(MarketHarness.RealContent().ValueTables);
            double truth = calc.Calculate(phone, E13()).TrueValueExact;

            AppraisalEvaluation e = Run(phone, "s3", Script(false, false));

            Assert.IsFalse(e.Findings[0].Found);
            Assert.AreEqual(Money.FromDoubleRoundedTo10(truth * 0.975), e.ValueRange.Min, "çizik ve paket (kutu+fatura) değere girer");
            Assert.AreEqual(Money.FromDoubleRoundedTo10(truth * 1.025), e.ValueRange.Max);
        }

        [Test]
        public void ScratchedScreen_IsNotADefectOfTheCheck_SoARollOfTrueIsAFalseAlarm()
        {
            var phone = ContentFixtures.Instance(E13(), 18, 90, "scratched", 100, "ok", false, false, 128);

            AppraisalEvaluation e = Run(phone, "s2", Script(true, false));

            Assert.IsTrue(e.Findings[0].Found);
            Assert.IsTrue(e.Findings[0].IsFalseAlarm);
        }

        [TestCase("spotted")]
        [TestCase("faulty")]
        public void CameraDefects_AreDetectedWithTheirOwnMultiplier(string camera)
        {
            var phone = ContentFixtures.Instance(E13(), 18, 90, "original", 100, camera, false, false, 128);
            var calc = new ValueCalculator(MarketHarness.RealContent().ValueTables);
            double multiplier = camera == "spotted" ? 0.93 : 0.82;
            double clean = calc.Calculate(ContentFixtures.Instance(E13(), 18, 90, "original", 100, "ok", false, false, 128), E13()).TrueValueExact;

            AppraisalEvaluation e = Run(phone, "s2", Script(false, true));

            Assert.IsTrue(e.Findings[1].Found);
            Assert.IsFalse(e.Findings[1].IsFalseAlarm);
            double observed = clean * Math.Pow(multiplier, 0.7);
            Assert.AreEqual(Money.FromDoubleRoundedTo10(observed * 0.95), e.ValueRange.Min);
            Assert.AreEqual(Money.FromDoubleRoundedTo10(observed * 1.05), e.ValueRange.Max);
            Assert.AreEqual(Money.FromDoubleRoundedTo10(clean * (1.0 - multiplier)), e.Cards.Single().ProblemValue);
        }

        // ---------- aralık merkezi kayması, sınırlar, gürültü ----------

        private static AppraisalConfig WithShift(string level, string shift, string noise)
        {
            string json = JsonEdit.Set(ContentFixtures.AppraisalLevelsJson, "centerShift", shift, "\"" + level + "\"");
            json = JsonEdit.Set(json, "valueNoise", noise, "\"" + level + "\"");
            var issues = new List<ContentIssue>();
            AppraisalConfig config = ContentParser.ParseAppraisal("appraisal_levels.json", json, issues);
            Assert.IsNotNull(config, string.Join("\n", issues));
            return config;
        }

        [Test]
        public void CenterShift_MovesTheRanges_ButNeverBeyondTheConfiguredFraction()
        {
            AppraisalConfig config = WithShift("s1", "0.5", "0");

            // u = 1,0 → +yarı genişliğin yarısı (+5); u = 0,0 → −5
            AppraisalEvaluation e = Run(ExamplePhone(), "s1", Script(false, false, 1.0, 0.0), config);

            Assert.AreEqual(73, e.BatteryRange.Min);
            Assert.AreEqual(93, e.BatteryRange.Max);
            Assert.AreEqual(70, e.BodyRange.Min);
            Assert.AreEqual(90, e.BodyRange.Max);
            Assert.AreEqual(Money.FromTl(23310), e.ValueRange.Min, "gözlenen değer kaymış pil (83) ve kasa (80) ile hesaplanır");
            Assert.AreEqual(Money.FromTl(28490), e.ValueRange.Max);
            Assert.IsTrue(e.BatteryRange.Contains(78), "aralık her zaman gerçeği içerir");
            Assert.IsTrue(e.BodyRange.Contains(85));
        }

        [Test]
        public void CenterShift_IsRoundedHalfAwayFromZero()
        {
            AppraisalConfig config = WithShift("s1", "0.5", "0");

            // u = 0,75 → 10 × 0,5 × 0,5 = 2,5 → 3 ; u = 0,25 → −2,5 → −3
            AppraisalEvaluation e = Run(ExamplePhone(), "s1", Script(false, false, 0.75, 0.25), config);

            Assert.AreEqual(71, e.BatteryRange.Min);
            Assert.AreEqual(91, e.BatteryRange.Max);
            Assert.AreEqual(72, e.BodyRange.Min);
            Assert.AreEqual(92, e.BodyRange.Max);
        }

        [Test]
        public void Ranges_AreClampedToZeroAndHundred()
        {
            var high = ContentFixtures.Instance(E13(), 18, 98, "original", 100, "ok", false, false, 128);
            var low = ContentFixtures.Instance(E13(), 18, 4, "original", 3, "ok", false, false, 128);

            AppraisalEvaluation a = Run(high, "s1", Script(false, false));
            AppraisalEvaluation b = Run(low, "s1", Script(false, false));

            Assert.AreEqual(88, a.BatteryRange.Min);
            Assert.AreEqual(100, a.BatteryRange.Max);
            Assert.AreEqual(90, a.BodyRange.Min);
            Assert.AreEqual(100, a.BodyRange.Max);
            Assert.AreEqual(0, b.BatteryRange.Min);
            Assert.AreEqual(14, b.BatteryRange.Max);
            Assert.AreEqual(0, b.BodyRange.Min);
            Assert.AreEqual(13, b.BodyRange.Max);
        }

        [Test]
        public void CenterShift_CannotPushTheCenterOutsideZeroHundred()
        {
            AppraisalConfig config = WithShift("s1", "0.9", "0");
            var high = ContentFixtures.Instance(E13(), 18, 100, "original", 100, "ok", false, false, 128);

            AppraisalEvaluation e = Run(high, "s1", Script(false, false, 1.0, 1.0), config);

            // shift +9 → merkez 109 → 100'e kenetlenir → aralık 90..100
            Assert.AreEqual(90, e.BatteryRange.Min);
            Assert.AreEqual(100, e.BatteryRange.Max);
            Assert.AreEqual(90, e.BodyRange.Min);
            Assert.AreEqual(100, e.BodyRange.Max);
        }

        [Test]
        public void ValueNoise_ScalesTheObservedValue()
        {
            AppraisalConfig config = WithShift("s2", "0", "0.05");

            AppraisalEvaluation up = Run(ExamplePhone(), "s2", Script(true, false, 0.5, 0.5, 1.0), config);
            AppraisalEvaluation down = Run(ExamplePhone(), "s2", Script(true, false, 0.5, 0.5, 0.0), config);
            AppraisalEvaluation zero = Run(ExamplePhone(), "s2", Script(true, false, 0.5, 0.5, 0.5), config);

            Assert.AreEqual(Money.FromTl(23120), up.ValueRange.Min, "+%5 gürültü");
            Assert.AreEqual(Money.FromTl(25550), up.ValueRange.Max);
            Assert.AreEqual(Money.FromTl(22020), zero.ValueRange.Min);
            Assert.Less(down.ValueRange.Max.Tl, zero.ValueRange.Max.Tl);
        }

        // ---------- rastgelelik sözleşmesi ----------

        [TestCase("s0")]
        [TestCase("s1")]
        [TestCase("s2")]
        [TestCase("s3")]
        public void Evaluate_ConsumesExactlyTwoChancesAndThreeDoubles(string level)
        {
            ScriptedRandom rng = Script(true, true);

            Run(ExamplePhone(), level, rng);

            Assert.AreEqual(0, rng.Remaining);
        }

        [Test]
        public void Evaluate_UsesDetectChanceForRealDefects_AndFalseAlarmChanceForSoundParts()
        {
            var rec = new RecordingRandom(false, 0.5);
            var defectiveScreenSoundCamera = ExamplePhone();

            Calculator().Evaluate(defectiveScreenSoundCamera, E13(), ContentFixtures.Appraisal().GetLevel("s1"), rec);

            CollectionAssert.AreEqual(new[] { 0.70, 0.05 }, rec.ChanceProbabilities.ToArray(), "ekran kusurlu → tespit; kamera sağlam → yanlış alarm");

            var rec2 = new RecordingRandom(false, 0.5);
            var soundScreenSpottedCamera = ContentFixtures.Instance(E13(), 18, 78, "original", 85, "spotted", false, false, 128);
            Calculator().Evaluate(soundScreenSpottedCamera, E13(), ContentFixtures.Appraisal().GetLevel("s2"), rec2);

            CollectionAssert.AreEqual(new[] { 0.02, 0.90 }, rec2.ChanceProbabilities.ToArray());
            Assert.AreEqual(3, rec2.DoubleCalls);
        }

        [Test]
        public void Evaluate_DoesNotChangeTheInstance()
        {
            ProductInstance phone = ExamplePhone();

            Run(phone, "s3", Script(true, false, 0.9, 0.1, 0.9), WithShift("s3", "0.5", "0.02"));

            Assert.AreEqual(78, phone.GetNumber("battery"));
            Assert.AreEqual(85, phone.GetNumber("body"));
            Assert.AreEqual("replaced_aftermarket", phone.GetText("screen"));
            Assert.AreEqual("ok", phone.GetText("camera"));
        }

        [Test]
        public void Evaluate_RejectsNulls()
        {
            AppraisalCalculator calc = Calculator();
            AppraisalLevel level = ContentFixtures.Appraisal().GetLevel("s1");

            Assert.Throws<ArgumentNullException>(() => calc.Evaluate(null, E13(), level, Script(true, true)));
            Assert.Throws<ArgumentNullException>(() => calc.Evaluate(ExamplePhone(), null, level, Script(true, true)));
            Assert.Throws<ArgumentNullException>(() => calc.Evaluate(ExamplePhone(), E13(), null, Script(true, true)));
            Assert.Throws<ArgumentNullException>(() => calc.Evaluate(ExamplePhone(), E13(), level, null));
            Assert.Throws<ArgumentNullException>(() => new AppraisalCalculator(null, ContentFixtures.Appraisal()));
            Assert.Throws<ArgumentNullException>(() => new AppraisalCalculator(new ValueCalculator(MarketHarness.RealContent().ValueTables), null));
        }

        [Test]
        public void Ranges_HaveHelpers()
        {
            var range = new NumericRange(68, 88);
            var money = new MoneyRange(Money.FromTl(28000), Money.FromTl(31000));

            Assert.IsTrue(range.Contains(68));
            Assert.IsTrue(range.Contains(88));
            Assert.IsFalse(range.Contains(67));
            Assert.IsFalse(range.Contains(89));
            Assert.IsTrue(money.Contains(Money.FromTl(28000)));
            Assert.IsTrue(money.Contains(Money.FromTl(31000)));
            Assert.IsFalse(money.Contains(Money.FromTl(27990)));
            Assert.IsFalse(money.Contains(Money.FromTl(31010)));
            Assert.AreEqual(Money.FromTl(29500), money.Midpoint, "v0.2 5.5: 28.000–31.000 → orta 29.500");
            Assert.AreEqual(Money.FromTl(29510), new MoneyRange(Money.FromTl(29000), Money.FromTl(30020)).Midpoint, "orta nokta 10 TL'ye yuvarlanır");
        }
    }
}
