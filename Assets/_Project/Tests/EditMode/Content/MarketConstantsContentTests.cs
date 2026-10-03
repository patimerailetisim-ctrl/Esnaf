using System.Collections.Generic;
using System.Linq;
using System.Text;
using Esnaf.Core;
using Esnaf.Domain.Content;
using Esnaf.Domain.Market;
using Esnaf.Domain.Npc;
using Esnaf.Domain.Products;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Content
{
    /// <summary>economy_constants.json içindeki "market" bölümü: ayrıştırma, doğrulama, GDD sayıları, yardımcı sorgular.</summary>
    public class MarketConstantsContentTests
    {
        private const string File = "economy_constants.json";

        // ---------- küçük JSON düzenleme yardımcıları (Unity testlerinde Newtonsoft yok) ----------

        /// <summary>
        /// "property" adlı özelliğin DEĞERİNİ değiştirir. Arama, sırayla verilen çapaların (yol) sonundan başlar;
        /// böylece aynı adlı iç içe alanlar ayrı ayrı hedeflenir.
        /// </summary>
        private static string Set(string json, string property, string newValue, params string[] path)
        {
            int from = 0;
            foreach (string anchor in path)
            {
                int at = json.IndexOf(anchor, from, System.StringComparison.Ordinal);
                Assert.GreaterOrEqual(at, 0, "Çapa bulunamadı: " + anchor);
                from = at + anchor.Length;
            }

            string key = "\"" + property + "\":";
            int keyAt = json.IndexOf(key, from, System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(keyAt, 0, "Özellik bulunamadı: " + property);
            int valueStart = keyAt + key.Length;
            while (json[valueStart] == ' ')
            {
                valueStart++;
            }

            int end = valueStart;
            char open = json[valueStart];
            if (open == '{' || open == '[')
            {
                int depth = 0;
                bool inString = false;
                for (; end < json.Length; end++)
                {
                    char c = json[end];
                    if (c == '"')
                    {
                        inString = !inString;
                    }
                    else if (!inString && (c == '{' || c == '['))
                    {
                        depth++;
                    }
                    else if (!inString && (c == '}' || c == ']'))
                    {
                        depth--;
                        if (depth == 0)
                        {
                            end++;
                            break;
                        }
                    }
                }
            }
            else
            {
                while (json[end] != ',' && json[end] != '}' && json[end] != ' ')
                {
                    end++;
                }
            }

            var sb = new StringBuilder();
            sb.Append(json, 0, valueStart).Append(newValue).Append(json, end, json.Length - end);
            return sb.ToString();
        }

        private static string Missing(string json, string property, params string[] path)
        {
            return Set(json, property, "null", path);
        }

        private static readonly string Good = ContentFixtures.EconomyConstantsJson;

        private static MarketConstants Parse(string json, List<ContentIssue> issues)
        {
            return ContentParser.ParseMarketConstants(File, json, issues);
        }

        private static IReadOnlyList<ProductDefinition> Products()
        {
            var issues = new List<ContentIssue>();
            IReadOnlyList<ProductDefinition> products = ContentParser.ParseProductModels(
                ContentFileNames.PhoneModels,
                ContentFixtures.ModelsFile(ContentFixtures.ValidModelJson, ContentFixtures.ModelWithId("phone.test_two")),
                issues);
            Assert.AreEqual(0, issues.Count);
            return products;
        }

        private static List<ContentIssue> Validate(string json, IReadOnlyList<NpcDefinition> npcs = null)
        {
            var issues = new List<ContentIssue>();
            MarketConstants market = Parse(json, issues);
            Assert.IsNotNull(market, "Test verisi ayrıştırılamadı: " + string.Join("\n", issues));
            Assert.AreEqual(0, issues.Count, "Ayrıştırma sorunu: " + string.Join("\n", issues));
            ContentValidator.ValidateMarket(market, npcs ?? ContentFixtures.Npcs(), Products(), ContentFixtures.Tables(), File, issues);
            return issues;
        }

        private static void AssertOneField(List<ContentIssue> issues, string field)
        {
            Assert.AreEqual(1, issues.Count, string.Join("\n", issues.Select(i => i.ToString())));
            Assert.AreEqual(ContentIssueCodes.MarketFieldInvalid, issues[0].Code, issues[0].ToString());
            Assert.AreEqual(File, issues[0].File);
            StringAssert.Contains(field, issues[0].Message);
        }

        private static void AssertOneReference(List<ContentIssue> issues, string id)
        {
            Assert.AreEqual(1, issues.Count, string.Join("\n", issues.Select(i => i.ToString())));
            Assert.AreEqual(ContentIssueCodes.MarketReferenceMissing, issues[0].Code, issues[0].ToString());
            StringAssert.Contains(id, issues[0].Message);
        }

        // ---------- ayrıştırma ----------

        [Test]
        public void Parse_Fixture_ReadsEverySection()
        {
            var issues = new List<ContentIssue>();

            MarketConstants m = Parse(Good, issues);

            Assert.AreEqual(0, issues.Count, string.Join("\n", issues));
            Assert.AreEqual(4, m.ListingCounts.Count);
            Assert.AreEqual(5, m.ListingCounts[3].FromDay);
            Assert.AreEqual(7, m.ListingCounts[3].Min);
            Assert.AreEqual(8, m.ListingCounts[3].Max);
            Assert.AreEqual(2, m.LifetimeMinDays);
            Assert.AreEqual(4, m.LifetimeMaxDays);
            Assert.AreEqual(2, m.SegmentWeights.Count);
            Assert.AreEqual(30, m.SegmentWeights[0].Entry);
            Assert.AreEqual(40, m.SegmentWeights[0].Mid);
            Assert.AreEqual(15, m.SegmentWeights[0].Upper);
            Assert.AreEqual(1, m.ModelAvailability.Count);
            Assert.AreEqual("phone.test_two", m.ModelAvailability[0].DefinitionId);
            Assert.AreEqual(5, m.ModelAvailability[0].FromDay);
            Assert.AreEqual(4, m.LearningFriendlyUntilDay);
            Assert.AreEqual(0.45, m.LearningFriendlyShare, 1e-12);
            Assert.AreEqual(2, m.OpportunityFromDay);
            Assert.AreEqual(2, m.MinOpportunitiesPerDay);
            Assert.AreEqual(0.90, m.OpportunityMaxRejectRatio, 1e-12);
            Assert.AreEqual(0.85, m.JackpotRejectRatioBelow, 1e-12);
            Assert.AreEqual(2, m.JackpotMaxPerDay.Count);
            Assert.AreEqual(6, m.JackpotMaxPerDay[1].FromDay);
            Assert.AreEqual(2, m.JackpotMaxPerDay[1].Max);
            Assert.AreEqual(5, m.TrapFromDay);
            Assert.AreEqual(1, m.TrapMinPerDay);
            Assert.AreEqual(1, m.TrapMaxPerDay.Count);
            Assert.AreEqual(1.15, m.TrapValueRatio, 1e-12);
            Assert.AreEqual(50, m.AskingPriceStep);
            Assert.AreEqual(2, m.HiddenDefects.Count);
            Assert.AreEqual("screen", m.HiddenDefects[0].Attribute);
            CollectionAssert.AreEqual(new[] { "replaced_aftermarket" }, m.HiddenDefects[0].HiddenValues.ToArray());
            Assert.AreEqual("original", m.HiddenDefects[0].CleanValue);
            CollectionAssert.AreEqual(new[] { "spotted", "faulty" }, m.HiddenDefects[1].HiddenValues.ToArray());
            GuidedListingSpec g = m.GuidedListing;
            Assert.AreEqual("npc.test_honest", g.SellerNpcId);
            Assert.AreEqual("phone.test_one", g.DefinitionId);
            Assert.AreEqual(128, g.StorageGb);
            Assert.AreEqual(12, g.AgeMonths);
            Assert.AreEqual(95, g.Battery);
            Assert.AreEqual(100, g.Body);
            Assert.AreEqual("original", g.Screen);
            Assert.AreEqual("ok", g.Camera);
            Assert.IsFalse(g.Box);
            Assert.IsFalse(g.Invoice);
            Assert.AreEqual(Money.FromTl(4750), g.RejectPrice);
        }

        [Test]
        public void Parse_GuidedFlags_AreRead()
        {
            string json = Set(Set(Good, "box", "true", "\"guidedListing\""), "invoice", "true", "\"guidedListing\"");
            var issues = new List<ContentIssue>();

            MarketConstants m = Parse(json, issues);

            Assert.IsTrue(m.GuidedListing.Box);
            Assert.IsTrue(m.GuidedListing.Invoice);
        }

        [Test]
        public void Parse_EconomyConstants_StillIgnoresTheMarketSection()
        {
            var issues = new List<ContentIssue>();

            Assert.IsNotNull(ContentParser.ParseEconomyConstants(File, Good, issues));
            Assert.AreEqual(0, issues.Count, string.Join("\n", issues));
        }

        private static readonly object[][] MissingCases =
        {
            new object[] { "listingCounts", new string[0], "listingCounts" },
            new object[] { "fromDay", new[] { "\"listingCounts\"" }, "listingCounts[0].fromDay" },
            new object[] { "min", new[] { "\"listingCounts\"" }, "listingCounts[0].min" },
            new object[] { "max", new[] { "\"listingCounts\"" }, "listingCounts[0].max" },
            new object[] { "listingLifetimeDays", new string[0], "listingLifetimeDays" },
            new object[] { "min", new[] { "\"listingLifetimeDays\"" }, "listingLifetimeDays.min" },
            new object[] { "max", new[] { "\"listingLifetimeDays\"" }, "listingLifetimeDays.max" },
            new object[] { "segmentWeights", new string[0], "segmentWeights" },
            new object[] { "fromDay", new[] { "\"segmentWeights\"" }, "segmentWeights[0].fromDay" },
            new object[] { "entry", new[] { "\"segmentWeights\"" }, "segmentWeights[0].entry" },
            new object[] { "mid", new[] { "\"segmentWeights\"" }, "segmentWeights[0].mid" },
            new object[] { "upper", new[] { "\"segmentWeights\"" }, "segmentWeights[0].upper" },
            new object[] { "modelAvailability", new string[0], "modelAvailability" },
            new object[] { "id", new[] { "\"modelAvailability\"" }, "modelAvailability[0].id" },
            new object[] { "fromDay", new[] { "\"modelAvailability\"" }, "modelAvailability[0].fromDay" },
            new object[] { "learningFriendlySellers", new string[0], "learningFriendlySellers" },
            new object[] { "untilDay", new[] { "\"learningFriendlySellers\"" }, "learningFriendlySellers.untilDay" },
            new object[] { "share", new[] { "\"learningFriendlySellers\"" }, "learningFriendlySellers.share" },
            new object[] { "opportunity", new string[0], "opportunity" },
            new object[] { "fromDay", new[] { "\"opportunity\"" }, "opportunity.fromDay" },
            new object[] { "minPerDay", new[] { "\"opportunity\"" }, "opportunity.minPerDay" },
            new object[] { "maxRejectRatio", new[] { "\"opportunity\"" }, "opportunity.maxRejectRatio" },
            new object[] { "jackpot", new string[0], "jackpot" },
            new object[] { "rejectRatioBelow", new[] { "\"jackpot\"" }, "jackpot.rejectRatioBelow" },
            new object[] { "maxPerDay", new[] { "\"jackpot\"" }, "jackpot.maxPerDay" },
            new object[] { "fromDay", new[] { "\"jackpot\"", "\"maxPerDay\"" }, "jackpot.maxPerDay[0].fromDay" },
            new object[] { "max", new[] { "\"jackpot\"", "\"maxPerDay\"" }, "jackpot.maxPerDay[0].max" },
            new object[] { "trap", new string[0], "trap" },
            new object[] { "fromDay", new[] { "\"trap\"" }, "trap.fromDay" },
            new object[] { "minPerDay", new[] { "\"trap\"" }, "trap.minPerDay" },
            new object[] { "maxPerDay", new[] { "\"trap\"" }, "trap.maxPerDay" },
            new object[] { "valueRatio", new[] { "\"trap\"" }, "trap.valueRatio" },
            new object[] { "fromDay", new[] { "\"trap\"", "\"maxPerDay\"" }, "trap.maxPerDay[0].fromDay" },
            new object[] { "max", new[] { "\"trap\"", "\"maxPerDay\"" }, "trap.maxPerDay[0].max" },
            new object[] { "askingPriceStep", new string[0], "askingPriceStep" },
            new object[] { "hiddenDefects", new string[0], "hiddenDefects" },
            new object[] { "attribute", new[] { "\"hiddenDefects\"" }, "hiddenDefects[0].attribute" },
            new object[] { "hiddenValues", new[] { "\"hiddenDefects\"" }, "hiddenDefects[0].hiddenValues" },
            new object[] { "cleanValue", new[] { "\"hiddenDefects\"" }, "hiddenDefects[0].cleanValue" },
            new object[] { "guidedListing", new string[0], "guidedListing" },
            new object[] { "sellerNpcId", new[] { "\"guidedListing\"" }, "guidedListing.sellerNpcId" },
            new object[] { "definitionId", new[] { "\"guidedListing\"" }, "guidedListing.definitionId" },
            new object[] { "storageGb", new[] { "\"guidedListing\"" }, "guidedListing.storageGb" },
            new object[] { "ageMonths", new[] { "\"guidedListing\"" }, "guidedListing.ageMonths" },
            new object[] { "battery", new[] { "\"guidedListing\"" }, "guidedListing.battery" },
            new object[] { "body", new[] { "\"guidedListing\"" }, "guidedListing.body" },
            new object[] { "screen", new[] { "\"guidedListing\"" }, "guidedListing.screen" },
            new object[] { "camera", new[] { "\"guidedListing\"" }, "guidedListing.camera" },
            new object[] { "box", new[] { "\"guidedListing\"" }, "guidedListing.box" },
            new object[] { "invoice", new[] { "\"guidedListing\"" }, "guidedListing.invoice" },
            new object[] { "rejectPrice", new[] { "\"guidedListing\"" }, "guidedListing.rejectPrice" }
        };

        [TestCaseSource(nameof(MissingCases))]
        public void Parse_EveryRequiredField_IsEnforced(string property, string[] path, string expectedField)
        {
            var issues = new List<ContentIssue>();

            Assert.IsNull(Parse(Missing(Good, property, path), issues));

            Assert.IsTrue(
                issues.Any(i => i.Code == ContentIssueCodes.FieldMissing && i.Message.Contains("'" + expectedField + "'")),
                "Eksik alan '" + expectedField + "' raporlanmadı:\n" + string.Join("\n", issues));
        }

        [Test]
        public void Parse_MissingMarketSection_IsReported()
        {
            var issues = new List<ContentIssue>();

            Assert.IsNull(Parse(Missing(Good, "market"), issues));

            Assert.IsTrue(issues.Any(i => i.Code == ContentIssueCodes.FieldMissing && i.Message.Contains("'market'")));
        }

        [Test]
        public void Parse_UnknownMarketField_IsReported()
        {
            var issues = new List<ContentIssue>();

            Assert.IsNull(Parse(Good.Replace("askingPriceStep", "askingPriceStp"), issues));

            Assert.AreEqual(ContentIssueCodes.FileSyntax, issues.Single().Code);
        }

        [Test]
        public void Parse_FractionalMarketCounts_AreRejected()
        {
            var issues = new List<ContentIssue>();

            Assert.IsNull(Parse(Good.Replace("\"untilDay\": 4", "\"untilDay\": 4.5"), issues));

            Assert.AreEqual(ContentIssueCodes.FileSyntax, issues.Single().Code);
        }

        // ---------- doğrulama ----------

        [Test]
        public void Validate_Good_HasNoIssues()
        {
            Assert.AreEqual(0, Validate(Good).Count);
        }

        private static readonly object[][] BadValues =
        {
            // ilan sayıları
            new object[] { "listingCounts", "[]", new string[0], "listingCounts" },
            new object[] { "listingCounts", "[ { \"fromDay\": 2, \"min\": 3, \"max\": 3 } ]", new string[0], "listingCounts[0].fromDay" },
            new object[] { "listingCounts", "[ { \"fromDay\": 1, \"min\": 3, \"max\": 3 }, { \"fromDay\": 1, \"min\": 3, \"max\": 3 } ]", new string[0], "listingCounts[1].fromDay" },
            new object[] { "listingCounts", "[ { \"fromDay\": 1, \"min\": 3, \"max\": 3 }, { \"fromDay\": 0, \"min\": 3, \"max\": 3 } ]", new string[0], "listingCounts[1].fromDay" },
            new object[] { "listingCounts", "[ { \"fromDay\": 1, \"min\": 0, \"max\": 3 } ]", new string[0], "listingCounts[0].min" },
            new object[] { "listingCounts", "[ { \"fromDay\": 1, \"min\": 5, \"max\": 4 } ]", new string[0], "listingCounts[0].max" },
            // ömür
            new object[] { "listingLifetimeDays", "{ \"min\": 0, \"max\": 4 }", new string[0], "listingLifetimeDays.min" },
            new object[] { "listingLifetimeDays", "{ \"min\": 3, \"max\": 2 }", new string[0], "listingLifetimeDays.max" },
            // segment ağırlıkları
            new object[] { "segmentWeights", "[]", new string[0], "segmentWeights" },
            new object[] { "segmentWeights", "[ { \"fromDay\": 2, \"entry\": 1, \"mid\": 1, \"upper\": 1 } ]", new string[0], "segmentWeights[0].fromDay" },
            new object[] { "segmentWeights", "[ { \"fromDay\": 1, \"entry\": 1, \"mid\": 1, \"upper\": 1 }, { \"fromDay\": 1, \"entry\": 1, \"mid\": 1, \"upper\": 1 } ]", new string[0], "segmentWeights[1].fromDay" },
            new object[] { "segmentWeights", "[ { \"fromDay\": 1, \"entry\": -1, \"mid\": 1, \"upper\": 1 } ]", new string[0], "segmentWeights[0].entry" },
            new object[] { "segmentWeights", "[ { \"fromDay\": 1, \"entry\": 1, \"mid\": -1, \"upper\": 1 } ]", new string[0], "segmentWeights[0].mid" },
            new object[] { "segmentWeights", "[ { \"fromDay\": 1, \"entry\": 1, \"mid\": 1, \"upper\": -1 } ]", new string[0], "segmentWeights[0].upper" },
            new object[] { "segmentWeights", "[ { \"fromDay\": 1, \"entry\": 0, \"mid\": 0, \"upper\": 0 } ]", new string[0], "segmentWeights[0]" },
            // öğrenme dostu satıcılar
            new object[] { "untilDay", "0", new[] { "\"learningFriendlySellers\"" }, "learningFriendlySellers.untilDay" },
            new object[] { "share", "-0.1", new[] { "\"learningFriendlySellers\"" }, "learningFriendlySellers.share" },
            new object[] { "share", "1.1", new[] { "\"learningFriendlySellers\"" }, "learningFriendlySellers.share" },
            // fırsat
            new object[] { "fromDay", "0", new[] { "\"opportunity\"" }, "opportunity.fromDay" },
            new object[] { "minPerDay", "-1", new[] { "\"opportunity\"" }, "opportunity.minPerDay" },
            new object[] { "maxRejectRatio", "0", new[] { "\"opportunity\"" }, "opportunity.maxRejectRatio" },
            new object[] { "maxRejectRatio", "1.01", new[] { "\"opportunity\"" }, "opportunity.maxRejectRatio" },
            // jackpot
            new object[] { "rejectRatioBelow", "0", new[] { "\"jackpot\"" }, "jackpot.rejectRatioBelow" },
            new object[] { "rejectRatioBelow", "1.01", new[] { "\"jackpot\"" }, "jackpot.rejectRatioBelow" },
            new object[] { "maxPerDay", "[]", new[] { "\"jackpot\"" }, "jackpot.maxPerDay" },
            new object[] { "maxPerDay", "[ { \"fromDay\": 2, \"max\": 1 } ]", new[] { "\"jackpot\"" }, "jackpot.maxPerDay[0].fromDay" },
            new object[] { "maxPerDay", "[ { \"fromDay\": 1, \"max\": 1 }, { \"fromDay\": 1, \"max\": 2 } ]", new[] { "\"jackpot\"" }, "jackpot.maxPerDay[1].fromDay" },
            new object[] { "maxPerDay", "[ { \"fromDay\": 1, \"max\": -1 } ]", new[] { "\"jackpot\"" }, "jackpot.maxPerDay[0].max" },
            // tuzak
            new object[] { "fromDay", "0", new[] { "\"trap\"" }, "trap.fromDay" },
            new object[] { "minPerDay", "-1", new[] { "\"trap\"" }, "trap.minPerDay" },
            new object[] { "valueRatio", "1", new[] { "\"trap\"" }, "trap.valueRatio" },
            new object[] { "valueRatio", "0.9", new[] { "\"trap\"" }, "trap.valueRatio" },
            new object[] { "maxPerDay", "[ { \"fromDay\": 6, \"max\": 0 } ]", new[] { "\"trap\"" }, "trap.maxPerDay[0].max" },
            new object[] { "maxPerDay", "[ { \"fromDay\": 0, \"max\": 2 } ]", new[] { "\"trap\"" }, "trap.maxPerDay[0].fromDay" },
            new object[] { "maxPerDay", "[ { \"fromDay\": 6, \"max\": 2 }, { \"fromDay\": 6, \"max\": 3 } ]", new[] { "\"trap\"" }, "trap.maxPerDay[1].fromDay" },
            // ilan fiyatı adımı
            new object[] { "askingPriceStep", "0", new string[0], "askingPriceStep" },
            new object[] { "askingPriceStep", "5", new string[0], "askingPriceStep" },
            new object[] { "askingPriceStep", "55", new string[0], "askingPriceStep" },
            new object[] { "askingPriceStep", "-50", new string[0], "askingPriceStep" },
            // saklanan kusurlar
            new object[] { "hiddenDefects", "[]", new string[0], "hiddenDefects" },
            new object[] { "attribute", "\"\"", new[] { "\"hiddenDefects\"" }, "hiddenDefects[0].attribute" },
            new object[] { "attribute", "\"battery\"", new[] { "\"hiddenDefects\"" }, "hiddenDefects[0].attribute" },
            new object[] { "hiddenValues", "[]", new[] { "\"hiddenDefects\"" }, "hiddenDefects[0].hiddenValues" },
            new object[] { "hiddenValues", "[ \"no_such_screen\" ]", new[] { "\"hiddenDefects\"" }, "hiddenDefects[0].hiddenValues" },
            new object[] { "cleanValue", "\"no_such_screen\"", new[] { "\"hiddenDefects\"" }, "hiddenDefects[0].cleanValue" },
            new object[] { "cleanValue", "\"replaced_aftermarket\"", new[] { "\"hiddenDefects\"" }, "hiddenDefects[0].cleanValue" },
            // rehberli ilan
            new object[] { "storageGb", "64", new[] { "\"guidedListing\"" }, "guidedListing.storageGb" },
            new object[] { "ageMonths", "5", new[] { "\"guidedListing\"" }, "guidedListing.ageMonths" },
            new object[] { "ageMonths", "37", new[] { "\"guidedListing\"" }, "guidedListing.ageMonths" },
            new object[] { "battery", "-1", new[] { "\"guidedListing\"" }, "guidedListing.battery" },
            new object[] { "battery", "101", new[] { "\"guidedListing\"" }, "guidedListing.battery" },
            new object[] { "body", "-1", new[] { "\"guidedListing\"" }, "guidedListing.body" },
            new object[] { "body", "101", new[] { "\"guidedListing\"" }, "guidedListing.body" },
            new object[] { "screen", "\"no_such_screen\"", new[] { "\"guidedListing\"" }, "guidedListing.screen" },
            new object[] { "camera", "\"no_such_camera\"", new[] { "\"guidedListing\"" }, "guidedListing.camera" },
            new object[] { "rejectPrice", "0", new[] { "\"guidedListing\"" }, "guidedListing.rejectPrice" },
            new object[] { "rejectPrice", "-4750", new[] { "\"guidedListing\"" }, "guidedListing.rejectPrice" },
            new object[] { "rejectPrice", "4755", new[] { "\"guidedListing\"" }, "guidedListing.rejectPrice" },
            new object[] { "sellerNpcId", "\"npc.test_hurried\"", new[] { "\"guidedListing\"" }, "guidedListing.sellerNpcId" }
        };

        [TestCaseSource(nameof(BadValues))]
        public void Validate_BadValue_IsReported(string property, string newValue, string[] path, string field)
        {
            AssertOneField(Validate(Set(Good, property, newValue, path)), field);
        }

        private static readonly object[][] BoundaryValues =
        {
            new object[] { "min", "1", new[] { "\"listingLifetimeDays\"" } },
            new object[] { "min", "4", new[] { "\"listingLifetimeDays\"" } },
            new object[] { "untilDay", "1", new[] { "\"learningFriendlySellers\"" } },
            new object[] { "share", "0", new[] { "\"learningFriendlySellers\"" } },
            new object[] { "share", "1", new[] { "\"learningFriendlySellers\"" } },
            new object[] { "minPerDay", "0", new[] { "\"opportunity\"" } },
            new object[] { "maxRejectRatio", "1", new[] { "\"opportunity\"" } },
            new object[] { "rejectRatioBelow", "1", new[] { "\"jackpot\"" } },
            new object[] { "minPerDay", "0", new[] { "\"trap\"" } },
            new object[] { "valueRatio", "1.01", new[] { "\"trap\"" } },
            new object[] { "maxPerDay", "[]", new[] { "\"trap\"" } },
            new object[] { "maxPerDay", "[ { \"fromDay\": 6, \"max\": 1 } ]", new[] { "\"trap\"" } },
            new object[] { "askingPriceStep", "10", new string[0] },
            new object[] { "askingPriceStep", "100", new string[0] },
            new object[] { "battery", "0", new[] { "\"guidedListing\"" } },
            new object[] { "battery", "100", new[] { "\"guidedListing\"" } },
            new object[] { "body", "0", new[] { "\"guidedListing\"" } },
            new object[] { "body", "100", new[] { "\"guidedListing\"" } },
            new object[] { "ageMonths", "6", new[] { "\"guidedListing\"" } },
            new object[] { "ageMonths", "36", new[] { "\"guidedListing\"" } },
            new object[] { "storageGb", "256", new[] { "\"guidedListing\"" } },
            new object[] { "rejectPrice", "10", new[] { "\"guidedListing\"" } }
        };

        [TestCaseSource(nameof(BoundaryValues))]
        public void Validate_BoundaryValues_AreAccepted(string property, string newValue, string[] path)
        {
            Assert.AreEqual(0, Validate(Set(Good, property, newValue, path)).Count);
        }

        [Test]
        public void Validate_UnknownModelInAvailability_IsAReferenceError()
        {
            AssertOneReference(Validate(Set(Good, "id", "\"phone.nope\"", "\"modelAvailability\"")), "phone.nope");
        }

        [Test]
        public void Validate_DuplicateModelInAvailability_IsReported()
        {
            string json = Set(Good, "modelAvailability",
                "[ { \"id\": \"phone.test_two\", \"fromDay\": 5 }, { \"id\": \"phone.test_two\", \"fromDay\": 6 } ]");

            AssertOneField(Validate(json), "modelAvailability[1].id");
        }

        [Test]
        public void Validate_ModelAvailabilityDayBelowOne_IsReported()
        {
            AssertOneField(Validate(Set(Good, "fromDay", "0", "\"modelAvailability\"")), "modelAvailability[0].fromDay");
        }

        [Test]
        public void Validate_UnknownGuidedModel_IsAReferenceError()
        {
            AssertOneReference(Validate(Set(Good, "definitionId", "\"phone.nope\"", "\"guidedListing\"")), "phone.nope");
        }

        [Test]
        public void Validate_UnknownGuidedSeller_IsAReferenceError()
        {
            AssertOneReference(Validate(Set(Good, "sellerNpcId", "\"npc.nope\"", "\"guidedListing\"")), "npc.nope");
        }

        [Test]
        public void Validate_GuidedModelNotAvailableOnDayOne_IsReported()
        {
            string json = Set(Good, "id", "\"phone.test_one\"", "\"modelAvailability\"");

            AssertOneField(Validate(json), "guidedListing.definitionId");
        }

        [Test]
        public void Validate_TrapWithoutAnyConcealingSeller_IsReported()
        {
            string json = Set(Good, "fromDay", "2", "\"trap\"");

            List<ContentIssue> issues = Validate(json);

            Assert.AreEqual(1, issues.Count, string.Join("\n", issues));
            Assert.AreEqual(ContentIssueCodes.MarketTrapSellerMissing, issues[0].Code);
        }

        [Test]
        public void Validate_TrapNotRequired_NeedsNoConcealingSeller()
        {
            string json = Set(Set(Good, "fromDay", "2", "\"trap\""), "minPerDay", "0", "\"trap\"");

            Assert.AreEqual(0, Validate(json).Count);
        }

        [Test]
        public void Validate_TrapSellerAvailableExactlyOnTheTrapDay_IsAccepted()
        {
            string json = Set(Good, "fromDay", "3", "\"trap\"");

            Assert.AreEqual(0, Validate(json).Count);
        }

        // ---------- yardımcı sorgular ----------

        [Test]
        public void CountBandFor_PicksTheLatestBandNotAfterTheDay()
        {
            MarketConstants m = ContentFixtures.Market();

            Assert.AreEqual(1, m.CountBandFor(1).FromDay);
            Assert.AreEqual(2, m.CountBandFor(2).FromDay);
            Assert.AreEqual(3, m.CountBandFor(3).FromDay);
            Assert.AreEqual(3, m.CountBandFor(4).FromDay);
            Assert.AreEqual(5, m.CountBandFor(5).FromDay);
            Assert.AreEqual(5, m.CountBandFor(1000).FromDay);
        }

        [Test]
        public void SegmentBandFor_PicksTheLatestBandNotAfterTheDay()
        {
            MarketConstants m = ContentFixtures.Market();

            Assert.AreEqual(15, m.SegmentBandFor(1).Upper);
            Assert.AreEqual(15, m.SegmentBandFor(4).Upper);
            Assert.AreEqual(30, m.SegmentBandFor(5).Upper);
            Assert.AreEqual(30, m.SegmentBandFor(50).Upper);
        }

        [Test]
        public void SegmentBand_WeightFor_ReturnsTheSegmentsWeight()
        {
            SegmentWeightBand band = ContentFixtures.Market().SegmentBandFor(1);

            Assert.AreEqual(30, band.WeightFor(ProductSegment.Entry));
            Assert.AreEqual(40, band.WeightFor(ProductSegment.Mid));
            Assert.AreEqual(15, band.WeightFor(ProductSegment.Upper));
        }

        [Test]
        public void JackpotMax_FollowsTheQuotaBands()
        {
            MarketConstants m = ContentFixtures.Market();

            Assert.AreEqual(1, m.JackpotMax(1));
            Assert.AreEqual(1, m.JackpotMax(5));
            Assert.AreEqual(2, m.JackpotMax(6));
            Assert.AreEqual(2, m.JackpotMax(999));
        }

        [Test]
        public void TrapMax_IsUnlimitedBeforeItsFirstBand()
        {
            MarketConstants m = ContentFixtures.Market();

            Assert.AreEqual(int.MaxValue, m.TrapMax(1));
            Assert.AreEqual(int.MaxValue, m.TrapMax(5));
            Assert.AreEqual(2, m.TrapMax(6));
            Assert.AreEqual(2, m.TrapMax(500));
        }

        [Test]
        public void IsModelAvailable_GatesOnlyListedModels()
        {
            MarketConstants m = ContentFixtures.Market();

            Assert.IsFalse(m.IsModelAvailable("phone.test_two", 1));
            Assert.IsFalse(m.IsModelAvailable("phone.test_two", 4));
            Assert.IsTrue(m.IsModelAvailable("phone.test_two", 5));
            Assert.IsTrue(m.IsModelAvailable("phone.test_two", 6));
            Assert.IsTrue(m.IsModelAvailable("phone.test_one", 1), "Listede olmayan model her zaman açıktır");
        }

        // ---------- gerçek dosya: GDD sayıları ----------

        private static string RealText()
        {
            return System.IO.File.ReadAllText(System.IO.Path.Combine(TestPaths.ContentDataDirectory(), File));
        }

        [Test]
        public void RealFile_ParsesAndKeepsTheEconomyConstants()
        {
            var issues = new List<ContentIssue>();

            EconomyConstantsAssert(ContentParser.ParseEconomyConstants(File, RealText(), issues));

            Assert.AreEqual(0, issues.Count, string.Join("\n", issues));
        }

        private static void EconomyConstantsAssert(Esnaf.Domain.Economy.EconomyConstants c)
        {
            Assert.AreEqual(Money.FromTl(250000), c.OpeningCapital);
            Assert.AreEqual(3, c.DailyExpenseFromDay);
            Assert.AreEqual(Money.FromTl(500), c.DailyExpenseAmount);
            Assert.AreEqual(15, c.InitialShelfCapacity);
        }

        [Test]
        public void RealFile_MatchesTheGddNumbers()
        {
            var issues = new List<ContentIssue>();

            MarketConstants m = Parse(RealText(), issues);

            Assert.AreEqual(0, issues.Count, string.Join("\n", issues));
            // v0.2 11.1: Gün 1: 3 · Gün 2: 5 · Gün 3–4: 6 · Gün 5+: 7–8
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 5 }, m.ListingCounts.Select(b => b.FromDay).ToArray());
            CollectionAssert.AreEqual(new[] { 3, 5, 6, 7 }, m.ListingCounts.Select(b => b.Min).ToArray());
            CollectionAssert.AreEqual(new[] { 3, 5, 6, 8 }, m.ListingCounts.Select(b => b.Max).ToArray());
            // ömür 2–4 gün
            Assert.AreEqual(2, m.LifetimeMinDays);
            Assert.AreEqual(4, m.LifetimeMaxDays);
            // v0.2 3.2: Giriş %30, Orta %40, Üst %30 (Gün 1–4'te Üst %15)
            Assert.AreEqual(2, m.SegmentWeights.Count);
            Assert.AreEqual(new[] { 1, 30, 40, 15 }, new[] { m.SegmentWeights[0].FromDay, m.SegmentWeights[0].Entry, m.SegmentWeights[0].Mid, m.SegmentWeights[0].Upper });
            Assert.AreEqual(new[] { 5, 30, 40, 30 }, new[] { m.SegmentWeights[1].FromDay, m.SegmentWeights[1].Entry, m.SegmentWeights[1].Mid, m.SegmentWeights[1].Upper });
            // v0.2 3.2: Model 10 Gün 5'ten önce çıkmaz
            Assert.AreEqual(1, m.ModelAvailability.Count);
            Assert.AreEqual("phone.elma_e14_pro_max", m.ModelAvailability[0].DefinitionId);
            Assert.AreEqual(5, m.ModelAvailability[0].FromDay);
            // P2
            Assert.AreEqual(4, m.LearningFriendlyUntilDay);
            Assert.AreEqual(0.45, m.LearningFriendlyShare, 1e-12);
            // P1: Gün 2+ ≥ 2 fırsat, R ≤ 0,90 × değer
            Assert.AreEqual(2, m.OpportunityFromDay);
            Assert.AreEqual(2, m.MinOpportunitiesPerDay);
            Assert.AreEqual(0.90, m.OpportunityMaxRejectRatio, 1e-12);
            // P3: ret oranı < 0,85; ≤ 1/gün (Gün 6+ ≤ 2)
            Assert.AreEqual(0.85, m.JackpotRejectRatioBelow, 1e-12);
            Assert.AreEqual(new[] { 1, 6 }, m.JackpotMaxPerDay.Select(b => b.FromDay).ToArray());
            Assert.AreEqual(new[] { 1, 2 }, m.JackpotMaxPerDay.Select(b => b.Max).ToArray());
            // P5: Gün 5+ ≥ 1 tuzak, Gün 6+ ≤ 2
            Assert.AreEqual(5, m.TrapFromDay);
            Assert.AreEqual(1, m.TrapMinPerDay);
            Assert.AreEqual(new[] { 6 }, m.TrapMaxPerDay.Select(b => b.FromDay).ToArray());
            Assert.AreEqual(new[] { 2 }, m.TrapMaxPerDay.Select(b => b.Max).ToArray());
            Assert.AreEqual(1.15, m.TrapValueRatio, 1e-12);
            // UA5
            Assert.AreEqual(50, m.AskingPriceStep);
            Assert.AreEqual(new[] { "screen", "camera" }, m.HiddenDefects.Select(h => h.Attribute).ToArray());
            CollectionAssert.AreEqual(new[] { "replaced_aftermarket" }, m.HiddenDefects[0].HiddenValues.ToArray());
            CollectionAssert.AreEqual(new[] { "spotted", "faulty" }, m.HiddenDefects[1].HiddenValues.ToArray());
            Assert.AreEqual("original", m.HiddenDefects[0].CleanValue);
            Assert.AreEqual("ok", m.HiddenDefects[1].CleanValue);
        }

        [Test]
        public void RealFile_GuidedListing_MatchesGddDayOne()
        {
            var issues = new List<ContentIssue>();

            GuidedListingSpec g = Parse(RealText(), issues).GuidedListing;

            // v0.2 Gün 1: Yıldız Y5 64 GB, 24 ay, Ayşe Hanım, kolay mod R = 4.750
            Assert.AreEqual("npc.ayse", g.SellerNpcId);
            Assert.AreEqual("phone.yildiz_y5", g.DefinitionId);
            Assert.AreEqual(64, g.StorageGb);
            Assert.AreEqual(24, g.AgeMonths);
            Assert.AreEqual(Money.FromTl(4750), g.RejectPrice);
            Assert.AreEqual(95, g.Battery);
            Assert.AreEqual(100, g.Body);
            Assert.AreEqual("original", g.Screen);
            Assert.AreEqual("ok", g.Camera);
            Assert.IsFalse(g.Box);
            Assert.IsFalse(g.Invoice);
        }

        [Test]
        public void RealContent_LoadsCompletely_WithNpcsAndMarketAndManifest()
        {
            ContentLoadResult result = ContentDatabase.Load(new DirectoryContentSource(TestPaths.ContentDataDirectory()));

            Assert.IsNotNull(result.Database, string.Join("\n", result.Issues));
            Assert.AreEqual(10, result.Database.Npcs.Count);
            Assert.IsNotNull(result.Database.MarketConstants);
            NpcDefinition ayse;
            Assert.IsTrue(result.Database.TryGetNpc("npc.ayse", out ayse));
            Assert.AreEqual("Ayşe Hanım", ayse.Name);
            Assert.AreEqual("Ayşe Hanım", result.Database.GetNpc("npc.ayse").Name);
            NpcDefinition none;
            Assert.IsFalse(result.Database.TryGetNpc("npc.nobody", out none));
            Assert.IsFalse(result.Database.TryGetNpc(null, out none));
            Assert.Throws<KeyNotFoundException>(() => result.Database.GetNpc("npc.nobody"));
        }
    }
}
