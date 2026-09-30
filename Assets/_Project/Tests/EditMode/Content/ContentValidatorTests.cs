using System.Collections.Generic;
using System.Linq;
using Esnaf.Domain.Content;
using Esnaf.Domain.Products;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Content
{
    public class ContentValidatorTests
    {
        private const string File = "phone_models.json";

        private static List<ContentIssue> Validate(params ProductDefinition[] products)
        {
            return ValidateWith(new ContentLoadOptions(), products);
        }

        private static List<ContentIssue> ValidateWith(ContentLoadOptions options, params ProductDefinition[] products)
        {
            var issues = new List<ContentIssue>();
            ContentValidator.ValidateProducts(products, options, File, issues);
            return issues;
        }

        private static void AssertOnly(List<ContentIssue> issues, string code)
        {
            Assert.AreEqual(1, issues.Count, string.Join("\n", issues.Select(i => i.ToString())));
            Assert.AreEqual(code, issues[0].Code);
            Assert.AreEqual(ContentIssueSeverity.Error, issues[0].Severity);
            Assert.AreEqual(File, issues[0].File);
        }

        // ---------- genel ----------

        [Test]
        public void ValidProduct_HasNoIssues()
        {
            Assert.AreEqual(0, Validate(ContentFixtures.Def()).Count);
        }

        [Test]
        public void EmptyList_IsReported()
        {
            AssertOnly(Validate(), ContentIssueCodes.ProductsEmpty);
        }

        [Test]
        public void AllProblemsAreCollected_NotJustTheFirst()
        {
            ProductDefinition broken = ContentFixtures.Def(name: " ", brand: "", basePrice: -10, releaseYear: 1900, minAge: 40, maxAge: 10);

            List<ContentIssue> issues = Validate(broken);

            string[] expected =
            {
                ContentIssueCodes.ProductNameEmpty,
                ContentIssueCodes.ProductBrandEmpty,
                ContentIssueCodes.ProductBasePriceNotPositive,
                ContentIssueCodes.ProductReleaseYearRange,
                ContentIssueCodes.ProductAgeMinGreaterThanMax
            };
            foreach (string code in expected)
            {
                Assert.IsTrue(issues.Any(i => i.Code == code), "Expected " + code + " in:\n" + string.Join("\n", issues));
            }
        }

        [Test]
        public void IssueMessage_NamesTheProduct()
        {
            List<ContentIssue> issues = Validate(ContentFixtures.Def(name: ""));

            StringAssert.Contains("phone.test_one", issues.Single().Message);
        }

        // ---------- ID ----------

        [TestCase("")]
        [TestCase(null)]
        [TestCase("phone")]
        [TestCase("Phone.Elma")]
        [TestCase("phone.Elma")]
        [TestCase("phone.elma-e13")]
        [TestCase("phone.elma e13")]
        [TestCase("phone.a.b")]
        [TestCase("1phone.x")]
        [TestCase("phone.")]
        public void InvalidIdFormat_IsReported(string id)
        {
            List<ContentIssue> issues = Validate(ContentFixtures.Def(id: id));

            Assert.IsTrue(issues.Any(i => i.Code == ContentIssueCodes.ProductIdFormat), string.Join("\n", issues));
        }

        [TestCase("phone.a")]
        [TestCase("phone.elma_e13_pro")]
        [TestCase("phone.x1_2_3")]
        public void ValidIdFormat_IsAccepted(string id)
        {
            Assert.AreEqual(0, Validate(ContentFixtures.Def(id: id)).Count);
        }

        [Test]
        public void DuplicateId_IsReported()
        {
            List<ContentIssue> issues = Validate(ContentFixtures.Def(), ContentFixtures.Def());

            AssertOnly(issues, ContentIssueCodes.ProductIdDuplicate);
        }

        [Test]
        public void UnknownSector_IsReported()
        {
            List<ContentIssue> issues = Validate(ContentFixtures.Def(id: "gold.ring", sector: "gold"));

            AssertOnly(issues, ContentIssueCodes.ProductSectorUnknown);
        }

        [Test]
        public void AllowedSectors_CanBeExtended()
        {
            var options = new ContentLoadOptions();
            options.AllowedSectors.Add("gold");

            Assert.AreEqual(0, ValidateWith(options, ContentFixtures.Def(id: "gold.ring", sector: "gold")).Count);
        }

        [Test]
        public void IdPrefixMustMatchSector()
        {
            var options = new ContentLoadOptions();
            options.AllowedSectors.Add("gold");

            List<ContentIssue> issues = ValidateWith(options, ContentFixtures.Def(id: "phone.ring", sector: "gold"));

            AssertOnly(issues, ContentIssueCodes.ProductIdSectorMismatch);
        }

        // ---------- metin / yıl ----------

        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void EmptyName_IsReported(string name)
        {
            AssertOnly(Validate(ContentFixtures.Def(name: name)), ContentIssueCodes.ProductNameEmpty);
        }

        [TestCase("")]
        [TestCase(null)]
        public void EmptyBrand_IsReported(string brand)
        {
            AssertOnly(Validate(ContentFixtures.Def(brand: brand)), ContentIssueCodes.ProductBrandEmpty);
        }

        [TestCase(1999, true)]
        [TestCase(2000, false)]
        [TestCase(2100, false)]
        [TestCase(2101, true)]
        public void ReleaseYear_MustBeInRange(int year, bool shouldFail)
        {
            List<ContentIssue> issues = Validate(ContentFixtures.Def(releaseYear: year));

            if (shouldFail)
            {
                AssertOnly(issues, ContentIssueCodes.ProductReleaseYearRange);
            }
            else
            {
                Assert.AreEqual(0, issues.Count);
            }
        }

        // ---------- fiyat ----------

        [TestCase(0L)]
        [TestCase(-10L)]
        public void BasePrice_MustBePositive(long price)
        {
            AssertOnly(Validate(ContentFixtures.Def(basePrice: price)), ContentIssueCodes.ProductBasePriceNotPositive);
        }

        [TestCase(10005L)]
        [TestCase(10001L)]
        [TestCase(1L)]
        public void BasePrice_MustBeMultipleOfTen(long price)
        {
            AssertOnly(Validate(ContentFixtures.Def(basePrice: price)), ContentIssueCodes.ProductBasePriceNotRounded);
        }

        [TestCase(10L)]
        [TestCase(4500L)]
        [TestCase(46000L)]
        public void BasePrice_RoundedIsAccepted(long price)
        {
            Assert.AreEqual(0, Validate(ContentFixtures.Def(basePrice: price)).Count);
        }

        // ---------- hafıza ----------

        [Test]
        public void EmptyStorageOptions_IsReported()
        {
            List<ContentIssue> issues = Validate(ContentFixtures.Def(storage: new StorageOption[0]));

            AssertOnly(issues, ContentIssueCodes.ProductStorageEmpty);
        }

        [Test]
        public void StorageGb_MustBePositive()
        {
            var storage = new[] { new StorageOption(128, 1.0), new StorageOption(0, 1.1) };

            Assert.IsTrue(Validate(ContentFixtures.Def(storage: storage)).Any(i => i.Code == ContentIssueCodes.ProductStorageGbNotPositive));
        }

        [Test]
        public void DuplicateStorageGb_IsReported()
        {
            var storage = new[] { new StorageOption(128, 1.0), new StorageOption(128, 1.1) };

            AssertOnly(Validate(ContentFixtures.Def(storage: storage)), ContentIssueCodes.ProductStorageDuplicate);
        }

        [TestCase(0.0)]
        [TestCase(-1.0)]
        [TestCase(double.NaN)]
        public void StorageMultiplier_MustBePositive(double multiplier)
        {
            var storage = new[] { new StorageOption(128, 1.0), new StorageOption(256, multiplier) };

            AssertOnly(Validate(ContentFixtures.Def(storage: storage)), ContentIssueCodes.ProductStorageMultNotPositive);
        }

        [Test]
        public void BaseStorage_MustBeAmongOptions()
        {
            var storage = new[] { new StorageOption(64, 1.0), new StorageOption(256, 1.2) };

            AssertOnly(Validate(ContentFixtures.Def(baseStorageGb: 128, storage: storage)), ContentIssueCodes.ProductStorageBaseMissing);
        }

        [Test]
        public void BaseStorageMultiplier_MustBeExactlyOne()
        {
            var storage = new[] { new StorageOption(128, 1.05), new StorageOption(256, 1.2) };

            AssertOnly(Validate(ContentFixtures.Def(storage: storage)), ContentIssueCodes.ProductStorageBaseMultNotOne);
        }

        [Test]
        public void LowerStorageWithDiscountMultiplier_IsAccepted()
        {
            var storage = new[] { new StorageOption(64, 0.90), new StorageOption(128, 1.0), new StorageOption(256, 1.15) };

            Assert.AreEqual(0, Validate(ContentFixtures.Def(storage: storage)).Count);
        }

        // ---------- yaş ----------

        [Test]
        public void NegativeAge_IsReported()
        {
            AssertOnly(Validate(ContentFixtures.Def(minAge: -1, maxAge: 10)), ContentIssueCodes.ProductAgeNegative);
        }

        [Test]
        public void MinGreaterThanMax_IsReported()
        {
            AssertOnly(Validate(ContentFixtures.Def(minAge: 20, maxAge: 10)), ContentIssueCodes.ProductAgeMinGreaterThanMax);
        }

        [Test]
        public void MinEqualsMax_AndZeroMin_AreAccepted()
        {
            Assert.AreEqual(0, Validate(ContentFixtures.Def(minAge: 12, maxAge: 12)).Count);
            Assert.AreEqual(0, Validate(ContentFixtures.Def(minAge: 0, maxAge: 18)).Count);
        }

        // ---------- manifest ----------

        private static List<ContentIssue> ValidateManifest(string[] manifest, string[] content)
        {
            var issues = new List<ContentIssue>();
            ContentValidator.ValidateManifest(manifest, content, "content_id_manifest.json", issues);
            return issues;
        }

        [Test]
        public void Manifest_Matching_HasNoIssues()
        {
            Assert.AreEqual(0, ValidateManifest(new[] { "phone.a", "phone.b" }, new[] { "phone.b", "phone.a" }).Count);
        }

        [Test]
        public void Manifest_IdMissingInContent_IsAnError_BecauseIdsMustNeverBeRemoved()
        {
            List<ContentIssue> issues = ValidateManifest(new[] { "phone.a", "phone.gone" }, new[] { "phone.a" });

            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual(ContentIssueCodes.ManifestIdMissingInContent, issues[0].Code);
            StringAssert.Contains("phone.gone", issues[0].Message);
            StringAssert.Contains("deprecated", issues[0].Message);
        }

        [Test]
        public void Manifest_NewUnregisteredId_IsAnError_AndMessageShowsHowToFix()
        {
            List<ContentIssue> issues = ValidateManifest(new[] { "phone.a" }, new[] { "phone.a", "phone.new" });

            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual(ContentIssueCodes.ManifestIdNotRegistered, issues[0].Code);
            StringAssert.Contains("phone.new", issues[0].Message);
            StringAssert.Contains("content_id_manifest.json", issues[0].Message);
        }

        [Test]
        public void Manifest_DuplicateEntry_IsReported()
        {
            List<ContentIssue> issues = ValidateManifest(new[] { "phone.a", "phone.a" }, new[] { "phone.a" });

            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual(ContentIssueCodes.ManifestDuplicate, issues[0].Code);
        }
    }
}
