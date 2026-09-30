using System.Collections.Generic;
using System.Linq;
using Esnaf.Domain.Content;
using Esnaf.Domain.Products;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Content
{
    public class ValueTablesContentTests
    {
        private const string File = "value_tables.json";

        private static ValueTables Parse(string json, List<ContentIssue> issues)
        {
            return ContentParser.ParseValueTables(File, json, issues);
        }

        private static List<ContentIssue> Validate(ValueTables tables)
        {
            var issues = new List<ContentIssue>();
            ContentValidator.ValidateValueTables(tables, File, issues);
            return issues;
        }

        private static ValueTables Build(
            AgeBand[] age = null,
            int batteryFull = 90,
            double batteryPenalty = 0.006,
            double bodyBase = 0.80,
            double bodySpan = 0.20,
            IdMultiplier[] screen = null,
            IdMultiplier[] camera = null,
            double boxBonus = 0.02,
            double invoiceBonus = 0.02)
        {
            return new ValueTables(
                age ?? new[] { new AgeBand(0, 1.10), new AgeBand(7, 1.00), new AgeBand(13, 0.88) },
                batteryFull,
                batteryPenalty,
                bodyBase,
                bodySpan,
                screen ?? new[]
                {
                    new IdMultiplier("original", 1.00), new IdMultiplier("scratched", 0.96),
                    new IdMultiplier("replaced_aftermarket", 0.88), new IdMultiplier("cracked", 0.75)
                },
                camera ?? new[] { new IdMultiplier("ok", 1.00), new IdMultiplier("spotted", 0.93), new IdMultiplier("faulty", 0.82) },
                boxBonus,
                invoiceBonus);
        }

        private static void AssertOnly(List<ContentIssue> issues, string code)
        {
            Assert.AreEqual(1, issues.Count, string.Join("\n", issues.Select(i => i.ToString())));
            Assert.AreEqual(code, issues[0].Code);
            Assert.AreEqual(ContentIssueSeverity.Error, issues[0].Severity);
            Assert.AreEqual(File, issues[0].File);
        }

        // ---------- ayrıştırma ----------

        [Test]
        public void Parse_ValidFixture_ReadsEverything()
        {
            var issues = new List<ContentIssue>();

            ValueTables t = Parse(ContentFixtures.ValueTablesJson, issues);

            Assert.AreEqual(0, issues.Count, string.Join("\n", issues));
            Assert.AreEqual(6, t.AgeBands.Count);
            Assert.AreEqual(0, t.AgeBands[0].FromMonths);
            Assert.AreEqual(1.10, t.AgeBands[0].Multiplier, 1e-12);
            Assert.AreEqual(49, t.AgeBands[5].FromMonths);
            Assert.AreEqual(0.60, t.AgeBands[5].Multiplier, 1e-12);
            Assert.AreEqual(90, t.BatteryFullAtOrAbove);
            Assert.AreEqual(0.006, t.BatteryPenaltyPerPoint, 1e-12);
            Assert.AreEqual(0.80, t.BodyBase, 1e-12);
            Assert.AreEqual(0.20, t.BodySpan, 1e-12);
            Assert.AreEqual(4, t.ScreenMultipliers.Count);
            Assert.AreEqual(3, t.CameraMultipliers.Count);
            Assert.AreEqual(0.02, t.BoxBonus, 1e-12);
            Assert.AreEqual(0.02, t.InvoiceBonus, 1e-12);
        }

        [Test]
        public void TryGetScreenAndCameraMultiplier_Lookups()
        {
            ValueTables t = ContentFixtures.Tables();
            double m;

            Assert.IsTrue(t.TryGetScreenMultiplier("cracked", out m));
            Assert.AreEqual(0.75, m, 1e-12);
            Assert.IsFalse(t.TryGetScreenMultiplier("smashed", out m));
            Assert.IsFalse(t.TryGetScreenMultiplier(null, out m));
            Assert.IsTrue(t.TryGetCameraMultiplier("faulty", out m));
            Assert.AreEqual(0.82, m, 1e-12);
            Assert.IsFalse(t.TryGetCameraMultiplier("blurry", out m));
        }

        [Test]
        public void Lists_AreReadOnly()
        {
            ValueTables t = ContentFixtures.Tables();

            Assert.IsFalse(t.AgeBands is List<AgeBand>);
            Assert.IsFalse(t.AgeBands is AgeBand[]);
            Assert.IsFalse(t.ScreenMultipliers is List<IdMultiplier>);
            Assert.IsFalse(t.ScreenMultipliers is IdMultiplier[]);
            Assert.IsFalse(t.CameraMultipliers is IdMultiplier[]);
        }

        [Test]
        public void Parse_MissingSection_IsReported()
        {
            string json = ContentFixtures.ValueTablesJson.Replace(" \"battery\": { \"fullAtOrAbove\": 90, \"penaltyPerPoint\": 0.006 },", string.Empty);
            var issues = new List<ContentIssue>();

            Assert.IsNull(Parse(json, issues));
            Assert.IsTrue(issues.Any(i => i.Code == ContentIssueCodes.FieldMissing && i.Message.Contains("battery")), string.Join("\n", issues));
        }

        [Test]
        public void Parse_MissingNestedField_IsReported()
        {
            string json = ContentFixtures.ValueTablesJson.Replace("{ \"fromMonths\": 7, \"mult\": 1.00 }", "{ \"fromMonths\": 7 }");
            var issues = new List<ContentIssue>();

            Assert.IsNull(Parse(json, issues));
            Assert.IsTrue(issues.Any(i => i.Code == ContentIssueCodes.FieldMissing && i.Message.Contains("age[1].mult")), string.Join("\n", issues));
        }

        [Test]
        public void Parse_UnknownField_IsReported()
        {
            string json = ContentFixtures.ValueTablesJson.Replace("\"penaltyPerPoint\"", "\"penaltyPerPoints\"");
            var issues = new List<ContentIssue>();

            Assert.IsNull(Parse(json, issues));
            AssertOnly(issues, ContentIssueCodes.FileSyntax);
        }

        [Test]
        public void Parse_QuotedNumber_IsRejected()
        {
            string json = ContentFixtures.ValueTablesJson.Replace("\"mult\": 0.96", "\"mult\": \"0.96\"");
            var issues = new List<ContentIssue>();

            Assert.IsNull(Parse(json, issues));
            AssertOnly(issues, ContentIssueCodes.FileSyntax);
        }

        [Test]
        public void Parse_WrongSchemaVersion_IsReported()
        {
            var issues = new List<ContentIssue>();

            Assert.IsNull(Parse(ContentFixtures.ValueTablesJson.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 3"), issues));
            AssertOnly(issues, ContentIssueCodes.SchemaVersionUnsupported);
        }


        private static readonly object[][] MissingFieldCases =
        {
            new object[] { "\"battery\": { \"fullAtOrAbove\": 90, \"penaltyPerPoint\": 0.006 }", "\"battery\": null", "battery" },
            new object[] { "\"fullAtOrAbove\": 90, ", "", "battery.fullAtOrAbove" },
            new object[] { ", \"penaltyPerPoint\": 0.006", "", "battery.penaltyPerPoint" },
            new object[] { "\"body\": { \"base\": 0.80, \"span\": 0.20 }", "\"body\": null", "body" },
            new object[] { "\"base\": 0.80, ", "", "body.base" },
            new object[] { ", \"span\": 0.20", "", "body.span" },
            new object[] { "\"package\": { \"boxBonus\": 0.02, \"invoiceBonus\": 0.02 }", "\"package\": null", "package" },
            new object[] { "\"boxBonus\": 0.02, ", "", "package.boxBonus" },
            new object[] { ", \"invoiceBonus\": 0.02", "", "package.invoiceBonus" },
            new object[] { "{ \"fromMonths\": 7, \"mult\": 1.00 }", "{ \"mult\": 1.00 }", "age[1].fromMonths" },
            new object[] { "{ \"id\": \"original\", \"mult\": 1.00 }", "{ \"mult\": 1.00 }", "screen[0].id" },
            new object[] { "{ \"id\": \"original\", \"mult\": 1.00 }", "{ \"id\": \"original\" }", "screen[0].mult" },
            new object[] { "{ \"id\": \"ok\", \"mult\": 1.00 }", "{ \"mult\": 1.00 }", "camera[0].id" },
            new object[] { "{ \"id\": \"ok\", \"mult\": 1.00 }", "{ \"id\": \"ok\" }", "camera[0].mult" }
        };

        [TestCaseSource(nameof(MissingFieldCases))]
        public void Parse_EveryRequiredField_IsEnforced(string find, string replacement, string expectedField)
        {
            Assert.IsTrue(ContentFixtures.ValueTablesJson.Contains(find), "Test verisi bulunamadı: " + find);
            string json = ContentFixtures.ValueTablesJson.Replace(find, replacement);
            var issues = new List<ContentIssue>();

            ValueTables tables = Parse(json, issues);

            Assert.IsNull(tables, "Eksik alan varken tablo üretilmemeli: " + expectedField);
            Assert.IsTrue(
                issues.Any(i => i.Code == ContentIssueCodes.FieldMissing && i.Message.Contains("'" + expectedField + "'")),
                "Beklenen eksik alan '" + expectedField + "' raporlanmadı:\n" + string.Join("\n", issues));
        }

        // ---------- doğrulama ----------

        [Test]
        public void Validate_GoodTables_HasNoIssues()
        {
            Assert.AreEqual(0, Validate(ContentFixtures.Tables()).Count);
        }

        [Test]
        public void Validate_EmptyAge_IsReported()
        {
            AssertOnly(Validate(Build(age: new AgeBand[0])), ContentIssueCodes.ValueTablesAgeEmpty);
        }

        [Test]
        public void Validate_FirstAgeBand_MustStartAtZero()
        {
            AssertOnly(Validate(Build(age: new[] { new AgeBand(1, 1.0), new AgeBand(7, 0.9) })), ContentIssueCodes.ValueTablesAgeFirstNotZero);
        }

        [Test]
        public void Validate_AgeBands_MustBeStrictlyIncreasing()
        {
            AssertOnly(
                Validate(Build(age: new[] { new AgeBand(0, 1.0), new AgeBand(13, 0.9), new AgeBand(13, 0.8) })),
                ContentIssueCodes.ValueTablesAgeNotIncreasing);
            AssertOnly(
                Validate(Build(age: new[] { new AgeBand(0, 1.0), new AgeBand(13, 0.9), new AgeBand(7, 0.8) })),
                ContentIssueCodes.ValueTablesAgeNotIncreasing);
        }

        [TestCase(0.0)]
        [TestCase(-0.5)]
        [TestCase(double.NaN)]
        public void Validate_AgeMultiplier_MustBePositive(double multiplier)
        {
            AssertOnly(Validate(Build(age: new[] { new AgeBand(0, 1.0), new AgeBand(7, multiplier) })), ContentIssueCodes.ValueTablesAgeMultNotPositive);
        }

        [TestCase(-1, 0.006)]
        [TestCase(101, 0.006)]
        [TestCase(90, 0.0)]
        [TestCase(90, -0.001)]
        [TestCase(90, 0.02)]   // 1 - 0.02*90 < 0: pil %0'da çarpan pozitif kalmaz
        public void Validate_Battery_Invalid(int full, double penalty)
        {
            AssertOnly(Validate(Build(batteryFull: full, batteryPenalty: penalty)), ContentIssueCodes.ValueTablesBatteryInvalid);
        }

        [TestCase(0.0, 0.2)]
        [TestCase(-0.1, 0.2)]
        [TestCase(0.8, -0.1)]
        [TestCase(double.NaN, 0.2)]
        public void Validate_Body_Invalid(double bodyBase, double span)
        {
            AssertOnly(Validate(Build(bodyBase: bodyBase, bodySpan: span)), ContentIssueCodes.ValueTablesBodyInvalid);
        }

        [Test]
        public void Validate_ScreenTable_MustContainAllRequiredIds()
        {
            var screen = new[] { new IdMultiplier("original", 1.0), new IdMultiplier("scratched", 0.96), new IdMultiplier("replaced_aftermarket", 0.88) };

            List<ContentIssue> issues = Validate(Build(screen: screen));

            AssertOnly(issues, ContentIssueCodes.ValueTablesScreenMissingId);
            StringAssert.Contains("cracked", issues[0].Message);
        }

        [Test]
        public void Validate_CameraTable_MustContainAllRequiredIds()
        {
            var camera = new[] { new IdMultiplier("ok", 1.0), new IdMultiplier("spotted", 0.93) };

            List<ContentIssue> issues = Validate(Build(camera: camera));

            AssertOnly(issues, ContentIssueCodes.ValueTablesCameraMissingId);
            StringAssert.Contains("faulty", issues[0].Message);
        }

        [Test]
        public void Validate_DuplicateScreenId_IsReported()
        {
            var screen = new[]
            {
                new IdMultiplier("original", 1.0), new IdMultiplier("scratched", 0.96), new IdMultiplier("replaced_aftermarket", 0.88),
                new IdMultiplier("cracked", 0.75), new IdMultiplier("cracked", 0.70)
            };

            AssertOnly(Validate(Build(screen: screen)), ContentIssueCodes.ValueTablesScreenDuplicate);
        }

        [Test]
        public void Validate_DuplicateCameraId_IsReported()
        {
            var camera = new[] { new IdMultiplier("ok", 1.0), new IdMultiplier("spotted", 0.93), new IdMultiplier("faulty", 0.82), new IdMultiplier("ok", 0.9) };

            AssertOnly(Validate(Build(camera: camera)), ContentIssueCodes.ValueTablesCameraDuplicate);
        }

        [TestCase(0.0)]
        [TestCase(-1.0)]
        [TestCase(double.NaN)]
        public void Validate_ScreenAndCameraMultipliers_MustBePositive(double multiplier)
        {
            var screen = new[]
            {
                new IdMultiplier("original", 1.0), new IdMultiplier("scratched", multiplier),
                new IdMultiplier("replaced_aftermarket", 0.88), new IdMultiplier("cracked", 0.75)
            };
            var camera = new[] { new IdMultiplier("ok", 1.0), new IdMultiplier("spotted", multiplier), new IdMultiplier("faulty", 0.82) };

            AssertOnly(Validate(Build(screen: screen)), ContentIssueCodes.ValueTablesScreenMultNotPositive);
            AssertOnly(Validate(Build(camera: camera)), ContentIssueCodes.ValueTablesCameraMultNotPositive);
        }

        [TestCase(-0.01, 0.02)]
        [TestCase(0.02, -0.01)]
        [TestCase(double.NaN, 0.02)]
        public void Validate_PackageBonuses_CannotBeNegative(double box, double invoice)
        {
            AssertOnly(Validate(Build(boxBonus: box, invoiceBonus: invoice)), ContentIssueCodes.ValueTablesPackageNegative);
        }

        [Test]
        public void Validate_CollectsAllProblems()
        {
            List<ContentIssue> issues = Validate(Build(age: new AgeBand[0], batteryFull: 200, bodyBase: -1));

            Assert.GreaterOrEqual(issues.Count, 3);
        }

        // ---------- gerçek dosya (GDD v0.2 Bölüm 4.2) ----------

        [Test]
        public void RealFile_MatchesGddV02Tables()
        {
            var source = new DirectoryContentSource(TestPaths.ContentDataDirectory());
            string text;
            Assert.IsTrue(source.TryGetText(ContentFileNames.ValueTables, out text), "value_tables.json bulunamadı");
            var issues = new List<ContentIssue>();

            ValueTables t = Parse(text, issues);

            Assert.AreEqual(0, issues.Count, string.Join("\n", issues));
            Assert.AreEqual(0, Validate(t).Count);

            int[] fromMonths = { 0, 7, 13, 25, 37, 49 };
            double[] mult = { 1.10, 1.00, 0.88, 0.77, 0.68, 0.60 };
            Assert.AreEqual(fromMonths.Length, t.AgeBands.Count);
            for (int i = 0; i < fromMonths.Length; i++)
            {
                Assert.AreEqual(fromMonths[i], t.AgeBands[i].FromMonths, "age band " + i);
                Assert.AreEqual(mult[i], t.AgeBands[i].Multiplier, 1e-12, "age band " + i);
            }

            Assert.AreEqual(90, t.BatteryFullAtOrAbove);
            Assert.AreEqual(0.006, t.BatteryPenaltyPerPoint, 1e-12);
            Assert.AreEqual(0.80, t.BodyBase, 1e-12);
            Assert.AreEqual(0.20, t.BodySpan, 1e-12);
            Assert.AreEqual(0.02, t.BoxBonus, 1e-12);
            Assert.AreEqual(0.02, t.InvoiceBonus, 1e-12);

            var expectedScreen = new Dictionary<string, double>
            {
                { "original", 1.00 }, { "scratched", 0.96 }, { "replaced_aftermarket", 0.88 }, { "cracked", 0.75 }
            };
            var expectedCamera = new Dictionary<string, double> { { "ok", 1.00 }, { "spotted", 0.93 }, { "faulty", 0.82 } };
            Assert.AreEqual(expectedScreen.Count, t.ScreenMultipliers.Count);
            Assert.AreEqual(expectedCamera.Count, t.CameraMultipliers.Count);
            foreach (KeyValuePair<string, double> pair in expectedScreen)
            {
                double m;
                Assert.IsTrue(t.TryGetScreenMultiplier(pair.Key, out m), pair.Key);
                Assert.AreEqual(pair.Value, m, 1e-12, pair.Key);
            }

            foreach (KeyValuePair<string, double> pair in expectedCamera)
            {
                double m;
                Assert.IsTrue(t.TryGetCameraMultiplier(pair.Key, out m), pair.Key);
                Assert.AreEqual(pair.Value, m, 1e-12, pair.Key);
            }
        }
    }
}
