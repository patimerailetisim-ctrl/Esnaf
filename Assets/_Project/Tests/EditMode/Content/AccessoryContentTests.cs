using System.Collections.Generic;
using System.IO;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Accessories;
using Esnaf.Domain.Content;
using Esnaf.Domain.Wholesale;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Content
{
    /// <summary>Sıfır aksesuar (accessories.json) ve toptancı (wholesale.json) içeriği: ayrıştırma, doğrulama, gerçek veri, paralel model.</summary>
    public class AccessoryContentTests
    {
        private const string A = "accessories.json";
        private const string W = "wholesale.json";

        private static string Acc(string id = "accessory.phone_case", string name = "Kılıf", string category = "protection", string condition = "new", string price = "160", string icon = "phone_case")
        {
            return "{ \"id\": \"" + id + "\", \"name\": \"" + name + "\", \"category\": \"" + category + "\", \"condition\": \"" + condition +
                   "\", \"retailPrice\": " + price + ", \"iconKey\": \"" + icon + "\" }";
        }

        private static string AccFile(string capacity, params string[] items)
        {
            return "{ \"schemaVersion\": 1," + (capacity == null ? string.Empty : " \"shelfCapacityUnits\": " + capacity + ",") + " \"accessories\": [" + string.Join(",", items) + "] }";
        }

        private static string Offer(string accessoryId = "accessory.phone_case", string cost = "70", string pack = "20", string day = "1")
        {
            return "{ \"accessoryId\": \"" + accessoryId + "\", \"unitCost\": " + cost + ", \"packSize\": " + pack + ", \"availableFromDay\": " + day + " }";
        }

        private static string WholesaleFile(string supplierId, string supplierName, params string[] offers)
        {
            return "{ \"schemaVersion\": 1, \"suppliers\": [ { \"id\": \"" + supplierId + "\", \"name\": \"" + supplierName + "\", \"offers\": [" + string.Join(",", offers) + "] } ] }";
        }

        private static List<ContentIssue> ValidateAccessories(string json, out AccessoryCatalog catalog)
        {
            var issues = new List<ContentIssue>();
            catalog = ContentParser.ParseAccessories(A, json, issues);
            Assert.IsNotNull(catalog, string.Join("\n", issues));
            ContentValidator.ValidateAccessories(catalog, A, issues);
            return issues;
        }

        private static List<ContentIssue> ValidateWholesale(string accessoriesJson, string wholesaleJson)
        {
            var issues = new List<ContentIssue>();
            AccessoryCatalog accessories = ContentParser.ParseAccessories(A, accessoriesJson, issues);
            WholesaleCatalog wholesale = ContentParser.ParseWholesale(W, wholesaleJson, issues);
            Assert.IsNotNull(accessories);
            Assert.IsNotNull(wholesale, string.Join("\n", issues));
            Assert.AreEqual(0, issues.Count, string.Join("\n", issues));
            ContentValidator.ValidateWholesale(wholesale, accessories, W, issues);
            return issues;
        }

        private static void AssertOne(List<ContentIssue> issues, string code, string text)
        {
            Assert.AreEqual(1, issues.Count, string.Join("\n", issues.Select(i => i.ToString())));
            Assert.AreEqual(code, issues[0].Code);
            StringAssert.Contains(text, issues[0].Message);
        }

        // ---------- gerçek içerik ----------

        [Test]
        public void TheRealContent_HasTheSixAccessories_WithTheDesignedRetailPrices()
        {
            ContentDatabase content = MarketHarness.RealContent();
            AccessoryCatalog catalog = content.Accessories;

            Assert.AreEqual(6, catalog.Definitions.Count);
            CollectionAssert.AreEqual(
                new[] { "Şarj Adaptörü", "Şarj Kablosu", "Kulaklık", "Kırılmaz Cam", "Telefon Kılıfı", "Powerbank" },
                catalog.Definitions.Select(d => d.Name).ToArray());
            CollectionAssert.AreEqual(new long[] { 250, 120, 380, 120, 160, 600 }, catalog.Definitions.Select(d => d.RetailPrice.Tl).ToArray());
            Assert.AreEqual(60, catalog.ShelfCapacityUnits);
        }

        [Test]
        public void TheRealAccessories_HaveUniqueIds_PositivePrices_AndAreAlwaysNew()
        {
            AccessoryCatalog catalog = MarketHarness.RealContent().Accessories;

            Assert.AreEqual(catalog.Definitions.Count, catalog.Definitions.Select(d => d.Id).Distinct().Count());
            Assert.IsTrue(catalog.Definitions.All(d => d.RetailPrice.Tl > 0));
            Assert.IsTrue(catalog.Definitions.All(d => d.Condition == "new"));
            Assert.IsTrue(catalog.Definitions.All(d => d.Id.StartsWith("accessory.")));
        }

        [Test]
        public void TheRealWholesale_ReferencesOnlyDefinedAccessories_WithTheDesignedCostsAndPacks()
        {
            ContentDatabase content = MarketHarness.RealContent();
            IReadOnlyList<WholesaleOffer> offers = content.Wholesale.Offers;

            Assert.AreEqual(6, offers.Count);
            Assert.IsTrue(offers.All(o => { AccessoryDefinition d; return content.Accessories.TryGet(o.AccessoryId, out d); }));
            CollectionAssert.AreEqual(new long[] { 150, 60, 200, 40, 70, 350 }, offers.Select(o => o.UnitCost.Tl).ToArray());
            CollectionAssert.AreEqual(new[] { 10, 20, 10, 20, 20, 5 }, offers.Select(o => o.PackSize).ToArray());
            Assert.IsTrue(offers.All(o => o.PackSize > 0 && o.UnitCost.Tl > 0 && o.AvailableFromDay >= 1));
            CollectionAssert.AreEqual(new[] { 1, 1, 1, 1, 1, 3 }, offers.Select(o => o.AvailableFromDay).ToArray());
            Assert.IsTrue(offers.All(o => o.SupplierId == "supplier.ucuz_toptan" && o.SupplierName == "Ucuz Toptan"), "tek toptancı");
        }

        [Test]
        public void ThePurchaseCost_IsSeparateFromTheRetailPrice_AndEveryPackCostsUnitCostTimesPack()
        {
            ContentDatabase content = MarketHarness.RealContent();

            foreach (WholesaleOffer offer in content.Wholesale.Offers)
            {
                AccessoryDefinition definition;
                Assert.IsTrue(content.Accessories.TryGet(offer.AccessoryId, out definition));
                Assert.AreNotEqual(definition.RetailPrice, offer.UnitCost, offer.AccessoryId);
                Assert.AreEqual(offer.UnitCost.Tl * offer.PackSize, offer.PackCost.Tl);
            }
        }

        [Test]
        public void Wholesale_AvailableOn_FollowsTheOpeningDay()
        {
            WholesaleCatalog wholesale = MarketHarness.RealContent().Wholesale;

            Assert.AreEqual(5, wholesale.AvailableOn(1).Count);
            Assert.AreEqual(5, wholesale.AvailableOn(2).Count);
            Assert.AreEqual(6, wholesale.AvailableOn(3).Count);
            Assert.IsFalse(wholesale.AvailableOn(2).Any(o => o.AccessoryId == "accessory.powerbank"));
        }

        [Test]
        public void TheAccessoryIds_AreRegisteredInTheManifest_AndThePhonesAreUntouched()
        {
            ContentDatabase content = MarketHarness.RealContent();
            string manifest = File.ReadAllText(Path.Combine(TestPaths.ContentDataDirectory(), "content_id_manifest.json"));

            foreach (AccessoryDefinition a in content.Accessories.Definitions)
            {
                StringAssert.Contains("\"" + a.Id + "\"", manifest);
            }

            StringAssert.Contains("supplier.ucuz_toptan", manifest);
            Assert.AreEqual(10, content.Products.Count, "telefon modelleri aynen 10");
            Assert.IsTrue(content.Products.All(p => p.Sector == "phone"), "aksesuarlar ProductDefinition'a girmez");
        }

        // ---------- dosyalar isteğe bağlı ----------

        [Test]
        public void WithoutTheFiles_TheCatalogsAreEmpty_AndTheDatabaseStillLoads()
        {
            ContentLoadResult result = ContentDatabase.Load(ContentFixtures.ValidSource());

            Assert.IsTrue(result.IsSuccess, result.FormatIssues());
            Assert.IsTrue(result.Database.Accessories.IsEmpty);
            Assert.IsTrue(result.Database.Wholesale.IsEmpty);
        }

        [Test]
        public void TheDatabase_LoadsAccessoryFiles_WhenTheyAreRegisteredInTheManifest()
        {
            DictionaryContentSource source = ContentFixtures.ValidSource()
                .Add(ContentFileNames.Accessories, AccFile("60", Acc()))
                .Add(ContentFileNames.Wholesale, WholesaleFile("supplier.test", "Test", Offer()))
                .Add(ContentFileNames.IdManifest, ContentFixtures.ManifestWithNpcs("phone.test_one", "phone.test_two", "accessory.phone_case", "supplier.test"));

            ContentLoadResult result = ContentDatabase.Load(source);

            Assert.IsTrue(result.IsSuccess, result.FormatIssues());
            Assert.AreEqual(1, result.Database.Accessories.Definitions.Count);
            Assert.AreEqual(1, result.Database.Wholesale.Offers.Count);
        }

        [Test]
        public void ANewAccessoryId_MustBeRegisteredInTheManifest()
        {
            DictionaryContentSource source = ContentFixtures.ValidSource().Add(ContentFileNames.Accessories, AccFile("60", Acc()));

            ContentLoadResult result = ContentDatabase.Load(source);

            Assert.IsTrue(result.Issues.Any(i => i.Code == ContentIssueCodes.ManifestIdNotRegistered && i.Message.Contains("accessory.phone_case")));
        }

        [Test]
        public void AWholesaleFile_WithoutAnAccessoriesFile_ReportsTheMissingReference()
        {
            DictionaryContentSource source = ContentFixtures.ValidSource().Add(ContentFileNames.Wholesale, WholesaleFile("supplier.test", "Test", Offer()));

            ContentLoadResult result = ContentDatabase.Load(source);

            Assert.IsTrue(result.Issues.Any(i => i.Code == ContentIssueCodes.WholesaleReferenceMissing));
        }

        // ---------- ayrıştırma ----------

        [Test]
        public void Parse_ReadsAllFields()
        {
            AccessoryCatalog catalog;
            Assert.AreEqual(0, ValidateAccessories(AccFile("60", Acc("accessory.earphones", "Kulaklık", "audio", "new", "380", "earphones")), out catalog).Count);

            AccessoryDefinition d = catalog.Definitions[0];
            Assert.AreEqual("accessory.earphones", d.Id);
            Assert.AreEqual("Kulaklık", d.Name);
            Assert.AreEqual("audio", d.Category);
            Assert.AreEqual("new", d.Condition);
            Assert.AreEqual(Money.FromTl(380), d.RetailPrice);
            Assert.AreEqual("earphones", d.IconKey);
            Assert.IsTrue(catalog.TryGet("accessory.earphones", out d));
            Assert.IsFalse(catalog.TryGet("accessory.none", out d));
            Assert.IsFalse(catalog.TryGet(null, out d));
        }

        [Test]
        public void Parse_AMissingField_IsAnError()
        {
            var issues = new List<ContentIssue>();

            Assert.IsNull(ContentParser.ParseAccessories(A, AccFile("60", "{ \"id\": \"accessory.x\", \"name\": \"X\" }"), issues));
            Assert.IsNull(ContentParser.ParseAccessories(A, AccFile(null, Acc()), new List<ContentIssue>()));
            Assert.IsTrue(issues.Any(i => i.Message.Contains("category")));
        }

        [Test]
        public void Parse_AnUnknownField_IsAnError()
        {
            var issues = new List<ContentIssue>();

            Assert.IsNull(ContentParser.ParseAccessories(A, AccFile("60", Acc().Replace("\"iconKey\"", "\"icon\"")), issues));
            Assert.GreaterOrEqual(issues.Count, 1);
        }

        [Test]
        public void ParseWholesale_AMissingOfferField_IsAnError()
        {
            var issues = new List<ContentIssue>();

            Assert.IsNull(ContentParser.ParseWholesale(W, WholesaleFile("supplier.t", "T", "{ \"accessoryId\": \"accessory.x\", \"unitCost\": 10 }"), issues));
            Assert.IsTrue(issues.Any(i => i.Message.Contains("packSize")));
        }

        // ---------- doğrulama ----------

        [Test]
        public void Validate_ACoherentCatalog_HasNoIssue()
        {
            AccessoryCatalog catalog;
            Assert.AreEqual(0, ValidateAccessories(AccFile("60", Acc(), Acc("accessory.cable", "Kablo")), out catalog).Count);
            Assert.AreEqual(0, ValidateWholesale(AccFile("60", Acc()), WholesaleFile("supplier.t", "T", Offer())).Count);
        }

        [TestCase("Accessory.phone_case")]
        [TestCase("phone.case")]
        [TestCase("accessory.")]
        [TestCase("accessory.Case")]
        public void Validate_AnAccessoryIdMustLookLikeOne(string id)
        {
            AccessoryCatalog catalog;
            AssertOne(ValidateAccessories(AccFile("60", Acc(id)), out catalog), ContentIssueCodes.AccessoryIdFormat, "accessory.");
        }

        [Test]
        public void Validate_ADuplicateAccessoryId_IsAnError()
        {
            AccessoryCatalog catalog;
            AssertOne(ValidateAccessories(AccFile("60", Acc(), Acc()), out catalog), ContentIssueCodes.AccessoryIdDuplicate, "accessory.phone_case");
        }

        [TestCase("used")]
        [TestCase("New")]
        [TestCase("")]
        public void Validate_TheConditionMustBeNew(string condition)
        {
            AccessoryCatalog catalog;
            AssertOne(ValidateAccessories(AccFile("60", Acc(condition: condition)), out catalog), ContentIssueCodes.AccessoryFieldInvalid, "condition");
        }

        [TestCase("0")]
        [TestCase("-120")]
        public void Validate_TheRetailPriceMustBePositive(string price)
        {
            AccessoryCatalog catalog;
            AssertOne(ValidateAccessories(AccFile("60", Acc(price: price)), out catalog), ContentIssueCodes.AccessoryFieldInvalid, "retailPrice");
        }

        [TestCase("name")]
        [TestCase("category")]
        [TestCase("iconKey")]
        public void Validate_TextFieldsMustNotBeEmpty(string field)
        {
            AccessoryCatalog catalog;
            string json = AccFile("60", Acc(
                name: field == "name" ? " " : "Kılıf", category: field == "category" ? " " : "protection", icon: field == "iconKey" ? " " : "phone_case"));

            AssertOne(ValidateAccessories(json, out catalog), ContentIssueCodes.AccessoryFieldInvalid, field);
        }

        [TestCase("0")]
        [TestCase("-5")]
        public void Validate_TheShelfCapacityMustBePositive(string capacity)
        {
            AccessoryCatalog catalog;
            AssertOne(ValidateAccessories(AccFile(capacity, Acc()), out catalog), ContentIssueCodes.AccessoryFieldInvalid, "shelfCapacityUnits");
        }

        [Test]
        public void Validate_PricesAndCostsNeedNotBeMultiplesOfTen()
        {
            AccessoryCatalog catalog;

            Assert.AreEqual(0, ValidateAccessories(AccFile("60", Acc(price: "129")), out catalog).Count);
            Assert.AreEqual(0, ValidateWholesale(AccFile("60", Acc(price: "129")), WholesaleFile("supplier.t", "T", Offer(cost: "47"))).Count);
        }

        [Test]
        public void Validate_AnEmptyAccessoryList_IsAnError()
        {
            AccessoryCatalog catalog;
            AssertOne(ValidateAccessories(AccFile("60"), out catalog), ContentIssueCodes.AccessoryListEmpty, "no accessories");
        }

        [Test]
        public void Validate_AWholesaleReferenceToAnUnknownAccessory_IsAnError()
        {
            AssertOne(
                ValidateWholesale(AccFile("60", Acc()), WholesaleFile("supplier.t", "T", Offer("accessory.ghost"))),
                ContentIssueCodes.WholesaleReferenceMissing, "accessory.ghost");
        }

        [Test]
        public void Validate_TheSameSupplierOfferingAnAccessoryTwice_IsAnError()
        {
            AssertOne(
                ValidateWholesale(AccFile("60", Acc()), WholesaleFile("supplier.t", "T", Offer(), Offer())),
                ContentIssueCodes.WholesaleOfferDuplicate, "already offers");
        }

        [TestCase("0")]
        [TestCase("-10")]
        public void Validate_TheUnitCostMustBePositive(string cost)
        {
            AssertOne(
                ValidateWholesale(AccFile("60", Acc()), WholesaleFile("supplier.t", "T", Offer(cost: cost))),
                ContentIssueCodes.WholesaleFieldInvalid, "unitCost");
        }

        [TestCase("0")]
        [TestCase("-1")]
        public void Validate_ThePackSizeMustBePositive(string pack)
        {
            AssertOne(
                ValidateWholesale(AccFile("60", Acc()), WholesaleFile("supplier.t", "T", Offer(pack: pack))),
                ContentIssueCodes.WholesaleFieldInvalid, "packSize");
        }

        [TestCase("0")]
        [TestCase("-3")]
        public void Validate_TheAvailableFromDayMustBeOneOrLater(string day)
        {
            AssertOne(
                ValidateWholesale(AccFile("60", Acc()), WholesaleFile("supplier.t", "T", Offer(day: day))),
                ContentIssueCodes.WholesaleFieldInvalid, "availableFromDay");
        }

        [TestCase("Supplier.t")]
        [TestCase("ucuz")]
        [TestCase("supplier.")]
        public void Validate_ASupplierIdMustLookLikeOne(string id)
        {
            AssertOne(
                ValidateWholesale(AccFile("60", Acc()), WholesaleFile(id, "T", Offer())),
                ContentIssueCodes.WholesaleIdFormat, "supplier.");
        }

        [Test]
        public void Validate_ASupplierNameMustNotBeEmpty()
        {
            AssertOne(
                ValidateWholesale(AccFile("60", Acc()), WholesaleFile("supplier.t", " ", Offer())),
                ContentIssueCodes.WholesaleFieldInvalid, "supplier name");
        }
    }
}
