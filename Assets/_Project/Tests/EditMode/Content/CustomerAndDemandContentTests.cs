using System.Collections.Generic;
using System.IO;
using System.Linq;
using Esnaf.Domain.Business;
using Esnaf.Domain.Content;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Npc;
using Esnaf.Domain.Products;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Content
{
    public class CustomerAndDemandContentTests
    {
        private const string File = "economy_constants.json";
        private static readonly string Good = ContentFixtures.EconomyConstantsJson;

        private static List<ContentIssue> ValidateCustomers(string json)
        {
            var issues = new List<ContentIssue>();
            CustomerConstants c = ContentParser.ParseCustomerConstants(File, json, issues);
            Assert.IsNotNull(c, string.Join("\n", issues));
            Assert.AreEqual(0, issues.Count, string.Join("\n", issues));
            ContentValidator.ValidateCustomers(c, File, issues);
            return issues;
        }

        private static List<ContentIssue> ValidateDemand(string json)
        {
            var issues = new List<ContentIssue>();
            DemandConstants d = ContentParser.ParseDemandConstants(File, json, issues);
            Assert.IsNotNull(d, string.Join("\n", issues));
            Assert.AreEqual(0, issues.Count, string.Join("\n", issues));
            ContentValidator.ValidateDemand(d, File, issues);
            return issues;
        }

        private static void AssertOne(List<ContentIssue> issues, string code, string field)
        {
            Assert.AreEqual(1, issues.Count, string.Join("\n", issues.Select(i => i.ToString())));
            Assert.AreEqual(code, issues[0].Code, issues[0].ToString());
            StringAssert.StartsWith(field + ":", issues[0].Message);
        }

        // ---------- ayrıştırma ----------

        [Test]
        public void Parse_Customers_ReadsEveryValue()
        {
            CustomerConstants c = ContentFixtures.Customers();

            Assert.AreEqual(0.05, c.ShopPremium, 1e-12);
            Assert.AreEqual(0.12, c.ShopPremiumCap, 1e-12);
            Assert.AreEqual(1.25, c.MaxRatioToTrueValue, 1e-12);
            Assert.AreEqual(2, c.CountBase);
            Assert.AreEqual(0.5, c.CountPerShelfItem, 1e-12);
            Assert.AreEqual(5, c.CountMax);
            CollectionAssert.AreEqual(new[] { "npc.test_liar" }, c.RichNpcIds.ToArray());
            Assert.AreEqual(1, c.RichMaxPerDay);
            Assert.IsTrue(c.IsRich("npc.test_liar"));
            Assert.IsFalse(c.IsRich("npc.test_honest"));
        }

        [Test]
        public void Parse_Demand_ReadsEveryValue()
        {
            DemandConstants d = ContentFixtures.Demand();

            Assert.AreEqual(5, d.LiveFromDay);
            Assert.AreEqual(0.02, d.DailyNoise, 1e-12);
            Assert.AreEqual(0.20, d.MeanReversion, 1e-12);
            Assert.AreEqual(0.90, d.Min, 1e-12);
            Assert.AreEqual(1.10, d.Max, 1e-12);
            Assert.AreEqual(0.985, d.PressurePerSale, 1e-12);
            Assert.AreEqual(5, d.PressureWindowDays);
            Assert.AreEqual(0.90, d.PressureFloor, 1e-12);
        }

        [TestCase(0, 2)]
        [TestCase(1, 3)]
        [TestCase(2, 3)]
        [TestCase(3, 4)]
        [TestCase(4, 4)]
        [TestCase(5, 5)]
        [TestCase(6, 5)]
        [TestCase(8, 5)]
        public void ArrivalCount_IsTwoPlusHalfTheShelfRoundedUp_CappedAtFive(int shelf, int expected)
        {
            Assert.AreEqual(expected, ContentFixtures.Customers().ArrivalCount(shelf));
        }

        private static readonly object[][] MissingCases =
        {
            new object[] { "customers", new string[0], "customers" },
            new object[] { "shopPremium", new[] { "\"customers\"" }, "customers.shopPremium" },
            new object[] { "shopPremiumCap", new[] { "\"customers\"" }, "customers.shopPremiumCap" },
            new object[] { "maxRatioToTrueValue", new[] { "\"customers\"" }, "customers.maxRatioToTrueValue" },
            new object[] { "count", new[] { "\"customers\"" }, "customers.count" },
            new object[] { "base", new[] { "\"customers\"", "\"count\"" }, "customers.count.base" },
            new object[] { "perShelfItem", new[] { "\"customers\"", "\"count\"" }, "customers.count.perShelfItem" },
            new object[] { "max", new[] { "\"customers\"", "\"count\"" }, "customers.count.max" },
            new object[] { "richQuota", new[] { "\"customers\"" }, "customers.richQuota" },
            new object[] { "npcIds", new[] { "\"customers\"", "\"richQuota\"" }, "customers.richQuota.npcIds" },
            new object[] { "maxPerDay", new[] { "\"customers\"", "\"richQuota\"" }, "customers.richQuota.maxPerDay" }
        };

        [TestCaseSource(nameof(MissingCases))]
        public void Parse_Customers_MissingField_IsReported(string property, string[] path, string expected)
        {
            var issues = new List<ContentIssue>();

            CustomerConstants c = ContentParser.ParseCustomerConstants(File, JsonEdit.Missing(Good, property, path), issues);

            Assert.IsNull(c);
            Assert.AreEqual(1, issues.Count, string.Join("\n", issues));
            Assert.AreEqual(ContentIssueCodes.FieldMissing, issues[0].Code);
            StringAssert.Contains(expected, issues[0].Message);
        }

        private static readonly object[][] DemandMissingCases =
        {
            new object[] { "demand", new string[0], "demand" },
            new object[] { "liveFromDay", new[] { "\"demand\"" }, "demand.liveFromDay" },
            new object[] { "dailyNoise", new[] { "\"demand\"" }, "demand.dailyNoise" },
            new object[] { "meanReversion", new[] { "\"demand\"" }, "demand.meanReversion" },
            new object[] { "min", new[] { "\"demand\"" }, "demand.min" },
            new object[] { "max", new[] { "\"demand\"" }, "demand.max" },
            new object[] { "salesPressure", new[] { "\"demand\"" }, "demand.salesPressure" },
            new object[] { "perSale", new[] { "\"demand\"", "\"salesPressure\"" }, "demand.salesPressure.perSale" },
            new object[] { "windowDays", new[] { "\"demand\"", "\"salesPressure\"" }, "demand.salesPressure.windowDays" },
            new object[] { "floor", new[] { "\"demand\"", "\"salesPressure\"" }, "demand.salesPressure.floor" }
        };

        [TestCaseSource(nameof(DemandMissingCases))]
        public void Parse_Demand_MissingField_IsReported(string property, string[] path, string expected)
        {
            var issues = new List<ContentIssue>();

            DemandConstants d = ContentParser.ParseDemandConstants(File, JsonEdit.Missing(Good, property, path), issues);

            Assert.IsNull(d);
            Assert.AreEqual(1, issues.Count, string.Join("\n", issues));
            Assert.AreEqual(ContentIssueCodes.FieldMissing, issues[0].Code);
            StringAssert.Contains(expected, issues[0].Message);
        }

        // ---------- doğrulama: müşteriler ----------

        [Test]
        public void Validate_Fixture_IsClean()
        {
            Assert.AreEqual(0, ValidateCustomers(Good).Count);
            Assert.AreEqual(0, ValidateDemand(Good).Count);
        }

        private static readonly object[][] CustomerInvalid =
        {
            new object[] { "shopPremium", "-0.01", "customers.shopPremium", new[] { "\"customers\"" } },
            new object[] { "shopPremium", "0.13", "customers.shopPremium", new[] { "\"customers\"" } },
            new object[] { "shopPremiumCap", "0.51", "customers.shopPremiumCap", new[] { "\"customers\"" } },
            new object[] { "maxRatioToTrueValue", "0.99", "customers.maxRatioToTrueValue", new[] { "\"customers\"" } },
            new object[] { "maxRatioToTrueValue", "3.01", "customers.maxRatioToTrueValue", new[] { "\"customers\"" } },
            new object[] { "base", "-1", "customers.count.base", new[] { "\"customers\"", "\"count\"" } },
            new object[] { "base", "6", "customers.count.base", new[] { "\"customers\"", "\"count\"" } },
            new object[] { "perShelfItem", "-0.1", "customers.count.perShelfItem", new[] { "\"customers\"", "\"count\"" } },
            new object[] { "max", "0", "customers.count.max", new[] { "\"customers\"", "\"count\"" } },
            new object[] { "maxPerDay", "-1", "customers.richQuota.maxPerDay", new[] { "\"customers\"", "\"richQuota\"" } },
            new object[] { "npcIds", "[ \"npc.a\", \"npc.a\" ]", "customers.richQuota.npcIds", new[] { "\"customers\"", "\"richQuota\"" } },
            new object[] { "npcIds", "[ \"\" ]", "customers.richQuota.npcIds", new[] { "\"customers\"", "\"richQuota\"" } }
        };

        [TestCaseSource(nameof(CustomerInvalid))]
        public void Validate_Customers_InvalidValue_IsReported(string property, string value, string field, string[] path)
        {
            AssertOne(ValidateCustomers(JsonEdit.Set(Good, property, value, path)), ContentIssueCodes.CustomerFieldInvalid, field);
        }

        [Test]
        public void Validate_Customers_PremiumMayNotExceedItsCap()
        {
            string json = JsonEdit.Set(JsonEdit.Set(Good, "shopPremium", "0.08", "\"customers\""), "shopPremiumCap", "0.07", "\"customers\"");

            AssertOne(ValidateCustomers(json), ContentIssueCodes.CustomerFieldInvalid, "customers.shopPremium");
        }

        [TestCase("shopPremium", "0.0", "0.12")]
        [TestCase("shopPremium", "0.12", "0.12")]
        [TestCase("shopPremiumCap", "0.5", "0.5")]
        public void Validate_Customers_PremiumBoundaries_AreAccepted(string property, string value, string cap)
        {
            string json = JsonEdit.Set(JsonEdit.Set(Good, property, value, "\"customers\""), "shopPremiumCap", cap, "\"customers\"");

            Assert.AreEqual(0, ValidateCustomers(json).Count);
        }

        [TestCase("maxRatioToTrueValue", "1.0")]
        [TestCase("maxRatioToTrueValue", "3.0")]
        public void Validate_Customers_RatioBoundaries_AreAccepted(string property, string value)
        {
            Assert.AreEqual(0, ValidateCustomers(JsonEdit.Set(Good, property, value, "\"customers\"")).Count);
        }

        [Test]
        public void Validate_Customers_CountBoundaries_AreAccepted()
        {
            string anchor = "\"count\"";
            Assert.AreEqual(0, ValidateCustomers(JsonEdit.Set(Good, "base", "0", "\"customers\"", anchor)).Count);
            Assert.AreEqual(0, ValidateCustomers(JsonEdit.Set(Good, "base", "5", "\"customers\"", anchor)).Count);
            Assert.AreEqual(0, ValidateCustomers(JsonEdit.Set(Good, "perShelfItem", "0", "\"customers\"", anchor)).Count);
            Assert.AreEqual(0, ValidateCustomers(JsonEdit.Set(JsonEdit.Set(Good, "max", "1", "\"customers\"", anchor), "base", "1", "\"customers\"", anchor)).Count);
            Assert.AreEqual(0, ValidateCustomers(JsonEdit.Set(Good, "maxPerDay", "0", "\"customers\"", "\"richQuota\"")).Count);
            Assert.AreEqual(0, ValidateCustomers(JsonEdit.Set(Good, "npcIds", "[]", "\"customers\"", "\"richQuota\"")).Count);
        }


        [Test]
        public void Validate_Customers_ZeroPremiumWithAZeroCap_IsAccepted()
        {
            string json = JsonEdit.Set(JsonEdit.Set(Good, "shopPremium", "0.0", "\"customers\""), "shopPremiumCap", "0.0", "\"customers\"");

            Assert.AreEqual(0, ValidateCustomers(json).Count);
        }

        [Test]
        public void Validate_Customers_BaseAboveAMaxOfOne_IsReported()
        {
            string json = JsonEdit.Set(JsonEdit.Set(Good, "max", "1", "\"customers\"", "\"count\""), "base", "2", "\"customers\"", "\"count\"");

            AssertOne(ValidateCustomers(json), ContentIssueCodes.CustomerFieldInvalid, "customers.count.base");
        }

        // ---------- doğrulama: talep ----------

        private static readonly object[][] DemandInvalid =
        {
            new object[] { "liveFromDay", "0", "demand.liveFromDay", new[] { "\"demand\"" } },
            new object[] { "dailyNoise", "-0.01", "demand.dailyNoise", new[] { "\"demand\"" } },
            new object[] { "dailyNoise", "0.5", "demand.dailyNoise", new[] { "\"demand\"" } },
            new object[] { "meanReversion", "-0.1", "demand.meanReversion", new[] { "\"demand\"" } },
            new object[] { "meanReversion", "1.01", "demand.meanReversion", new[] { "\"demand\"" } },
            new object[] { "min", "0", "demand.min", new[] { "\"demand\"" } },
            new object[] { "min", "1.01", "demand.min", new[] { "\"demand\"" } },
            new object[] { "max", "0.99", "demand.max", new[] { "\"demand\"" } },
            new object[] { "perSale", "0", "demand.salesPressure.perSale", new[] { "\"demand\"", "\"salesPressure\"" } },
            new object[] { "perSale", "1.01", "demand.salesPressure.perSale", new[] { "\"demand\"", "\"salesPressure\"" } },
            new object[] { "windowDays", "0", "demand.salesPressure.windowDays", new[] { "\"demand\"", "\"salesPressure\"" } },
            new object[] { "floor", "0", "demand.salesPressure.floor", new[] { "\"demand\"", "\"salesPressure\"" } },
            new object[] { "floor", "1.01", "demand.salesPressure.floor", new[] { "\"demand\"", "\"salesPressure\"" } }
        };

        [TestCaseSource(nameof(DemandInvalid))]
        public void Validate_Demand_InvalidValue_IsReported(string property, string value, string field, string[] path)
        {
            AssertOne(ValidateDemand(JsonEdit.Set(Good, property, value, path)), ContentIssueCodes.DemandFieldInvalid, field);
        }

        [TestCase("liveFromDay", "1")]
        [TestCase("dailyNoise", "0.0")]
        [TestCase("meanReversion", "0.0")]
        [TestCase("meanReversion", "1.0")]
        [TestCase("min", "1.0")]
        [TestCase("max", "1.0")]
        public void Validate_Demand_BoundaryValues_AreAccepted(string property, string value)
        {
            // min 1.0 ile max 1.10, max 1.0 ile min 0.90 geçerli; her ikisi 1.0 olabilir (talep sabit).
            Assert.AreEqual(0, ValidateDemand(JsonEdit.Set(Good, property, value, "\"demand\"")).Count);
        }

        [TestCase("perSale", "1.0")]
        [TestCase("windowDays", "1")]
        [TestCase("floor", "1.0")]
        public void Validate_Demand_PressureBoundaries_AreAccepted(string property, string value)
        {
            Assert.AreEqual(0, ValidateDemand(JsonEdit.Set(Good, property, value, "\"demand\"", "\"salesPressure\"")).Count);
        }

        // ---------- gerçek dosya ve veritabanı ----------

        [Test]
        public void RealFile_ParsesAndValidates_WithTheGddNumbers()
        {
            string text = System.IO.File.ReadAllText(Path.Combine(TestPaths.ContentDataDirectory(), File));
            var issues = new List<ContentIssue>();

            CustomerConstants c = ContentParser.ParseCustomerConstants(File, text, issues);
            DemandConstants d = ContentParser.ParseDemandConstants(File, text, issues);
            ContentValidator.ValidateCustomers(c, File, issues);
            ContentValidator.ValidateDemand(d, File, issues);

            Assert.AreEqual(0, issues.Count, string.Join("\n", issues));
            Assert.AreEqual(0.05, c.ShopPremium);
            Assert.AreEqual(0.12, c.ShopPremiumCap);
            Assert.AreEqual(1.25, c.MaxRatioToTrueValue);
            Assert.AreEqual(2, c.CountBase);
            Assert.AreEqual(0.5, c.CountPerShelfItem);
            Assert.AreEqual(5, c.CountMax);
            CollectionAssert.AreEqual(new[] { "npc.berk", "npc.nermin" }, c.RichNpcIds.ToArray());
            Assert.AreEqual(1, c.RichMaxPerDay);
            Assert.AreEqual(5, d.LiveFromDay);
            Assert.AreEqual(0.02, d.DailyNoise);
            Assert.AreEqual(0.20, d.MeanReversion);
            Assert.AreEqual(0.90, d.Min);
            Assert.AreEqual(1.10, d.Max);
            Assert.AreEqual(0.985, d.PressurePerSale);
            Assert.AreEqual(5, d.PressureWindowDays);
            Assert.AreEqual(0.90, d.PressureFloor);
        }

        [Test]
        public void Database_ExposesTheConstants_AndRejectsUnknownRichNpcs()
        {
            ContentLoadResult ok = ContentDatabase.Load(ContentFixtures.ValidSource());
            Assert.IsNotNull(ok.Database, string.Join("\n", ok.Issues));
            Assert.AreEqual(5, ok.Database.Customers.CountMax);
            Assert.AreEqual(5, ok.Database.Demand.LiveFromDay);

            DictionaryContentSource bad = ContentFixtures.ValidSource();
            bad.Add(ContentFileNames.EconomyConstants, Good.Replace("npc.test_liar", "npc.nobody"));
            ContentLoadResult result = ContentDatabase.Load(bad);

            Assert.IsNull(result.Database);
            Assert.IsTrue(result.Issues.Any(i => i.Code == ContentIssueCodes.CustomerFieldInvalid && i.Message.Contains("npc.nobody")));
        }

        [Test]
        public void Database_NeedsAtLeastOneCustomerOnDayOne()
        {
            string npcs = ContentFixtures.NpcProfilesJson.Replace("\"customer\": { \"openingOfferRatio\"", "\"customer\": { \"availableFromDay\": 2, \"openingOfferRatio\"");
            DictionaryContentSource source = ContentFixtures.ValidSource();
            source.Add(ContentFileNames.NpcProfiles, npcs);

            ContentLoadResult result = ContentDatabase.Load(source);

            Assert.IsNull(result.Database);
            Assert.IsTrue(result.Issues.Any(i => i.Code == ContentIssueCodes.CustomerFieldInvalid && i.Message.Contains("day 1")), string.Join("\n", result.Issues));
        }

        // ---------- NPC müşteri alanları ----------

        [Test]
        public void Npc_CustomerFields_AreParsed_AndDefaultWhenAbsent()
        {
            IReadOnlyList<NpcDefinition> npcs = ContentFixtures.Npcs();

            NpcCustomerRole liar = npcs.Single(n => n.Id == "npc.test_liar").Customer;
            Assert.AreEqual(3, liar.AvailableFromDay);
            CollectionAssert.AreEqual(new[] { ProductSegment.Entry, ProductSegment.Mid }, liar.Segments.ToArray());
            Assert.AreEqual(15, liar.ReportTrustGain);
            Assert.IsTrue(liar.AcceptsSegment(ProductSegment.Mid));
            Assert.IsFalse(liar.AcceptsSegment(ProductSegment.Upper));

            NpcCustomerRole honest = npcs.Single(n => n.Id == "npc.test_honest").Customer;
            Assert.AreEqual(1, honest.AvailableFromDay);
            Assert.AreEqual(0, honest.Segments.Count);
            Assert.IsNull(honest.ReportTrustGain);
            Assert.IsTrue(honest.AcceptsSegment(ProductSegment.Upper), "segment listesi boşsa hepsi kabul");
        }

        [Test]
        public void Npc_RealData_HasTheGddCustomerRules()
        {
            ContentDatabase content = MarketHarness.RealContent();

            Assert.AreEqual(1, content.GetNpc("npc.kemal").Customer.AvailableFromDay);
            Assert.AreEqual(1, content.GetNpc("npc.selin").Customer.AvailableFromDay);
            Assert.AreEqual(4, content.GetNpc("npc.riza").Customer.AvailableFromDay);
            Assert.AreEqual(4, content.GetNpc("npc.murat").Customer.AvailableFromDay);
            Assert.AreEqual(6, content.GetNpc("npc.berk").Customer.AvailableFromDay);
            Assert.AreEqual(6, content.GetNpc("npc.nermin").Customer.AvailableFromDay);
            Assert.AreEqual(15, content.GetNpc("npc.riza").Customer.ReportTrustGain);
            CollectionAssert.AreEqual(new[] { ProductSegment.Entry, ProductSegment.Mid }, content.GetNpc("npc.cengiz").Customer.Segments.ToArray());
            foreach (NpcDefinition npc in content.Npcs.Where(n => n.Id != "npc.riza"))
            {
                Assert.IsNull(npc.Customer.ReportTrustGain, npc.Id);
            }
        }

        [TestCase("\"availableFromDay\": 3, \"segments\": [ \"entry\", \"mid\" ]", "\"availableFromDay\": 0, \"segments\": [ \"entry\", \"mid\" ]", "customer.availableFromDay")]
        [TestCase("\"reportTrustGain\": 15", "\"reportTrustGain\": 101", "customer.reportTrustGain")]
        [TestCase("\"reportTrustGain\": 15", "\"reportTrustGain\": -1", "customer.reportTrustGain")]
        [TestCase("\"segments\": [ \"entry\", \"mid\" ]", "\"segments\": [ \"entry\", \"entry\" ]", "customer.segments")]
        public void Npc_CustomerFields_InvalidValues_AreReported(string find, string replacement, string field)
        {
            string json = ContentFixtures.NpcProfilesJson.Replace(find, replacement);
            Assert.AreNotEqual(ContentFixtures.NpcProfilesJson, json, "Test verisi bulunamadı: " + find);
            var issues = new List<ContentIssue>();

            IReadOnlyList<NpcDefinition> npcs = ContentParser.ParseNpcs("npc_profiles.json", json, issues);
            ContentValidator.ValidateNpcs(npcs, "npc_profiles.json", issues);

            Assert.IsTrue(issues.Any(i => i.Message.Contains(field)), string.Join("\n", issues));
        }

        [TestCase("0")]
        [TestCase("100")]
        public void Npc_ReportTrustGain_BoundariesAreAccepted(string value)
        {
            string json = ContentFixtures.NpcProfilesJson.Replace("\"reportTrustGain\": 15", "\"reportTrustGain\": " + value);
            var issues = new List<ContentIssue>();

            IReadOnlyList<NpcDefinition> npcs = ContentParser.ParseNpcs("npc_profiles.json", json, issues);
            ContentValidator.ValidateNpcs(npcs, "npc_profiles.json", issues);

            Assert.AreEqual(0, issues.Count, string.Join("\n", issues));
        }

        [Test]
        public void Npc_UnknownSegmentName_IsAParseIssue()
        {
            string json = ContentFixtures.NpcProfilesJson.Replace("\"segments\": [ \"entry\", \"mid\" ]", "\"segments\": [ \"luxury\" ]");
            var issues = new List<ContentIssue>();

            IReadOnlyList<NpcDefinition> npcs = ContentParser.ParseNpcs("npc_profiles.json", json, issues);

            Assert.IsNull(npcs);
            Assert.IsTrue(issues.Any(i => i.Message.Contains("luxury")));
        }
    }
}
