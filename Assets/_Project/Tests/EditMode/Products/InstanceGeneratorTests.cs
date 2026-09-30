using System;
using System.Collections.Generic;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Content;
using Esnaf.Domain.Products;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Products
{
    public class InstanceGeneratorTests
    {
        // profil, hafıza, yaş, pil, kasa, ekran, kamera, kutu, fatura, RF, gerçek değer
        // Beklenen değerler C# koduna bakmadan yazılan bağımsız Python referans modelinden üretildi (GDD v0.2 4.2-4.3 + v0.3 4.5).
        // Sabit alpha/beta fixture profilleri, ContentFixtures.Def() (baz 10.000; 128 x1,0 / 256 x1,2; yaş 6-36).
        private static readonly object[][] Seed2024Stream7Day1 =
        {
            new object[] { "alpha", 256, 20, 91L, 96L, "scratched", "ok", false, true, 10560L, 10260L },
            new object[] { "alpha", 256, 17, 88L, 77L, "scratched", "ok", true, false, 10560L, 9750L },
            new object[] { "alpha", 256, 11, 97L, 80L, "original", "ok", true, false, 12000L, 11750L },
            new object[] { "alpha", 256, 15, 94L, 91L, "original", "ok", true, true, 10560L, 10780L },
            new object[] { "alpha", 256, 11, 82L, 92L, "scratched", "ok", false, false, 12000L, 10790L },
            new object[] { "alpha", 256, 19, 84L, 66L, "scratched", "ok", true, false, 10560L, 9290L }
        };

        private static readonly object[][] Seed2024Stream7Day5 =
        {
            new object[] { "alpha", 256, 20, 91L, 96L, "scratched", "ok", false, true, 10560L, 10260L },
            new object[] { "beta", 256, 17, 61L, 58L, "replaced_aftermarket", "spotted", false, false, 10560L, 6540L },
            new object[] { "alpha", 256, 11, 97L, 80L, "original", "ok", true, false, 12000L, 11750L },
            new object[] { "beta", 256, 15, 67L, 60L, "replaced_aftermarket", "spotted", false, false, 10560L, 6850L },
            new object[] { "alpha", 256, 11, 82L, 92L, "scratched", "ok", false, false, 12000L, 10790L },
            new object[] { "alpha", 256, 19, 84L, 66L, "scratched", "ok", true, false, 10560L, 9290L },
            new object[] { "alpha", 256, 20, 98L, 63L, "original", "ok", false, false, 10560L, 9780L },
            new object[] { "alpha", 128, 6, 99L, 98L, "scratched", "ok", true, false, 11000L, 10730L }
        };

        private static readonly object[][] Seed99Stream1Day3 =
        {
            new object[] { "alpha", 128, 14, 83L, 66L, "scratched", "ok", true, false, 8800L, 7690L },
            new object[] { "alpha", 256, 22, 91L, 77L, "original", "ok", true, true, 10560L, 10480L },
            new object[] { "beta", 256, 14, 67L, 57L, "replaced_aftermarket", "spotted", false, false, 10560L, 6810L },
            new object[] { "alpha", 128, 6, 86L, 86L, "scratched", "ok", false, false, 11000L, 10020L },
            new object[] { "alpha", 256, 13, 81L, 61L, "scratched", "ok", true, false, 10560L, 9020L },
            new object[] { "beta", 128, 11, 67L, 52L, "replaced_aftermarket", "spotted", false, false, 10000L, 6380L }
        };

        private static InstanceGenerator NewGenerator(IdGenerator ids = null)
        {
            return new InstanceGenerator(ContentFixtures.Profiles(), ids ?? new IdGenerator());
        }

        private static void AssertGolden(object[][] golden, ulong seed, ulong stream, int day)
        {
            var rng = new PcgRandom(seed, stream);
            InstanceGenerator generator = NewGenerator();
            ProductDefinition def = ContentFixtures.Def();
            var calc = new ValueCalculator(ContentFixtures.Tables());

            for (int i = 0; i < golden.Length; i++)
            {
                object[] e = golden[i];
                ConditionProfile profile;
                ProductInstance p = generator.Generate(def, day, rng, out profile);
                string label = "instance #" + i;

                Assert.AreEqual((string)e[0], profile.Id, label + " profile");
                Assert.AreEqual((int)e[1], p.StorageGb, label + " storage");
                Assert.AreEqual((int)e[2], p.AgeMonths, label + " age");
                Assert.AreEqual((long)e[3], p.GetNumber("battery"), label + " battery");
                Assert.AreEqual((long)e[4], p.GetNumber("body"), label + " body");
                Assert.AreEqual((string)e[5], p.GetText("screen"), label + " screen");
                Assert.AreEqual((string)e[6], p.GetText("camera"), label + " camera");
                Assert.AreEqual((bool)e[7], p.GetFlag("box"), label + " box");
                Assert.AreEqual((bool)e[8], p.GetFlag("invoice"), label + " invoice");

                ValueBreakdown v = calc.Calculate(p, def);
                Assert.AreEqual(Money.FromTl((long)e[9]), v.ReferencePrice, label + " RF");
                Assert.AreEqual(Money.FromTl((long)e[10]), v.TrueValue, label + " value");
            }
        }

        // ---------- sabit tohumla altın değerler (Python referansıyla) ----------

        [Test]
        public void Golden_Seed2024_Stream7_Day1()
        {
            AssertGolden(Seed2024Stream7Day1, 2024UL, 7UL, 1);
        }

        [Test]
        public void Golden_Seed2024_Stream7_Day5()
        {
            AssertGolden(Seed2024Stream7Day5, 2024UL, 7UL, 5);
        }

        [Test]
        public void Golden_Seed99_Stream1_Day3()
        {
            AssertGolden(Seed99Stream1Day3, 99UL, 1UL, 3);
        }

        // ---------- kimlik, varsayılanlar, determinizm ----------

        [Test]
        public void Generate_AssignsSequentialIds_FromTheIdGenerator()
        {
            var ids = new IdGenerator(1041);
            InstanceGenerator generator = NewGenerator(ids);
            var rng = new PcgRandom(1UL, 1UL);
            ProductDefinition def = ContentFixtures.Def();

            long a = generator.Generate(def, 1, rng).InstanceId;
            long b = generator.Generate(def, 1, rng).InstanceId;

            Assert.AreEqual(1042L, a);
            Assert.AreEqual(1043L, b);
            Assert.AreEqual(1043L, ids.LastIssued);
        }

        [Test]
        public void Generate_LinksToDefinitionById_AndStartsOnTheMarket()
        {
            ProductInstance p = NewGenerator().Generate(ContentFixtures.Def(), 1, new PcgRandom(5UL, 5UL));

            Assert.AreEqual("phone.test_one", p.DefinitionId);
            Assert.AreEqual(ProductLocation.Market, p.Location);
            Assert.IsNull(p.AcquiredDay);
            Assert.AreEqual(Money.Zero, p.PurchasePrice);
            Assert.AreEqual(Money.Zero, p.CostBasis);
            Assert.IsNull(p.SellerNpcId);
            Assert.AreEqual(0L, p.ListingId);
            CollectionAssert.AreEquivalent(
                new[] { "battery", "screen", "body", "camera", "box", "invoice" }, p.Attributes.Keys.ToArray());
        }

        [Test]
        public void SameSeed_GivesIdenticalInstances_DifferentSeed_DoesNot()
        {
            ProductDefinition def = ContentFixtures.Def();
            var calc = new ValueCalculator(ContentFixtures.Tables());

            List<long> Values(ulong seed)
            {
                var rng = new PcgRandom(seed, 3UL);
                InstanceGenerator generator = NewGenerator();
                var list = new List<long>();
                for (int i = 0; i < 25; i++)
                {
                    list.Add(calc.TrueValue(generator.Generate(def, 5, rng), def).Tl);
                }

                return list;
            }

            CollectionAssert.AreEqual(Values(42UL), Values(42UL));
            CollectionAssert.AreNotEqual(Values(42UL), Values(43UL));
        }

        [Test]
        public void GeneratorsAreIndependentOfEachOther_OnlyTheRngMatters()
        {
            // Aynı rng durumu + aynı girdi = aynı sonuç (ID hariç): gizli global durum yok.
            ProductDefinition def = ContentFixtures.Def();
            var rngA = new PcgRandom(8UL, 8UL);
            var rngB = new PcgRandom(8UL, 8UL);

            ProductInstance a = NewGenerator(new IdGenerator(0)).Generate(def, 5, rngA);
            ProductInstance b = NewGenerator(new IdGenerator(500)).Generate(def, 5, rngB);

            Assert.AreEqual(a.StorageGb, b.StorageGb);
            Assert.AreEqual(a.AgeMonths, b.AgeMonths);
            Assert.AreEqual(a.GetNumber("battery"), b.GetNumber("battery"));
            Assert.AreEqual(a.GetText("screen"), b.GetText("screen"));
            Assert.AreNotEqual(a.InstanceId, b.InstanceId);
        }

        // ---------- gün kapısı (Gün 1–4 güvenlik ağı) ----------

        [Test]
        public void ProfileUnavailableBeforeItsDay_NeverAppears()
        {
            InstanceGenerator generator = NewGenerator();
            var rng = new PcgRandom(11UL, 11UL);
            ProductDefinition def = ContentFixtures.Def();

            for (int day = 1; day <= 2; day++)
            {
                for (int i = 0; i < 1500; i++)
                {
                    ConditionProfile profile;
                    generator.Generate(def, day, rng, out profile);
                    Assert.AreEqual("alpha", profile.Id, "beta Gün 3'ten önce çıkmamalı (gün " + day + ")");
                }
            }
        }

        [Test]
        public void ProfileAppearsFromItsDay_WithExpectedShare()
        {
            InstanceGenerator generator = NewGenerator();
            var rng = new PcgRandom(12UL, 12UL);
            ProductDefinition def = ContentFixtures.Def();
            const int draws = 20000;
            int beta = 0;

            for (int i = 0; i < draws; i++)
            {
                ConditionProfile profile;
                generator.Generate(def, 3, rng, out profile);
                if (profile.Id == "beta")
                {
                    beta++;
                }
            }

            double share = beta / (double)draws;
            Assert.That(share, Is.InRange(0.23, 0.27), "beta payı ~ 1/4 olmalı, ölçülen: " + share);
        }

        // ---------- değişmezler (invariants) ----------

        [Test]
        public void GeneratedAttributes_StayInsideTheirProfileRanges()
        {
            IReadOnlyList<ConditionProfile> profiles = ContentFixtures.Profiles();
            InstanceGenerator generator = new InstanceGenerator(profiles, new IdGenerator());
            var rng = new PcgRandom(13UL, 13UL);
            ProductDefinition def = ContentFixtures.Def();
            ValueTables tables = ContentFixtures.Tables();

            for (int i = 0; i < 10000; i++)
            {
                ConditionProfile profile;
                ProductInstance p = generator.Generate(def, 5, rng, out profile);

                Assert.That(p.GetNumber("battery"), Is.InRange((long)profile.BatteryMin, (long)profile.BatteryMax));
                Assert.That(p.GetNumber("body"), Is.InRange((long)profile.BodyMin, (long)profile.BodyMax));
                Assert.IsTrue(profile.ScreenChoices.Any(c => c.Value == p.GetText("screen")));
                Assert.IsTrue(profile.CameraChoices.Any(c => c.Value == p.GetText("camera")));
                Assert.That(p.AgeMonths, Is.InRange(def.MinAgeMonths, def.MaxAgeMonths));
                Assert.IsTrue(def.StorageOptions.Any(s => s.Gb == p.StorageGb));
                double ignored;
                Assert.IsTrue(tables.TryGetScreenMultiplier(p.GetText("screen"), out ignored));
                Assert.IsTrue(tables.TryGetCameraMultiplier(p.GetText("camera"), out ignored));
            }
        }

        [Test]
        public void BoxAndInvoiceFrequencies_FollowProfileChances()
        {
            InstanceGenerator generator = NewGenerator();
            var rng = new PcgRandom(14UL, 14UL);
            ProductDefinition def = ContentFixtures.Def();
            const int draws = 20000;
            int boxes = 0;
            int invoices = 0;
            int alphas = 0;

            for (int i = 0; i < draws; i++)
            {
                ConditionProfile profile;
                ProductInstance p = generator.Generate(def, 1, rng, out profile); // yalnızca alpha (kutu 0,5 / fatura 0,25)
                alphas++;
                boxes += p.GetFlag("box") ? 1 : 0;
                invoices += p.GetFlag("invoice") ? 1 : 0;
            }

            Assert.That(boxes / (double)alphas, Is.InRange(0.48, 0.52));
            Assert.That(invoices / (double)alphas, Is.InRange(0.23, 0.27));
        }

        [Test]
        public void DefinitionAndProfiles_AreNotMutated()
        {
            IReadOnlyList<ConditionProfile> profiles = ContentFixtures.Profiles();
            ProductDefinition def = ContentFixtures.Def();
            int storageCount = def.StorageOptions.Count;
            int weight = profiles[0].Weight;
            var generator = new InstanceGenerator(profiles, new IdGenerator());
            var rng = new PcgRandom(15UL, 15UL);

            for (int i = 0; i < 100; i++)
            {
                generator.Generate(def, 5, rng);
            }

            Assert.AreEqual(storageCount, def.StorageOptions.Count);
            Assert.AreEqual(weight, profiles[0].Weight);
            Assert.AreEqual(Money.FromTl(10000), def.BasePrice);
        }

        // ---------- hatalı girdi ----------

        [Test]
        public void Constructor_RejectsNullOrEmpty()
        {
            Assert.Throws<ArgumentNullException>(() => new InstanceGenerator(null, new IdGenerator()));
            Assert.Throws<ArgumentNullException>(() => new InstanceGenerator(ContentFixtures.Profiles(), null));
            Assert.Throws<ArgumentException>(() => new InstanceGenerator(new List<ConditionProfile>(), new IdGenerator()));
        }

        [Test]
        public void Generate_RejectsBadArguments()
        {
            InstanceGenerator generator = NewGenerator();
            var rng = new PcgRandom(1UL, 1UL);
            ProductDefinition def = ContentFixtures.Def();

            Assert.Throws<ArgumentNullException>(() => generator.Generate(null, 1, rng));
            Assert.Throws<ArgumentNullException>(() => generator.Generate(def, 1, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => generator.Generate(def, 0, rng));
            Assert.Throws<ArgumentOutOfRangeException>(() => generator.Generate(def, -3, rng));
        }

        [Test]
        public void Generate_WhenNoProfileIsAvailableYet_Throws()
        {
            var late = new ConditionProfile(
                "late", "Late", 1, 9, 50, 60, 50, 60,
                new[] { new WeightedValue("original", 1) }, new[] { new WeightedValue("ok", 1) }, 0.0, 0.0);
            var generator = new InstanceGenerator(new[] { late }, new IdGenerator());

            Assert.Throws<InvalidOperationException>(() => generator.Generate(ContentFixtures.Def(), 1, new PcgRandom(1UL, 1UL)));
        }

        [Test]
        public void Generate_DefinitionWithoutStorageOptions_Throws()
        {
            ProductDefinition noStorage = ContentFixtures.Def(storage: new StorageOption[0]);

            Assert.Throws<ArgumentException>(() => NewGenerator().Generate(noStorage, 1, new PcgRandom(1UL, 1UL)));
        }

        [Test]
        public void Generate_DefinitionWithInvertedAgeRange_Throws()
        {
            ProductDefinition inverted = ContentFixtures.Def(minAge: 30, maxAge: 10);

            Assert.Throws<ArgumentException>(() => NewGenerator().Generate(inverted, 1, new PcgRandom(1UL, 1UL)));
        }

        // ---------- gerçek içerik ile uçtan uca ----------

        private static ContentDatabase LoadReal()
        {
            ContentLoadResult result = ContentDatabase.Load(new DirectoryContentSource(TestPaths.ContentDataDirectory()));
            Assert.IsTrue(result.IsSuccess, result.FormatIssues());
            return result.Database;
        }

        [Test]
        public void RealContent_AllModels_ProduceValidInstances_WithPositiveValues()
        {
            ContentDatabase db = LoadReal();
            var generator = new InstanceGenerator(db.ConditionProfiles, new IdGenerator());
            var calc = new ValueCalculator(db.ValueTables);
            var rng = new PcgRandom(2026UL, 1UL);

            foreach (ProductDefinition def in db.Products)
            {
                for (int i = 0; i < 1500; i++)
                {
                    ProductInstance p = generator.Generate(def, 5, rng);
                    ValueBreakdown v = calc.Calculate(p, def);

                    Assert.That(p.AgeMonths, Is.InRange(def.MinAgeMonths, def.MaxAgeMonths), def.Id);
                    Assert.IsTrue(def.StorageOptions.Any(s => s.Gb == p.StorageGb), def.Id);
                    Assert.IsTrue(v.TrueValue.IsPositive, def.Id + " değer pozitif olmalı");
                    Assert.IsTrue(v.TrueValue.IsRoundedTo10, def.Id);
                }
            }
        }

        [Test]
        public void RealContent_ProblematicProfile_NeverAppearsBeforeDayFive()
        {
            ContentDatabase db = LoadReal();
            var generator = new InstanceGenerator(db.ConditionProfiles, new IdGenerator());
            var rng = new PcgRandom(2027UL, 1UL);
            ProductDefinition def = db.GetProduct("phone.elma_e13_pro");

            for (int day = 1; day <= 4; day++)
            {
                for (int i = 0; i < 3000; i++)
                {
                    ConditionProfile profile;
                    generator.Generate(def, day, rng, out profile);
                    Assert.AreNotEqual("problematic", profile.Id, "Gün " + day + ": sorunlu profil çıkmamalı");
                }
            }
        }

        [Test]
        public void RealContent_RepairedProfile_HasAtMostOneHiddenDefect_OnDaysOneToFour()
        {
            ContentDatabase db = LoadReal();
            var generator = new InstanceGenerator(db.ConditionProfiles, new IdGenerator());
            var rng = new PcgRandom(2028UL, 1UL);
            ProductDefinition def = db.GetProduct("phone.elma_e13_pro");
            int repairedSeen = 0;

            for (int i = 0; i < 6000; i++)
            {
                ConditionProfile profile;
                ProductInstance p = generator.Generate(def, 2, rng, out profile);
                if (profile.Id != "repaired")
                {
                    continue;
                }

                repairedSeen++;
                Assert.AreEqual("replaced_aftermarket", p.GetText("screen"));
                Assert.AreEqual("ok", p.GetText("camera"), "Tamirli profilde ekran dışında gizli kusur olmamalı");
            }

            Assert.Greater(repairedSeen, 500);
        }

        [Test]
        public void RealContent_ProfileShares_MatchGddWeights()
        {
            ContentDatabase db = LoadReal();
            var generator = new InstanceGenerator(db.ConditionProfiles, new IdGenerator());
            ProductDefinition def = db.GetProduct("phone.nova_n3_pro");
            const int draws = 30000;

            Dictionary<string, double> Shares(int day, ulong seed)
            {
                var rng = new PcgRandom(seed, 9UL);
                var counts = new Dictionary<string, int>();
                for (int i = 0; i < draws; i++)
                {
                    ConditionProfile profile;
                    generator.Generate(def, day, rng, out profile);
                    counts[profile.Id] = counts.ContainsKey(profile.Id) ? counts[profile.Id] + 1 : 1;
                }

                return counts.ToDictionary(kv => kv.Key, kv => kv.Value / (double)draws);
            }

            // Gün 5+: %25 / %35 / %20 / %15 / %5
            Dictionary<string, double> late = Shares(5, 31UL);
            Assert.That(late["clean"], Is.InRange(0.23, 0.27));
            Assert.That(late["used"], Is.InRange(0.33, 0.37));
            Assert.That(late["worn"], Is.InRange(0.18, 0.22));
            Assert.That(late["repaired"], Is.InRange(0.13, 0.17));
            Assert.That(late["problematic"], Is.InRange(0.03, 0.07));

            // Gün 1–4: sorunlu yok, kalanlar %95 üzerinden yeniden ölçeklenir (26,3 / 36,8 / 21,1 / 15,8)
            Dictionary<string, double> early = Shares(2, 32UL);
            Assert.IsFalse(early.ContainsKey("problematic"));
            Assert.That(early["clean"], Is.InRange(0.243, 0.283));
            Assert.That(early["used"], Is.InRange(0.348, 0.388));
            Assert.That(early["worn"], Is.InRange(0.191, 0.231));
            Assert.That(early["repaired"], Is.InRange(0.138, 0.178));
        }

        [Test]
        public void RealContent_BetterConditionProfiles_AreWorthMoreOnAverage()
        {
            ContentDatabase db = LoadReal();
            var generator = new InstanceGenerator(db.ConditionProfiles, new IdGenerator());
            var calc = new ValueCalculator(db.ValueTables);
            ProductDefinition def = db.GetProduct("phone.elma_e13_pro");
            var rng = new PcgRandom(2029UL, 1UL);
            var sums = new Dictionary<string, double>();
            var counts = new Dictionary<string, int>();

            for (int i = 0; i < 40000; i++)
            {
                ConditionProfile profile;
                ProductInstance p = generator.Generate(def, 5, rng, out profile);
                sums[profile.Id] = (sums.ContainsKey(profile.Id) ? sums[profile.Id] : 0.0) + calc.TrueValue(p, def).Tl;
                counts[profile.Id] = (counts.ContainsKey(profile.Id) ? counts[profile.Id] : 0) + 1;
            }

            double Mean(string id)
            {
                return sums[id] / counts[id];
            }

            // Sıra kuralı: temiz > kullanılmış > yıpranmış, tamirli < temiz, sorunlu en düşük.
            Assert.Greater(Mean("clean"), Mean("used"));
            Assert.Greater(Mean("used"), Mean("worn"));
            Assert.Greater(Mean("clean"), Mean("repaired"));
            Assert.Greater(Mean("worn"), Mean("problematic"));
            Assert.Greater(Mean("repaired"), Mean("problematic"));
        }
    }
}
