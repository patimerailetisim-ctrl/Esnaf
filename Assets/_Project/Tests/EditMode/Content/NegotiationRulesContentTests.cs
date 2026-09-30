using System.Collections.Generic;
using System.IO;
using System.Linq;
using Esnaf.Domain.Content;
using Esnaf.Domain.Negotiation;
using Esnaf.Domain.Npc;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Content
{
    public class NegotiationRulesContentTests
    {
        private const string File = "negotiation_rules.json";
        private static readonly string Good = ContentFixtures.NegotiationRulesJson;

        private static NegotiationRules Parse(string json, List<ContentIssue> issues)
        {
            return ContentParser.ParseNegotiation(File, json, issues);
        }

        private static List<ContentIssue> Validate(string json)
        {
            var issues = new List<ContentIssue>();
            NegotiationRules rules = Parse(json, issues);
            Assert.IsNotNull(rules, "Test verisi ayrıştırılamadı: " + string.Join("\n", issues));
            Assert.AreEqual(0, issues.Count, string.Join("\n", issues));
            ContentValidator.ValidateNegotiation(rules, File, issues);
            return issues;
        }

        private static void AssertOneField(List<ContentIssue> issues, string field)
        {
            Assert.AreEqual(1, issues.Count, string.Join("\n", issues.Select(i => i.ToString())));
            Assert.AreEqual(ContentIssueCodes.NegotiationFieldInvalid, issues[0].Code, issues[0].ToString());
            Assert.AreEqual(File, issues[0].File);
            StringAssert.StartsWith(field + ":", issues[0].Message);
        }

        // ---------- ayrıştırma ----------

        [Test]
        public void Parse_Fixture_ReadsEveryValue()
        {
            var issues = new List<ContentIssue>();

            NegotiationRules r = Parse(Good, issues);

            Assert.AreEqual(0, issues.Count, string.Join("\n", issues));
            Assert.AreEqual(0.20, r.PriceBaseShare, 1e-12);
            Assert.AreEqual(0.30, r.PriceTrustShare, 1e-12);
            Assert.AreEqual(0.15, r.PriceUrgencyShare, 1e-12);
            Assert.AreEqual(0.90, r.InsultRatio, 1e-12);
            Assert.AreEqual(15, r.InsultTrustLoss);
            Assert.AreEqual(1, r.InsultExtraPatienceLoss);
            Assert.AreEqual(5, r.InsultPenaltyFromDay);
            Assert.AreEqual(0.97, r.NearOfferRatio, 1e-12);
            Assert.AreEqual(5, r.NearOfferTrustGain);
            Assert.AreEqual(5, r.CardCorrectTrustGain);
            Assert.AreEqual(10, r.CardWrongTrustLoss);
            Assert.AreEqual(1, r.CardWrongPatienceLoss);
            Assert.AreEqual("s3", r.ReportLevelId);
            Assert.AreEqual(0.20, r.ReportPersuasionBonus, 1e-12);
            Assert.AreEqual(0.65, r.RejectFloorRatio, 1e-12);
            Assert.AreEqual(50, r.StartTrust);
            Assert.AreEqual(10, r.StartTrustSpread);
            Assert.AreEqual(0.03, r.RejectMoodSwing, 1e-12);
            Assert.AreEqual(40, r.MoodLowBelow);
            Assert.AreEqual(70, r.MoodHighFrom);
            Assert.AreEqual(1, r.PatienceLowAtMost);
            Assert.AreEqual(3, r.PatienceMediumAtMost);
        }

        private static readonly object[][] MissingCases =
        {
            new object[] { "price", new string[0], "price" },
            new object[] { "baseShare", new[] { "\"price\"" }, "price.baseShare" },
            new object[] { "trustShare", new[] { "\"price\"" }, "price.trustShare" },
            new object[] { "urgencyShare", new[] { "\"price\"" }, "price.urgencyShare" },
            new object[] { "insult", new string[0], "insult" },
            new object[] { "ratio", new[] { "\"insult\"" }, "insult.ratio" },
            new object[] { "trustLoss", new[] { "\"insult\"" }, "insult.trustLoss" },
            new object[] { "extraPatienceLoss", new[] { "\"insult\"" }, "insult.extraPatienceLoss" },
            new object[] { "penaltyFromDay", new[] { "\"insult\"" }, "insult.penaltyFromDay" },
            new object[] { "nearOffer", new string[0], "nearOffer" },
            new object[] { "ratio", new[] { "\"nearOffer\"" }, "nearOffer.ratio" },
            new object[] { "trustGain", new[] { "\"nearOffer\"" }, "nearOffer.trustGain" },
            new object[] { "card", new string[0], "card" },
            new object[] { "correctTrustGain", new[] { "\"card\"" }, "card.correctTrustGain" },
            new object[] { "wrongTrustLoss", new[] { "\"card\"" }, "card.wrongTrustLoss" },
            new object[] { "wrongPatienceLoss", new[] { "\"card\"" }, "card.wrongPatienceLoss" },
            new object[] { "reportLevelId", new[] { "\"card\"" }, "card.reportLevelId" },
            new object[] { "reportPersuasionBonus", new[] { "\"card\"" }, "card.reportPersuasionBonus" },
            new object[] { "rejectFloorRatio", new[] { "\"card\"" }, "card.rejectFloorRatio" },
            new object[] { "start", new string[0], "start" },
            new object[] { "trust", new[] { "\"start\"" }, "start.trust" },
            new object[] { "trustSpread", new[] { "\"start\"" }, "start.trustSpread" },
            new object[] { "rejectMoodSwing", new[] { "\"start\"" }, "start.rejectMoodSwing" },
            new object[] { "view", new string[0], "view" },
            new object[] { "moodLowBelow", new[] { "\"view\"" }, "view.moodLowBelow" },
            new object[] { "moodHighFrom", new[] { "\"view\"" }, "view.moodHighFrom" },
            new object[] { "patienceLowAtMost", new[] { "\"view\"" }, "view.patienceLowAtMost" },
            new object[] { "patienceMediumAtMost", new[] { "\"view\"" }, "view.patienceMediumAtMost" }
        };

        [TestCaseSource(nameof(MissingCases))]
        public void Parse_MissingField_IsReportedByName(string property, string[] path, string expected)
        {
            var issues = new List<ContentIssue>();

            NegotiationRules r = Parse(JsonEdit.Missing(Good, property, path), issues);

            Assert.IsNull(r);
            Assert.AreEqual(1, issues.Count, string.Join("\n", issues.Select(i => i.ToString())));
            Assert.AreEqual(ContentIssueCodes.FieldMissing, issues[0].Code);
            StringAssert.Contains(expected, issues[0].Message);
        }

        [Test]
        public void Parse_Broken_ReportsStructuralIssues()
        {
            var issues = new List<ContentIssue>();
            Assert.IsNull(Parse("{ nope", issues));
            Assert.AreEqual(ContentIssueCodes.FileSyntax, issues[0].Code);

            issues.Clear();
            Assert.IsNull(Parse(Good.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2"), issues));
            Assert.AreEqual(ContentIssueCodes.SchemaVersionUnsupported, issues[0].Code);

            issues.Clear();
            Assert.IsNull(Parse(Good.Replace("\"trustShare\"", "\"trustShar\""), issues));
            Assert.AreEqual(ContentIssueCodes.FileSyntax, issues[0].Code, "yazım hatalı alan adı = hata");
        }

        // ---------- doğrulama ----------

        [Test]
        public void Validate_Fixture_IsClean()
        {
            Assert.AreEqual(0, Validate(Good).Count);
        }

        private static readonly object[][] InvalidCases =
        {
            new object[] { "baseShare", "-0.01", "price.baseShare", new[] { "\"price\"" } },
            new object[] { "trustShare", "-0.1", "price.trustShare", new[] { "\"price\"" } },
            new object[] { "urgencyShare", "-0.1", "price.urgencyShare", new[] { "\"price\"" } },
            new object[] { "baseShare", "0.6", "price", new[] { "\"price\"" } },
            new object[] { "ratio", "0", "insult.ratio", new[] { "\"insult\"" } },
            new object[] { "ratio", "1.0", "insult.ratio", new[] { "\"insult\"" } },
            new object[] { "trustLoss", "-1", "insult.trustLoss", new[] { "\"insult\"" } },
            new object[] { "trustLoss", "101", "insult.trustLoss", new[] { "\"insult\"" } },
            new object[] { "extraPatienceLoss", "-1", "insult.extraPatienceLoss", new[] { "\"insult\"" } },
            new object[] { "penaltyFromDay", "0", "insult.penaltyFromDay", new[] { "\"insult\"" } },
            new object[] { "ratio", "0.90", "nearOffer.ratio", new[] { "\"nearOffer\"" } },
            new object[] { "ratio", "1.01", "nearOffer.ratio", new[] { "\"nearOffer\"" } },
            new object[] { "trustGain", "-1", "nearOffer.trustGain", new[] { "\"nearOffer\"" } },
            new object[] { "trustGain", "101", "nearOffer.trustGain", new[] { "\"nearOffer\"" } },
            new object[] { "correctTrustGain", "-1", "card.correctTrustGain", new[] { "\"card\"" } },
            new object[] { "correctTrustGain", "101", "card.correctTrustGain", new[] { "\"card\"" } },
            new object[] { "wrongTrustLoss", "-1", "card.wrongTrustLoss", new[] { "\"card\"" } },
            new object[] { "wrongTrustLoss", "101", "card.wrongTrustLoss", new[] { "\"card\"" } },
            new object[] { "wrongPatienceLoss", "-1", "card.wrongPatienceLoss", new[] { "\"card\"" } },
            new object[] { "reportLevelId", "\"\"", "card.reportLevelId", new[] { "\"card\"" } },
            new object[] { "reportPersuasionBonus", "-0.1", "card.reportPersuasionBonus", new[] { "\"card\"" } },
            new object[] { "reportPersuasionBonus", "1.1", "card.reportPersuasionBonus", new[] { "\"card\"" } },
            new object[] { "rejectFloorRatio", "0", "card.rejectFloorRatio", new[] { "\"card\"" } },
            new object[] { "rejectFloorRatio", "1.1", "card.rejectFloorRatio", new[] { "\"card\"" } },
            new object[] { "trust", "-1", "start.trust", new[] { "\"start\"" } },
            new object[] { "trust", "101", "start.trust", new[] { "\"start\"" } },
            new object[] { "trustSpread", "-1", "start.trustSpread", new[] { "\"start\"" } },
            new object[] { "trustSpread", "51", "start.trust", new[] { "\"start\"" } },
            new object[] { "rejectMoodSwing", "-0.01", "start.rejectMoodSwing", new[] { "\"start\"" } },
            new object[] { "rejectMoodSwing", "0.25", "start.rejectMoodSwing", new[] { "\"start\"" } },
            new object[] { "moodLowBelow", "-1", "view.moodLowBelow", new[] { "\"view\"" } },
            new object[] { "moodHighFrom", "101", "view.moodHighFrom", new[] { "\"view\"" } },
            new object[] { "moodLowBelow", "70", "view.mood", new[] { "\"view\"" } },
            new object[] { "patienceLowAtMost", "-1", "view.patienceLowAtMost", new[] { "\"view\"" } },
            new object[] { "patienceMediumAtMost", "1", "view.patience", new[] { "\"view\"" } }
        };

        [TestCaseSource(nameof(InvalidCases))]
        public void Validate_InvalidValue_IsReported(string property, string value, string field, string[] path)
        {
            AssertOneField(Validate(JsonEdit.Set(Good, property, value, path)), field);
        }

        [TestCase("baseShare", "0.0", "price")]
        [TestCase("ratio", "0.01", "insult")]
        [TestCase("trustLoss", "0", "insult")]
        [TestCase("trustLoss", "100", "insult")]
        [TestCase("trustGain", "100", "nearOffer")]
        [TestCase("ratio", "1.0", "nearOffer")]
        [TestCase("rejectFloorRatio", "1.0", "card")]
        [TestCase("reportPersuasionBonus", "0.0", "card")]
        [TestCase("reportPersuasionBonus", "1.0", "card")]
        [TestCase("trustSpread", "0", "start")]
        [TestCase("trustSpread", "50", "start")]
        [TestCase("rejectMoodSwing", "0.2", "start")]
        [TestCase("moodLowBelow", "0", "view")]
        [TestCase("moodHighFrom", "100", "view")]
        [TestCase("patienceLowAtMost", "0", "view")]
        public void Validate_BoundaryValues_AreAccepted(string property, string value, string section)
        {
            Assert.AreEqual(0, Validate(JsonEdit.Set(Good, property, value, "\"" + section + "\"")).Count);
        }


        private static string Shares(double b, double t, double u)
        {
            string json = JsonEdit.Set(Good, "baseShare", b.ToString(System.Globalization.CultureInfo.InvariantCulture), "\"price\"");
            json = JsonEdit.Set(json, "trustShare", t.ToString(System.Globalization.CultureInfo.InvariantCulture), "\"price\"");
            return JsonEdit.Set(json, "urgencyShare", u.ToString(System.Globalization.CultureInfo.InvariantCulture), "\"price\"");
        }

        [TestCase(1.0, 0.0, 0.0)]
        [TestCase(0.0, 0.0, 0.0)]
        [TestCase(0.0, 1.0, 0.0)]
        [TestCase(0.0, 0.0, 1.0)]
        [TestCase(0.5, 0.25, 0.25)]
        public void Validate_PriceShares_BoundaryCombinationsAreAccepted(double b, double t, double u)
        {
            Assert.AreEqual(0, Validate(Shares(b, t, u)).Count);
        }

        [TestCase(0.0, 1.01, 0.0, "price")]
        [TestCase(0.0, 0.0, 1.01, "price")]
        [TestCase(0.0, -0.01, 0.0, "price.trustShare")]
        [TestCase(0.0, 0.0, -0.01, "price.urgencyShare")]
        public void Validate_PriceShares_InvalidCombinationsAreReported(double b, double t, double u, string field)
        {
            AssertOneField(Validate(Shares(b, t, u)), field);
        }

        [TestCase("extraPatienceLoss", "0", "insult")]
        [TestCase("penaltyFromDay", "1", "insult")]
        [TestCase("trustGain", "0", "nearOffer")]
        [TestCase("correctTrustGain", "0", "card")]
        [TestCase("correctTrustGain", "100", "card")]
        [TestCase("wrongTrustLoss", "0", "card")]
        [TestCase("wrongTrustLoss", "100", "card")]
        [TestCase("wrongPatienceLoss", "0", "card")]
        [TestCase("rejectMoodSwing", "0", "start")]
        [TestCase("rejectMoodSwing", "0.0", "start")]
        public void Validate_MoreBoundaryValues_AreAccepted(string property, string value, string section)
        {
            Assert.AreEqual(0, Validate(JsonEdit.Set(Good, property, value, "\"" + section + "\"")).Count);
        }

        [Test]
        public void Validate_InsultRatioOfOne_IsReportedOnItsOwnField()
        {
            AssertOneField(Validate(JsonEdit.Set(Good, "ratio", "1.0", "\"insult\"")), "insult.ratio");
        }

        [Test]
        public void Validate_MoodThresholds_EdgeCombinations()
        {
            string view = "\"view\"";
            Assert.AreEqual(0, Validate(JsonEdit.Set(JsonEdit.Set(Good, "moodLowBelow", "0", view), "moodHighFrom", "1", view)).Count);
            Assert.AreEqual(0, Validate(JsonEdit.Set(JsonEdit.Set(Good, "moodLowBelow", "99", view), "moodHighFrom", "100", view)).Count);
            AssertOneField(Validate(JsonEdit.Set(JsonEdit.Set(Good, "moodLowBelow", "0", view), "moodHighFrom", "0", view)), "view.mood");
            AssertOneField(Validate(JsonEdit.Set(JsonEdit.Set(Good, "moodLowBelow", "100", view), "moodHighFrom", "100", view)), "view.mood");
            AssertOneField(Validate(JsonEdit.Set(JsonEdit.Set(Good, "moodLowBelow", "0", view), "moodHighFrom", "101", view)), "view.moodHighFrom");
            AssertOneField(Validate(JsonEdit.Set(JsonEdit.Set(Good, "moodLowBelow", "-1", view), "moodHighFrom", "100", view)), "view.moodLowBelow");
        }

        [Test]
        public void Validate_PriceSharesMayNotExceedOneInTotal_AndExactlyOneIsAllowed()
        {
            Assert.AreEqual(0, Validate(JsonEdit.Set(Good, "baseShare", "0.55", "\"price\"")).Count);
            AssertOneField(Validate(JsonEdit.Set(Good, "baseShare", "0.56", "\"price\"")), "price");
        }

        // ---------- gerçek dosya ----------

        [Test]
        public void RealFile_ParsesValidatesAndEqualsTheFixture()
        {
            string text = System.IO.File.ReadAllText(Path.Combine(TestPaths.ContentDataDirectory(), File));
            var issues = new List<ContentIssue>();

            NegotiationRules real = Parse(text, issues);
            ContentValidator.ValidateNegotiation(real, File, issues);

            Assert.AreEqual(0, issues.Count, string.Join("\n", issues));
            NegotiationRules fixture = ContentFixtures.Negotiation();
            Assert.AreEqual(fixture.PriceBaseShare, real.PriceBaseShare);
            Assert.AreEqual(fixture.PriceTrustShare, real.PriceTrustShare);
            Assert.AreEqual(fixture.PriceUrgencyShare, real.PriceUrgencyShare);
            Assert.AreEqual(fixture.InsultRatio, real.InsultRatio);
            Assert.AreEqual(fixture.InsultPenaltyFromDay, real.InsultPenaltyFromDay);
            Assert.AreEqual(fixture.NearOfferRatio, real.NearOfferRatio);
            Assert.AreEqual(fixture.RejectFloorRatio, real.RejectFloorRatio);
            Assert.AreEqual(fixture.StartTrust, real.StartTrust);
            Assert.AreEqual(fixture.MoodLowBelow, real.MoodLowBelow);
        }

        // ---------- ContentDatabase ----------

        [Test]
        public void Database_RequiresTheNegotiationFile()
        {
            DictionaryContentSource source = ContentFixtures.ValidSource();
            ContentLoadResult ok = ContentDatabase.Load(source);
            Assert.IsNotNull(ok.Database, string.Join("\n", ok.Issues));
            Assert.AreEqual(0.20, ok.Database.Negotiation.PriceBaseShare, 1e-12);

            var without = new DictionaryContentSource();
            foreach (string name in new[]
            {
                ContentFileNames.PhoneModels, ContentFileNames.IdManifest, ContentFileNames.ValueTables, ContentFileNames.ConditionProfiles,
                ContentFileNames.TransactionTypes, ContentFileNames.EconomyConstants, ContentFileNames.NpcProfiles, ContentFileNames.AppraisalLevels
            })
            {
                string text;
                Assert.IsTrue(source.TryGetText(name, out text));
                without.Add(name, text);
            }

            ContentLoadResult missing = ContentDatabase.Load(without);

            Assert.IsNull(missing.Database);
            Assert.IsTrue(missing.Issues.Any(i => i.Code == ContentIssueCodes.FileMissing && i.File == ContentFileNames.NegotiationRules));
        }

        [Test]
        public void Database_RejectsAReportLevelThatDoesNotExistInTheAppraisalLevels()
        {
            DictionaryContentSource source = ContentFixtures.ValidSource();
            source.Add(ContentFileNames.NegotiationRules, Good.Replace("\"reportLevelId\": \"s3\"", "\"reportLevelId\": \"s9\""));

            ContentLoadResult result = ContentDatabase.Load(source);

            Assert.IsNull(result.Database);
            Assert.IsTrue(result.Issues.Any(i => i.Code == ContentIssueCodes.NegotiationFieldInvalid && i.Message.Contains("s9")));
        }

        [Test]
        public void Database_InvalidRules_AreReportedAgainstTheirFile()
        {
            DictionaryContentSource source = ContentFixtures.ValidSource();
            source.Add(ContentFileNames.NegotiationRules, JsonEdit.Set(Good, "ratio", "0", "\"insult\""));

            ContentLoadResult result = ContentDatabase.Load(source);

            Assert.IsNull(result.Database);
            Assert.IsTrue(result.Issues.Any(i => i.File == ContentFileNames.NegotiationRules && i.Code == ContentIssueCodes.NegotiationFieldInvalid));
        }

        // ---------- NPC: yanlış kart çarpanı ----------

        [Test]
        public void Npc_WrongCardPenaltyMultiplier_IsOneUnlessTheNpcSaysOtherwise()
        {
            ContentDatabase content = MarketHarness.RealContent();

            Assert.AreEqual(2.0, content.GetNpc("npc.murat").Seller.WrongCardPenaltyMultiplier, 0.0);
            foreach (NpcDefinition npc in content.Npcs.Where(n => n.Id != "npc.murat"))
            {
                Assert.AreEqual(1.0, npc.Seller.WrongCardPenaltyMultiplier, 0.0, npc.Id);
            }
        }

        [TestCase("0.5")]
        [TestCase("0.99")]
        [TestCase("5.01")]
        public void Npc_WrongCardPenaltyMultiplier_MustBeBetweenOneAndFive(string value)
        {
            string json = ContentFixtures.NpcProfilesJson.Replace("\"persuasion\": 0.9, \"learningFriendly\": true },", "\"persuasion\": 0.9, \"learningFriendly\": true, \"wrongCardPenaltyMultiplier\": " + value + " },");
            var issues = new List<ContentIssue>();

            IReadOnlyList<NpcDefinition> npcs = ContentParser.ParseNpcs("npc_profiles.json", json, issues);
            ContentValidator.ValidateNpcs(npcs, "npc_profiles.json", issues);

            Assert.IsTrue(issues.Any(i => i.Message.Contains("wrongCardPenaltyMultiplier")), string.Join("\n", issues));
        }

        [TestCase("1.0")]
        [TestCase("5.0")]
        [TestCase("2.5")]
        public void Npc_WrongCardPenaltyMultiplier_BoundaryValuesAreAccepted(string value)
        {
            string json = ContentFixtures.NpcProfilesJson.Replace("\"persuasion\": 0.9, \"learningFriendly\": true },", "\"persuasion\": 0.9, \"learningFriendly\": true, \"wrongCardPenaltyMultiplier\": " + value + " },");
            var issues = new List<ContentIssue>();

            IReadOnlyList<NpcDefinition> npcs = ContentParser.ParseNpcs("npc_profiles.json", json, issues);
            ContentValidator.ValidateNpcs(npcs, "npc_profiles.json", issues);

            Assert.AreEqual(0, issues.Count, string.Join("\n", issues));
            Assert.AreEqual(double.Parse(value, System.Globalization.CultureInfo.InvariantCulture), npcs[0].Seller.WrongCardPenaltyMultiplier, 0.0);
        }
    }
}
