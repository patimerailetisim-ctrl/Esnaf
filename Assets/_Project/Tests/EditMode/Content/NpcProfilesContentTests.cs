using System.Collections.Generic;
using System.Linq;
using Esnaf.Domain.Content;
using Esnaf.Domain.Npc;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Content
{
    public class NpcProfilesContentTests
    {
        private const string File = "npc_profiles.json";

        private const string SellerJson =
            "{ \"availableFromDay\": 1, \"askMultiplier\": 1.10, \"rejectRatio\": 0.94, \"patience\": 4, \"valueSigma\": 0.05," +
            " \"valueBias\": -0.02, \"urgency\": 0.3, \"persuasion\": 0.9, \"concealChance\": 0.25, \"learningFriendly\": true," +
            " \"urgentLabelFromDay\": 6 }";

        private const string CustomerJson =
            "{ \"openingOfferRatio\": 0.90, \"valueRatio\": 1.00, \"patience\": 4, \"valueSigma\": 0.02, \"packageRatio\": 1.12 }";

        private static string Npc(string id = "npc.one", string name = "Bir", string personality = "honest", string seller = SellerJson, string customer = CustomerJson)
        {
            return "{ \"id\": \"" + id + "\", \"name\": \"" + name + "\", \"personality\": \"" + personality + "\"," +
                   " \"seller\": " + seller + ", \"customer\": " + customer + " }";
        }

        private static string FileOf(params string[] npcs)
        {
            return "{ \"schemaVersion\": 1, \"npcs\": [" + string.Join(",", npcs) + "] }";
        }

        private static IReadOnlyList<NpcDefinition> Parse(string json, List<ContentIssue> issues)
        {
            return ContentParser.ParseNpcs(File, json, issues);
        }

        private static List<ContentIssue> Validate(string json)
        {
            var issues = new List<ContentIssue>();
            IReadOnlyList<NpcDefinition> npcs = Parse(json, issues);
            Assert.IsNotNull(npcs, "Test verisi ayrıştırılamadı: " + string.Join("\n", issues));
            Assert.AreEqual(0, issues.Count, "Ayrıştırma sorunu: " + string.Join("\n", issues));
            ContentValidator.ValidateNpcs(npcs, File, issues);
            return issues;
        }

        private static void AssertOneField(List<ContentIssue> issues, string field)
        {
            Assert.AreEqual(1, issues.Count, string.Join("\n", issues.Select(i => i.ToString())));
            Assert.AreEqual(ContentIssueCodes.NpcFieldInvalid, issues[0].Code);
            Assert.AreEqual(File, issues[0].File);
            StringAssert.Contains(field, issues[0].Message);
        }

        // ---------- ayrıştırma ----------

        [Test]
        public void Parse_AllFields_AreRead()
        {
            var issues = new List<ContentIssue>();

            IReadOnlyList<NpcDefinition> npcs = Parse(FileOf(Npc()), issues);

            Assert.AreEqual(0, issues.Count, string.Join("\n", issues));
            Assert.AreEqual(1, npcs.Count);
            NpcDefinition n = npcs[0];
            Assert.AreEqual("npc.one", n.Id);
            Assert.AreEqual("Bir", n.Name);
            Assert.AreEqual("honest", n.Personality);
            Assert.AreEqual(1, n.Seller.AvailableFromDay);
            Assert.AreEqual(1.10, n.Seller.AskMultiplier, 1e-12);
            Assert.AreEqual(0.94, n.Seller.RejectRatio, 1e-12);
            Assert.AreEqual(4, n.Seller.Patience);
            Assert.AreEqual(0.05, n.Seller.ValueSigma, 1e-12);
            Assert.AreEqual(-0.02, n.Seller.ValueBias, 1e-12);
            Assert.AreEqual(0.3, n.Seller.Urgency, 1e-12);
            Assert.AreEqual(0.9, n.Seller.Persuasion, 1e-12);
            Assert.AreEqual(0.25, n.Seller.ConcealChance, 1e-12);
            Assert.IsTrue(n.Seller.LearningFriendly);
            Assert.AreEqual(6, n.Seller.UrgentLabelFromDay);
            Assert.AreEqual(0.90, n.Customer.OpeningOfferRatio, 1e-12);
            Assert.AreEqual(1.00, n.Customer.ValueRatio, 1e-12);
            Assert.AreEqual(4, n.Customer.Patience);
            Assert.AreEqual(0.02, n.Customer.ValueSigma, 1e-12);
            Assert.AreEqual(1.12, n.Customer.PackageRatio, 1e-12);
        }

        [Test]
        public void Parse_OptionalFields_HaveNeutralDefaults()
        {
            string seller = "{ \"availableFromDay\": 2, \"askMultiplier\": 1.2, \"rejectRatio\": 0.9, \"patience\": 3, \"valueSigma\": 0.1, \"urgency\": 0.2, \"persuasion\": 0.6 }";
            string customer = "{ \"openingOfferRatio\": 0.8, \"valueRatio\": 1.0, \"patience\": 5 }";
            var issues = new List<ContentIssue>();

            IReadOnlyList<NpcDefinition> npcs = Parse(FileOf(Npc(seller: seller, customer: customer)), issues);

            Assert.AreEqual(0, issues.Count, string.Join("\n", issues));
            Assert.AreEqual(0.0, npcs[0].Seller.ValueBias);
            Assert.AreEqual(0.0, npcs[0].Seller.ConcealChance);
            Assert.IsFalse(npcs[0].Seller.LearningFriendly);
            Assert.IsNull(npcs[0].Seller.UrgentLabelFromDay);
            Assert.AreEqual(0.0, npcs[0].Customer.ValueSigma);
            Assert.AreEqual(1.0, npcs[0].Customer.PackageRatio);
        }

        private static readonly object[][] MissingSellerFields =
        {
            new object[] { "\"availableFromDay\": 1, ", "seller.availableFromDay" },
            new object[] { "\"askMultiplier\": 1.10, ", "seller.askMultiplier" },
            new object[] { "\"rejectRatio\": 0.94, ", "seller.rejectRatio" },
            new object[] { "\"patience\": 4, ", "seller.patience" },
            new object[] { "\"valueSigma\": 0.05,", "seller.valueSigma" },
            new object[] { "\"urgency\": 0.3, ", "seller.urgency" },
            new object[] { "\"persuasion\": 0.9, ", "seller.persuasion" }
        };

        [TestCaseSource(nameof(MissingSellerFields))]
        public void Parse_EveryRequiredSellerField_IsEnforced(string find, string field)
        {
            Assert.IsTrue(SellerJson.Contains(find), "Test verisi bulunamadı: " + find);
            var issues = new List<ContentIssue>();

            Assert.IsNull(Parse(FileOf(Npc(seller: SellerJson.Replace(find, ""))), issues));

            Assert.IsTrue(
                issues.Any(i => i.Code == ContentIssueCodes.FieldMissing && i.Message.Contains("'" + field + "'")),
                "Eksik alan '" + field + "' raporlanmadı:\n" + string.Join("\n", issues));
        }

        private static readonly object[][] MissingCustomerFields =
        {
            new object[] { "\"openingOfferRatio\": 0.90, ", "customer.openingOfferRatio" },
            new object[] { "\"valueRatio\": 1.00, ", "customer.valueRatio" },
            new object[] { "\"patience\": 4, ", "customer.patience" }
        };

        [TestCaseSource(nameof(MissingCustomerFields))]
        public void Parse_EveryRequiredCustomerField_IsEnforced(string find, string field)
        {
            Assert.IsTrue(CustomerJson.Contains(find), "Test verisi bulunamadı: " + find);
            var issues = new List<ContentIssue>();

            Assert.IsNull(Parse(FileOf(Npc(customer: CustomerJson.Replace(find, ""))), issues));

            Assert.IsTrue(
                issues.Any(i => i.Code == ContentIssueCodes.FieldMissing && i.Message.Contains("'" + field + "'")),
                "Eksik alan '" + field + "' raporlanmadı:\n" + string.Join("\n", issues));
        }

        [Test]
        public void Parse_MissingRolesAndTopLevelFields_AreEnforced()
        {
            string[][] cases =
            {
                new[] { "seller", "{ \"id\": \"npc.one\", \"name\": \"Bir\", \"personality\": \"honest\", \"seller\": null, \"customer\": " + CustomerJson + " }" },
                new[] { "customer", "{ \"id\": \"npc.one\", \"name\": \"Bir\", \"personality\": \"honest\", \"seller\": " + SellerJson + ", \"customer\": null }" },
                new[] { "id", "{ \"name\": \"Bir\", \"personality\": \"honest\", \"seller\": " + SellerJson + ", \"customer\": " + CustomerJson + " }" },
                new[] { "name", "{ \"id\": \"npc.one\", \"personality\": \"honest\", \"seller\": " + SellerJson + ", \"customer\": " + CustomerJson + " }" },
                new[] { "personality", "{ \"id\": \"npc.one\", \"name\": \"Bir\", \"seller\": " + SellerJson + ", \"customer\": " + CustomerJson + " }" }
            };

            foreach (string[] c in cases)
            {
                var issues = new List<ContentIssue>();

                Assert.IsNull(Parse(FileOf(c[1]), issues), c[0]);

                Assert.IsTrue(
                    issues.Any(i => i.Code == ContentIssueCodes.FieldMissing && i.Message.Contains("'" + c[0] + "'")),
                    "Eksik alan '" + c[0] + "' raporlanmadı:\n" + string.Join("\n", issues));
            }
        }

        [Test]
        public void Parse_MissingNpcsList_IsReported()
        {
            var issues = new List<ContentIssue>();

            Assert.IsNull(Parse("{ \"schemaVersion\": 1 }", issues));

            Assert.IsTrue(issues.Any(i => i.Code == ContentIssueCodes.FieldMissing && i.Message.Contains("npcs")));
        }

        [Test]
        public void Parse_UnknownField_IsReported()
        {
            var issues = new List<ContentIssue>();

            Assert.IsNull(Parse(FileOf(Npc(seller: SellerJson.Replace("rejectRatio", "rejectRatoi"))), issues));

            Assert.AreEqual(ContentIssueCodes.FileSyntax, issues.Single().Code);
        }

        [Test]
        public void Parse_WrongSchemaVersion_IsReported()
        {
            var issues = new List<ContentIssue>();

            Assert.IsNull(Parse(FileOf(Npc()).Replace("\"schemaVersion\": 1", "\"schemaVersion\": 9"), issues));

            Assert.AreEqual(ContentIssueCodes.SchemaVersionUnsupported, issues.Single().Code);
        }

        [Test]
        public void Parse_FixtureNpcs_KeepFileOrder()
        {
            IReadOnlyList<NpcDefinition> npcs = ContentFixtures.Npcs();

            CollectionAssert.AreEqual(
                new[] { "npc.test_honest", "npc.test_hurried", "npc.test_liar" }, npcs.Select(n => n.Id).ToArray());
            Assert.AreEqual(0.6, npcs[2].Seller.ConcealChance, 1e-12);
        }

        // ---------- doğrulama ----------

        [Test]
        public void Validate_Good_HasNoIssues()
        {
            Assert.AreEqual(0, Validate(FileOf(Npc())).Count);
            Assert.AreEqual(0, Validate(ContentFixtures.NpcProfilesJson).Count);
        }

        [Test]
        public void Validate_EmptyList_IsReported()
        {
            List<ContentIssue> issues = Validate(FileOf());

            Assert.AreEqual(ContentIssueCodes.NpcListEmpty, issues.Single().Code);
        }

        [TestCase("npc.Bad")]
        [TestCase("kemal")]
        [TestCase("phone.kemal")]
        [TestCase("npc.")]
        [TestCase("npc.1abc")]
        [TestCase("")]
        public void Validate_BadIdFormat_IsReported(string id)
        {
            List<ContentIssue> issues = Validate(FileOf(Npc(id: id)));

            Assert.AreEqual(ContentIssueCodes.NpcIdFormat, issues.Single().Code, string.Join("\n", issues));
        }

        [Test]
        public void Validate_DuplicateId_IsReported()
        {
            List<ContentIssue> issues = Validate(FileOf(Npc(id: "npc.a"), Npc(id: "npc.a")));

            Assert.AreEqual(ContentIssueCodes.NpcIdDuplicate, issues.Single().Code);
        }

        [Test]
        public void Validate_EmptyNameOrPersonality_IsReported()
        {
            AssertOneField(Validate(FileOf(Npc(name: " "))), "name");
            AssertOneField(Validate(FileOf(Npc(personality: ""))), "personality");
        }

        private static readonly object[][] BadSellerValues =
        {
            new object[] { "\"availableFromDay\": 1", "\"availableFromDay\": 0", "seller.availableFromDay" },
            new object[] { "\"askMultiplier\": 1.10", "\"askMultiplier\": 0", "seller.askMultiplier" },
            new object[] { "\"askMultiplier\": 1.10", "\"askMultiplier\": -1.10", "seller.askMultiplier" },
            new object[] { "\"rejectRatio\": 0.94", "\"rejectRatio\": 0", "seller.rejectRatio" },
            new object[] { "\"rejectRatio\": 0.94", "\"rejectRatio\": 1.05", "seller.rejectRatio" },
            new object[] { "\"rejectRatio\": 0.94", "\"rejectRatio\": 1.10", "seller.rejectRatio" },
            new object[] { "\"rejectRatio\": 0.94", "\"rejectRatio\": 1.20", "seller.rejectRatio" },
            new object[] { "\"patience\": 4", "\"patience\": 0", "seller.patience" },
            new object[] { "\"valueSigma\": 0.05", "\"valueSigma\": -0.01", "seller.valueSigma" },
            new object[] { "\"valueSigma\": 0.05", "\"valueSigma\": 1.0", "seller.valueSigma" },
            new object[] { "\"valueBias\": -0.02", "\"valueBias\": -0.06", "seller.valueBias" },
            new object[] { "\"valueBias\": -0.02", "\"valueBias\": 0.06", "seller.valueBias" },
            new object[] { "\"urgency\": 0.3", "\"urgency\": -0.1", "seller.urgency" },
            new object[] { "\"urgency\": 0.3", "\"urgency\": 1.1", "seller.urgency" },
            new object[] { "\"persuasion\": 0.9", "\"persuasion\": -0.1", "seller.persuasion" },
            new object[] { "\"persuasion\": 0.9", "\"persuasion\": 1.1", "seller.persuasion" },
            new object[] { "\"concealChance\": 0.25", "\"concealChance\": -0.1", "seller.concealChance" },
            new object[] { "\"concealChance\": 0.25", "\"concealChance\": 1.1", "seller.concealChance" },
            new object[] { "\"urgentLabelFromDay\": 6", "\"urgentLabelFromDay\": 0", "seller.urgentLabelFromDay" }
        };

        [TestCaseSource(nameof(BadSellerValues))]
        public void Validate_BadSellerValue_IsReported(string find, string replacement, string field)
        {
            Assert.IsTrue(SellerJson.Contains(find), "Test verisi bulunamadı: " + find);

            AssertOneField(Validate(FileOf(Npc(seller: SellerJson.Replace(find, replacement)))), field);
        }

        [Test]
        public void Validate_RejectRatioEqualToAskMultiplier_IsReported()
        {
            string seller = SellerJson.Replace("\"askMultiplier\": 1.10", "\"askMultiplier\": 0.94");

            AssertOneField(Validate(FileOf(Npc(seller: seller))), "seller.rejectRatio");
        }

        [Test]
        public void Validate_ZeroSigmaWithZeroBias_IsAccepted()
        {
            string seller = SellerJson.Replace("\"valueSigma\": 0.05", "\"valueSigma\": 0").Replace("\"valueBias\": -0.02", "\"valueBias\": 0");

            Assert.AreEqual(0, Validate(FileOf(Npc(seller: seller))).Count);
        }

        private static readonly object[][] BoundarySellerValues =
        {
            new object[] { "\"availableFromDay\": 1", "\"availableFromDay\": 1" },
            new object[] { "\"rejectRatio\": 0.94", "\"rejectRatio\": 1" },
            new object[] { "\"patience\": 4", "\"patience\": 1" },
            new object[] { "\"valueSigma\": 0.05", "\"valueSigma\": 0.999" },
            new object[] { "\"valueBias\": -0.02", "\"valueBias\": -0.05" },
            new object[] { "\"valueBias\": -0.02", "\"valueBias\": 0.05" },
            new object[] { "\"urgency\": 0.3", "\"urgency\": 0" },
            new object[] { "\"urgency\": 0.3", "\"urgency\": 1" },
            new object[] { "\"persuasion\": 0.9", "\"persuasion\": 0" },
            new object[] { "\"persuasion\": 0.9", "\"persuasion\": 1" },
            new object[] { "\"concealChance\": 0.25", "\"concealChance\": 0" },
            new object[] { "\"concealChance\": 0.25", "\"concealChance\": 1" },
            new object[] { "\"urgentLabelFromDay\": 6", "\"urgentLabelFromDay\": 1" }
        };

        [TestCaseSource(nameof(BoundarySellerValues))]
        public void Validate_SellerBoundaryValues_AreAccepted(string find, string replacement)
        {
            Assert.IsTrue(SellerJson.Contains(find), "Test verisi bulunamadı: " + find);

            Assert.AreEqual(0, Validate(FileOf(Npc(seller: SellerJson.Replace(find, replacement)))).Count);
        }

        private static readonly object[][] BadCustomerValues =
        {
            new object[] { "\"openingOfferRatio\": 0.90", "\"openingOfferRatio\": 0", "customer.openingOfferRatio" },
            new object[] { "\"openingOfferRatio\": 0.90", "\"openingOfferRatio\": 1.01", "customer.openingOfferRatio" },
            new object[] { "\"valueRatio\": 1.00", "\"valueRatio\": 0", "customer.valueRatio" },
            new object[] { "\"valueRatio\": 1.00", "\"valueRatio\": 1.26", "customer.valueRatio" },
            new object[] { "\"patience\": 4", "\"patience\": 0", "customer.patience" },
            new object[] { "\"valueSigma\": 0.02", "\"valueSigma\": -0.01", "customer.valueSigma" },
            new object[] { "\"valueSigma\": 0.02", "\"valueSigma\": 1.0", "customer.valueSigma" },
            new object[] { "\"packageRatio\": 1.12", "\"packageRatio\": 0.99", "customer.packageRatio" }
        };

        [TestCaseSource(nameof(BadCustomerValues))]
        public void Validate_BadCustomerValue_IsReported(string find, string replacement, string field)
        {
            Assert.IsTrue(CustomerJson.Contains(find), "Test verisi bulunamadı: " + find);

            AssertOneField(Validate(FileOf(Npc(customer: CustomerJson.Replace(find, replacement)))), field);
        }

        private static readonly object[][] BoundaryCustomerValues =
        {
            new object[] { "\"openingOfferRatio\": 0.90", "\"openingOfferRatio\": 1" },
            new object[] { "\"valueRatio\": 1.00", "\"valueRatio\": 1.25" },
            new object[] { "\"patience\": 4", "\"patience\": 1" },
            new object[] { "\"valueSigma\": 0.02", "\"valueSigma\": 0" },
            new object[] { "\"packageRatio\": 1.12", "\"packageRatio\": 1" }
        };

        [TestCaseSource(nameof(BoundaryCustomerValues))]
        public void Validate_CustomerBoundaryValues_AreAccepted(string find, string replacement)
        {
            Assert.IsTrue(CustomerJson.Contains(find), "Test verisi bulunamadı: " + find);

            Assert.AreEqual(0, Validate(FileOf(Npc(customer: CustomerJson.Replace(find, replacement)))).Count);
        }

        // ---------- gerçek dosya: GDD v0.2 6.1 / 6.2 sayıları ----------

        private struct Row
        {
            public string Id;
            public string Name;
            public int From;
            public double Ask, Reject, Sigma, Urgency, Persuasion;
            public int Patience;
            public double Open, MRatio;
            public int CustPatience;
        }

        private static Row R(string id, string name, int from, double ask, double reject, int patience, double sigma, double urgency, double persuasion, double open, double mRatio, int custPatience)
        {
            return new Row
            {
                Id = id, Name = name, From = from, Ask = ask, Reject = reject, Patience = patience, Sigma = sigma,
                Urgency = urgency, Persuasion = persuasion, Open = open, MRatio = mRatio, CustPatience = custPatience
            };
        }

        private static readonly Row[] Gdd =
        {
            R("npc.kemal", "Kemal Abi", 2, 1.20, 0.96, 5, 0.08, 0.2, 0.6, 0.80, 1.00, 5),
            R("npc.selin", "Selin", 2, 1.05, 0.82, 3, 0.12, 0.9, 0.9, 0.92, 1.00, 3),
            R("npc.murat", "Dr. Murat", 4, 1.08, 0.97, 4, 0.03, 0.3, 1.0, 0.90, 0.97, 4),
            R("npc.hatice", "Hatice Teyze", 3, 1.10, 0.92, 4, 0.30, 0.4, 0.8, 0.85, 1.00, 4),
            R("npc.berk", "Berk", 6, 1.15, 0.98, 3, 0.06, 0.1, 0.5, 0.90, 1.10, 3),
            R("npc.ozan", "Ozan", 2, 1.05, 0.90, 2, 0.10, 0.5, 0.7, 0.88, 1.00, 2),
            R("npc.riza", "Rıza Bey", 4, 1.10, 0.95, 4, 0.08, 0.3, 0.5, 0.82, 0.95, 4),
            R("npc.nermin", "Nermin", 6, 1.25, 0.98, 5, 0.04, 0.1, 0.6, 0.88, 1.02, 5),
            R("npc.cengiz", "Cengiz", 5, 1.02, 0.78, 2, 0.15, 1.0, 0.9, 0.85, 0.95, 3),
            R("npc.ayse", "Ayşe Hanım", 1, 1.10, 0.94, 4, 0.05, 0.3, 0.9, 0.90, 1.00, 4)
        };

        private static IReadOnlyList<NpcDefinition> RealNpcs()
        {
            var issues = new List<ContentIssue>();
            string text = System.IO.File.ReadAllText(System.IO.Path.Combine(TestPaths.ContentDataDirectory(), File));
            IReadOnlyList<NpcDefinition> npcs = Parse(text, issues);
            Assert.AreEqual(0, issues.Count, string.Join("\n", issues));
            return npcs;
        }

        [Test]
        public void RealFile_ParsesAndValidates_WithNoIssues()
        {
            IReadOnlyList<NpcDefinition> npcs = RealNpcs();
            var issues = new List<ContentIssue>();

            ContentValidator.ValidateNpcs(npcs, File, issues);

            Assert.AreEqual(0, issues.Count, string.Join("\n", issues));
            Assert.AreEqual(10, npcs.Count, "GDD v0.2 6: en az 10 NPC");
        }

        [Test]
        public void RealFile_MatchesTheGddTables_ForEveryNpc()
        {
            IReadOnlyList<NpcDefinition> npcs = RealNpcs();

            foreach (Row row in Gdd)
            {
                NpcDefinition n = npcs.Single(x => x.Id == row.Id);
                Assert.AreEqual(row.Name, n.Name, row.Id);
                Assert.AreEqual(row.From, n.Seller.AvailableFromDay, row.Id + " availableFromDay (UA4)");
                Assert.AreEqual(row.Ask, n.Seller.AskMultiplier, 1e-12, row.Id + " ask");
                Assert.AreEqual(row.Reject, n.Seller.RejectRatio, 1e-12, row.Id + " R");
                Assert.AreEqual(row.Patience, n.Seller.Patience, row.Id + " patience");
                Assert.AreEqual(row.Sigma, n.Seller.ValueSigma, 1e-12, row.Id + " sigma");
                Assert.AreEqual(row.Urgency, n.Seller.Urgency, 1e-12, row.Id + " urgency");
                Assert.AreEqual(row.Persuasion, n.Seller.Persuasion, 1e-12, row.Id + " persuasion");
                Assert.AreEqual(row.Open, n.Customer.OpeningOfferRatio, 1e-12, row.Id + " opening");
                Assert.AreEqual(row.MRatio, n.Customer.ValueRatio, 1e-12, row.Id + " mRatio");
                Assert.AreEqual(row.CustPatience, n.Customer.Patience, row.Id + " customer patience");
            }
        }

        [Test]
        public void RealFile_SpecialTraits_AreEncoded()
        {
            IReadOnlyList<NpcDefinition> npcs = RealNpcs();
            NpcDefinition Get(string id) { return npcs.Single(n => n.Id == id); }

            Assert.AreEqual(-0.28, Get("npc.hatice").Seller.ValueBias, 1e-12, "Hatice ~%28 eksik bilir");
            Assert.AreEqual(0.30, Get("npc.hatice").Customer.ValueSigma, 1e-12, "Hatice müşteri hatası %30");
            Assert.AreEqual(0.60, Get("npc.cengiz").Seller.ConcealChance, 1e-12, "Cengiz kusur saklama %60");
            Assert.AreEqual(6, Get("npc.cengiz").Seller.UrgentLabelFromDay, "'Acil satış' etiketi Gün 6+");
            Assert.AreEqual(1.12, Get("npc.nermin").Customer.PackageRatio, 1e-12, "Nermin paketli üründe ×1,12");

            foreach (NpcDefinition n in npcs.Where(x => x.Id != "npc.cengiz"))
            {
                Assert.IsNull(n.Seller.UrgentLabelFromDay, n.Id);
                Assert.AreEqual(0.0, n.Seller.ConcealChance, n.Id + " yalnız Cengiz saklar");
            }

            CollectionAssert.AreEquivalent(
                new[] { "npc.selin", "npc.hatice", "npc.ayse" },
                npcs.Where(n => n.Seller.LearningFriendly).Select(n => n.Id).ToArray(),
                "P2: Aceleci, Bilgisiz, Dürüst");
        }
    }
}
