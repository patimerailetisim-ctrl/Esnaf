using System.Collections.Generic;
using System.Linq;
using Esnaf.Domain.Content;
using Esnaf.Domain.Products;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Content
{
    public class ConditionProfilesContentTests
    {
        private const string File = "condition_profiles.json";

        private static IReadOnlyList<ConditionProfile> Parse(string json, List<ContentIssue> issues)
        {
            return ContentParser.ParseConditionProfiles(File, json, issues);
        }

        private static List<ContentIssue> Validate(params ConditionProfile[] profiles)
        {
            var issues = new List<ContentIssue>();
            ContentValidator.ValidateConditionProfiles(profiles, ContentFixtures.Tables(), File, issues);
            return issues;
        }

        private static ConditionProfile Profile(
            string id = "p1",
            int weight = 1,
            int fromDay = 1,
            int batteryMin = 70,
            int batteryMax = 90,
            int bodyMin = 60,
            int bodyMax = 90,
            WeightedValue[] screen = null,
            WeightedValue[] camera = null,
            double boxChance = 0.5,
            double invoiceChance = 0.5)
        {
            return new ConditionProfile(
                id,
                "Profile " + id,
                weight,
                fromDay,
                batteryMin,
                batteryMax,
                bodyMin,
                bodyMax,
                screen ?? new[] { new WeightedValue("original", 1) },
                camera ?? new[] { new WeightedValue("ok", 1) },
                boxChance,
                invoiceChance);
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
        public void Parse_ValidFixture_ReadsProfiles()
        {
            var issues = new List<ContentIssue>();

            IReadOnlyList<ConditionProfile> profiles = Parse(ContentFixtures.ProfilesJson, issues);

            Assert.AreEqual(0, issues.Count, string.Join("\n", issues));
            Assert.AreEqual(2, profiles.Count);

            ConditionProfile alpha = profiles[0];
            Assert.AreEqual("alpha", alpha.Id);
            Assert.AreEqual("Alpha", alpha.Name);
            Assert.AreEqual(3, alpha.Weight);
            Assert.AreEqual(1, alpha.AvailableFromDay);
            Assert.AreEqual(80, alpha.BatteryMin);
            Assert.AreEqual(99, alpha.BatteryMax);
            Assert.AreEqual(60, alpha.BodyMin);
            Assert.AreEqual(100, alpha.BodyMax);
            Assert.AreEqual(2, alpha.ScreenChoices.Count);
            Assert.AreEqual("scratched", alpha.ScreenChoices[1].Value);
            Assert.AreEqual(1, alpha.ScreenChoices[1].Weight);
            Assert.AreEqual(1, alpha.CameraChoices.Count);
            Assert.AreEqual(0.5, alpha.BoxChance, 1e-12);
            Assert.AreEqual(0.25, alpha.InvoiceChance, 1e-12);

            ConditionProfile beta = profiles[1];
            Assert.AreEqual(3, beta.AvailableFromDay);
            Assert.AreEqual(2, beta.ScreenChoices[0].Weight);
            Assert.AreEqual(0.0, beta.BoxChance, 1e-12);
        }

        [Test]
        public void Parsed_ProfileListsAreReadOnly()
        {
            ConditionProfile alpha = ContentFixtures.Profiles()[0];

            Assert.IsFalse(alpha.ScreenChoices is List<WeightedValue>);
            Assert.IsFalse(alpha.ScreenChoices is WeightedValue[]);
            Assert.IsFalse(alpha.CameraChoices is WeightedValue[]);
        }

        [Test]
        public void Parse_MissingField_IsReported_WithProfileLabel()
        {
            string json = ContentFixtures.ProfilesJson.Replace("\"weight\": 3, ", string.Empty);
            var issues = new List<ContentIssue>();

            IReadOnlyList<ConditionProfile> profiles = Parse(json, issues);

            Assert.AreEqual(1, profiles.Count, "Sağlam profil yine de okunmalı");
            Assert.AreEqual("beta", profiles[0].Id);
            AssertOnly(issues, ContentIssueCodes.FieldMissing);
            StringAssert.Contains("profiles[0]", issues[0].Message);
            StringAssert.Contains("alpha", issues[0].Message);
            StringAssert.Contains("weight", issues[0].Message);
        }

        [Test]
        public void Parse_MissingRangeMax_IsReported()
        {
            string json = ContentFixtures.ProfilesJson.Replace("\"battery\": { \"min\": 80, \"max\": 99 }", "\"battery\": { \"min\": 80 }");
            var issues = new List<ContentIssue>();

            Parse(json, issues);

            Assert.IsTrue(issues.Any(i => i.Code == ContentIssueCodes.FieldMissing && i.Message.Contains("battery.max")), string.Join("\n", issues));
        }

        [Test]
        public void Parse_MissingChoiceValue_IsReported()
        {
            string json = ContentFixtures.ProfilesJson.Replace("{ \"value\": \"ok\", \"weight\": 1 }", "{ \"weight\": 1 }");
            var issues = new List<ContentIssue>();

            Parse(json, issues);

            Assert.IsTrue(issues.Any(i => i.Code == ContentIssueCodes.FieldMissing && i.Message.Contains("camera[0].value")), string.Join("\n", issues));
        }

        [Test]
        public void Parse_UnknownField_IsReported()
        {
            string json = ContentFixtures.ProfilesJson.Replace("\"boxChance\": 0.5", "\"boxChanse\": 0.5");
            var issues = new List<ContentIssue>();

            Assert.IsNull(Parse(json, issues));
            AssertOnly(issues, ContentIssueCodes.FileSyntax);
        }

        [Test]
        public void Parse_FractionalWeight_IsRejected()
        {
            string json = ContentFixtures.ProfilesJson.Replace("\"weight\": 3", "\"weight\": 3.5");
            var issues = new List<ContentIssue>();

            Assert.IsNull(Parse(json, issues));
            AssertOnly(issues, ContentIssueCodes.FileSyntax);
        }

        [Test]
        public void Parse_ProfilesMissing_IsReported()
        {
            var issues = new List<ContentIssue>();

            Assert.IsNull(Parse("{ \"schemaVersion\": 1 }", issues));
            AssertOnly(issues, ContentIssueCodes.FieldMissing);
        }


        private static readonly object[][] MissingFieldCases =
        {
            new object[] { "\"id\": \"alpha\", ", "", "id" },
            new object[] { "\"name\": \"Alpha\", ", "", "name" },
            new object[] { "\"weight\": 3, ", "", "weight" },
            new object[] { "\"availableFromDay\": 1,", "", "availableFromDay" },
            new object[] { "\"battery\": { \"min\": 80, \"max\": 99 }", "\"battery\": null", "battery" },
            new object[] { "\"battery\": { \"min\": 80, \"max\": 99 }", "\"battery\": { \"max\": 99 }", "battery.min" },
            new object[] { "\"battery\": { \"min\": 80, \"max\": 99 }", "\"battery\": { \"min\": 80 }", "battery.max" },
            new object[] { "\"body\": { \"min\": 60, \"max\": 100 }", "\"body\": null", "body" },
            new object[] { "\"body\": { \"min\": 60, \"max\": 100 }", "\"body\": { \"max\": 100 }", "body.min" },
            new object[] { "\"body\": { \"min\": 60, \"max\": 100 }", "\"body\": { \"min\": 60 }", "body.max" },
            new object[] { "\"screen\": [ { \"value\": \"original\", \"weight\": 1 }, { \"value\": \"scratched\", \"weight\": 1 } ]", "\"screen\": null", "screen" },
            new object[] { "{ \"value\": \"original\", \"weight\": 1 }", "{ \"value\": \"original\" }", "screen[0].weight" },
            new object[] { "{ \"value\": \"original\", \"weight\": 1 }", "{ \"weight\": 1 }", "screen[0].value" },
            new object[] { "\"camera\": [ { \"value\": \"ok\", \"weight\": 1 } ]", "\"camera\": null", "camera" },
            new object[] { "\"boxChance\": 0.5, ", "", "boxChance" },
            new object[] { ", \"invoiceChance\": 0.25", "", "invoiceChance" }
        };

        [TestCaseSource(nameof(MissingFieldCases))]
        public void Parse_EveryRequiredField_IsEnforced(string find, string replacement, string expectedField)
        {
            Assert.IsTrue(ContentFixtures.ProfilesJson.Contains(find), "Test verisi bulunamadı: " + find);
            string json = ContentFixtures.ProfilesJson.Replace(find, replacement);
            var issues = new List<ContentIssue>();

            IReadOnlyList<ConditionProfile> profiles = Parse(json, issues);

            Assert.IsNotNull(profiles);
            Assert.AreEqual(1, profiles.Count, "Yalnızca bozuk profil (alpha) atlanmalı: " + expectedField);
            Assert.AreEqual("beta", profiles[0].Id);
            Assert.IsTrue(
                issues.Any(i => i.Code == ContentIssueCodes.FieldMissing && i.Message.Contains("'" + expectedField + "'")),
                "Beklenen eksik alan '" + expectedField + "' raporlanmadı:\n" + string.Join("\n", issues));
        }

        // ---------- doğrulama ----------

        [Test]
        public void Validate_GoodProfiles_HaveNoIssues()
        {
            Assert.AreEqual(0, Validate(ContentFixtures.Profiles().ToArray()).Count);
        }

        [Test]
        public void Validate_EmptyList_IsReported()
        {
            AssertOnly(Validate(), ContentIssueCodes.ProfilesEmpty);
        }

        [Test]
        public void Validate_EmptyId_IsReported()
        {
            AssertOnly(Validate(Profile(id: "")), ContentIssueCodes.ProfileIdEmpty);
        }

        [Test]
        public void Validate_DuplicateId_IsReported()
        {
            AssertOnly(Validate(Profile("p1"), Profile("p1")), ContentIssueCodes.ProfileIdDuplicate);
        }

        [TestCase(0)]
        [TestCase(-3)]
        public void Validate_Weight_MustBePositive(int weight)
        {
            AssertOnly(Validate(Profile(weight: weight)), ContentIssueCodes.ProfileWeightNotPositive);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Validate_AvailableFromDay_MustBeAtLeastOne(int day)
        {
            List<ContentIssue> issues = Validate(Profile(fromDay: day), Profile("p2", fromDay: 1));

            AssertOnly(issues, ContentIssueCodes.ProfileDayInvalid);
        }

        [Test]
        public void Validate_AtLeastOneProfile_MustBeAvailableOnDayOne()
        {
            AssertOnly(Validate(Profile("p1", fromDay: 2), Profile("p2", fromDay: 5)), ContentIssueCodes.ProfilesNoDayOne);
        }

        [TestCase(-1, 50)]
        [TestCase(50, 101)]
        [TestCase(80, 70)]
        public void Validate_BatteryRange_MustBeSensible(int min, int max)
        {
            AssertOnly(Validate(Profile(batteryMin: min, batteryMax: max)), ContentIssueCodes.ProfileRangeInvalid);
        }

        [TestCase(-5, 50)]
        [TestCase(50, 120)]
        [TestCase(90, 60)]
        public void Validate_BodyRange_MustBeSensible(int min, int max)
        {
            AssertOnly(Validate(Profile(bodyMin: min, bodyMax: max)), ContentIssueCodes.ProfileRangeInvalid);
        }

        [Test]
        public void Validate_SingleValueRange_IsAllowed()
        {
            Assert.AreEqual(0, Validate(Profile(batteryMin: 85, batteryMax: 85, bodyMin: 100, bodyMax: 100)).Count);
        }

        [Test]
        public void Validate_EmptyScreenChoices_IsReported()
        {
            AssertOnly(Validate(Profile(screen: new WeightedValue[0])), ContentIssueCodes.ProfileChoicesEmpty);
        }

        [Test]
        public void Validate_EmptyCameraChoices_IsReported()
        {
            AssertOnly(Validate(Profile(camera: new WeightedValue[0])), ContentIssueCodes.ProfileChoicesEmpty);
        }

        [Test]
        public void Validate_ChoiceWeight_MustBePositive()
        {
            AssertOnly(Validate(Profile(screen: new[] { new WeightedValue("original", 0) })), ContentIssueCodes.ProfileChoiceWeightNotPositive);
        }

        [Test]
        public void Validate_UnknownScreenValue_IsReported_AgainstValueTables()
        {
            List<ContentIssue> issues = Validate(Profile(screen: new[] { new WeightedValue("smashed", 1) }));

            AssertOnly(issues, ContentIssueCodes.ProfileChoiceUnknownValue);
            StringAssert.Contains("smashed", issues[0].Message);
        }

        [Test]
        public void Validate_UnknownCameraValue_IsReported_AgainstValueTables()
        {
            AssertOnly(Validate(Profile(camera: new[] { new WeightedValue("blurry", 1) })), ContentIssueCodes.ProfileChoiceUnknownValue);
        }

        [Test]
        public void Validate_DuplicateChoiceValue_IsReported()
        {
            AssertOnly(
                Validate(Profile(screen: new[] { new WeightedValue("original", 1), new WeightedValue("original", 2) })),
                ContentIssueCodes.ProfileChoiceDuplicate);
        }

        [TestCase(-0.1, 0.5)]
        [TestCase(0.5, 1.1)]
        [TestCase(double.NaN, 0.5)]
        public void Validate_Chances_MustBeBetweenZeroAndOne(double box, double invoice)
        {
            AssertOnly(Validate(Profile(boxChance: box, invoiceChance: invoice)), ContentIssueCodes.ProfileChanceRange);
        }

        [TestCase(0.0)]
        [TestCase(1.0)]
        public void Validate_ChanceBoundaries_AreAllowed(double chance)
        {
            Assert.AreEqual(0, Validate(Profile(boxChance: chance, invoiceChance: chance)).Count);
        }

        [Test]
        public void Validate_IssueMessages_NameTheProfile()
        {
            List<ContentIssue> issues = Validate(Profile("special", weight: 0));

            StringAssert.Contains("special", issues.Single().Message);
        }

        // ---------- gerçek dosya (GDD v0.2 Bölüm 4.3) ----------

        private static IReadOnlyList<ConditionProfile> LoadReal()
        {
            var source = new DirectoryContentSource(TestPaths.ContentDataDirectory());
            string text;
            Assert.IsTrue(source.TryGetText(ContentFileNames.ConditionProfiles, out text), "condition_profiles.json bulunamadı");
            var issues = new List<ContentIssue>();
            IReadOnlyList<ConditionProfile> profiles = Parse(text, issues);
            Assert.AreEqual(0, issues.Count, string.Join("\n", issues));
            return profiles;
        }

        [Test]
        public void RealFile_MatchesGddV02ProfileTable()
        {
            IReadOnlyList<ConditionProfile> profiles = LoadReal();

            // id, ağırlık, gün, pil min-max, kasa min-max
            object[][] expected =
            {
                new object[] { "clean", 25, 1, 88, 98, 88, 100 },
                new object[] { "used", 35, 1, 76, 88, 70, 88 },
                new object[] { "worn", 20, 1, 60, 76, 50, 72 },
                new object[] { "repaired", 15, 1, 70, 88, 75, 90 },
                new object[] { "problematic", 5, 5, 50, 68, 50, 70 }
            };
            Assert.AreEqual(expected.Length, profiles.Count);
            for (int i = 0; i < expected.Length; i++)
            {
                object[] e = expected[i];
                ConditionProfile p = profiles[i];
                Assert.AreEqual((string)e[0], p.Id);
                Assert.AreEqual((int)e[1], p.Weight, p.Id);
                Assert.AreEqual((int)e[2], p.AvailableFromDay, p.Id);
                Assert.AreEqual((int)e[3], p.BatteryMin, p.Id);
                Assert.AreEqual((int)e[4], p.BatteryMax, p.Id);
                Assert.AreEqual((int)e[5], p.BodyMin, p.Id);
                Assert.AreEqual((int)e[6], p.BodyMax, p.Id);
            }

            Assert.AreEqual(100, profiles.Sum(p => p.Weight), "Ağırlıklar %100'e tamamlanmalı");
        }

        [Test]
        public void RealFile_ScreenAndCameraChoices_MatchGddV02()
        {
            IReadOnlyList<ConditionProfile> profiles = LoadReal();

            Assert.AreEqual(new[] { "original" }, Values(profiles, "clean").Screen);
            Assert.AreEqual(new[] { "ok" }, Values(profiles, "clean").Camera);
            Assert.AreEqual(new[] { "original", "scratched" }, Values(profiles, "used").Screen);
            Assert.AreEqual(new[] { "ok" }, Values(profiles, "used").Camera);
            Assert.AreEqual(new[] { "scratched" }, Values(profiles, "worn").Screen);
            Assert.AreEqual(new[] { "ok", "spotted" }, Values(profiles, "worn").Camera);
            Assert.AreEqual(new[] { "replaced_aftermarket" }, Values(profiles, "repaired").Screen);
            Assert.AreEqual(new[] { "ok" }, Values(profiles, "repaired").Camera);
            Assert.AreEqual(new[] { "replaced_aftermarket", "cracked" }, Values(profiles, "problematic").Screen);
            Assert.AreEqual(new[] { "spotted", "faulty" }, Values(profiles, "problematic").Camera);
        }

        [Test]
        public void RealFile_PackageChances_AreOrderedByCondition()
        {
            IReadOnlyList<ConditionProfile> profiles = LoadReal();

            // "Temiz" genelde kutulu/faturalı; sorunlu ürün nadiren paketli. (Sayılar başlangıç değeri, sıra kuralı GDD'den.)
            ConditionProfile clean = profiles.Single(p => p.Id == "clean");
            ConditionProfile used = profiles.Single(p => p.Id == "used");
            ConditionProfile worn = profiles.Single(p => p.Id == "worn");
            Assert.Greater(clean.BoxChance, used.BoxChance);
            Assert.Greater(used.BoxChance, worn.BoxChance);
            Assert.Greater(clean.InvoiceChance, used.InvoiceChance);
            Assert.GreaterOrEqual(clean.BoxChance, 0.5, "Temiz: genelde kutulu");
        }

        [Test]
        public void RealFile_PassesValidatorAgainstRealValueTables()
        {
            var source = new DirectoryContentSource(TestPaths.ContentDataDirectory());
            ContentLoadResult result = ContentDatabase.Load(source);

            Assert.IsTrue(result.IsSuccess, result.FormatIssues());
            Assert.AreEqual(5, result.Database.ConditionProfiles.Count);
            ConditionProfile repaired;
            Assert.IsTrue(result.Database.TryGetConditionProfile("repaired", out repaired));
            Assert.AreEqual("repaired", repaired.Id);
            Assert.IsFalse(result.Database.TryGetConditionProfile("nope", out repaired));
            Assert.IsNotNull(result.Database.ValueTables);
        }

        private static ChoiceValues Values(IReadOnlyList<ConditionProfile> profiles, string id)
        {
            ConditionProfile p = profiles.Single(x => x.Id == id);
            return new ChoiceValues
            {
                Screen = p.ScreenChoices.Select(c => c.Value).ToArray(),
                Camera = p.CameraChoices.Select(c => c.Value).ToArray()
            };
        }

        private sealed class ChoiceValues
        {
            public string[] Screen;
            public string[] Camera;
        }
    }
}
