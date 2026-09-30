using System.Collections.Generic;
using Esnaf.Core;
using Esnaf.Domain.Appraisal;
using Esnaf.Domain.Content;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Market;
using Esnaf.Domain.Npc;
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

        /// <summary>GDD v0.3 2.3 / v0.2 9.3: 8 çekirdek defter türü (gerçek dosyayla aynı değerler).</summary>
        public const string TransactionTypesJson =
            "{ \"schemaVersion\": 1, \"types\": [" +
            " { \"id\": \"opening_capital\", \"displayKey\": \"ledger.type.opening_capital\", \"category\": \"capital\", \"direction\": \"inflow\", \"profitEffect\": \"none\" }," +
            " { \"id\": \"purchase\", \"displayKey\": \"ledger.type.purchase\", \"category\": \"trade\", \"direction\": \"outflow\", \"profitEffect\": \"none\" }," +
            " { \"id\": \"sale\", \"displayKey\": \"ledger.type.sale\", \"category\": \"trade\", \"direction\": \"inflow\", \"profitEffect\": \"sale\" }," +
            " { \"id\": \"appraisal\", \"displayKey\": \"ledger.type.appraisal\", \"category\": \"trade\", \"direction\": \"outflow\", \"profitEffect\": \"none\" }," +
            " { \"id\": \"wasted_appraisal\", \"displayKey\": \"ledger.type.wasted_appraisal\", \"category\": \"expense\", \"direction\": \"neutral\", \"profitEffect\": \"write_off\" }," +
            " { \"id\": \"repair\", \"displayKey\": \"ledger.type.repair\", \"category\": \"trade\", \"direction\": \"outflow\", \"profitEffect\": \"none\" }," +
            " { \"id\": \"daily_expense\", \"displayKey\": \"ledger.type.daily_expense\", \"category\": \"expense\", \"direction\": \"outflow\", \"profitEffect\": \"expense\" }," +
            " { \"id\": \"investment\", \"displayKey\": \"ledger.type.investment\", \"category\": \"investment\", \"direction\": \"outflow\", \"profitEffect\": \"none\" } ] }";

        /// <summary>
        /// Başlangıç sermayesi 250.000; günlük gider Gün 3'ten itibaren 500; başlangıç raf kapasitesi 6; pazar kuralları
        /// (gerçek dosyadaki GDD sayılarıyla aynı yapı; modeller test modelleridir).
        /// </summary>
        public const string EconomyConstantsJson =
            "{ \"schemaVersion\": 1, \"openingCapital\": 250000," +
            " \"dailyExpense\": { \"fromDay\": 3, \"amount\": 500 }, \"initialShelfCapacity\": 6," +
            " \"market\": " + MarketJson + " }";

        /// <summary>economy_constants.json içindeki "market" bölümünün test kopyası.</summary>
        public const string MarketJson =
            "{ \"listingCounts\": [ { \"fromDay\": 1, \"min\": 3, \"max\": 3 }, { \"fromDay\": 2, \"min\": 5, \"max\": 5 }," +
            " { \"fromDay\": 3, \"min\": 6, \"max\": 6 }, { \"fromDay\": 5, \"min\": 7, \"max\": 8 } ]," +
            " \"listingLifetimeDays\": { \"min\": 2, \"max\": 4 }," +
            " \"segmentWeights\": [ { \"fromDay\": 1, \"entry\": 30, \"mid\": 40, \"upper\": 15 }, { \"fromDay\": 5, \"entry\": 30, \"mid\": 40, \"upper\": 30 } ]," +
            " \"modelAvailability\": [ { \"id\": \"phone.test_two\", \"fromDay\": 5 } ]," +
            " \"learningFriendlySellers\": { \"untilDay\": 4, \"share\": 0.45 }," +
            " \"opportunity\": { \"fromDay\": 2, \"minPerDay\": 2, \"maxRejectRatio\": 0.90 }," +
            " \"jackpot\": { \"rejectRatioBelow\": 0.85, \"maxPerDay\": [ { \"fromDay\": 1, \"max\": 1 }, { \"fromDay\": 6, \"max\": 2 } ] }," +
            " \"trap\": { \"fromDay\": 5, \"minPerDay\": 1, \"maxPerDay\": [ { \"fromDay\": 6, \"max\": 2 } ], \"valueRatio\": 1.15 }," +
            " \"askingPriceStep\": 50," +
            " \"hiddenDefects\": [ { \"attribute\": \"screen\", \"hiddenValues\": [ \"replaced_aftermarket\" ], \"cleanValue\": \"original\" }," +
            " { \"attribute\": \"camera\", \"hiddenValues\": [ \"spotted\", \"faulty\" ], \"cleanValue\": \"ok\" } ]," +
            " \"guidedListing\": { \"sellerNpcId\": \"npc.test_honest\", \"definitionId\": \"phone.test_one\", \"storageGb\": 128, \"ageMonths\": 12," +
            " \"battery\": 95, \"body\": 100, \"screen\": \"original\", \"camera\": \"ok\", \"box\": false, \"invoice\": false, \"rejectPrice\": 4750 } }";

        /// <summary>
        /// GDD v0.2 5.1/5.2 tablolarının test kopyası. centerShift ve valueNoise 0'dır: v0.2 5.4 örneği "sabit rastgele
        /// değerlerle" (sıfır sapma) hesaplandığı için altın testler bu fixture'ı kullanır.
        /// </summary>
        public const string AppraisalLevelsJson =
            "{ \"schemaVersion\": 1, \"levels\": [" +
            " { \"id\": \"s0\", \"name\": \"Göz muayenesi\", \"unlockDay\": 1, \"fees\": { \"entry\": 0, \"mid\": 0, \"upper\": 0 }," +
            "   \"confidence\": \"hint\", \"evidencePower\": 0.2, \"detect\": { \"screen\": 0.35, \"camera\": 0.20 }, \"falseAlarm\": 0.05," +
            "   \"bodyHalfWidth\": 15, \"centerShift\": 0, \"valueNoise\": 0 }," +
            " { \"id\": \"s1\", \"name\": \"Temel kontrol\", \"unlockDay\": 3, \"fees\": { \"entry\": 100, \"mid\": 200, \"upper\": 300 }," +
            "   \"confidence\": \"low\", \"evidencePower\": 0.4, \"detect\": { \"screen\": 0.70, \"camera\": 0.60 }, \"falseAlarm\": 0.05," +
            "   \"batteryHalfWidth\": 10, \"bodyHalfWidth\": 10, \"valueHalfWidth\": 0.10, \"centerShift\": 0, \"valueNoise\": 0, \"coverageEstimate\": 0.85 }," +
            " { \"id\": \"s2\", \"name\": \"Ayrıntılı kontrol\", \"unlockDay\": 5, \"fees\": { \"entry\": 350, \"mid\": 600, \"upper\": 1000 }," +
            "   \"confidence\": \"medium\", \"evidencePower\": 0.7, \"detect\": { \"screen\": 0.92, \"camera\": 0.90 }, \"falseAlarm\": 0.02," +
            "   \"batteryHalfWidth\": 5, \"bodyHalfWidth\": 5, \"valueHalfWidth\": 0.05, \"centerShift\": 0, \"valueNoise\": 0, \"coverageEstimate\": 0.92 }," +
            " { \"id\": \"s3\", \"name\": \"Profesyonel ekspertiz\", \"unlockDay\": 6, \"requiredEquipment\": \"test_device\"," +
            "   \"fees\": { \"entry\": 700, \"mid\": 1200, \"upper\": 2000 }," +
            "   \"confidence\": \"certain\", \"evidencePower\": 1.0, \"detect\": { \"screen\": 0.99, \"camera\": 0.99 }, \"falseAlarm\": 0.005," +
            "   \"batteryHalfWidth\": 2, \"bodyHalfWidth\": 3, \"valueHalfWidth\": 0.025, \"centerShift\": 0, \"valueNoise\": 0, \"coverageEstimate\": 0.96 } ]," +
            " \"checks\": [" +
            " { \"attribute\": \"screen\", \"defectValues\": [ \"replaced_aftermarket\" ], \"falseAlarmValue\": \"replaced_aftermarket\", \"cleanValue\": \"original\", \"wordingKey\": \"appraisal.finding.screen_replaced\" }," +
            " { \"attribute\": \"camera\", \"defectValues\": [ \"spotted\", \"faulty\" ], \"falseAlarmValue\": \"spotted\", \"cleanValue\": \"ok\", \"wordingKey\": \"appraisal.finding.camera_problem\" } ]," +
            " \"riskCard\": { \"expectedSaleFactor\": 1.02 } }";

        /// <summary>Üç test NPC'si: dürüst (Gün 1), aceleci (Gün 2), kusur saklayan (Gün 3).</summary>
        public const string NpcProfilesJson =
            "{ \"schemaVersion\": 1, \"npcs\": [" +
            " { \"id\": \"npc.test_honest\", \"name\": \"Dürüst Test\", \"personality\": \"honest\"," +
            "   \"seller\": { \"availableFromDay\": 1, \"askMultiplier\": 1.10, \"rejectRatio\": 0.94, \"patience\": 4, \"valueSigma\": 0.05, \"urgency\": 0.3, \"persuasion\": 0.9, \"learningFriendly\": true }," +
            "   \"customer\": { \"openingOfferRatio\": 0.90, \"valueRatio\": 1.00, \"patience\": 4 } }," +
            " { \"id\": \"npc.test_hurried\", \"name\": \"Aceleci Test\", \"personality\": \"hurried\"," +
            "   \"seller\": { \"availableFromDay\": 2, \"askMultiplier\": 1.05, \"rejectRatio\": 0.82, \"patience\": 3, \"valueSigma\": 0.12, \"urgency\": 0.9, \"persuasion\": 0.9, \"learningFriendly\": true }," +
            "   \"customer\": { \"openingOfferRatio\": 0.92, \"valueRatio\": 1.00, \"patience\": 3 } }," +
            " { \"id\": \"npc.test_liar\", \"name\": \"Saklayan Test\", \"personality\": \"hurried_indebted\"," +
            "   \"seller\": { \"availableFromDay\": 3, \"askMultiplier\": 1.02, \"rejectRatio\": 0.78, \"patience\": 2, \"valueSigma\": 0.15, \"urgency\": 1.0, \"persuasion\": 0.9, \"concealChance\": 0.6, \"urgentLabelFromDay\": 6 }," +
            "   \"customer\": { \"openingOfferRatio\": 0.85, \"valueRatio\": 0.95, \"patience\": 3, \"valueSigma\": 0.30, \"packageRatio\": 1.12 } } ] }";

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

        /// <summary>Fixture NPC kimlikleri (manifest için).</summary>
        public static readonly string[] NpcIds = { "npc.test_honest", "npc.test_hurried", "npc.test_liar" };

        /// <summary>Verilen ürün kimliklerine fixture NPC kimliklerini ekleyerek manifest dosyası üretir.</summary>
        public static string ManifestWithNpcs(params string[] productIds)
        {
            var all = new List<string>(productIds);
            all.AddRange(NpcIds);
            return ManifestFile(all.ToArray());
        }

        /// <summary>İki geçerli model (phone.test_one, phone.test_two), eşleşen manifest ve geçerli çarpan/profil tabloları.</summary>
        public static DictionaryContentSource ValidSource()
        {
            return new DictionaryContentSource()
                .AddValidTables()
                .Add(ContentFileNames.PhoneModels, ModelsFile(ValidModelJson, ModelWithId("phone.test_two")))
                .Add(ContentFileNames.IdManifest, ManifestWithNpcs("phone.test_one", "phone.test_two"));
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

        /// <summary>Test JSON'undan defter türlerini ayrıştırır ve kayıt defterini kurar.</summary>
        public static TransactionTypes Types()
        {
            var issues = new List<ContentIssue>();
            IReadOnlyList<TransactionType> types = ContentParser.ParseTransactionTypes(ContentFileNames.TransactionTypes, TransactionTypesJson, issues);
            if (types == null || issues.Count > 0)
            {
                throw new System.InvalidOperationException("Fixture transaction types are invalid: " + string.Join("; ", issues));
            }

            return new TransactionTypes(types);
        }

        /// <summary>Test JSON'undan ekspertiz kurallarını ayrıştırır (sıfır sapma/gürültü).</summary>
        public static AppraisalConfig Appraisal()
        {
            var issues = new List<ContentIssue>();
            AppraisalConfig config = ContentParser.ParseAppraisal(ContentFileNames.AppraisalLevels, AppraisalLevelsJson, issues);
            if (config == null || issues.Count > 0)
            {
                throw new System.InvalidOperationException("Fixture appraisal levels are invalid: " + string.Join("; ", issues));
            }

            return config;
        }

        /// <summary>Test JSON'undan NPC tanımlarını ayrıştırır.</summary>
        public static IReadOnlyList<NpcDefinition> Npcs()
        {
            var issues = new List<ContentIssue>();
            IReadOnlyList<NpcDefinition> npcs = ContentParser.ParseNpcs(ContentFileNames.NpcProfiles, NpcProfilesJson, issues);
            if (npcs == null || issues.Count > 0)
            {
                throw new System.InvalidOperationException("Fixture npcs are invalid: " + string.Join("; ", issues));
            }

            return npcs;
        }

        /// <summary>Test JSON'undan pazar sabitlerini ayrıştırır.</summary>
        public static MarketConstants Market()
        {
            var issues = new List<ContentIssue>();
            MarketConstants market = ContentParser.ParseMarketConstants(ContentFileNames.EconomyConstants, EconomyConstantsJson, issues);
            if (market == null || issues.Count > 0)
            {
                throw new System.InvalidOperationException("Fixture market constants are invalid: " + string.Join("; ", issues));
            }

            return market;
        }

        /// <summary>Test JSON'undan ekonomi sabitlerini ayrıştırır.</summary>
        public static EconomyConstants Constants()
        {
            var issues = new List<ContentIssue>();
            EconomyConstants constants = ContentParser.ParseEconomyConstants(ContentFileNames.EconomyConstants, EconomyConstantsJson, issues);
            if (constants == null || issues.Count > 0)
            {
                throw new System.InvalidOperationException("Fixture economy constants are invalid: " + string.Join("; ", issues));
            }

            return constants;
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
        /// <summary>Kaynağa geçerli value_tables, condition_profiles, transaction_types, economy_constants ve npc_profiles ve appraisal_levels dosyalarını ekler.</summary>
        public static DictionaryContentSource AddValidTables(this DictionaryContentSource source)
        {
            return source
                .Add(ContentFileNames.ValueTables, ContentFixtures.ValueTablesJson)
                .Add(ContentFileNames.ConditionProfiles, ContentFixtures.ProfilesJson)
                .Add(ContentFileNames.TransactionTypes, ContentFixtures.TransactionTypesJson)
                .Add(ContentFileNames.EconomyConstants, ContentFixtures.EconomyConstantsJson)
                .Add(ContentFileNames.NpcProfiles, ContentFixtures.NpcProfilesJson)
                .Add(ContentFileNames.AppraisalLevels, ContentFixtures.AppraisalLevelsJson);
        }
    }
}
