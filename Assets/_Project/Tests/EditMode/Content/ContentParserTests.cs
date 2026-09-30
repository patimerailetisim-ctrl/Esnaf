using System.Collections.Generic;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Content;
using Esnaf.Domain.Products;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Content
{
    public class ContentParserTests
    {
        private const string File = "phone_models.json";

        private static IReadOnlyList<ProductDefinition> Parse(string json, List<ContentIssue> issues)
        {
            return ContentParser.ParseProductModels(File, json, issues);
        }

        private static void AssertSingleError(List<ContentIssue> issues, string code)
        {
            Assert.AreEqual(1, issues.Count, string.Join("\n", issues.Select(i => i.ToString())));
            Assert.AreEqual(code, issues[0].Code);
            Assert.AreEqual(ContentIssueSeverity.Error, issues[0].Severity);
            Assert.AreEqual(File, issues[0].File);
        }

        // ---------- başarılı ayrıştırma ----------

        [Test]
        public void ValidFile_ParsesAllFields()
        {
            var issues = new List<ContentIssue>();

            IReadOnlyList<ProductDefinition> products = Parse(ContentFixtures.ModelsFile(ContentFixtures.ValidModelJson), issues);

            Assert.AreEqual(0, issues.Count);
            Assert.AreEqual(1, products.Count);
            ProductDefinition p = products[0];
            Assert.AreEqual("phone.test_one", p.Id);
            Assert.AreEqual("phone", p.Sector);
            Assert.AreEqual("Test One", p.Name);
            Assert.AreEqual("Testco", p.Brand);
            Assert.AreEqual(ProductSegment.Mid, p.Segment);
            Assert.AreEqual(2023, p.ReleaseYear);
            Assert.AreEqual(Money.FromTl(10000), p.BasePrice);
            Assert.AreEqual(128, p.BaseStorageGb);
            Assert.AreEqual(6, p.MinAgeMonths);
            Assert.AreEqual(36, p.MaxAgeMonths);
            Assert.AreEqual(2, p.StorageOptions.Count);
            Assert.AreEqual(256, p.StorageOptions[1].Gb);
            Assert.AreEqual(1.2, p.StorageOptions[1].Multiplier, 1e-12);
        }

        [Test]
        public void IconKey_DefaultsToId_AndDeprecatedDefaultsToFalse()
        {
            var issues = new List<ContentIssue>();
            ProductDefinition p = Parse(ContentFixtures.ModelsFile(ContentFixtures.ValidModelJson), issues)[0];

            Assert.AreEqual("phone.test_one", p.IconKey);
            Assert.IsFalse(p.IsDeprecated);
        }

        [Test]
        public void ExplicitIconKeyAndDeprecated_AreRead()
        {
            string model = ContentFixtures.ValidModelJson.Replace(
                "\"ageMonths\"", "\"iconKey\": \"icons.custom\", \"deprecated\": true, \"ageMonths\"");
            var issues = new List<ContentIssue>();

            ProductDefinition p = Parse(ContentFixtures.ModelsFile(model), issues)[0];

            Assert.AreEqual(0, issues.Count);
            Assert.AreEqual("icons.custom", p.IconKey);
            Assert.IsTrue(p.IsDeprecated);
        }

        [Test]
        public void Utf8Bom_IsAccepted()
        {
            var issues = new List<ContentIssue>();

            IReadOnlyList<ProductDefinition> products = Parse("﻿" + ContentFixtures.ModelsFile(ContentFixtures.ValidModelJson), issues);

            Assert.AreEqual(0, issues.Count);
            Assert.AreEqual(1, products.Count);
        }

        [Test]
        public void TurkishCharacters_ArePreserved()
        {
            string model = ContentFixtures.ValidModelJson.Replace("Test One", "Yıldız Şık Öğe");
            var issues = new List<ContentIssue>();

            Assert.AreEqual("Yıldız Şık Öğe", Parse(ContentFixtures.ModelsFile(model), issues)[0].Name);
        }

        // ---------- yapısal hatalar (dosya reddedilir, null döner) ----------

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   \n ")]
        public void EmptyText_IsReported(string json)
        {
            var issues = new List<ContentIssue>();

            Assert.IsNull(Parse(json, issues));
            AssertSingleError(issues, ContentIssueCodes.FileEmpty);
        }

        [Test]
        public void MalformedJson_IsReportedAsSyntaxError()
        {
            var issues = new List<ContentIssue>();

            Assert.IsNull(Parse("{ \"schemaVersion\": 1, \"models\": [ { oops } ] }", issues));
            AssertSingleError(issues, ContentIssueCodes.FileSyntax);
        }

        [Test]
        public void NullRoot_IsReported()
        {
            var issues = new List<ContentIssue>();

            Assert.IsNull(Parse("null", issues));
            AssertSingleError(issues, ContentIssueCodes.FileSyntax);
        }

        [Test]
        public void ArrayRoot_IsReported()
        {
            var issues = new List<ContentIssue>();

            Assert.IsNull(Parse("[]", issues));
            AssertSingleError(issues, ContentIssueCodes.FileSyntax);
        }

        [Test]
        public void UnknownField_IsReported_SoTyposAreCaught()
        {
            string model = ContentFixtures.ValidModelJson.Replace("\"basePrice\"", "\"basePrise\"");
            var issues = new List<ContentIssue>();

            Assert.IsNull(Parse(ContentFixtures.ModelsFile(model), issues));
            AssertSingleError(issues, ContentIssueCodes.FileSyntax);
            StringAssert.Contains("basePrise", issues[0].Message);
        }

        [Test]
        public void WrongType_IsReported()
        {
            string model = ContentFixtures.ValidModelJson.Replace("\"basePrice\": 10000", "\"basePrice\": \"abc\"");
            var issues = new List<ContentIssue>();

            Assert.IsNull(Parse(ContentFixtures.ModelsFile(model), issues));
            AssertSingleError(issues, ContentIssueCodes.FileSyntax);
        }

        [Test]
        public void FractionalPrice_IsNotSilentlyTruncated()
        {
            string model = ContentFixtures.ValidModelJson.Replace("\"basePrice\": 10000", "\"basePrice\": 10000.5");
            var issues = new List<ContentIssue>();

            IReadOnlyList<ProductDefinition> products = Parse(ContentFixtures.ModelsFile(model), issues);

            Assert.IsNull(products);
            AssertSingleError(issues, ContentIssueCodes.FileSyntax);
        }

        [TestCase("\"basePrice\": 10000", "\"basePrice\": \"10000\"")]      // tırnaklı sayı
        [TestCase("\"basePrice\": 10000", "\"basePrice\": 10000.0")]        // ondalıklı gösterim
        [TestCase("\"releaseYear\": 2023", "\"releaseYear\": 2023.5")]
        [TestCase("\"releaseYear\": 2023", "\"releaseYear\": 99999999999")]  // int aralığı dışı
        [TestCase("\"name\": \"Test One\"", "\"name\": 123")]              // sayı, metin yerine
        [TestCase("\"mult\": 1.2", "\"mult\": \"1.2\"")]                   // tırnaklı ondalık
        [TestCase("\"gb\": 256", "\"gb\": 256.0")]
        [TestCase("\"basePrice\": 10000", "\"basePrice\": null")]             // null = eksik değil, yapısal hata değil: aşağıda ayrı test
        public void StrictTypes_AreEnforced(string original, string replacement)
        {
            string model = ContentFixtures.ValidModelJson.Replace(original, replacement);
            Assert.AreNotEqual(ContentFixtures.ValidModelJson, model, "Test verisi değişmedi");
            var issues = new List<ContentIssue>();

            IReadOnlyList<ProductDefinition> products = Parse(ContentFixtures.ModelsFile(model), issues);

            Assert.AreEqual(1, issues.Count, string.Join("\n", issues.Select(i => i.ToString())));
            Assert.IsTrue(
                issues[0].Code == ContentIssueCodes.FileSyntax || issues[0].Code == ContentIssueCodes.FieldMissing,
                issues[0].ToString());
            if (issues[0].Code == ContentIssueCodes.FileSyntax)
            {
                Assert.IsNull(products);
            }
        }

        [Test]
        public void DeprecatedAsString_IsRejected()
        {
            string model = ContentFixtures.ValidModelJson.Replace("\"ageMonths\"", "\"deprecated\": \"true\", \"ageMonths\"");
            var issues = new List<ContentIssue>();

            Assert.IsNull(Parse(ContentFixtures.ModelsFile(model), issues));
            AssertSingleError(issues, ContentIssueCodes.FileSyntax);
        }

        [Test]
        public void IntegerMultiplier_IsAcceptedAsNumber()
        {
            string model = ContentFixtures.ValidModelJson.Replace("\"mult\": 1.2", "\"mult\": 2");
            var issues = new List<ContentIssue>();

            ProductDefinition p = Parse(ContentFixtures.ModelsFile(model), issues)[0];

            Assert.AreEqual(0, issues.Count);
            Assert.AreEqual(2.0, p.StorageOptions[1].Multiplier, 1e-12);
        }

        [Test]
        public void NullPrice_IsReportedAsMissingField()
        {
            string model = ContentFixtures.ValidModelJson.Replace("\"basePrice\": 10000", "\"basePrice\": null");
            var issues = new List<ContentIssue>();

            IReadOnlyList<ProductDefinition> products = Parse(ContentFixtures.ModelsFile(model), issues);

            Assert.AreEqual(0, products.Count);
            AssertSingleError(issues, ContentIssueCodes.FieldMissing);
            StringAssert.Contains("basePrice", issues[0].Message);
        }

        [Test]
        public void TrailingContent_IsReported()
        {
            var issues = new List<ContentIssue>();

            Assert.IsNull(Parse(ContentFixtures.ModelsFile(ContentFixtures.ValidModelJson) + " garbage", issues));
            AssertSingleError(issues, ContentIssueCodes.FileSyntax);
        }

        [Test]
        public void SchemaVersion_Missing_IsReported()
        {
            var issues = new List<ContentIssue>();

            Assert.IsNull(Parse("{ \"models\": [] }", issues));
            AssertSingleError(issues, ContentIssueCodes.SchemaVersionMissing);
        }

        [Test]
        public void SchemaVersion_Unsupported_IsReported()
        {
            var issues = new List<ContentIssue>();

            Assert.IsNull(Parse("{ \"schemaVersion\": 2, \"models\": [] }", issues));
            AssertSingleError(issues, ContentIssueCodes.SchemaVersionUnsupported);
        }

        [Test]
        public void ModelsArray_Missing_IsReported()
        {
            var issues = new List<ContentIssue>();

            Assert.IsNull(Parse("{ \"schemaVersion\": 1 }", issues));
            AssertSingleError(issues, ContentIssueCodes.FieldMissing);
            StringAssert.Contains("models", issues[0].Message);
        }

        // ---------- model bazında eksikler (dosya kısmen okunur) ----------

        [Test]
        public void MissingField_IsReported_WithModelIndexAndId_AndOtherModelsStillParse()
        {
            string bad = ContentFixtures.ModelWithId("phone.test_bad").Replace("\"brand\": \"Testco\",", string.Empty);
            var issues = new List<ContentIssue>();

            IReadOnlyList<ProductDefinition> products = Parse(
                ContentFixtures.ModelsFile(ContentFixtures.ValidModelJson, bad), issues);

            Assert.AreEqual(1, products.Count, "Sağlam model yine de okunmalı");
            Assert.AreEqual("phone.test_one", products[0].Id);
            AssertSingleError(issues, ContentIssueCodes.FieldMissing);
            StringAssert.Contains("models[1]", issues[0].Message);
            StringAssert.Contains("phone.test_bad", issues[0].Message);
            StringAssert.Contains("brand", issues[0].Message);
        }

        [Test]
        public void MultipleMissingFields_AreAllReported()
        {
            string bad = ContentFixtures.ValidModelJson
                .Replace("\"brand\": \"Testco\",", string.Empty)
                .Replace("\"releaseYear\": 2023,", string.Empty);
            var issues = new List<ContentIssue>();

            Parse(ContentFixtures.ModelsFile(bad), issues);

            Assert.AreEqual(2, issues.Count);
            Assert.IsTrue(issues.All(i => i.Code == ContentIssueCodes.FieldMissing));
        }

        [Test]
        public void ZeroValue_IsNotTreatedAsMissing()
        {
            string model = ContentFixtures.ValidModelJson.Replace("\"min\": 6", "\"min\": 0");
            var issues = new List<ContentIssue>();

            ProductDefinition p = Parse(ContentFixtures.ModelsFile(model), issues)[0];

            Assert.AreEqual(0, issues.Count);
            Assert.AreEqual(0, p.MinAgeMonths);
        }

        [Test]
        public void InvalidSegment_IsReported_AndModelSkipped()
        {
            string model = ContentFixtures.ValidModelJson.Replace("\"segment\": \"mid\"", "\"segment\": \"luxury\"");
            var issues = new List<ContentIssue>();

            IReadOnlyList<ProductDefinition> products = Parse(ContentFixtures.ModelsFile(model), issues);

            Assert.AreEqual(0, products.Count);
            AssertSingleError(issues, ContentIssueCodes.ProductSegmentInvalid);
            StringAssert.Contains("luxury", issues[0].Message);
        }

        [Test]
        public void MissingNestedStorageField_IsReported()
        {
            string model = ContentFixtures.ValidModelJson.Replace("{ \"gb\": 256, \"mult\": 1.2 }", "{ \"gb\": 256 }");
            var issues = new List<ContentIssue>();

            IReadOnlyList<ProductDefinition> products = Parse(ContentFixtures.ModelsFile(model), issues);

            Assert.AreEqual(0, products.Count);
            AssertSingleError(issues, ContentIssueCodes.FieldMissing);
            StringAssert.Contains("storageOptions[1].mult", issues[0].Message);
        }

        [Test]
        public void MissingAgeMax_IsReported()
        {
            string model = ContentFixtures.ValidModelJson.Replace("\"ageMonths\": { \"min\": 6, \"max\": 36 }", "\"ageMonths\": { \"min\": 6 }");
            var issues = new List<ContentIssue>();

            Parse(ContentFixtures.ModelsFile(model), issues);

            AssertSingleError(issues, ContentIssueCodes.FieldMissing);
            StringAssert.Contains("ageMonths.max", issues[0].Message);
        }

        [Test]
        public void NullModelEntry_IsReported()
        {
            var issues = new List<ContentIssue>();

            IReadOnlyList<ProductDefinition> products = Parse("{ \"schemaVersion\": 1, \"models\": [ null ] }", issues);

            Assert.AreEqual(0, products.Count);
            AssertSingleError(issues, ContentIssueCodes.FieldMissing);
            StringAssert.Contains("models[0]", issues[0].Message);
        }

        [Test]
        public void EmptyModelsArray_ParsesToEmptyList_ValidatorReportsIt()
        {
            var issues = new List<ContentIssue>();

            IReadOnlyList<ProductDefinition> products = Parse("{ \"schemaVersion\": 1, \"models\": [] }", issues);

            Assert.AreEqual(0, issues.Count);
            Assert.AreEqual(0, products.Count);
        }

        [Test]
        public void ParseDoesNotValidateValues_ThatIsTheValidatorsJob()
        {
            string model = ContentFixtures.ValidModelJson.Replace("\"basePrice\": 10000", "\"basePrice\": -5");
            var issues = new List<ContentIssue>();

            IReadOnlyList<ProductDefinition> products = Parse(ContentFixtures.ModelsFile(model), issues);

            Assert.AreEqual(0, issues.Count);
            Assert.AreEqual(Money.FromTl(-5), products[0].BasePrice);
        }

        // ---------- manifest ----------

        [Test]
        public void Manifest_Valid_ReturnsIdsInOrder()
        {
            var issues = new List<ContentIssue>();

            IReadOnlyList<string> ids = ContentParser.ParseIdManifest(
                "content_id_manifest.json", ContentFixtures.ManifestFile("phone.b", "phone.a"), issues);

            Assert.AreEqual(0, issues.Count);
            Assert.AreEqual(new[] { "phone.b", "phone.a" }, ids);
        }

        [Test]
        public void Manifest_MissingIds_IsReported()
        {
            var issues = new List<ContentIssue>();

            Assert.IsNull(ContentParser.ParseIdManifest("content_id_manifest.json", "{ \"schemaVersion\": 1 }", issues));
            Assert.AreEqual(ContentIssueCodes.FieldMissing, issues.Single().Code);
        }

        [Test]
        public void Manifest_NullEntry_IsReported()
        {
            var issues = new List<ContentIssue>();

            Assert.IsNull(ContentParser.ParseIdManifest(
                "content_id_manifest.json", "{ \"schemaVersion\": 1, \"ids\": [ \"phone.a\", null ] }", issues));
            Assert.AreEqual(ContentIssueCodes.FieldMissing, issues.Single().Code);
        }

        [Test]
        public void Manifest_WrongSchemaVersion_IsReported()
        {
            var issues = new List<ContentIssue>();

            Assert.IsNull(ContentParser.ParseIdManifest(
                "content_id_manifest.json", "{ \"schemaVersion\": 9, \"ids\": [] }", issues));
            Assert.AreEqual(ContentIssueCodes.SchemaVersionUnsupported, issues.Single().Code);
        }
    }
}
