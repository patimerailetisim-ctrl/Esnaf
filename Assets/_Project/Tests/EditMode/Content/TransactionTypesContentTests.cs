using System.Collections.Generic;
using System.Linq;
using Esnaf.Domain.Content;
using Esnaf.Domain.Economy;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Content
{
    public class TransactionTypesContentTests
    {
        private const string File = "transaction_types.json";

        private static IReadOnlyList<TransactionType> Parse(string json, List<ContentIssue> issues)
        {
            return ContentParser.ParseTransactionTypes(File, json, issues);
        }

        private static List<ContentIssue> Validate(params TransactionType[] types)
        {
            var issues = new List<ContentIssue>();
            ContentValidator.ValidateTransactionTypes(types, File, issues);
            return issues;
        }

        /// <summary>8 çekirdek türün geçerli listesi; testler ondan bir türü çıkarır/değiştirir.</summary>
        private static List<TransactionType> Core()
        {
            var issues = new List<ContentIssue>();
            return Parse(ContentFixtures.TransactionTypesJson, issues).ToList();
        }

        private static TransactionType Type(
            string id, string category = "trade", string direction = "outflow", string effect = "none", string displayKey = "ledger.type.x")
        {
            TransactionCategory c;
            TransactionDirection d;
            ProfitEffect e;
            Assert.IsTrue(TransactionTypeEnums.TryParseCategory(category, out c));
            Assert.IsTrue(TransactionTypeEnums.TryParseDirection(direction, out d));
            Assert.IsTrue(TransactionTypeEnums.TryParseProfitEffect(effect, out e));
            return new TransactionType(id, displayKey, c, d, e);
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
        public void Parse_ValidFixture_ReadsEightCoreTypes()
        {
            var issues = new List<ContentIssue>();

            IReadOnlyList<TransactionType> types = Parse(ContentFixtures.TransactionTypesJson, issues);

            Assert.AreEqual(0, issues.Count, string.Join("\n", issues));
            Assert.AreEqual(8, types.Count);

            TransactionType sale = types.Single(t => t.Id == "sale");
            Assert.AreEqual("ledger.type.sale", sale.DisplayKey);
            Assert.AreEqual(TransactionCategory.Trade, sale.Category);
            Assert.AreEqual(TransactionDirection.Inflow, sale.Direction);
            Assert.AreEqual(ProfitEffect.Sale, sale.ProfitEffect);

            TransactionType wasted = types.Single(t => t.Id == "wasted_appraisal");
            Assert.AreEqual(TransactionCategory.Expense, wasted.Category);
            Assert.AreEqual(TransactionDirection.Neutral, wasted.Direction);
            Assert.AreEqual(ProfitEffect.WriteOff, wasted.ProfitEffect);
        }

        [TestCase("category", "luxury")]
        [TestCase("direction", "sideways")]
        [TestCase("profitEffect", "magic")]
        public void Parse_InvalidEnumText_IsReported_AndTypeSkipped(string field, string badValue)
        {
            string original = "\"" + field + "\": \"";
            int index = ContentFixtures.TransactionTypesJson.IndexOf(original, System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(index, 0);
            int valueStart = index + original.Length;
            int valueEnd = ContentFixtures.TransactionTypesJson.IndexOf('"', valueStart);
            string json = ContentFixtures.TransactionTypesJson.Substring(0, valueStart) + badValue + ContentFixtures.TransactionTypesJson.Substring(valueEnd);
            var issues = new List<ContentIssue>();

            IReadOnlyList<TransactionType> types = Parse(json, issues);

            Assert.AreEqual(7, types.Count, "Yalnızca bozuk tür atlanmalı");
            Assert.AreEqual(1, issues.Count, string.Join("\n", issues));
            StringAssert.Contains(badValue, issues[0].Message);
        }

        private static readonly object[][] MissingFieldCases =
        {
            new object[] { "\"id\": \"opening_capital\", ", "", "id" },
            new object[] { "\"displayKey\": \"ledger.type.opening_capital\", ", "", "displayKey" },
            new object[] { "\"category\": \"capital\", ", "", "category" },
            new object[] { "\"direction\": \"inflow\", \"profitEffect\": \"none\" },", "\"profitEffect\": \"none\" },", "direction" },
            new object[] { ", \"profitEffect\": \"none\" }, { \"id\": \"purchase\"", " }, { \"id\": \"purchase\"", "profitEffect" }
        };

        [TestCaseSource(nameof(MissingFieldCases))]
        public void Parse_EveryRequiredField_IsEnforced(string find, string replacement, string expectedField)
        {
            Assert.IsTrue(ContentFixtures.TransactionTypesJson.Contains(find), "Test verisi bulunamadı: " + find);
            string json = ContentFixtures.TransactionTypesJson.Replace(find, replacement);
            var issues = new List<ContentIssue>();

            IReadOnlyList<TransactionType> types = Parse(json, issues);

            Assert.IsNotNull(types);
            Assert.AreEqual(7, types.Count, "Yalnızca bozuk tür atlanmalı: " + expectedField);
            Assert.IsTrue(
                issues.Any(i => i.Code == ContentIssueCodes.FieldMissing && i.Message.Contains("'" + expectedField + "'")),
                "Beklenen eksik alan '" + expectedField + "' raporlanmadı:\n" + string.Join("\n", issues));
        }

        [Test]
        public void Parse_UnknownField_IsReported()
        {
            string json = ContentFixtures.TransactionTypesJson.Replace("\"direction\"", "\"directon\"");
            var issues = new List<ContentIssue>();

            Assert.IsNull(Parse(json, issues));
            AssertOnly(issues, ContentIssueCodes.FileSyntax);
        }

        [Test]
        public void Parse_TypesArrayMissing_IsReported()
        {
            var issues = new List<ContentIssue>();

            Assert.IsNull(Parse("{ \"schemaVersion\": 1 }", issues));
            AssertOnly(issues, ContentIssueCodes.FieldMissing);
        }

        [Test]
        public void Parse_WrongSchemaVersion_IsReported()
        {
            var issues = new List<ContentIssue>();

            Assert.IsNull(Parse(ContentFixtures.TransactionTypesJson.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2"), issues));
            AssertOnly(issues, ContentIssueCodes.SchemaVersionUnsupported);
        }

        // ---------- kayıt defteri (registry) ----------

        [Test]
        public void Registry_LooksUpById_AndKeepsOrder()
        {
            TransactionTypes registry = ContentFixtures.Types();
            TransactionType found;

            Assert.IsTrue(registry.TryGet("purchase", out found));
            Assert.AreEqual(TransactionDirection.Outflow, found.Direction);
            Assert.IsFalse(registry.TryGet("loan_repayment", out found));
            Assert.IsFalse(registry.TryGet(null, out found));
            Assert.AreEqual("investment", registry.Get("investment").Id);
            Assert.Throws<KeyNotFoundException>(() => registry.Get("nope"));
            Assert.AreEqual(8, registry.Types.Count);
            Assert.AreEqual("opening_capital", registry.Types[0].Id);
            Assert.IsFalse(registry.Types is List<TransactionType>);
            Assert.IsFalse(registry.Types is TransactionType[]);
        }

        [Test]
        public void Registry_DuplicateIds_Throw()
        {
            Assert.Throws<System.ArgumentException>(() => new TransactionTypes(new[] { Type("a"), Type("a") }));
        }

        // ---------- doğrulama ----------

        [Test]
        public void Validate_CoreTypes_HaveNoIssues()
        {
            Assert.AreEqual(0, Validate(Core().ToArray()).Count);
        }

        [Test]
        public void Validate_EmptyList_IsReported()
        {
            AssertOnly(Validate(), ContentIssueCodes.TransactionTypesEmpty);
        }

        [TestCase("")]
        [TestCase("Purchase")]
        [TestCase("pur chase")]
        [TestCase("1purchase")]
        [TestCase("pur-chase")]
        public void Validate_IdFormat(string id)
        {
            List<TransactionType> types = Core();
            types.Add(Type(id));

            Assert.IsTrue(Validate(types.ToArray()).Any(i => i.Code == ContentIssueCodes.TransactionTypeIdFormat));
        }

        [Test]
        public void Validate_DuplicateId_IsReported()
        {
            List<TransactionType> types = Core();
            types.Add(Type("purchase"));

            Assert.IsTrue(Validate(types.ToArray()).Any(i => i.Code == ContentIssueCodes.TransactionTypeIdDuplicate));
        }

        [Test]
        public void Validate_EmptyDisplayKey_IsReported()
        {
            List<TransactionType> types = Core();
            types.Add(Type("extra_type", displayKey: " "));

            AssertOnly(Validate(types.ToArray()), ContentIssueCodes.TransactionTypeDisplayKeyEmpty);
        }

        [TestCase("sale", "outflow")]      // satış etkisi gelir yönlü olmalı
        [TestCase("expense", "inflow")]    // gider etkisi çıkış yönlü olmalı
        [TestCase("write_off", "outflow")] // gider yazma nakitsiz (neutral) olmalı
        public void Validate_ProfitEffectMustMatchDirection(string effect, string direction)
        {
            List<TransactionType> types = Core();
            types.Add(Type("extra_type", direction: direction, effect: effect));

            AssertOnly(Validate(types.ToArray()), ContentIssueCodes.TransactionTypeEffectDirectionMismatch);
        }

        [Test]
        public void Validate_EachRequiredCoreType_MustExist()
        {
            foreach (string id in TransactionTypeIds.Required)
            {
                List<TransactionType> types = Core().Where(t => t.Id != id).ToList();

                List<ContentIssue> issues = Validate(types.ToArray());

                Assert.IsTrue(
                    issues.Any(i => i.Code == ContentIssueCodes.TransactionTypeRequiredMissing && i.Message.Contains("'" + id + "'")),
                    "Zorunlu tür eksikliği yakalanmadı: " + id);
            }
        }

        [TestCase("purchase", "trade", "inflow", "none")]
        [TestCase("sale", "trade", "inflow", "none")]
        [TestCase("sale", "trade", "outflow", "sale")]
        [TestCase("appraisal", "trade", "inflow", "none")]
        [TestCase("wasted_appraisal", "expense", "outflow", "none")]
        [TestCase("repair", "trade", "inflow", "none")]
        [TestCase("daily_expense", "expense", "outflow", "none")]
        [TestCase("investment", "investment", "inflow", "none")]
        [TestCase("opening_capital", "capital", "outflow", "none")]
        // Yalnızca kategori yanlış (yön ve etki doğru):
        [TestCase("opening_capital", "trade", "inflow", "none")]
        [TestCase("purchase", "expense", "outflow", "none")]
        [TestCase("sale", "expense", "inflow", "sale")]
        [TestCase("appraisal", "expense", "outflow", "none")]
        [TestCase("wasted_appraisal", "trade", "neutral", "write_off")]
        [TestCase("repair", "investment", "outflow", "none")]
        [TestCase("daily_expense", "trade", "outflow", "expense")]
        [TestCase("investment", "expense", "outflow", "none")]
        public void Validate_CoreTypeWithWrongBehavior_IsReported(string id, string category, string direction, string effect)
        {
            List<TransactionType> types = Core().Where(t => t.Id != id).ToList();
            types.Add(Type(id, category, direction, effect));

            List<ContentIssue> issues = Validate(types.ToArray());

            Assert.IsTrue(
                issues.Any(i => i.Code == ContentIssueCodes.TransactionTypeRequiredMismatch && i.Message.Contains("'" + id + "'")),
                "Yanlış çekirdek tür davranışı yakalanmadı: " + id + "\n" + string.Join("\n", issues));
        }

        [Test]
        public void Validate_ExtraTypes_AreAllowed_ThisIsTheCreditExtensionPoint()
        {
            // Kredi MVP'de yok; ama yeni bir tür yalnızca VERİ satırı olarak eklenebilmeli (GDD v0.3 2.3).
            List<TransactionType> types = Core();
            types.Add(Type("loan_disbursement", category: "capital", direction: "inflow", effect: "none"));
            types.Add(Type("loan_repayment", category: "capital", direction: "outflow", effect: "none"));
            types.Add(Type("interest", category: "expense", direction: "outflow", effect: "expense"));

            Assert.AreEqual(0, Validate(types.ToArray()).Count);
            Assert.AreEqual(11, new TransactionTypes(types).Types.Count);
        }

        // ---------- gerçek dosya ----------

        [Test]
        public void RealFile_MatchesGddCoreTypes()
        {
            var source = new DirectoryContentSource(TestPaths.ContentDataDirectory());
            string text;
            Assert.IsTrue(source.TryGetText(ContentFileNames.TransactionTypes, out text), "transaction_types.json bulunamadı");
            var issues = new List<ContentIssue>();

            IReadOnlyList<TransactionType> types = Parse(text, issues);

            Assert.AreEqual(0, issues.Count, string.Join("\n", issues));
            Assert.AreEqual(0, Validate(types.ToArray()).Count);

            // id, kategori, yön, kâr etkisi (GDD v0.2 9.3 + v0.3 2.3)
            object[][] expected =
            {
                new object[] { "opening_capital", TransactionCategory.Capital, TransactionDirection.Inflow, ProfitEffect.None },
                new object[] { "purchase", TransactionCategory.Trade, TransactionDirection.Outflow, ProfitEffect.None },
                new object[] { "sale", TransactionCategory.Trade, TransactionDirection.Inflow, ProfitEffect.Sale },
                new object[] { "appraisal", TransactionCategory.Trade, TransactionDirection.Outflow, ProfitEffect.None },
                new object[] { "wasted_appraisal", TransactionCategory.Expense, TransactionDirection.Neutral, ProfitEffect.WriteOff },
                new object[] { "repair", TransactionCategory.Trade, TransactionDirection.Outflow, ProfitEffect.None },
                new object[] { "daily_expense", TransactionCategory.Expense, TransactionDirection.Outflow, ProfitEffect.Expense },
                new object[] { "investment", TransactionCategory.Investment, TransactionDirection.Outflow, ProfitEffect.None },
                new object[] { "content_refund", TransactionCategory.Trade, TransactionDirection.Inflow, ProfitEffect.None } // çekirdek değil: GDD 6.7 içerik iadesi
            };
            Assert.AreEqual(expected.Length, types.Count);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual((string)expected[i][0], types[i].Id);
                Assert.AreEqual((TransactionCategory)expected[i][1], types[i].Category, types[i].Id);
                Assert.AreEqual((TransactionDirection)expected[i][2], types[i].Direction, types[i].Id);
                Assert.AreEqual((ProfitEffect)expected[i][3], types[i].ProfitEffect, types[i].Id);
                Assert.IsTrue(types[i].DisplayKey.StartsWith("ledger.type.", System.StringComparison.Ordinal), types[i].Id);
            }
        }
    }
}
