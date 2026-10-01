using System.Collections.Generic;
using System.Linq;
using Esnaf.Domain.Content;
using Esnaf.Domain.Negotiation;
using Esnaf.Domain.Npc;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Content
{
    /// <summary>Kişilik kataloğu (npc_profiles.json: personalityScale, personalities, npcs[].personalityId): ayrıştırma ve doğrulama.</summary>
    public class PersonalityContentTests
    {
        private const string File = "npc_profiles.json";

        private const string ScalesJson =
            "{ \"urgency\": { \"direction\": \"up\", \"medium\": 0.3, \"high\": 0.6 }," +
            " \"knowledge\": { \"direction\": \"down\", \"medium\": 0.12, \"high\": 0.06 }," +
            " \"budget\": { \"direction\": \"up\", \"medium\": 1.0, \"high\": 1.05 }," +
            " \"haggling\": { \"direction\": \"down\", \"medium\": 0.90, \"high\": 0.85 } }";

        private const string SellerJson =
            "{ \"availableFromDay\": 1, \"askMultiplier\": 1.10, \"rejectRatio\": 0.94, \"patience\": 4, \"valueSigma\": 0.05, \"urgency\": 0.3, \"persuasion\": 0.9 }";

        // Müşteri rolü: açılış 0.80 (pazarlık toleransı yüksek), değer oranı 1.00 (orta), sabır 4 (yüksek); satıcı σ 0.05 (bilgi yüksek), aciliyet 0.3 (orta)
        private const string CustomerJson = "{ \"openingOfferRatio\": 0.80, \"valueRatio\": 1.00, \"patience\": 4 }";

        private static string Npc(string id = "npc.one", string personalityId = null)
        {
            return "{ \"id\": \"" + id + "\", \"name\": \"Bir\", \"personality\": \"honest\"," +
                   (personalityId == null ? string.Empty : " \"personalityId\": \"" + personalityId + "\",") +
                   " \"seller\": " + SellerJson + ", \"customer\": " + CustomerJson + " }";
        }

        private static string Definition(string id = "haggler", string name = "Pazarlıkçı", string expects = "{ \"haggling\": [ \"high\" ] }")
        {
            return "{ \"id\": \"" + id + "\", \"name\": \"" + name + "\", \"expects\": " + expects + " }";
        }

        private static string FileOf(string scales, string personalities, params string[] npcs)
        {
            return "{ \"schemaVersion\": 1," +
                   (scales == null ? string.Empty : " \"personalityScale\": " + scales + ",") +
                   (personalities == null ? string.Empty : " \"personalities\": [" + personalities + "],") +
                   " \"npcs\": [" + string.Join(",", npcs) + "] }";
        }

        private static PersonalityCatalog Parse(string json, List<ContentIssue> issues)
        {
            return ContentParser.ParsePersonalities(File, json, issues);
        }

        private static List<ContentIssue> Validate(string json)
        {
            var issues = new List<ContentIssue>();
            PersonalityCatalog catalog = Parse(json, issues);
            Assert.IsNotNull(catalog, "Test verisi ayrıştırılamadı: " + string.Join("\n", issues));
            IReadOnlyList<NpcDefinition> npcs = ContentParser.ParseNpcs(File, json, issues);
            Assert.IsNotNull(npcs, string.Join("\n", issues));
            Assert.AreEqual(0, issues.Count, string.Join("\n", issues));
            ContentValidator.ValidatePersonalities(catalog, npcs, ContentFixtures.Negotiation(), File, issues);
            return issues;
        }

        private static void AssertOne(List<ContentIssue> issues, string code, string text)
        {
            Assert.AreEqual(1, issues.Count, string.Join("\n", issues.Select(i => i.ToString())));
            Assert.AreEqual(code, issues[0].Code);
            Assert.AreEqual(File, issues[0].File);
            StringAssert.Contains(text, issues[0].Message);
        }

        // ---------- ölçek ----------

        [TestCase(0.0, NegotiationLevel.Low)]
        [TestCase(0.29, NegotiationLevel.Low)]
        [TestCase(0.3, NegotiationLevel.Medium)]
        [TestCase(0.59, NegotiationLevel.Medium)]
        [TestCase(0.6, NegotiationLevel.High)]
        [TestCase(1.0, NegotiationLevel.High)]
        public void AnUpScale_RaisesTheLevelWithTheValue_BoundariesInclusive(double value, NegotiationLevel expected)
        {
            Assert.AreEqual(expected, new PersonalityScale(ScaleDirection.Up, 0.3, 0.6).LevelOf(value));
        }

        [TestCase(0.3, NegotiationLevel.Low)]
        [TestCase(0.13, NegotiationLevel.Low)]
        [TestCase(0.12, NegotiationLevel.Medium)]
        [TestCase(0.07, NegotiationLevel.Medium)]
        [TestCase(0.06, NegotiationLevel.High)]
        [TestCase(0.0, NegotiationLevel.High)]
        public void ADownScale_RaisesTheLevelAsTheValueFalls_BoundariesInclusive(double value, NegotiationLevel expected)
        {
            Assert.AreEqual(expected, new PersonalityScale(ScaleDirection.Down, 0.12, 0.06).LevelOf(value));
        }

        // ---------- ayrıştırma ----------

        [Test]
        public void Parse_WithoutTheSections_GivesAnEmptyCatalog_AndNoIssue()
        {
            var issues = new List<ContentIssue>();

            PersonalityCatalog catalog = Parse(FileOf(null, null, Npc()), issues);

            Assert.AreEqual(0, issues.Count, string.Join("\n", issues));
            Assert.IsTrue(catalog.IsEmpty);
            Assert.IsNull(catalog.Scales);
            Assert.AreEqual(0, catalog.Definitions.Count);
        }

        [Test]
        public void Parse_ReadsTheScalesAndTheDefinitions()
        {
            var issues = new List<ContentIssue>();
            string expects = "{ \"haggling\": [ \"high\" ], \"urgency\": [ \"low\", \"medium\" ], \"patience\": [ \"high\" ], \"knowledge\": [ \"medium\" ], \"budget\": [ \"low\" ] }";

            PersonalityCatalog catalog = Parse(FileOf(ScalesJson, Definition(expects: expects) + "," + Definition("easygoing", "Rahat/samimi", "{ \"urgency\": [ \"low\" ] }"), Npc(personalityId: "haggler")), issues);

            Assert.AreEqual(0, issues.Count, string.Join("\n", issues));
            Assert.IsFalse(catalog.IsEmpty);
            Assert.AreEqual(ScaleDirection.Up, catalog.Scales.Urgency.Direction);
            Assert.AreEqual(0.3, catalog.Scales.Urgency.Medium, 1e-12);
            Assert.AreEqual(0.6, catalog.Scales.Urgency.High, 1e-12);
            Assert.AreEqual(ScaleDirection.Down, catalog.Scales.Knowledge.Direction);
            Assert.AreEqual(0.12, catalog.Scales.Knowledge.Medium, 1e-12);
            Assert.AreEqual(0.06, catalog.Scales.Knowledge.High, 1e-12);
            Assert.AreEqual(ScaleDirection.Up, catalog.Scales.Budget.Direction);
            Assert.AreEqual(1.0, catalog.Scales.Budget.Medium, 1e-12);
            Assert.AreEqual(1.05, catalog.Scales.Budget.High, 1e-12);
            Assert.AreEqual(ScaleDirection.Down, catalog.Scales.Haggling.Direction);
            Assert.AreEqual(0.90, catalog.Scales.Haggling.Medium, 1e-12);
            Assert.AreEqual(0.85, catalog.Scales.Haggling.High, 1e-12);
            Assert.AreEqual(2, catalog.Definitions.Count);
            PersonalityDefinition haggler = catalog.Definitions[0];
            Assert.AreEqual("haggler", haggler.Id);
            Assert.AreEqual("Pazarlıkçı", haggler.Name);
            Assert.IsTrue(haggler.Accepts(CustomerTrait.Haggling, NegotiationLevel.High));
            Assert.IsFalse(haggler.Accepts(CustomerTrait.Haggling, NegotiationLevel.Medium));
            Assert.IsTrue(haggler.Accepts(CustomerTrait.Urgency, NegotiationLevel.Low));
            Assert.IsTrue(haggler.Accepts(CustomerTrait.Urgency, NegotiationLevel.Medium));
            Assert.IsFalse(haggler.Accepts(CustomerTrait.Urgency, NegotiationLevel.High));
            Assert.IsTrue(haggler.Accepts(CustomerTrait.Patience, NegotiationLevel.High));
            Assert.IsFalse(haggler.Accepts(CustomerTrait.Patience, NegotiationLevel.Low));
            Assert.IsTrue(haggler.Accepts(CustomerTrait.Knowledge, NegotiationLevel.Medium));
            Assert.IsFalse(haggler.Accepts(CustomerTrait.Budget, NegotiationLevel.High));
            Assert.IsTrue(catalog.Definitions[1].Accepts(CustomerTrait.Budget, NegotiationLevel.High), "sınırlanmayan özellik her düzeyi kabul eder");
            PersonalityDefinition found;
            Assert.IsTrue(catalog.TryGet("easygoing", out found));
            Assert.AreEqual("Rahat/samimi", found.Name);
            Assert.IsFalse(catalog.TryGet("nobody", out found));
        }

        [Test]
        public void ParseNpcs_ReadsThePersonalityId()
        {
            var issues = new List<ContentIssue>();

            IReadOnlyList<NpcDefinition> npcs = ContentParser.ParseNpcs(File, FileOf(ScalesJson, Definition(), Npc(personalityId: "haggler"), Npc("npc.two")), issues);

            Assert.AreEqual(0, issues.Count, string.Join("\n", issues));
            Assert.AreEqual("haggler", npcs[0].PersonalityId);
            Assert.IsNull(npcs[1].PersonalityId);
        }

        [TestCase("\"direction\": \"up\"", "\"direction\": \"sideways\"", "direction")]
        public void Parse_AnUnknownScaleDirection_IsAnError(string from, string to, string text)
        {
            var issues = new List<ContentIssue>();

            Assert.IsNull(Parse(FileOf(ScalesJson.Replace(from, to), Definition(), Npc()), issues));

            Assert.GreaterOrEqual(issues.Count, 1, string.Join("\n", issues)); // her ölçekte aynı hata bildirilir
            StringAssert.Contains(text, issues[0].Message);
        }

        [TestCase("{ \"haggling\": [ \"extreme\" ] }")]
        [TestCase("{ \"haggling\": [ ] }")]
        [TestCase("{ \"charisma\": [ \"high\" ] }")]
        public void Parse_AnUnusableExpectation_IsAnError(string expects)
        {
            var issues = new List<ContentIssue>();

            Assert.IsNull(Parse(FileOf(ScalesJson, Definition(expects: expects), Npc()), issues));

            Assert.GreaterOrEqual(issues.Count, 1);
        }

        [Test]
        public void Parse_PersonalitiesWithoutScales_OrScalesWithoutPersonalities_IsAnError()
        {
            var a = new List<ContentIssue>();
            var b = new List<ContentIssue>();

            Assert.IsNull(Parse(FileOf(null, Definition(), Npc()), a));
            Assert.IsNull(Parse(FileOf(ScalesJson, null, Npc()), b));

            StringAssert.Contains("personalityScale", a[0].Message);
            StringAssert.Contains("personalities", b[0].Message);
        }

        [Test]
        public void Parse_AMissingScale_IsAnError()
        {
            var issues = new List<ContentIssue>();
            string withoutBudget = ScalesJson.Replace(" \"budget\": { \"direction\": \"up\", \"medium\": 1.0, \"high\": 1.05 },", string.Empty);

            Assert.IsNull(Parse(FileOf(withoutBudget, Definition(), Npc()), issues));

            StringAssert.Contains("personalityScale.budget", issues[0].Message);
        }

        // ---------- doğrulama ----------

        [Test]
        public void Validate_ACoherentCatalog_HasNoIssue()
        {
            Assert.AreEqual(0, Validate(FileOf(ScalesJson, Definition(), Npc(personalityId: "haggler"))).Count);
        }

        [Test]
        public void Validate_NoCatalogAndNoPersonalityIds_HasNoIssue()
        {
            Assert.AreEqual(0, Validate(FileOf(null, null, Npc())).Count);
        }

        [TestCase("Haggler")]
        [TestCase("1haggler")]
        [TestCase("hag gler")]
        [TestCase("")]
        public void Validate_APersonalityIdMustLookLikeAnId(string id)
        {
            List<ContentIssue> issues = Validate(FileOf(ScalesJson, Definition(id: id), Npc()));

            AssertOne(issues, ContentIssueCodes.PersonalityFieldInvalid, "id");
        }

        [Test]
        public void Validate_ADuplicatePersonalityId_IsAnError()
        {
            List<ContentIssue> issues = Validate(FileOf(ScalesJson, Definition() + "," + Definition(), Npc(personalityId: "haggler")));

            AssertOne(issues, ContentIssueCodes.PersonalityIdDuplicate, "haggler");
        }

        [Test]
        public void Validate_AnEmptyPersonalityName_IsAnError()
        {
            List<ContentIssue> issues = Validate(FileOf(ScalesJson, Definition(name: "  "), Npc(personalityId: "haggler")));

            AssertOne(issues, ContentIssueCodes.PersonalityFieldInvalid, "name");
        }

        [TestCase("up", 0.6, 0.3)]
        [TestCase("down", 0.06, 0.12)]
        public void Validate_AScaleMustRiseTowardsHigh(string direction, double medium, double high)
        {
            string scale = "{ \"urgency\": { \"direction\": \"" + direction + "\", \"medium\": " + medium.ToString(System.Globalization.CultureInfo.InvariantCulture) + ", \"high\": " + high.ToString(System.Globalization.CultureInfo.InvariantCulture) + " }," +
                           " \"knowledge\": { \"direction\": \"down\", \"medium\": 0.12, \"high\": 0.06 }, \"budget\": { \"direction\": \"up\", \"medium\": 1.0, \"high\": 1.05 }," +
                           " \"haggling\": { \"direction\": \"down\", \"medium\": 0.90, \"high\": 0.85 } }";

            List<ContentIssue> issues = Validate(FileOf(scale, Definition(), Npc(personalityId: "haggler")));

            AssertOne(issues, ContentIssueCodes.PersonalityFieldInvalid, "urgency");
        }

        [Test]
        public void Validate_AnUnknownPersonalityOnAnNpc_IsAnError()
        {
            List<ContentIssue> issues = Validate(FileOf(ScalesJson, Definition(), Npc(personalityId: "ghost")));

            AssertOne(issues, ContentIssueCodes.PersonalityUnknown, "ghost");
        }

        [Test]
        public void Validate_AnNpcWithoutAPersonality_WhenTheCatalogExists_IsAnError()
        {
            List<ContentIssue> issues = Validate(FileOf(ScalesJson, Definition(), Npc(personalityId: "haggler"), Npc("npc.two")));

            AssertOne(issues, ContentIssueCodes.PersonalityMissing, "npc.two");
        }

        [Test]
        public void Validate_APersonalityOnAnNpc_WithoutACatalog_IsAnError()
        {
            List<ContentIssue> issues = Validate(FileOf(null, null, Npc(personalityId: "haggler")));

            AssertOne(issues, ContentIssueCodes.PersonalityUnknown, "haggler");
        }

        [TestCase("{ \"haggling\": [ \"low\" ] }", "haggling")]
        [TestCase("{ \"knowledge\": [ \"low\" ] }", "knowledge")]
        [TestCase("{ \"patience\": [ \"low\", \"medium\" ] }", "patience")]
        [TestCase("{ \"urgency\": [ \"high\" ] }", "urgency")]
        [TestCase("{ \"budget\": [ \"high\" ] }", "budget")]
        public void Validate_AnNpcWhoseRealParametersContradictThePersonality_IsAnError(string expects, string trait)
        {
            // Kişilik gerçek mekaniğe bağlıdır: NPC'nin sıfır değerleri (açılış 0.80, sabır 4, σ 0.05, aciliyet 0.3, değer oranı 1.00) arketipin beklentisine uymalı.
            List<ContentIssue> issues = Validate(FileOf(ScalesJson, Definition(expects: expects), Npc(personalityId: "haggler")));

            AssertOne(issues, ContentIssueCodes.PersonalityInconsistent, trait);
        }

        // ---------- mutation sonrası ek denetimler ----------

        [Test]
        public void TryGet_WithANullId_FindsNothing()
        {
            var issues = new List<ContentIssue>();
            PersonalityCatalog catalog = Parse(FileOf(ScalesJson, Definition(), Npc(personalityId: "haggler")), issues);
            PersonalityDefinition found;

            Assert.IsFalse(catalog.TryGet(null, out found));
            Assert.IsNull(found);
        }

        [TestCase("up", 0.6, 0.6, "'up'")]
        [TestCase("down", 0.06, 0.06, "'down'")]
        [TestCase("up", 0.6, 0.5, "'up'")]
        [TestCase("down", 0.06, 0.07, "'down'")]
        public void Validate_AScaleWhoseThresholdsDoNotRiseStrictly_IsAnErrorNamingItsDirection(string direction, double medium, double high, string directionText)
        {
            string scale = "{ \"urgency\": { \"direction\": \"" + direction + "\", \"medium\": " + medium.ToString(System.Globalization.CultureInfo.InvariantCulture) + ", \"high\": " + high.ToString(System.Globalization.CultureInfo.InvariantCulture) + " }," +
                           " \"knowledge\": { \"direction\": \"down\", \"medium\": 0.12, \"high\": 0.06 }, \"budget\": { \"direction\": \"up\", \"medium\": 1.0, \"high\": 1.05 }," +
                           " \"haggling\": { \"direction\": \"down\", \"medium\": 0.90, \"high\": 0.85 } }";

            List<ContentIssue> issues = Validate(FileOf(scale, Definition(), Npc(personalityId: "haggler")));

            AssertOne(issues, ContentIssueCodes.PersonalityFieldInvalid, directionText);
        }

        [Test]
        public void Parse_BrokenJson_OrAMissingSchemaVersion_IsAnErrorNotACrash()
        {
            var broken = new List<ContentIssue>();
            var noVersion = new List<ContentIssue>();

            Assert.IsNull(Parse("{ not json", broken));
            Assert.IsNull(Parse("{ \"npcs\": [] }", noVersion));

            Assert.AreEqual(1, broken.Count);
            Assert.AreEqual(1, noVersion.Count);
            Assert.AreEqual(ContentIssueCodes.SchemaVersionMissing, noVersion[0].Code);
        }
    }
}
