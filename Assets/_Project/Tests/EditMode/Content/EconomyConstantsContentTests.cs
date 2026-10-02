using System.Collections.Generic;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Content;
using Esnaf.Domain.Economy;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Content
{
    public class EconomyConstantsContentTests
    {
        private const string File = "economy_constants.json";

        private static EconomyConstants Parse(string json, List<ContentIssue> issues)
        {
            return ContentParser.ParseEconomyConstants(File, json, issues);
        }

        private static List<ContentIssue> Validate(EconomyConstants constants)
        {
            var issues = new List<ContentIssue>();
            ContentValidator.ValidateEconomyConstants(constants, File, issues);
            return issues;
        }

        private static EconomyConstants Make(long opening = 250000, int fromDay = 3, long expense = 500, int capacity = 6)
        {
            return new EconomyConstants(Money.FromTl(opening), fromDay, Money.FromTl(expense), capacity);
        }

        private static void AssertOnly(List<ContentIssue> issues, string code)
        {
            Assert.AreEqual(1, issues.Count, string.Join("\n", issues.Select(i => i.ToString())));
            Assert.AreEqual(code, issues[0].Code);
            Assert.AreEqual(File, issues[0].File);
        }

        [Test]
        public void Parse_ValidFixture_ReadsAllConstants()
        {
            var issues = new List<ContentIssue>();

            EconomyConstants c = Parse(ContentFixtures.EconomyConstantsJson, issues);

            Assert.AreEqual(0, issues.Count, string.Join("\n", issues));
            Assert.AreEqual(Money.FromTl(250000), c.OpeningCapital);
            Assert.AreEqual(3, c.DailyExpenseFromDay);
            Assert.AreEqual(Money.FromTl(500), c.DailyExpenseAmount);
            Assert.AreEqual(6, c.InitialShelfCapacity);
        }

        private static readonly object[][] MissingFieldCases =
        {
            new object[] { "\"openingCapital\": 250000,", "", "openingCapital" },
            new object[] { "\"dailyExpense\": { \"fromDay\": 3, \"amount\": 500 }", "\"dailyExpense\": null", "dailyExpense" },
            new object[] { "\"fromDay\": 3, ", "", "dailyExpense.fromDay" },
            new object[] { ", \"amount\": 500", "", "dailyExpense.amount" },
            new object[] { ", \"initialShelfCapacity\": 6", "", "initialShelfCapacity" }
        };

        [TestCaseSource(nameof(MissingFieldCases))]
        public void Parse_EveryRequiredField_IsEnforced(string find, string replacement, string expectedField)
        {
            Assert.IsTrue(ContentFixtures.EconomyConstantsJson.Contains(find), "Test verisi bulunamadı: " + find);
            string json = ContentFixtures.EconomyConstantsJson.Replace(find, replacement);
            var issues = new List<ContentIssue>();

            EconomyConstants c = Parse(json, issues);

            Assert.IsNull(c);
            Assert.IsTrue(
                issues.Any(i => i.Code == ContentIssueCodes.FieldMissing && i.Message.Contains("'" + expectedField + "'")),
                "Beklenen eksik alan '" + expectedField + "' raporlanmadı:\n" + string.Join("\n", issues));
        }

        [Test]
        public void Parse_FractionalMoney_IsRejected()
        {
            var issues = new List<ContentIssue>();

            Assert.IsNull(Parse(ContentFixtures.EconomyConstantsJson.Replace("250000", "250000.5"), issues));
            AssertOnly(issues, ContentIssueCodes.FileSyntax);
        }

        [Test]
        public void Parse_UnknownField_IsReported()
        {
            var issues = new List<ContentIssue>();

            Assert.IsNull(Parse(ContentFixtures.EconomyConstantsJson.Replace("openingCapital", "openingCapitol"), issues));
            AssertOnly(issues, ContentIssueCodes.FileSyntax);
        }

        [Test]
        public void Parse_WrongSchemaVersion_IsReported()
        {
            var issues = new List<ContentIssue>();

            Assert.IsNull(Parse(ContentFixtures.EconomyConstantsJson.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 5"), issues));
            AssertOnly(issues, ContentIssueCodes.SchemaVersionUnsupported);
        }

        // ---------- doğrulama ----------

        [Test]
        public void Validate_Good_HasNoIssues()
        {
            Assert.AreEqual(0, Validate(Make()).Count);
        }

        [TestCase(0L)]
        [TestCase(-10L)]
        [TestCase(250005L)]
        public void Validate_OpeningCapital_MustBePositiveAndRounded(long value)
        {
            AssertOnly(Validate(Make(opening: value)), ContentIssueCodes.EconomyOpeningCapitalInvalid);
        }

        [TestCase(-10L)]
        [TestCase(505L)]
        public void Validate_DailyExpense_MustBeNonNegativeAndRounded(long value)
        {
            AssertOnly(Validate(Make(expense: value)), ContentIssueCodes.EconomyDailyExpenseInvalid);
        }

        [Test]
        public void Validate_ZeroDailyExpense_IsAllowed()
        {
            Assert.AreEqual(0, Validate(Make(expense: 0)).Count);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Validate_DailyExpenseFromDay_MustBeAtLeastOne(int day)
        {
            AssertOnly(Validate(Make(fromDay: day)), ContentIssueCodes.EconomyDailyExpenseInvalid);
        }

        [TestCase(0)]
        [TestCase(-3)]
        public void Validate_ShelfCapacity_MustBePositive(int capacity)
        {
            AssertOnly(Validate(Make(capacity: capacity)), ContentIssueCodes.EconomyShelfCapacityInvalid);
        }

        // ---------- gerçek dosya ----------

        [Test]
        public void RealFile_MatchesGdd()
        {
            var source = new DirectoryContentSource(TestPaths.ContentDataDirectory());
            string text;
            Assert.IsTrue(source.TryGetText(ContentFileNames.EconomyConstants, out text), "economy_constants.json bulunamadı");
            var issues = new List<ContentIssue>();

            EconomyConstants c = Parse(text, issues);

            Assert.AreEqual(0, issues.Count, string.Join("\n", issues));
            Assert.AreEqual(0, Validate(c).Count);
            Assert.AreEqual(Money.FromTl(250000), c.OpeningCapital, "GDD karar 2: 250.000 TL");
            Assert.AreEqual(3, c.DailyExpenseFromDay, "GDD: gider Gün 3'ten");
            Assert.AreEqual(Money.FromTl(500), c.DailyExpenseAmount, "GDD: günlük gider 500 TL");
            Assert.AreEqual(6, c.InitialShelfCapacity, "GDD: başlangıç raf kapasitesi 6");
        }

        [Test]
        public void RealContent_LoadsWithTypesAndConstants()
        {
            ContentLoadResult result = ContentDatabase.Load(new DirectoryContentSource(TestPaths.ContentDataDirectory()));

            Assert.IsTrue(result.IsSuccess, result.FormatIssues());
            Assert.AreEqual(11, result.Database.TransactionTypes.Types.Count, "8 çekirdek tür + içerik iadesi (GDD 6.7) + toptan alış (Day 11.2.2) + aksesuar ek satışı (Day 11.3.1)");
            Assert.AreEqual(Money.FromTl(250000), result.Database.EconomyConstants.OpeningCapital);
        }
    }
}
