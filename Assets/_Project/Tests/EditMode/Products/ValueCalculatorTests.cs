using System;
using System.Collections.Generic;
using Esnaf.Core;
using Esnaf.Domain.Content;
using Esnaf.Domain.Products;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Products
{
    public class ValueCalculatorTests
    {
        private static ValueCalculator Calc()
        {
            return new ValueCalculator(ContentFixtures.Tables());
        }

        private static ValueBreakdown E13(int age, long battery, string screen, long body, string camera, bool box = false, bool invoice = false, int storage = 0)
        {
            ProductDefinition def = ContentFixtures.E13Pro();
            return Calc().Calculate(ContentFixtures.Instance(def, age, battery, screen, body, camera, box, invoice, storage), def);
        }

        // ---------- GDD v0.2 Bölüm 4.4: Elma E13 Pro, 128 GB, 18 ay (RF = 28.160) ----------

        [TestCase(94L, "original", 92L, "ok", true, true, 28820L, TestName = "E13_ProfileA")]
        [TestCase(82L, "scratched", 78L, "ok", true, false, 25100L, TestName = "E13_ProfileB")]
        [TestCase(68L, "scratched", 55L, "spotted", false, false, 19860L, TestName = "E13_ProfileC")]
        [TestCase(78L, "replaced_aftermarket", 85L, "ok", false, false, 22310L, TestName = "E13_ProfileD")]
        [TestCase(62L, "cracked", 60L, "faulty", false, false, 13260L, TestName = "E13_ProfileE")]
        public void E13Pro_GddReferenceValues(long battery, string screen, long body, string camera, bool box, bool invoice, long expected)
        {
            ValueBreakdown v = E13(18, battery, screen, body, camera, box, invoice);

            Assert.AreEqual(Money.FromTl(28160), v.ReferencePrice, "RF");
            Assert.AreEqual(Money.FromTl(expected), v.TrueValue);
        }

        [Test]
        public void E13Pro_ProfileD_HiddenListingValue_MatchesGddScenario1()
        {
            // v0.2 Senaryo 1: ekran orijinalmiş gibi gösterilen ilan değeri = 22.310 / 0,88 = ~25.350
            ValueBreakdown real = E13(18, 78, "replaced_aftermarket", 85, "ok");
            ValueBreakdown asListed = E13(18, 78, "original", 85, "ok");

            Assert.AreEqual(Money.FromTl(22310), real.TrueValue);
            Assert.AreEqual(Money.FromTl(25350), asListed.TrueValue);
        }

        // ---------- diğer v0.2 senaryo telefonları ----------

        [Test]
        public void OtherScenarioPhones_MatchGddV02()
        {
            var calc = Calc();

            ProductDefinition n3 = ContentFixtures.Def("phone.nova_n3_pro", basePrice: 12500,
                storage: new[] { new StorageOption(64, 0.9), new StorageOption(128, 1.0), new StorageOption(256, 1.15) });
            ValueBreakdown hatice = calc.Calculate(ContentFixtures.Instance(n3, 30, 84, "scratched", 70, "ok", box: true), n3);
            Assert.AreEqual(Money.FromTl(9630), hatice.ReferencePrice, "N3 Pro RF (9.625 -> 10 TL'ye yuvarlı)");
            Assert.AreEqual(Money.FromTl(8540), hatice.TrueValue, "Senaryo 2 (Hatice Teyze)");

            ProductDefinition s21 = ContentFixtures.Def("phone.samsun_vega_s21", basePrice: 24000);
            ValueBreakdown selin = calc.Calculate(ContentFixtures.Instance(s21, 26, 80, "original", 75, "ok", box: true, invoice: true), s21);
            Assert.AreEqual(Money.FromTl(18480), selin.ReferencePrice);
            Assert.AreEqual(Money.FromTl(17160), selin.TrueValue, "Senaryo 3 (Selin)");

            ProductDefinition y8 = ContentFixtures.Def("phone.yildiz_y8_plus", basePrice: 17500);
            ValueBreakdown cengiz = calc.Calculate(ContentFixtures.Instance(y8, 20, 66, "replaced_aftermarket", 65, "faulty"), y8);
            Assert.AreEqual(Money.FromTl(15400), cengiz.ReferencePrice);
            Assert.AreEqual(Money.FromTl(8850), cengiz.TrueValue, "Senaryo 4 (Cengiz)");

            ProductDefinition y5 = ContentFixtures.Def("phone.yildiz_y5", basePrice: 6000);
            ValueBreakdown tutorial = calc.Calculate(ContentFixtures.Instance(y5, 24, 92, "original", 90, "ok", box: true), y5);
            Assert.AreEqual(Money.FromTl(5280), tutorial.TrueValue, "Gün 1 öğretici telefon");
        }

        // ---------- bileşen çarpanları ----------

        [TestCase(0, 1.10)]
        [TestCase(6, 1.10)]
        [TestCase(7, 1.00)]
        [TestCase(12, 1.00)]
        [TestCase(13, 0.88)]
        [TestCase(24, 0.88)]
        [TestCase(25, 0.77)]
        [TestCase(36, 0.77)]
        [TestCase(37, 0.68)]
        [TestCase(48, 0.68)]
        [TestCase(49, 0.60)]
        [TestCase(120, 0.60)]
        public void AgeMultiplier_BandBoundaries(int age, double expected)
        {
            Assert.AreEqual(expected, E13(age, 95, "original", 100, "ok").AgeMultiplier, 1e-12);
        }

        [TestCase(100L, 1.0)]
        [TestCase(95L, 1.0)]
        [TestCase(90L, 1.0)]
        [TestCase(89L, 0.994)]
        [TestCase(78L, 0.928)]
        [TestCase(62L, 0.832)]
        [TestCase(50L, 0.76)]
        [TestCase(0L, 0.46)]
        public void BatteryMultiplier_Formula(long battery, double expected)
        {
            Assert.AreEqual(expected, E13(18, battery, "original", 100, "ok").BatteryMultiplier, 1e-9);
        }

        [TestCase(0L, 0.80)]
        [TestCase(50L, 0.90)]
        [TestCase(55L, 0.91)]
        [TestCase(85L, 0.97)]
        [TestCase(100L, 1.00)]
        public void BodyMultiplier_Formula(long body, double expected)
        {
            Assert.AreEqual(expected, E13(18, 95, "original", body, "ok").BodyMultiplier, 1e-9);
        }

        [TestCase("original", 1.00)]
        [TestCase("scratched", 0.96)]
        [TestCase("replaced_aftermarket", 0.88)]
        [TestCase("cracked", 0.75)]
        public void ScreenMultiplier_PerState(string screen, double expected)
        {
            Assert.AreEqual(expected, E13(18, 95, screen, 100, "ok").ScreenMultiplier, 1e-12);
        }

        [TestCase("ok", 1.00)]
        [TestCase("spotted", 0.93)]
        [TestCase("faulty", 0.82)]
        public void CameraMultiplier_PerState(string camera, double expected)
        {
            Assert.AreEqual(expected, E13(18, 95, "original", 100, camera).CameraMultiplier, 1e-12);
        }

        [TestCase(false, false, 1.00)]
        [TestCase(true, false, 1.02)]
        [TestCase(false, true, 1.02)]
        [TestCase(true, true, 1.04)]
        public void PackageMultiplier_BoxAndInvoice(bool box, bool invoice, double expected)
        {
            Assert.AreEqual(expected, E13(18, 95, "original", 100, "ok", box, invoice).PackageMultiplier, 1e-12);
        }

        [TestCase(128, 1.00)]
        [TestCase(256, 1.12)]
        [TestCase(512, 1.28)]
        public void StorageMultiplier_FromDefinition(int gb, double expected)
        {
            Assert.AreEqual(expected, E13(18, 95, "original", 100, "ok", storage: gb).StorageMultiplier, 1e-12);
        }

        [Test]
        public void StorageVariant_ChangesReferencePrice()
        {
            // 32.000 x 1,12 x 0,88 = 31.539,2 -> 31.540
            ValueBreakdown v = E13(18, 95, "original", 100, "ok", storage: 256);

            Assert.AreEqual(Money.FromTl(31540), v.ReferencePrice);
            Assert.AreEqual(Money.FromTl(31540), v.TrueValue);
        }

        [Test]
        public void ConditionMultiplier_IsBatteryScreenBodyCamera_WithoutPackage()
        {
            ValueBreakdown v = E13(18, 82, "scratched", 78, "ok", box: true);

            double expected = v.BatteryMultiplier * v.ScreenMultiplier * v.BodyMultiplier * v.CameraMultiplier;
            Assert.AreEqual(expected, v.ConditionMultiplier, 1e-12);
            Assert.AreEqual(1.02, v.PackageMultiplier, 1e-12);
        }

        [Test]
        public void TrueValue_IsReferenceTimesConditionTimesPackage()
        {
            ValueBreakdown v = E13(18, 82, "scratched", 78, "ok", box: true);

            Assert.AreEqual(v.ReferencePriceExact * v.ConditionMultiplier * v.PackageMultiplier, v.TrueValueExact, 1e-6);
            Assert.AreEqual(Money.FromDoubleRoundedTo10(v.TrueValueExact), v.TrueValue);
            Assert.AreEqual(Money.FromDoubleRoundedTo10(v.ReferencePriceExact), v.ReferencePrice);
        }

        [Test]
        public void Results_AreRoundedToTenTl()
        {
            ValueBreakdown v = E13(17, 83, "scratched", 71, "spotted", box: true);

            Assert.IsTrue(v.ReferencePrice.IsRoundedTo10);
            Assert.IsTrue(v.TrueValue.IsRoundedTo10);
        }

        [Test]
        public void ConvenienceMethods_MatchBreakdown()
        {
            ProductDefinition def = ContentFixtures.E13Pro();
            ProductInstance instance = ContentFixtures.Instance(def, 18, 82, "scratched", 78, "ok", box: true);
            var calc = Calc();

            ValueBreakdown v = calc.Calculate(instance, def);

            Assert.AreEqual(v.ReferencePrice, calc.ReferencePrice(instance, def));
            Assert.AreEqual(v.TrueValue, calc.TrueValue(instance, def));
        }

        // ---------- talep çarpanı ----------

        [Test]
        public void Demand_ScalesReferencePriceAndValue()
        {
            ProductDefinition def = ContentFixtures.E13Pro();
            ProductInstance pristine = ContentFixtures.Instance(def, 18, 95, "original", 100, "ok");
            var calc = Calc();

            ValueBreakdown high = calc.Calculate(pristine, def, 1.10);
            ValueBreakdown low = calc.Calculate(pristine, def, 0.90);
            ValueBreakdown neutral = calc.Calculate(pristine, def);

            Assert.AreEqual(1.0, neutral.DemandMultiplier, 1e-12);
            Assert.AreEqual(Money.FromTl(28160), neutral.TrueValue);
            Assert.AreEqual(Money.FromTl(30980), high.ReferencePrice, "32.000 x 0,88 x 1,10 = 30.976");
            Assert.AreEqual(Money.FromTl(30980), high.TrueValue);
            Assert.AreEqual(Money.FromTl(25340), low.TrueValue, "32.000 x 0,88 x 0,90 = 25.344");
        }

        [TestCase(0.0)]
        [TestCase(-1.0)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void Demand_MustBePositiveAndFinite(double demand)
        {
            ProductDefinition def = ContentFixtures.E13Pro();
            ProductInstance instance = ContentFixtures.Instance(def, 18, 95, "original", 100, "ok");

            Assert.Throws<ArgumentOutOfRangeException>(() => Calc().Calculate(instance, def, demand));
        }

        // ---------- mantıksal özellikler (mutation'a karşı ek koruma) ----------

        [Test]
        public void HigherBattery_NeverLowersValue()
        {
            long previous = -1;
            for (long battery = 0; battery <= 100; battery++)
            {
                long value = E13(18, battery, "original", 90, "ok").TrueValue.Tl;
                Assert.GreaterOrEqual(value, previous, "battery " + battery);
                previous = value;
            }
        }

        [Test]
        public void HigherBody_NeverLowersValue()
        {
            long previous = -1;
            for (long body = 0; body <= 100; body++)
            {
                long value = E13(18, 95, "original", body, "ok").TrueValue.Tl;
                Assert.GreaterOrEqual(value, previous, "body " + body);
                previous = value;
            }
        }

        [Test]
        public void OlderPhone_NeverWorthMore_AtSameCondition()
        {
            long previous = long.MaxValue;
            for (int age = 0; age <= 60; age++)
            {
                long value = E13(age, 95, "original", 100, "ok").TrueValue.Tl;
                Assert.LessOrEqual(value, previous, "age " + age);
                previous = value;
            }
        }

        [Test]
        public void WorseScreenOrCamera_IsWorth_Less()
        {
            long original = E13(18, 90, "original", 90, "ok").TrueValue.Tl;
            long scratched = E13(18, 90, "scratched", 90, "ok").TrueValue.Tl;
            long aftermarket = E13(18, 90, "replaced_aftermarket", 90, "ok").TrueValue.Tl;
            long cracked = E13(18, 90, "cracked", 90, "ok").TrueValue.Tl;
            Assert.Greater(original, scratched);
            Assert.Greater(scratched, aftermarket);
            Assert.Greater(aftermarket, cracked);

            long ok = E13(18, 90, "original", 90, "ok").TrueValue.Tl;
            long spotted = E13(18, 90, "original", 90, "spotted").TrueValue.Tl;
            long faulty = E13(18, 90, "original", 90, "faulty").TrueValue.Tl;
            Assert.Greater(ok, spotted);
            Assert.Greater(spotted, faulty);
        }

        [Test]
        public void Package_RaisesValue()
        {
            long none = E13(18, 90, "original", 90, "ok").TrueValue.Tl;
            long box = E13(18, 90, "original", 90, "ok", box: true).TrueValue.Tl;
            long both = E13(18, 90, "original", 90, "ok", box: true, invoice: true).TrueValue.Tl;
            Assert.Greater(box, none);
            Assert.Greater(both, box);
        }

        [Test]
        public void Calculator_DoesNotChangeInstanceOrDefinition()
        {
            ProductDefinition def = ContentFixtures.E13Pro();
            ProductInstance instance = ContentFixtures.Instance(def, 18, 82, "scratched", 78, "ok", box: true);

            Calc().Calculate(instance, def);

            Assert.AreEqual(82L, instance.GetNumber("battery"));
            Assert.AreEqual(Money.Zero, instance.PurchasePrice);
            Assert.AreEqual(Money.FromTl(32000), def.BasePrice);
        }

        [Test]
        public void Calculator_UsesTheTablesItWasGiven_NotHardcodedNumbers()
        {
            // Aynı ürün, farklı tablo -> farklı değer (sabit gömülü sayı olmadığını kanıtlar).
            var flat = new ValueTables(
                new[] { new AgeBand(0, 1.0) },
                90,
                0.0001,
                1.0,
                0.0,
                new[]
                {
                    new IdMultiplier("original", 1.0), new IdMultiplier("scratched", 1.0),
                    new IdMultiplier("replaced_aftermarket", 1.0), new IdMultiplier("cracked", 1.0)
                },
                new[] { new IdMultiplier("ok", 1.0), new IdMultiplier("spotted", 1.0), new IdMultiplier("faulty", 1.0) },
                0.0,
                0.0);
            ProductDefinition def = ContentFixtures.E13Pro();
            ProductInstance worst = ContentFixtures.Instance(def, 18, 90, "cracked", 0, "faulty", box: true, invoice: true);

            Assert.AreEqual(Money.FromTl(32000), new ValueCalculator(flat).TrueValue(worst, def));
            Assert.AreNotEqual(new ValueCalculator(flat).TrueValue(worst, def), Calc().TrueValue(worst, def));
        }

        [Test]
        public void Calculator_ReadsEveryTable_WithDistinctNumbers()
        {
            // Her tabloya FARKLI bir sayı verilir; hesap elle: hiçbir çarpan başka bir tabloyla karışamaz.
            var tables = new ValueTables(
                new[] { new AgeBand(0, 2.0), new AgeBand(10, 0.5) },
                80,
                0.01,
                0.5,
                1.0,
                new[]
                {
                    new IdMultiplier("original", 1.5), new IdMultiplier("scratched", 0.5),
                    new IdMultiplier("replaced_aftermarket", 0.4), new IdMultiplier("cracked", 0.3)
                },
                new[] { new IdMultiplier("ok", 1.25), new IdMultiplier("spotted", 0.25), new IdMultiplier("faulty", 0.2) },
                0.3,
                0.7);
            ProductDefinition def = ContentFixtures.Def("phone.custom", basePrice: 100000);
            ProductInstance instance = ContentFixtures.Instance(def, 12, 70, "scratched", 50, "spotted", box: true, invoice: false);

            ValueBreakdown v = new ValueCalculator(tables).Calculate(instance, def);

            Assert.AreEqual(1.0, v.StorageMultiplier, 1e-12);
            Assert.AreEqual(0.5, v.AgeMultiplier, 1e-12, "yaş 12 -> 10. aydan başlayan bant");
            Assert.AreEqual(0.9, v.BatteryMultiplier, 1e-12, "1 - 0,01 x (80 - 70)");
            Assert.AreEqual(0.5, v.ScreenMultiplier, 1e-12);
            Assert.AreEqual(1.0, v.BodyMultiplier, 1e-12, "0,5 + 1,0 x 0,5");
            Assert.AreEqual(0.25, v.CameraMultiplier, 1e-12);
            Assert.AreEqual(1.3, v.PackageMultiplier, 1e-12, "yalnızca kutu: 1 + 0,3 (fatura bonusu 0,7 karışmamalı)");
            Assert.AreEqual(50000.0, v.ReferencePriceExact, 1e-6);
            Assert.AreEqual(7312.5, v.TrueValueExact, 1e-6);
            Assert.AreEqual(Money.FromTl(50000), v.ReferencePrice);
            Assert.AreEqual(Money.FromTl(7310), v.TrueValue);

            ProductInstance invoiceOnly = ContentFixtures.Instance(def, 12, 70, "scratched", 50, "spotted", box: false, invoice: true);
            Assert.AreEqual(1.7, new ValueCalculator(tables).Calculate(invoiceOnly, def).PackageMultiplier, 1e-12);
        }

        // ---------- hatalı girdi ----------

        [Test]
        public void Constructor_RejectsNullTables()
        {
            Assert.Throws<ArgumentNullException>(() => new ValueCalculator(null));
        }

        [Test]
        public void Calculate_RejectsNullArguments()
        {
            ProductDefinition def = ContentFixtures.E13Pro();
            ProductInstance instance = ContentFixtures.Instance(def, 18, 95, "original", 100, "ok");

            Assert.Throws<ArgumentNullException>(() => Calc().Calculate(null, def));
            Assert.Throws<ArgumentNullException>(() => Calc().Calculate(instance, null));
        }

        [Test]
        public void Calculate_RejectsInstanceOfAnotherDefinition()
        {
            ProductDefinition e13 = ContentFixtures.E13Pro();
            ProductDefinition other = ContentFixtures.Def("phone.other");
            ProductInstance instance = ContentFixtures.Instance(e13, 18, 95, "original", 100, "ok");

            Assert.Throws<ArgumentException>(() => Calc().Calculate(instance, other));
        }

        [Test]
        public void Calculate_RejectsStorageNotOfferedByTheModel()
        {
            ProductDefinition def = ContentFixtures.E13Pro();
            ProductInstance instance = ContentFixtures.Instance(def, 18, 95, "original", 100, "ok", storageGb: 64);

            Assert.Throws<ArgumentException>(() => Calc().Calculate(instance, def));
        }

        [Test]
        public void Calculate_RejectsNegativeAge()
        {
            ProductDefinition def = ContentFixtures.E13Pro();
            ProductInstance instance = ContentFixtures.Instance(def, -1, 95, "original", 100, "ok");

            Assert.Throws<ArgumentOutOfRangeException>(() => Calc().Calculate(instance, def));
        }

        [TestCase(-1L)]
        [TestCase(101L)]
        public void Calculate_RejectsBatteryOutsideZeroToHundred(long battery)
        {
            ProductDefinition def = ContentFixtures.E13Pro();
            ProductInstance instance = ContentFixtures.Instance(def, 18, battery, "original", 100, "ok");

            Assert.Throws<ArgumentOutOfRangeException>(() => Calc().Calculate(instance, def));
        }

        [TestCase(-1L)]
        [TestCase(101L)]
        public void Calculate_RejectsBodyOutsideZeroToHundred(long body)
        {
            ProductDefinition def = ContentFixtures.E13Pro();
            ProductInstance instance = ContentFixtures.Instance(def, 18, 95, "original", body, "ok");

            Assert.Throws<ArgumentOutOfRangeException>(() => Calc().Calculate(instance, def));
        }

        [Test]
        public void Calculate_RejectsUnknownScreenOrCamera_AndNamesTheValue()
        {
            ProductDefinition def = ContentFixtures.E13Pro();

            ArgumentException screen = Assert.Throws<ArgumentException>(
                () => Calc().Calculate(ContentFixtures.Instance(def, 18, 95, "smashed", 100, "ok"), def));
            StringAssert.Contains("smashed", screen.Message);

            ArgumentException camera = Assert.Throws<ArgumentException>(
                () => Calc().Calculate(ContentFixtures.Instance(def, 18, 95, "original", 100, "blurry"), def));
            StringAssert.Contains("blurry", camera.Message);
        }

        [Test]
        public void Calculate_MissingAttribute_ThrowsKeyNotFound()
        {
            ProductDefinition def = ContentFixtures.E13Pro();
            ProductInstance instance = ContentFixtures.Instance(def, 18, 95, "original", 100, "ok");
            instance.Attributes.Remove("camera");

            Assert.Throws<KeyNotFoundException>(() => Calc().Calculate(instance, def));
        }

        [Test]
        public void Calculate_WrongAttributeKind_ThrowsInvalidOperation()
        {
            ProductDefinition def = ContentFixtures.E13Pro();
            ProductInstance instance = ContentFixtures.Instance(def, 18, 95, "original", 100, "ok");
            instance.Attributes["battery"] = AttributeValue.FromText("78");

            Assert.Throws<InvalidOperationException>(() => Calc().Calculate(instance, def));
        }

        // ---------- gerçek içerik ile uçtan uca ----------

        [Test]
        public void RealContent_GddE13ProGoldens()
        {
            var source = new DirectoryContentSource(TestPaths.ContentDataDirectory());
            ContentLoadResult result = ContentDatabase.Load(source);
            Assert.IsTrue(result.IsSuccess, result.FormatIssues());
            ContentDatabase db = result.Database;
            ProductDefinition e13 = db.GetProduct("phone.elma_e13_pro");
            var calc = new ValueCalculator(db.ValueTables);

            object[][] cases =
            {
                new object[] { 94L, "original", 92L, "ok", true, true, 28820L },
                new object[] { 82L, "scratched", 78L, "ok", true, false, 25100L },
                new object[] { 68L, "scratched", 55L, "spotted", false, false, 19860L },
                new object[] { 78L, "replaced_aftermarket", 85L, "ok", false, false, 22310L },
                new object[] { 62L, "cracked", 60L, "faulty", false, false, 13260L }
            };
            foreach (object[] c in cases)
            {
                ProductInstance instance = ContentFixtures.Instance(
                    e13, 18, (long)c[0], (string)c[1], (long)c[2], (string)c[3], (bool)c[4], (bool)c[5]);

                ValueBreakdown v = calc.Calculate(instance, e13);

                Assert.AreEqual(Money.FromTl(28160), v.ReferencePrice);
                Assert.AreEqual(Money.FromTl((long)c[6]), v.TrueValue, "GERÇEK İÇERİKLE GDD değeri tutmuyor: " + c[6]);
            }
        }
    }
}
