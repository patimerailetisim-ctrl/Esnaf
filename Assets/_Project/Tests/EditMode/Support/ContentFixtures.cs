using System.Collections.Generic;
using Esnaf.Core;
using Esnaf.Domain.Content;
using Esnaf.Domain.Products;

namespace Esnaf.Tests.Support
{
    /// <summary>Testler için küçük, geçerli içerik parçaları ve kolay bozma yardımcıları.</summary>
    public static class ContentFixtures
    {
        public const string TestModelId = "phone.test_one";

        /// <summary>Tek, geçerli model JSON'u (id: phone.test_one).</summary>
        public const string ValidModelJson =
            "{ \"id\": \"phone.test_one\", \"sector\": \"phone\", \"name\": \"Test One\", \"brand\": \"Testco\"," +
            " \"segment\": \"mid\", \"releaseYear\": 2023, \"basePrice\": 10000, \"baseStorageGb\": 128," +
            " \"storageOptions\": [ { \"gb\": 128, \"mult\": 1.0 }, { \"gb\": 256, \"mult\": 1.2 } ]," +
            " \"ageMonths\": { \"min\": 6, \"max\": 36 } }";

        /// <summary>GDD v0.2 Bölüm 4.2 çarpan tablolarının test kopyası (gerçek dosyayla aynı değerler).</summary>
        public const string ValueTablesJson =
            "{ \"schemaVersion\": 1," +
            " \"age\": [ { \"fromMonths\": 0, \"mult\": 1.10 }, { \"fromMonths\": 7, \"mult\": 1.00 }, { \"fromMonths\": 13, \"mult\": 0.88 }," +
            " { \"fromMonths\": 25, \"mult\": 0.77 }, { \"fromMonths\": 37, \"mult\": 0.68 }, { \"fromMonths\": 49, \"mult\": 0.60 } ]," +
            " \"battery\": { \"fullAtOrAbove\": 90, \"penaltyPerPoint\": 0.006 }," +
            " \"body\": { \"base\": 0.80, \"span\": 0.20 }," +
            " \"screen\": [ { \"id\": \"original\", \"mult\": 1.00 }, { \"id\": \"scratched\", \"mult\": 0.96 }," +
            " { \"id\": \"replaced_aftermarket\", \"mult\": 0.88 }, { \"id\": \"cracked\", \"mult\": 0.75 } ]," +
            " \"camera\": [ { \"id\": \"ok\", \"mult\": 1.00 }, { \"id\": \"spotted\", \"mult\": 0.93 }, { \"id\": \"faulty\", \"mult\": 0.82 } ]," +
            " \"package\": { \"boxBonus\": 0.02, \"invoiceBonus\": 0.02 } }";

        /// <summary>
        /// Üretici testleri için SABİT iki profil (Python referans modeliyle aynı sayılar).
        /// alpha: ağırlık 3, Gün 1+; beta: ağırlık 1, Gün 3+. Gerçek denge dosyası ayarlansa da bu testler bozulmaz.
        /// </summary>
        public const string ProfilesJson =
            "{ \"schemaVersion\": 1, \"profiles\": [" +
            " { \"id\": \"alpha\", \"name\": \"Alpha\", \"weight\": 3, \"availableFromDay\": 1," +
            "   \"battery\": { \"min\": 80, \"max\": 99 }, \"body\": { \"min\": 60, \"max\": 100 }," +
            "   \"screen\": [ { \"value\": \"original\", \"weight\": 1 }, { \"value\": \"scratched\", \"weight\": 1 } ]," +
            "   \"camera\": [ { \"value\": \"ok\", \"weight\": 1 } ]," +
            "   \"boxChance\": 0.5, \"invoiceChance\": 0.25 }," +
            " { \"id\": \"beta\", \"name\": \"Beta\", \"weight\": 1, \"availableFromDay\": 3," +
            "   \"battery\": { \"min\": 50, \"max\": 70 }, \"body\": { \"min\": 40, \"max\": 60 }," +
            "   \"screen\": [ { \"value\": \"cracked\", \"weight\": 2 }, { \"value\": \"replaced_aftermarket\", \"weight\": 1 } ]," +
            "   \"camera\": [ { \"value\": \"faulty\", \"weight\": 1 }, { \"value\": \"spotted\", \"weight\": 1 } ]," +
            "   \"boxChance\": 0.0, \"invoiceChance\": 0.0 } ] }";

        public static string ModelWithId(string id)
        {
            return ValidModelJson.Replace(TestModelId, id);
        }

        public static string ModelsFile(params string[] modelJsons)
        {
            return "{ \"schemaVersion\": 1, \"models\": [" + string.Join(",", modelJsons) + "] }";
        }

