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

        /// <summary>İki geçerli model (phone.test_one, phone.test_two) ve eşleşen manifest.</summary>
        public static DictionaryContentSource ValidSource()
        {
            return new DictionaryContentSource()
                .Add(ContentFileNames.PhoneModels, ModelsFile(ValidModelJson, ModelWithId("phone.test_two")))
                .Add(ContentFileNames.IdManifest, ManifestFile("phone.test_one", "phone.test_two"));
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
    }
}
