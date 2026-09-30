using System;
using System.Collections.Generic;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Content;
using Esnaf.Domain.Products;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Content
{
    public class ContentDatabaseTests
    {
        [Test]
        public void ValidContent_LoadsSuccessfully()
        {
            ContentLoadResult result = ContentDatabase.Load(ContentFixtures.ValidSource());

            Assert.IsTrue(result.IsSuccess, result.FormatIssues());
            Assert.AreEqual(0, result.ErrorCount);
            Assert.IsNotNull(result.Database);
            Assert.AreEqual(2, result.Database.Products.Count);
        }

        [Test]
        public void Products_KeepFileOrder()
        {
            ContentDatabase db = ContentDatabase.Load(ContentFixtures.ValidSource()).Database;

            Assert.AreEqual(new[] { "phone.test_one", "phone.test_two" }, db.Products.Select(p => p.Id).ToArray());
        }

        [Test]
        public void GetProduct_And_TryGetProduct()
        {
            ContentDatabase db = ContentDatabase.Load(ContentFixtures.ValidSource()).Database;

            Assert.AreEqual("phone.test_two", db.GetProduct("phone.test_two").Id);

            ProductDefinition found;
            Assert.IsTrue(db.TryGetProduct("phone.test_one", out found));
            Assert.AreEqual("phone.test_one", found.Id);

            Assert.IsFalse(db.TryGetProduct("phone.nope", out found));
            Assert.IsNull(found);
            Assert.IsFalse(db.TryGetProduct(null, out found));
        }

        [Test]
        public void GetProduct_UnknownId_Throws()
        {
            ContentDatabase db = ContentDatabase.Load(ContentFixtures.ValidSource()).Database;

            Assert.Throws<KeyNotFoundException>(() => db.GetProduct("phone.nope"));
            Assert.Throws<KeyNotFoundException>(() => db.GetProduct(null));
        }

        [Test]
        public void LoadedDefinitions_AreReadOnly()
        {
            ContentDatabase db = ContentDatabase.Load(ContentFixtures.ValidSource()).Database;

            Assert.IsFalse(db.Products is List<ProductDefinition>);
            Assert.IsFalse(db.Products is ProductDefinition[]);
            Assert.IsFalse(db.Products[0].StorageOptions is List<StorageOption>);
            Assert.IsFalse(db.Products[0].StorageOptions is StorageOption[]);
        }

        [Test]
        public void Load_NullSource_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => ContentDatabase.Load(null));
        }

        // ---------- hatalı içerik: veritabanı üretilmez ----------

        [Test]
        public void MissingProductsFile_Fails()
        {
            DictionaryContentSource source = ContentFixtures.ValidSource();
            source.Remove(ContentFileNames.PhoneModels);

            ContentLoadResult result = ContentDatabase.Load(source);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNull(result.Database);
            Assert.IsTrue(result.Issues.Any(i => i.Code == ContentIssueCodes.FileMissing && i.File == ContentFileNames.PhoneModels));
        }

        [Test]
        public void MalformedProductsFile_Fails_WithSyntaxError()
        {
            var source = new DictionaryContentSource()
                .AddValidTables()
                .Add(ContentFileNames.PhoneModels, "{ broken")
                .Add(ContentFileNames.IdManifest, ContentFixtures.ManifestFile("phone.test_one"));

            ContentLoadResult result = ContentDatabase.Load(source);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.Issues.Any(i => i.Code == ContentIssueCodes.FileSyntax));
        }

        [Test]
        public void DuplicateIds_Fail()
        {
            var source = new DictionaryContentSource()
                .AddValidTables()
                .Add(ContentFileNames.PhoneModels, ContentFixtures.ModelsFile(ContentFixtures.ValidModelJson, ContentFixtures.ValidModelJson))
                .Add(ContentFileNames.IdManifest, ContentFixtures.ManifestFile("phone.test_one"));

            ContentLoadResult result = ContentDatabase.Load(source);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.Issues.Any(i => i.Code == ContentIssueCodes.ProductIdDuplicate));
        }

        [Test]
        public void ValidatorRules_ApplyDuringLoad()
        {
            string badPrice = ContentFixtures.ValidModelJson.Replace("\"basePrice\": 10000", "\"basePrice\": 10005");
            var source = new DictionaryContentSource()
                .AddValidTables()
                .Add(ContentFileNames.PhoneModels, ContentFixtures.ModelsFile(badPrice))
                .Add(ContentFileNames.IdManifest, ContentFixtures.ManifestFile("phone.test_one"));

            ContentLoadResult result = ContentDatabase.Load(source);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.Issues.Any(i => i.Code == ContentIssueCodes.ProductBasePriceNotRounded));
        }

        [Test]
        public void Errors_InBothFiles_AreAggregated()
        {
            var source = new DictionaryContentSource()
                .AddValidTables()
                .Add(ContentFileNames.PhoneModels, "{ broken")
                .Add(ContentFileNames.IdManifest, "{ also broken");

            ContentLoadResult result = ContentDatabase.Load(source);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(2, result.ErrorCount);
            Assert.IsTrue(result.Issues.Any(i => i.File == ContentFileNames.PhoneModels));
            Assert.IsTrue(result.Issues.Any(i => i.File == ContentFileNames.IdManifest));
        }

        [Test]
        public void FormatIssues_ContainsFileAndCode()
        {
            ContentLoadResult result = ContentDatabase.Load(new DictionaryContentSource());

            string text = result.FormatIssues();

            StringAssert.Contains(ContentFileNames.PhoneModels, text);
            StringAssert.Contains(ContentIssueCodes.FileMissing, text);
        }

        [Test]
        public void IssuesList_IsReadOnly()
        {
            ContentLoadResult result = ContentDatabase.Load(new DictionaryContentSource());

            Assert.IsFalse(result.Issues is List<ContentIssue>);
        }

        // ---------- manifest ----------

        [Test]
        public void MissingManifest_Fails_WhenRequired()
        {
            DictionaryContentSource source = ContentFixtures.ValidSource();
            source.Remove(ContentFileNames.IdManifest);

            ContentLoadResult result = ContentDatabase.Load(source);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.Issues.Any(i => i.Code == ContentIssueCodes.FileMissing && i.File == ContentFileNames.IdManifest));
        }

        [Test]
        public void MissingManifest_IsFine_WhenNotRequired()
        {
            DictionaryContentSource source = ContentFixtures.ValidSource();
            source.Remove(ContentFileNames.IdManifest);

            ContentLoadResult result = ContentDatabase.Load(source, new ContentLoadOptions { RequireManifest = false });

            Assert.IsTrue(result.IsSuccess, result.FormatIssues());
        }

        [Test]
        public void ManifestMismatch_Fails_EvenWhenNotRequired_IfManifestIsPresent()
        {
            var source = new DictionaryContentSource()
                .AddValidTables()
                .Add(ContentFileNames.PhoneModels, ContentFixtures.ModelsFile(ContentFixtures.ValidModelJson, ContentFixtures.ModelWithId("phone.test_two")))
                .Add(ContentFileNames.IdManifest, ContentFixtures.ManifestWithNpcs("phone.other"));

            ContentLoadResult result = ContentDatabase.Load(source, new ContentLoadOptions { RequireManifest = false });

            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.Issues.Any(i => i.Code == ContentIssueCodes.ManifestIdMissingInContent));
            Assert.IsTrue(result.Issues.Any(i => i.Code == ContentIssueCodes.ManifestIdNotRegistered));
        }

        [Test]
        public void RemovedProduct_IsCaughtByManifest()
        {
            var source = new DictionaryContentSource()
                .AddValidTables()
                .Add(ContentFileNames.PhoneModels, ContentFixtures.ModelsFile(ContentFixtures.ValidModelJson, ContentFixtures.ModelWithId("phone.test_two")))
                .Add(ContentFileNames.IdManifest, ContentFixtures.ManifestWithNpcs("phone.test_one", "phone.test_two", "phone.test_three"));

            ContentLoadResult result = ContentDatabase.Load(source);

            Assert.IsFalse(result.IsSuccess);
            ContentIssue issue = result.Issues.Single();
            Assert.AreEqual(ContentIssueCodes.ManifestIdMissingInContent, issue.Code);
            StringAssert.Contains("phone.test_three", issue.Message);
        }

        [Test]
        public void DeprecatedProduct_StillLoads_AndSatisfiesManifest()
        {
            string deprecated = ContentFixtures.ModelWithId("phone.test_two")
                .Replace("\"ageMonths\"", "\"deprecated\": true, \"ageMonths\"");
            var source = new DictionaryContentSource()
                .AddValidTables()
                .Add(ContentFileNames.PhoneModels, ContentFixtures.ModelsFile(ContentFixtures.ValidModelJson, deprecated))
                .Add(ContentFileNames.IdManifest, ContentFixtures.ManifestWithNpcs("phone.test_one", "phone.test_two"));

            ContentLoadResult result = ContentDatabase.Load(source);

            Assert.IsTrue(result.IsSuccess, result.FormatIssues());
            Assert.IsTrue(result.Database.GetProduct("phone.test_two").IsDeprecated);
        }

        [Test]
        public void BrokenModel_DoesNotCauseManifestNoise()
        {
            // İkinci modelde 'brand' eksik -> model atlanır. Manifest hâlâ ikisini de listeliyor;
            // ama gerçek sorun (eksik alan) dışında "manifest.id_missing_in_content" gürültüsü OLMAMALI.
            string bad = ContentFixtures.ModelWithId("phone.test_two").Replace("\"brand\": \"Testco\",", string.Empty);
            var source = new DictionaryContentSource()
                .AddValidTables()
                .Add(ContentFileNames.PhoneModels, ContentFixtures.ModelsFile(ContentFixtures.ValidModelJson, bad))
                .Add(ContentFileNames.IdManifest, ContentFixtures.ManifestWithNpcs("phone.test_one", "phone.test_two"));

            ContentLoadResult result = ContentDatabase.Load(source);

            Assert.IsFalse(result.IsSuccess);
            ContentIssue issue = result.Issues.Single();
            Assert.AreEqual(ContentIssueCodes.FieldMissing, issue.Code);
        }

        // ---------- Gün 3: değer tabloları ve durum profilleri ----------

        [Test]
        public void ValidContent_ExposesValueTablesAndConditionProfiles()
        {
            ContentDatabase db = ContentDatabase.Load(ContentFixtures.ValidSource()).Database;

            Assert.IsNotNull(db.ValueTables);
            Assert.AreEqual(6, db.ValueTables.AgeBands.Count);
            Assert.AreEqual(new[] { "alpha", "beta" }, db.ConditionProfiles.Select(p => p.Id).ToArray());
            Assert.IsFalse(db.ConditionProfiles is List<ConditionProfile>);

            ConditionProfile beta;
            Assert.IsTrue(db.TryGetConditionProfile("beta", out beta));
            Assert.AreEqual(3, beta.AvailableFromDay);
            Assert.IsFalse(db.TryGetConditionProfile("gamma", out beta));
            Assert.IsFalse(db.TryGetConditionProfile(null, out beta));
        }

        [Test]
        public void MissingValueTablesFile_Fails()
        {
            DictionaryContentSource source = ContentFixtures.ValidSource();
            source.Remove(ContentFileNames.ValueTables);

            ContentLoadResult result = ContentDatabase.Load(source);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.Issues.Any(i => i.Code == ContentIssueCodes.FileMissing && i.File == ContentFileNames.ValueTables));
        }

        [Test]
        public void MissingConditionProfilesFile_Fails()
        {
            DictionaryContentSource source = ContentFixtures.ValidSource();
            source.Remove(ContentFileNames.ConditionProfiles);

            ContentLoadResult result = ContentDatabase.Load(source);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.Issues.Any(i => i.Code == ContentIssueCodes.FileMissing && i.File == ContentFileNames.ConditionProfiles));
        }

        [Test]
        public void ProfileReferencingUnknownScreenValue_FailsAtLoad_CrossFileCheck()
        {
            DictionaryContentSource source = ContentFixtures.ValidSource();
            source.Add(ContentFileNames.ConditionProfiles, ContentFixtures.ProfilesJson.Replace("\"original\"", "\"smashed\""));

            ContentLoadResult result = ContentDatabase.Load(source);

            Assert.IsFalse(result.IsSuccess);
            ContentIssue issue = result.Issues.Single();
            Assert.AreEqual(ContentIssueCodes.ProfileChoiceUnknownValue, issue.Code);
            Assert.AreEqual(ContentFileNames.ConditionProfiles, issue.File);
        }

        [Test]
        public void InvalidValueTables_FailAtLoad_WithValidatorIssue()
        {
            DictionaryContentSource source = ContentFixtures.ValidSource();
            source.Add(ContentFileNames.ValueTables, ContentFixtures.ValueTablesJson.Replace("\"fromMonths\": 0", "\"fromMonths\": 1"));

            ContentLoadResult result = ContentDatabase.Load(source);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.Issues.Any(i => i.Code == ContentIssueCodes.ValueTablesAgeFirstNotZero));
        }

        [Test]
        public void UnparsableValueTables_DoNotCauseProfileCrossCheckNoise()
        {
            DictionaryContentSource source = ContentFixtures.ValidSource();
            source.Add(ContentFileNames.ValueTables, "{ broken");

            ContentLoadResult result = ContentDatabase.Load(source);

            Assert.IsFalse(result.IsSuccess);
            ContentIssue issue = result.Issues.Single();
            Assert.AreEqual(ContentIssueCodes.FileSyntax, issue.Code);
            Assert.AreEqual(ContentFileNames.ValueTables, issue.File);
        }

        [Test]
        public void ProfilesWithoutDayOneEntry_FailAtLoad()
        {
            DictionaryContentSource source = ContentFixtures.ValidSource();
            source.Add(ContentFileNames.ConditionProfiles, ContentFixtures.ProfilesJson.Replace("\"availableFromDay\": 1", "\"availableFromDay\": 2"));

            ContentLoadResult result = ContentDatabase.Load(source);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.Issues.Any(i => i.Code == ContentIssueCodes.ProfilesNoDayOne));
        }
    }
}
