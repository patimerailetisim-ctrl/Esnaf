using System.Collections.Generic;
using System.Linq;
using Esnaf.Domain.Game;
using Esnaf.Domain.Npc;
using Esnaf.Presentation;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Presentation
{
    public class CustomerPersonaTests
    {
        [Test]
        public void ThePool_IsExactlyThe28Portraits_16Male12Female_NoDuplicates()
        {
            var all = CustomerPersonas.MaleNames.Concat(CustomerPersonas.FemaleNames).ToArray();

            Assert.AreEqual(16, CustomerPersonas.MaleNames.Count);
            Assert.AreEqual(12, CustomerPersonas.FemaleNames.Count);
            CollectionAssert.AreEquivalent(CustomerPortraitNaming.ExpectedNames.ToArray(), all);
            Assert.AreEqual(28, all.Select(CustomerPortraitNaming.Key).Distinct().Count());
        }

        [Test]
        public void AMaleNpc_OnlyGetsMaleNames_AndAFemaleNpc_OnlyFemaleNames()
        {
            for (long id = -5; id < 500; id++)
            {
                Assert.Contains(CustomerPersonas.For(id, "male").Name, CustomerPersonas.MaleNames.ToArray());
                Assert.Contains(CustomerPersonas.For(id, "female").Name, CustomerPersonas.FemaleNames.ToArray());
            }
        }

        [Test]
        public void TheSameCustomerId_AlwaysGivesTheSamePersona()
        {
            for (long id = 1; id < 100; id++)
            {
                Assert.AreEqual(CustomerPersonas.For(id, "male").Name, CustomerPersonas.For(id, "male").Name);
            }
        }

        [Test]
        public void ConsecutiveCustomers_NeverShareAName_WithinAFullPool()
        {
            for (long start = 1; start < 60; start++)
            {
                var male = Enumerable.Range(0, 16).Select(i => CustomerPersonas.For(start + i, "male").Name).ToArray();
                var female = Enumerable.Range(0, 12).Select(i => CustomerPersonas.For(start + i, "female").Name).ToArray();
                Assert.AreEqual(16, male.Distinct().Count(), "erkek, başlangıç " + start);
                Assert.AreEqual(12, female.Distinct().Count(), "kadın, başlangıç " + start);
            }
        }

        [Test]
        public void EveryPortraitName_IsUsedByLongRuns_SoAll28AreReachable()
        {
            var seen = new HashSet<string>();
            for (long id = 1; id <= 16; id++)
            {
                seen.Add(CustomerPersonas.For(id, "male").Name);
                seen.Add(CustomerPersonas.For(id, "female").Name);
            }

            Assert.AreEqual(28, seen.Count);
        }

        [Test]
        public void WithoutAGender_ThereIsNoPersona()
        {
            Assert.IsNull(CustomerPersonas.For(1, null));
            Assert.IsNull(CustomerPersonas.For(1, "other"));
        }

        [Test]
        public void TheRealNpcs_AllCarryAGender_AndTheirNamesAgreeWithIt()
        {
            var content = MarketHarness.RealContent();
            var expected = new Dictionary<string, string>
            {
                { "npc.kemal", "male" }, { "npc.selin", "female" }, { "npc.murat", "male" }, { "npc.hatice", "female" }, { "npc.berk", "male" },
                { "npc.ozan", "male" }, { "npc.riza", "male" }, { "npc.nermin", "female" }, { "npc.cengiz", "male" }, { "npc.ayse", "female" }
            };

            foreach (NpcDefinition npc in content.Npcs)
            {
                Assert.AreEqual(expected[npc.Id], npc.Gender, npc.Id);
            }
        }

        [Test]
        public void TheDisplayedName_ComesFromThePool_NotFromTheNpcsOwnName_AndMatchesTheGender()
        {
            var content = new ContentPresentation(MarketHarness.RealContent());

            string kemal = content.CustomerName(7, "npc.kemal");
            string selin = content.CustomerName(7, "npc.selin");

            Assert.Contains(kemal, CustomerPersonas.MaleNames.ToArray());
            Assert.Contains(selin, CustomerPersonas.FemaleNames.ToArray());
            Assert.AreEqual(kemal, content.CustomerName(7, "npc.kemal"));
        }

        [Test]
        public void ANpcWithoutAGender_KeepsItsOwnName()
        {
            var content = new ContentPresentation(MarketHarness.RealContent());

            Assert.AreEqual(content.NpcName("npc.nope"), content.CustomerName(3, "npc.nope"));
            Assert.AreEqual(string.Empty, content.CustomerName(3, null));
        }

        [Test]
        public void TheSamePersonality_CanCarryDifferentNames_NameAndPersonalityAreNotBound()
        {
            var content = new ContentPresentation(MarketHarness.RealContent());

            var names = Enumerable.Range(1, 12).Select(i => content.CustomerName(i, "npc.kemal")).Distinct().ToArray();

            Assert.Greater(names.Length, 8);
        }

        [Test]
        public void ADay_OfFiveCustomers_ShowsFiveDifferentNames_AndDeterministicallyAcrossRuns()
        {
            string Day(ulong seed)
            {
                GameSession s = GameSession.NewGame(MarketHarness.RealContent(), seed);
                var c = new ContentPresentation(s.Content);
                return string.Join(",", s.Customers.State.Slots.Select(sl => c.CustomerName(sl.CustomerId, sl.NpcId)));
            }

            string a = Day(11UL);
            Assert.AreEqual(a, Day(11UL));
            Assert.AreEqual(5, a.Split(',').Distinct().Count(), a);
        }

        [Test]
        public void TheGenderField_IsValidated()
        {
            var issues = new List<Esnaf.Domain.Content.ContentIssue>();
            string json = System.IO.File.ReadAllText(System.IO.Path.Combine(TestPaths.ContentDataDirectory(), "npc_profiles.json"));
            string bad = json.Replace("\"gender\": \"male\"", "\"gender\": \"robot\"");

            var npcs = Esnaf.Domain.Content.ContentParser.ParseNpcs("npc_profiles.json", bad, issues);
            Esnaf.Domain.Content.ContentValidator.ValidateNpcs(npcs, "npc_profiles.json", issues);

            Assert.IsTrue(issues.Any(i => i.Code == Esnaf.Domain.Content.ContentIssueCodes.NpcFieldInvalid && i.Message.Contains("gender")));
        }
    }
}