        public static string ManifestFile(params string[] ids)
        {
            var quoted = new List<string>();
            foreach (string id in ids)
            {
                quoted.Add("\"" + id + "\"");
            }

            return "{ \"schemaVersion\": 1, \"ids\": [" + string.Join(",", quoted) + "] }";
        }

        /// <summary>İki geçerli model (phone.test_one, phone.test_two), eşleşen manifest ve geçerli çarpan/profil tabloları.</summary>
        public static DictionaryContentSource ValidSource()
        {
            return new DictionaryContentSource()
                .AddValidTables()
                .Add(ContentFileNames.PhoneModels, ModelsFile(ValidModelJson, ModelWithId("phone.test_two")))
                .Add(ContentFileNames.IdManifest, ManifestFile("phone.test_one", "phone.test_two"));
        }

        /// <summary>Test JSON'undan çarpan tablolarını ayrıştırır (ayrıştırma hatasız olmalı).</summary>
        public static ValueTables Tables()
        {
            var issues = new List<ContentIssue>();
            ValueTables tables = ContentParser.ParseValueTables(ContentFileNames.ValueTables, ValueTablesJson, issues);
            if (tables == null || issues.Count > 0)
            {
                throw new System.InvalidOperationException("Fixture value tables are invalid: " + string.Join("; ", issues));
            }

            return tables;
        }

        /// <summary>Test JSON'undan sabit alpha/beta profillerini ayrıştırır.</summary>
        public static IReadOnlyList<ConditionProfile> Profiles()
        {
            var issues = new List<ContentIssue>();
            IReadOnlyList<ConditionProfile> profiles = ContentParser.ParseConditionProfiles(ContentFileNames.ConditionProfiles, ProfilesJson, issues);
            if (profiles == null || issues.Count > 0)
            {
                throw new System.InvalidOperationException("Fixture profiles are invalid: " + string.Join("; ", issues));
            }

            return profiles;
        }

        /// <summary>Doğrudan kodla, geçerli varsayılanlarla ürün tanımı üretir; testler tek bir alanı bozar.</summary>
        public static ProductDefinition Def(
            string id = TestModelId,
            string sector = "phone",
            string name = "Test One",
            string brand = "Testco",
            ProductSegment segment = ProductSegment.Mid,
            int releaseYear = 2023,
            long basePrice = 10000,
            int baseStorageGb = 128,
            IEnumerable<StorageOption> storage = null,
            int minAge = 6,
            int maxAge = 36)
        {
            IEnumerable<StorageOption> options = storage ?? new[] { new StorageOption(128, 1.0), new StorageOption(256, 1.2) };
            return new ProductDefinition(
                id, sector, name, brand, segment, releaseYear, Money.FromTl(basePrice), baseStorageGb, options, minAge, maxAge, id, false);
        }

        /// <summary>GDD Bölüm 4.4'teki Elma E13 Pro (baz 32.000; 128/256/512 GB).</summary>
        public static ProductDefinition E13Pro()
        {
            return Def(
                id: "phone.elma_e13_pro",
                name: "Elma E13 Pro",
                brand: "Elma",
                segment: ProductSegment.Upper,
                basePrice: 32000,
                storage: new[] { new StorageOption(128, 1.00), new StorageOption(256, 1.12), new StorageOption(512, 1.28) });
        }

        /// <summary>Verilen tanım için, nitelikleri tek tek verilen ürün örneği.</summary>
        public static ProductInstance Instance(
            ProductDefinition definition,
            int ageMonths,
            long battery,
            string screen,
            long body,
            string camera,
            bool box = false,
            bool invoice = false,
            int storageGb = 0)
        {
            var instance = new ProductInstance
            {
                InstanceId = 1,
                DefinitionId = definition.Id,
                StorageGb = storageGb == 0 ? definition.BaseStorageGb : storageGb,
                AgeMonths = ageMonths
            };
            instance.Attributes["battery"] = AttributeValue.FromNumber(battery);
            instance.Attributes["screen"] = AttributeValue.FromText(screen);
            instance.Attributes["body"] = AttributeValue.FromNumber(body);
            instance.Attributes["camera"] = AttributeValue.FromText(camera);
            instance.Attributes["box"] = AttributeValue.FromFlag(box);
            instance.Attributes["invoice"] = AttributeValue.FromFlag(invoice);
            return instance;
        }
    }

    public static class ContentFixtureExtensions
    {
        /// <summary>Kaynağa geçerli value_tables.json ve condition_profiles.json ekler.</summary>
        public static DictionaryContentSource AddValidTables(this DictionaryContentSource source)
        {
            return source
                .Add(ContentFileNames.ValueTables, ContentFixtures.ValueTablesJson)
                .Add(ContentFileNames.ConditionProfiles, ContentFixtures.ProfilesJson);
        }
    }
}
