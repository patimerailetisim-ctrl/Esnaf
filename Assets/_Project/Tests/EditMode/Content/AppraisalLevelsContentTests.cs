using System.Collections.Generic;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Appraisal;
using Esnaf.Domain.Content;
using Esnaf.Domain.Products;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Content
{
    public class AppraisalLevelsContentTests
    {
        private const string File = "appraisal_levels.json";
        private static readonly string Good = ContentFixtures.AppraisalLevelsJson;
        private const string S1 = "\"s1\"";
        private const string S3 = "\"s3\"";
        private const string Check0 = "\"checks\"";

        private static AppraisalConfig Parse(string json, List<ContentIssue> issues)
        {
            return ContentParser.ParseAppraisal(File, json, issues);
        }

        private static List<ContentIssue> Validate(string json)
        {
            var issues = new List<ContentIssue>();
            AppraisalConfig config = Parse(json, issues);
            Assert.IsNotNull(config, "Test verisi ayrıştırılamadı: " + string.Join("\n", issues));
            Assert.AreEqual(0, issues.Count, "Ayrıştırma sorunu: " + string.Join("\n", issues));
            ContentValidator.ValidateAppraisal(config, ContentFixtures.Tables(), File, issues);
            return issues;
        }

        private static void AssertOneField(List<ContentIssue> issues, string field)
        {
            Assert.AreEqual(1, issues.Count, string.Join("\n", issues.Select(i => i.ToString())));
            Assert.AreEqual(ContentIssueCodes.AppraisalFieldInvalid, issues[0].Code, issues[0].ToString());
            Assert.AreEqual(File, issues[0].File);
            StringAssert.Contains(field, issues[0].Message);
        }

        // ---------- ayrıştırma ----------

        [Test]
        public void Parse_Fixture_ReadsEveryLevelAndCheck()
        {
            var issues = new List<ContentIssue>();

            AppraisalConfig c = Parse(Good, issues);

            Assert.AreEqual(0, issues.Count, string.Join("\n", issues));
            CollectionAssert.AreEqual(new[] { "s0", "s1", "s2", "s3" }, c.Levels.Select(l => l.Id).ToArray());
            AppraisalLevel s3 = c.Levels[3];
            Assert.AreEqual("Profesyonel ekspertiz", s3.Name);
            Assert.AreEqual(6, s3.UnlockDay);
            Assert.AreEqual("test_device", s3.RequiredEquipment);
            Assert.AreEqual(Money.FromTl(700), s3.FeeFor(ProductSegment.Entry));
            Assert.AreEqual(Money.FromTl(1200), s3.FeeFor(ProductSegment.Mid));
            Assert.AreEqual(Money.FromTl(2000), s3.FeeFor(ProductSegment.Upper));
            Assert.AreEqual(AppraisalConfidence.Certain, s3.Confidence);
            Assert.AreEqual(1.0, s3.EvidencePower, 1e-12);
            Assert.AreEqual(0.99, s3.DetectChance("screen"), 1e-12);
            Assert.AreEqual(0.99, s3.DetectChance("camera"), 1e-12);
            Assert.AreEqual(0.0, s3.DetectChance("battery"), "kontrol edilmeyen nitelik");
            Assert.AreEqual(0.005, s3.FalseAlarmChance, 1e-12);
            Assert.AreEqual(2, s3.BatteryHalfWidth);
            Assert.AreEqual(3, s3.BodyHalfWidth);
            Assert.AreEqual(0.025, s3.ValueHalfWidth.Value, 1e-12);
            Assert.AreEqual(0.0, s3.CenterShift);
            Assert.AreEqual(0.0, s3.ValueNoise);
            Assert.AreEqual(0.96, s3.CoverageEstimate.Value, 1e-12);
            Assert.AreEqual(1.02, c.ExpectedSaleFactor, 1e-12);
            Assert.AreEqual(2, c.Checks.Count);
            Assert.AreEqual("screen", c.Checks[0].Attribute);
            CollectionAssert.AreEqual(new[] { "replaced_aftermarket" }, c.Checks[0].DefectValues.ToArray());
            Assert.AreEqual("replaced_aftermarket", c.Checks[0].FalseAlarmValue);
            Assert.AreEqual("original", c.Checks[0].CleanValue);
            Assert.AreEqual("appraisal.finding.screen_replaced", c.Checks[0].WordingKey);
            CollectionAssert.AreEqual(new[] { "spotted", "faulty" }, c.Checks[1].DefectValues.ToArray());
            Assert.IsTrue(c.Checks[1].IsDefect("faulty"));
            Assert.IsFalse(c.Checks[1].IsDefect("ok"));
        }

        [Test]
        public void Parse_OptionalFields_AreNullWhenAbsent()
        {
            AppraisalLevel s0 = ContentFixtures.Appraisal().Levels[0];

            Assert.IsNull(s0.RequiredEquipment);
            Assert.IsNull(s0.BatteryHalfWidth, "S0: pil beyan (aralık yok)");
            Assert.AreEqual(15, s0.BodyHalfWidth);
            Assert.IsNull(s0.ValueHalfWidth, "S0: değer aralığı yok");
            Assert.IsNull(s0.CoverageEstimate);
            Assert.AreEqual(AppraisalConfidence.Hint, s0.Confidence);
            Assert.AreEqual(Money.Zero, s0.FeeFor(ProductSegment.Upper), "S0 ücretsiz");
        }

        [Test]
        public void Config_LevelLookup()
        {
            AppraisalConfig c = ContentFixtures.Appraisal();
            AppraisalLevel level;

            Assert.IsTrue(c.TryGetLevel("s2", out level));
            Assert.AreEqual(5, level.UnlockDay);
            Assert.IsFalse(c.TryGetLevel("s9", out level));
            Assert.IsFalse(c.TryGetLevel(null, out level));
            Assert.AreEqual("s1", c.GetLevel("s1").Id);
            Assert.Throws<KeyNotFoundException>(() => c.GetLevel("s9"));
            Assert.AreEqual(1, c.IndexOf("s1"));
            Assert.AreEqual(-1, c.IndexOf("nope"));
        }

        [Test]
        public void ConfidenceStrings_Parse()
        {
            AppraisalConfidence value;

            Assert.IsTrue(AppraisalConfidences.TryParse("hint", out value));
            Assert.AreEqual(AppraisalConfidence.Hint, value);
            Assert.IsTrue(AppraisalConfidences.TryParse("low", out value));
            Assert.AreEqual(AppraisalConfidence.Low, value);
            Assert.IsTrue(AppraisalConfidences.TryParse("medium", out value));
            Assert.AreEqual(AppraisalConfidence.Medium, value);
            Assert.IsTrue(AppraisalConfidences.TryParse("certain", out value));
            Assert.AreEqual(AppraisalConfidence.Certain, value);
            Assert.IsFalse(AppraisalConfidences.TryParse("sure", out value));
            Assert.IsFalse(AppraisalConfidences.TryParse(null, out value));
        }

        private static readonly object[][] MissingCases =
        {
            new object[] { "levels", new string[0], "levels" },
            new object[] { "name", new[] { S1 }, "levels[1].name" },
            new object[] { "unlockDay", new[] { S1 }, "levels[1].unlockDay" },
            new object[] { "fees", new[] { S1 }, "levels[1].fees" },
            new object[] { "entry", new[] { S1, "\"fees\"" }, "levels[1].fees.entry" },
            new object[] { "mid", new[] { S1, "\"fees\"" }, "levels[1].fees.mid" },
            new object[] { "upper", new[] { S1, "\"fees\"" }, "levels[1].fees.upper" },
            new object[] { "confidence", new[] { S1 }, "levels[1].confidence" },
            new object[] { "evidencePower", new[] { S1 }, "levels[1].evidencePower" },
            new object[] { "detect", new[] { S1 }, "levels[1].detect" },
            new object[] { "falseAlarm", new[] { S1 }, "levels[1].falseAlarm" },
            new object[] { "centerShift", new[] { S1 }, "levels[1].centerShift" },
            new object[] { "valueNoise", new[] { S1 }, "levels[1].valueNoise" },
            new object[] { "id", new[] { "\"levels\"" }, "levels[0].id" },
            new object[] { "checks", new string[0], "checks" },
            new object[] { "attribute", new[] { Check0 }, "checks[0].attribute" },
            new object[] { "defectValues", new[] { Check0 }, "checks[0].defectValues" },
            new object[] { "falseAlarmValue", new[] { Check0 }, "checks[0].falseAlarmValue" },
            new object[] { "cleanValue", new[] { Check0 }, "checks[0].cleanValue" },
            new object[] { "wordingKey", new[] { Check0 }, "checks[0].wordingKey" },
            new object[] { "riskCard", new string[0], "riskCard" },
            new object[] { "expectedSaleFactor", new[] { "\"riskCard\"" }, "riskCard.expectedSaleFactor" }
        };

        [TestCaseSource(nameof(MissingCases))]
        public void Parse_EveryRequiredField_IsEnforced(string property, string[] path, string field)
        {
            var issues = new List<ContentIssue>();

            Assert.IsNull(Parse(JsonEdit.Missing(Good, property, path), issues));

            Assert.IsTrue(
                issues.Any(i => i.Code == ContentIssueCodes.FieldMissing && i.Message.Contains("'" + field + "'")),
                "Eksik alan '" + field + "' raporlanmadı:\n" + string.Join("\n", issues));
        }

        [Test]
        public void Parse_UnknownConfidence_IsReported()
        {
            var issues = new List<ContentIssue>();

            Assert.IsNull(Parse(JsonEdit.Set(Good, "confidence", "\"sure\"", S1), issues));

            Assert.IsTrue(issues.Any(i => i.Code == ContentIssueCodes.AppraisalConfidenceInvalid && i.Message.Contains("sure")));
        }

        [Test]
        public void Parse_UnknownField_AndWrongSchema_AreReported()
        {
            var issues = new List<ContentIssue>();
            Assert.IsNull(Parse(Good.Replace("evidencePower", "evidencePwer"), issues));
            Assert.AreEqual(ContentIssueCodes.FileSyntax, issues.Single().Code);

            issues.Clear();
            Assert.IsNull(Parse(Good.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 4"), issues));
            Assert.AreEqual(ContentIssueCodes.SchemaVersionUnsupported, issues.Single().Code);
        }

        [Test]
        public void Parse_FractionalMoney_IsRejected()
        {
            var issues = new List<ContentIssue>();

            Assert.IsNull(Parse(JsonEdit.Set(Good, "entry", "100.5", S1, "\"fees\""), issues));

            Assert.AreEqual(ContentIssueCodes.FileSyntax, issues.Single().Code);
        }

        // ---------- doğrulama ----------

        [Test]
        public void Validate_Good_HasNoIssues()
        {
            Assert.AreEqual(0, Validate(Good).Count);
        }

        [Test]
        public void Validate_EmptyLevels_IsReported()
        {
            List<ContentIssue> issues = Validate(JsonEdit.Set(Good, "levels", "[]"));

            Assert.AreEqual(ContentIssueCodes.AppraisalLevelsEmpty, issues.Single().Code);
        }

        [TestCase("S1")]
        [TestCase("1s")]
        [TestCase("s-1")]
        [TestCase("")]
        public void Validate_BadLevelId_IsReported(string id)
        {
            List<ContentIssue> issues = Validate(Good.Replace("\"id\": \"s0\"", "\"id\": \"" + id + "\""));

            Assert.IsTrue(issues.Any(i => i.Code == ContentIssueCodes.AppraisalLevelIdFormat), string.Join("\n", issues));
        }

        [Test]
        public void Validate_DuplicateLevelId_IsReported()
        {
            string json = Good.Replace("\"id\": \"s2\"", "\"id\": \"s1\"");

            List<ContentIssue> issues = Validate(json);

            Assert.AreEqual(ContentIssueCodes.AppraisalLevelIdDuplicate, issues.Single().Code);
        }

        private static readonly object[][] BadValues =
        {
            new object[] { "unlockDay", "0", new[] { S1 }, "unlockDay" },
            new object[] { "entry", "-10", new[] { S1, "\"fees\"" }, "fees.entry" },
            new object[] { "mid", "205", new[] { S1, "\"fees\"" }, "fees.mid" },
            new object[] { "upper", "-1", new[] { S1, "\"fees\"" }, "fees.upper" },
            new object[] { "evidencePower", "0", new[] { S1 }, "evidencePower" },
            new object[] { "evidencePower", "1.01", new[] { S1 }, "evidencePower" },
            new object[] { "falseAlarm", "-0.01", new[] { S1 }, "falseAlarm" },
            new object[] { "falseAlarm", "1.01", new[] { S1 }, "falseAlarm" },
            new object[] { "screen", "1.01", new[] { S1, "\"detect\"" }, "detect.screen" },
            new object[] { "camera", "-0.01", new[] { S1, "\"detect\"" }, "detect.camera" },
            new object[] { "detect", "{ \"screen\": 0.7 }", new[] { S1 }, "detect.camera" },
            new object[] { "detect", "{ \"screen\": 0.7, \"camera\": 0.6, \"battery\": 0.5 }", new[] { S1 }, "detect.battery" },
            new object[] { "batteryHalfWidth", "0", new[] { S1 }, "batteryHalfWidth" },
            new object[] { "batteryHalfWidth", "51", new[] { S1 }, "batteryHalfWidth" },
            new object[] { "bodyHalfWidth", "0", new[] { S1 }, "bodyHalfWidth" },
            new object[] { "bodyHalfWidth", "51", new[] { S1 }, "bodyHalfWidth" },
            new object[] { "valueHalfWidth", "0", new[] { S1 }, "valueHalfWidth" },
            new object[] { "valueHalfWidth", "0.5", new[] { S1 }, "valueHalfWidth" },
            new object[] { "centerShift", "-0.01", new[] { S1 }, "centerShift" },
            new object[] { "centerShift", "1", new[] { S1 }, "centerShift" },
            new object[] { "valueNoise", "-0.01", new[] { S1 }, "valueNoise" },
            new object[] { "valueNoise", "0.5", new[] { S1 }, "valueNoise" },
            new object[] { "coverageEstimate", "0", new[] { S1 }, "coverageEstimate" },
            new object[] { "coverageEstimate", "1.01", new[] { S1 }, "coverageEstimate" },
            new object[] { "requiredEquipment", "\"\"", new[] { S3 }, "requiredEquipment" },
            new object[] { "name", "\" \"", new[] { S1 }, "name" },
            new object[] { "defectValues", "[]", new[] { Check0 }, "checks[0].defectValues" },
            new object[] { "defectValues", "[ \"no_such_screen\" ]", new[] { Check0 }, "checks[0].defectValues" },
            new object[] { "falseAlarmValue", "\"scratched\"", new[] { Check0 }, "checks[0].falseAlarmValue" },
            new object[] { "cleanValue", "\"no_such_screen\"", new[] { Check0 }, "checks[0].cleanValue" },
            new object[] { "cleanValue", "\"replaced_aftermarket\"", new[] { Check0 }, "checks[0].cleanValue" },
            new object[] { "wordingKey", "\" \"", new[] { Check0 }, "checks[0].wordingKey" },
            new object[] { "expectedSaleFactor", "0", new[] { "\"riskCard\"" }, "expectedSaleFactor" },
            new object[] { "expectedSaleFactor", "1.26", new[] { "\"riskCard\"" }, "expectedSaleFactor" }
        };

        [TestCaseSource(nameof(BadValues))]
        public void Validate_BadValue_IsReported(string property, string newValue, string[] path, string field)
        {
            AssertOneField(Validate(JsonEdit.Set(Good, property, newValue, path)), field);
        }

        private static readonly object[][] BoundaryValues =
        {
            new object[] { "unlockDay", "1", new[] { S1 } },
            new object[] { "entry", "0", new[] { S1, "\"fees\"" } },
            new object[] { "evidencePower", "1", new[] { S1 } },
            new object[] { "evidencePower", "0.01", new[] { S1 } },
            new object[] { "falseAlarm", "0", new[] { S1 } },
            new object[] { "falseAlarm", "1", new[] { S1 } },
            new object[] { "screen", "0", new[] { S1, "\"detect\"" } },
            new object[] { "screen", "1", new[] { S1, "\"detect\"" } },
            new object[] { "batteryHalfWidth", "50", new[] { S1 } },
            new object[] { "batteryHalfWidth", "1", new[] { S1 } },
            new object[] { "bodyHalfWidth", "50", new[] { S1 } },
            new object[] { "valueHalfWidth", "0.49", new[] { S1 } },
            new object[] { "centerShift", "0", new[] { S1 } },
            new object[] { "centerShift", "0.99", new[] { S1 } },
            new object[] { "valueNoise", "0.49", new[] { S1 } },
            new object[] { "coverageEstimate", "1", new[] { S1 } },
            new object[] { "expectedSaleFactor", "1.25", new[] { "\"riskCard\"" } },
            new object[] { "expectedSaleFactor", "0.01", new[] { "\"riskCard\"" } }
        };

        [TestCaseSource(nameof(BoundaryValues))]
        public void Validate_BoundaryValues_AreAccepted(string property, string newValue, string[] path)
        {
            Assert.AreEqual(0, Validate(JsonEdit.Set(Good, property, newValue, path)).Count);
        }

        [Test]
        public void Validate_UnsupportedCheckAttribute_IsReported()
        {
            List<ContentIssue> issues = Validate(JsonEdit.Set(Good, "attribute", "\"battery\"", Check0));

            Assert.IsTrue(issues.Any(i => i.Code == ContentIssueCodes.AppraisalFieldInvalid && i.Message.Contains("checks[0].attribute")), string.Join("\n", issues));
        }

        [Test]
        public void Validate_DuplicateCheckAttribute_IsReported()
        {
            string json = Good.Replace("\"attribute\": \"camera\"", "\"attribute\": \"screen\"");

            List<ContentIssue> issues = Validate(json);

            Assert.IsTrue(issues.Any(i => i.Code == ContentIssueCodes.AppraisalFieldInvalid && i.Message.Contains("checks[1].attribute")), string.Join("\n", issues));
        }

        [Test]
        public void Validate_ValueRangeWithoutBatteryOrBodyRange_IsReported()
        {
            AssertOneField(Validate(JsonEdit.Missing(Good, "batteryHalfWidth", S1)), "valueHalfWidth");
            AssertOneField(Validate(JsonEdit.Missing(Good, "bodyHalfWidth", S1)), "valueHalfWidth");
        }

        [Test]
        public void Validate_EmptyChecks_IsReported()
        {
            List<ContentIssue> issues = Validate(JsonEdit.Set(Good, "checks", "[]"));

            Assert.IsTrue(issues.Any(i => i.Code == ContentIssueCodes.AppraisalFieldInvalid && i.Message.Contains("checks")), string.Join("\n", issues));
        }

        // ---------- gerçek dosya: GDD v0.2 5.1 / 5.2 ----------

        private static AppraisalConfig Real()
        {
            var issues = new List<ContentIssue>();
            string text = System.IO.File.ReadAllText(System.IO.Path.Combine(TestPaths.ContentDataDirectory(), File));
            AppraisalConfig config = Parse(text, issues);
            Assert.AreEqual(0, issues.Count, string.Join("\n", issues));
            return config;
        }

        [Test]
        public void RealFile_ParsesAndValidates_WithNoIssues()
        {
            AppraisalConfig config = Real();
            var issues = new List<ContentIssue>();

            ContentValidator.ValidateAppraisal(config, ContentFixtures.Tables(), File, issues);

            Assert.AreEqual(0, issues.Count, string.Join("\n", issues));
        }

        [Test]
        public void RealFile_MatchesTheGddTables()
        {
            AppraisalConfig c = Real();

            CollectionAssert.AreEqual(new[] { "s0", "s1", "s2", "s3" }, c.Levels.Select(l => l.Id).ToArray());
            // 5.1: açılış günleri ve ücretler (Giriş / Orta / Üst)
            CollectionAssert.AreEqual(new[] { 1, 3, 5, 6 }, c.Levels.Select(l => l.UnlockDay).ToArray());
            Assert.AreEqual(new long[] { 0, 0, 0 }, Fees(c.Levels[0]));
            Assert.AreEqual(new long[] { 100, 200, 300 }, Fees(c.Levels[1]));
            Assert.AreEqual(new long[] { 350, 600, 1000 }, Fees(c.Levels[2]));
            Assert.AreEqual(new long[] { 700, 1200, 2000 }, Fees(c.Levels[3]));
            Assert.AreEqual("test_device", c.Levels[3].RequiredEquipment);
            Assert.IsNull(c.Levels[0].RequiredEquipment);
            Assert.IsNull(c.Levels[1].RequiredEquipment);
            Assert.IsNull(c.Levels[2].RequiredEquipment);
            // 5.2: yakalama, yanlış alarm, güven düzeyi, kanıt gücü
            CollectionAssert.AreEqual(
                new[] { AppraisalConfidence.Hint, AppraisalConfidence.Low, AppraisalConfidence.Medium, AppraisalConfidence.Certain },
                c.Levels.Select(l => l.Confidence).ToArray());
            CollectionAssert.AreEqual(new[] { 0.2, 0.4, 0.7, 1.0 }, c.Levels.Select(l => l.EvidencePower).ToArray());
            CollectionAssert.AreEqual(new[] { 0.35, 0.70, 0.92, 0.99 }, c.Levels.Select(l => l.DetectChance("screen")).ToArray());
            CollectionAssert.AreEqual(new[] { 0.20, 0.60, 0.90, 0.99 }, c.Levels.Select(l => l.DetectChance("camera")).ToArray());
            CollectionAssert.AreEqual(new[] { 0.05, 0.05, 0.02, 0.005 }, c.Levels.Select(l => l.FalseAlarmChance).ToArray());
            // 5.2 sayısal alanlar: S0 pil beyan, kasa ±15; S1 ±10/±10; S2 ±5/±5; S3 ±2/±3
            CollectionAssert.AreEqual(new int?[] { null, 10, 5, 2 }, c.Levels.Select(l => l.BatteryHalfWidth).ToArray());
            CollectionAssert.AreEqual(new int?[] { 15, 10, 5, 3 }, c.Levels.Select(l => l.BodyHalfWidth).ToArray());
            // 5.3: S1 ±%10 · S2 ±%5 · S3 ±%2,5; hedef kapsama %85/%92/%96
            CollectionAssert.AreEqual(new double?[] { null, 0.10, 0.05, 0.025 }, c.Levels.Select(l => l.ValueHalfWidth).ToArray());
            CollectionAssert.AreEqual(new double?[] { null, 0.85, 0.92, 0.96 }, c.Levels.Select(l => l.CoverageEstimate).ToArray());
            // 5.5: tahmini satış = değer × 1,02
            Assert.AreEqual(1.02, c.ExpectedSaleFactor, 1e-12);
            CollectionAssert.AreEqual(new[] { "screen", "camera" }, c.Checks.Select(x => x.Attribute).ToArray());
            CollectionAssert.AreEqual(new[] { "replaced_aftermarket" }, c.Checks[0].DefectValues.ToArray());
            CollectionAssert.AreEqual(new[] { "spotted", "faulty" }, c.Checks[1].DefectValues.ToArray());
            // UA11: yanlış alarmda varsayılan kusur ve kusursuz değer
            Assert.AreEqual("replaced_aftermarket", c.Checks[0].FalseAlarmValue);
            Assert.AreEqual("original", c.Checks[0].CleanValue);
            Assert.AreEqual("spotted", c.Checks[1].FalseAlarmValue);
            Assert.AreEqual("ok", c.Checks[1].CleanValue);
            Assert.AreEqual("appraisal.finding.screen_replaced", c.Checks[0].WordingKey);
            Assert.AreEqual("appraisal.finding.camera_problem", c.Checks[1].WordingKey);
        }

        private static long[] Fees(AppraisalLevel level)
        {
            return new[]
            {
                level.FeeFor(ProductSegment.Entry).Tl, level.FeeFor(ProductSegment.Mid).Tl, level.FeeFor(ProductSegment.Upper).Tl
            };
        }

        [Test]
        public void RealContent_LoadsCompletely_WithAppraisal()
        {
            ContentLoadResult result = ContentDatabase.Load(new DirectoryContentSource(TestPaths.ContentDataDirectory()));

            Assert.IsNotNull(result.Database, result.FormatIssues());
            Assert.AreEqual(4, result.Database.Appraisal.Levels.Count);
        }

        [Test]
        public void Load_WithoutTheFile_Fails()
        {
            DictionaryContentSource source = ContentFixtures.ValidSource();
            source.Remove(ContentFileNames.AppraisalLevels);

            ContentLoadResult result = ContentDatabase.Load(source);

            Assert.IsNull(result.Database);
            Assert.IsTrue(result.Issues.Any(i => i.Code == ContentIssueCodes.FileMissing && i.File == ContentFileNames.AppraisalLevels));
        }
    }
}
