using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Esnaf.Core;
using Esnaf.Domain.Business;
using Esnaf.Domain.Content;
using Esnaf.Domain.Game;
using Esnaf.Domain.Market;
using Esnaf.Domain.Negotiation;
using Esnaf.Domain.Npc;
using Esnaf.Domain.Products;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Business
{
    /// <summary>
    /// Müşteri kişiliği (Gün 10 Adım 7): gerçek içerikteki 10 NPC için kişilik profili, gerçek mekanik parametrelerinden türer
    /// (sabır, aciliyet, değer bilgisi, bütçe esnekliği, pazarlık toleransı). Yeni ekonomi formülü yoktur; Day 8 kuralları değişmez.
    /// </summary>
    public class CustomerProfileTests
    {
        private const NegotiationLevel L = NegotiationLevel.Low;
        private const NegotiationLevel M = NegotiationLevel.Medium;
        private const NegotiationLevel H = NegotiationLevel.High;

        private static ContentDatabase Content()
        {
            return MarketHarness.RealContent();
        }

        private static GameSession New(ulong seed = 42UL)
        {
            return GameSession.NewGame(Content(), seed);
        }

        // Elle çıkarılmış beklenen tablo (npc_profiles.json sayıları + ölçekler): sabır, aciliyet, bilgi, bütçe, pazarlık
        // sabır: <=1 düşük, <=3 orta, aksi yüksek (negotiation_rules.view); aciliyet >=0.3 orta >=0.6 yüksek; bilgi σ<=0.12 orta <=0.06 yüksek;
        // bütçe oran >=1.0 orta >=1.05 yüksek; pazarlık açılış<=0.90 orta <=0.85 yüksek.
        private static readonly object[][] Table =
        {
            new object[] { "npc.kemal", "haggler", "Pazarlıkçı", H, L, M, M, H },
            new object[] { "npc.selin", "hurried", "Aceleci", M, H, M, M, L },
            new object[] { "npc.murat", "informed_buyer", "Bilinçli alıcı", H, M, H, L, M },
            new object[] { "npc.hatice", "indecisive", "Kararsız", H, M, L, M, H },
            new object[] { "npc.berk", "showoff", "Gösteriş meraklısı", M, L, H, H, M },
            new object[] { "npc.ozan", "hurried", "Aceleci", M, M, M, M, M },
            new object[] { "npc.riza", "trust_seeker", "Güven odaklı", H, M, M, L, H },
            new object[] { "npc.nermin", "tech_enthusiast", "Teknoloji meraklısı", H, L, H, M, M },
            new object[] { "npc.cengiz", "budget_limited", "Bütçesi sınırlı", M, H, L, L, H },
            new object[] { "npc.ayse", "easygoing", "Rahat/samimi", H, M, H, M, M }
        };

        [TestCaseSource(nameof(Table))]
        public void EveryNpc_HasItsPersonalityAndItsLevels_FromTheRealMechanicParameters(
            string npcId, string personalityId, string personalityName,
            NegotiationLevel patience, NegotiationLevel urgency, NegotiationLevel knowledge, NegotiationLevel budget, NegotiationLevel haggling)
        {
            ContentDatabase content = Content();
            NpcDefinition npc = content.GetNpc(npcId);

            CustomerProfile profile = CustomerProfiler.Build(npc, content.Personalities, content.Negotiation);

            Assert.AreEqual(npcId, profile.NpcId);
            Assert.AreEqual(npc.Name, profile.Name);
            Assert.AreEqual(personalityId, profile.PersonalityId);
            Assert.AreEqual(personalityName, profile.PersonalityName);
            Assert.AreEqual(patience, profile.Patience, "sabır");
            Assert.AreEqual(urgency, profile.Urgency, "aciliyet");
            Assert.AreEqual(knowledge, profile.Knowledge, "bilgi");
            Assert.AreEqual(budget, profile.Budget, "bütçe esnekliği");
            Assert.AreEqual(haggling, profile.Haggling, "pazarlık toleransı");
            Assert.IsNull(profile.StartTrust, "başlangıç güveni yuvaya özgüdür");
        }

        [Test]
        public void TheCatalog_HoldsTheTenPersonalitiesOfTheDesign()
        {
            ContentDatabase content = Content();

            CollectionAssert.AreEquivalent(
                new[] { "Pazarlıkçı", "Aceleci", "Kararsız", "Teknoloji meraklısı", "Fiyat odaklı", "Rahat/samimi", "Bilinçli alıcı", "Gösteriş meraklısı", "Bütçesi sınırlı", "Güven odaklı" },
                content.Personalities.Definitions.Select(d => d.Name).ToArray());
        }

        [Test]
        public void EveryNpc_CarriesAValidPersonality_ThatItsRealParametersSatisfy()
        {
            ContentDatabase content = Content();

            foreach (NpcDefinition npc in content.Npcs)
            {
                PersonalityDefinition definition;
                Assert.IsNotNull(npc.PersonalityId, npc.Id);
                Assert.IsTrue(content.Personalities.TryGet(npc.PersonalityId, out definition), npc.Id);
                foreach (CustomerTrait trait in new[] { CustomerTrait.Patience, CustomerTrait.Urgency, CustomerTrait.Knowledge, CustomerTrait.Budget, CustomerTrait.Haggling })
                {
                    NegotiationLevel level = CustomerProfiler.LevelOf(trait, npc, content.Personalities.Scales, content.Negotiation);
                    Assert.IsTrue(definition.Accepts(trait, level), npc.Id + " " + trait + " " + level + " kişilik " + definition.Id);
                }
            }
        }

        [Test]
        public void DifferentNpcs_HaveGenuinelyDifferentBehaviourProfiles()
        {
            ContentDatabase content = Content();

            var tuples = content.Npcs
                .Select(n => CustomerProfiler.Build(n, content.Personalities, content.Negotiation))
                .Select(p => string.Join("/", new[] { p.Patience, p.Urgency, p.Knowledge, p.Budget, p.Haggling }))
                .ToList();

            Assert.AreEqual(tuples.Count, tuples.Distinct().Count(), "her NPC'nin düzey demeti ayrıdır: " + string.Join(" ", tuples));
        }

        [Test]
        public void ThePreferredSegments_AreTheRealInterestRestriction()
        {
            ContentDatabase content = Content();

            CustomerProfile cengiz = CustomerProfiler.Build(content.GetNpc("npc.cengiz"), content.Personalities, content.Negotiation);
            CustomerProfile kemal = CustomerProfiler.Build(content.GetNpc("npc.kemal"), content.Personalities, content.Negotiation);

            CollectionAssert.AreEqual(new[] { ProductSegment.Entry, ProductSegment.Mid }, cengiz.PreferredSegments.ToArray());
            Assert.AreEqual(0, kemal.PreferredSegments.Count, "boş = her segment");
        }

        [Test]
        public void ANpcWithoutAPersonalityCatalog_HasNoProfile()
        {
            NpcDefinition npc = ContentFixtures.Npcs()[0];

            Assert.IsNull(CustomerProfiler.Build(npc, PersonalityCatalog.Empty, ContentFixtures.Negotiation()));
        }

        [Test]
        public void TheProfiler_ChecksItsArguments()
        {
            ContentDatabase content = Content();

            Assert.Throws<ArgumentNullException>(() => CustomerProfiler.Build(null, content.Personalities, content.Negotiation));
            Assert.Throws<ArgumentNullException>(() => CustomerProfiler.Build(content.Npcs[0], null, content.Negotiation));
            Assert.Throws<ArgumentNullException>(() => CustomerProfiler.Build(content.Npcs[0], content.Personalities, null));
        }

        // ---------- oyunda: müşteriler ve satış ----------

        // Gün 1 rehberli ürünü alıp etiketler; interested müşteriler için uygun fiyatı bulmak üzere birkaç gün dener.
        private static GameSession SessionWithCustomers(ulong seed)
        {
            GameSession s = New(seed);
            MarketListing g = s.Market.Listings.Single(l => l.IsGuided);
            Assert.IsTrue(s.Api.StartNegotiation(g.ListingId).IsSuccess);
            Assert.IsTrue(s.Api.MakeOffer(Money.FromTl(5800)).IsSuccess);
            Assert.IsTrue(s.Api.SetPrice(g.InstanceId, Money.FromTl(5900)).IsSuccess);
            return s;
        }

        private static GameSession FindSessionWithACustomer(out CustomerView customer)
        {
            for (ulong seed = 1; seed < 200; seed++)
            {
                GameSession s = SessionWithCustomers(seed);
                IReadOnlyList<CustomerView> views = s.Api.GetCustomers();
                if (views.Count > 0)
                {
                    customer = views[0];
                    return s;
                }
            }

            Assert.Fail("ilgilenen müşteri bulunamadı");
            customer = null;
            return null;
        }

        [Test]
        public void TheCustomerViews_CarryTheNpcProfile_AndTheSlotsStartingTrustLevel()
        {
            CustomerView view;
            GameSession s = FindSessionWithACustomer(out view);
            ContentDatabase content = s.Content;
            CustomerSlot slot;
            Assert.IsTrue(s.Customers.TryGetWaiting(view.CustomerId, out slot));

            CustomerProfile profile = view.Profile;

            Assert.IsNotNull(profile);
            Assert.AreEqual(view.NpcId, profile.NpcId);
            CustomerProfile npcLevel = CustomerProfiler.Build(content.GetNpc(view.NpcId), content.Personalities, content.Negotiation);
            Assert.AreEqual(npcLevel.PersonalityId, profile.PersonalityId);
            Assert.AreEqual(npcLevel.Patience, profile.Patience);
            Assert.AreEqual(npcLevel.Urgency, profile.Urgency);
            Assert.AreEqual(npcLevel.Knowledge, profile.Knowledge);
            Assert.AreEqual(npcLevel.Budget, profile.Budget);
            Assert.AreEqual(npcLevel.Haggling, profile.Haggling);
            int trust = content.Negotiation.StartTrust + (int)Math.Round(content.Negotiation.StartTrustSpread * (2.0 * slot.TrustDraw - 1.0), MidpointRounding.AwayFromZero);
            Assert.AreEqual(NegotiationLevels.MoodOf(content.Negotiation, Math.Max(0, Math.Min(100, trust))), profile.StartTrust.Value);
        }

        [Test]
        public void TheSaleView_CarriesTheSameProfile_AsTheCustomerView()
        {
            CustomerView view;
            GameSession s = FindSessionWithACustomer(out view);

            Result<SaleView> started = s.Api.StartSale(view.CustomerId);

            Assert.IsTrue(started.IsSuccess, started.ErrorCode);
            Assert.AreEqual(view.Profile.PersonalityId, started.Value.Profile.PersonalityId);
            Assert.AreEqual(view.Profile.Patience, started.Value.Profile.Patience);
            Assert.AreEqual(view.Profile.StartTrust, started.Value.Profile.StartTrust);
            Assert.AreEqual(started.Value.Profile.NpcId, started.Value.NpcId);
        }

        [Test]
        public void AskingForProfiles_ChangesNoStateAndConsumesNoRandomness()
        {
            CustomerView view;
            GameSession s = FindSessionWithACustomer(out view);
            string digest = s.Api.GetStateDigest();
            var rng = s.Capture().Rng;

            for (int i = 0; i < 20; i++)
            {
                foreach (CustomerView v in s.Api.GetCustomers())
                {
                    Assert.IsNotNull(v.Profile);
                }

                s.Customers.ProfileOf("npc.kemal");
            }

            Assert.AreEqual(digest, s.Api.GetStateDigest());
            Assert.IsNull(DeepCompare.FirstDifference(rng, s.Capture().Rng), "RNG devamı bozulmaz");
        }

        private static string Dump(GameSession s, int days)
        {
            var sb = new StringBuilder();
            for (int day = 0; day < days; day++)
            {
                foreach (CustomerSlot slot in s.Customers.State.Slots)
                {
                    CustomerProfile p = s.Customers.ProfileFor(slot);
                    sb.Append(day).Append(':').Append(slot.CustomerId).Append(':').Append(p.NpcId).Append(':').Append(p.PersonalityId)
                      .Append(':').Append(p.Patience).Append(p.Urgency).Append(p.Knowledge).Append(p.Budget).Append(p.Haggling).Append(p.StartTrust).Append(';');
                }

                Assert.IsTrue(s.Api.EndDay().IsSuccess);
            }

            return sb.ToString();
        }

        [Test]
        public void TheSameSeed_GivesTheSameCustomersAndPersonalities_AndOtherSeedsDiffer()
        {
            string a = Dump(New(77UL), 8);
            string b = Dump(New(77UL), 8);
            string c = Dump(New(78UL), 8);

            Assert.AreEqual(a, b);
            Assert.AreNotEqual(a, c);
        }

        [Test]
        public void EverySlotOfEveryDay_HasAValidProfile_AndTheDay8DailyRulesHold()
        {
            GameSession s = New(5UL);
            ContentDatabase content = s.Content;
            for (int day = 1; day <= 12; day++)
            {
                IReadOnlyList<CustomerSlot> slots = s.Customers.State.Slots;
                Assert.AreEqual(content.Customers.CountMax, slots.Count, "günlük yuva sayısı (Day 8)");
                Assert.LessOrEqual(slots.Count(sl => content.Customers.IsRich(sl.NpcId)), content.Customers.RichMaxPerDay, "zengin/koleksiyoncu sınırı");
                foreach (CustomerSlot slot in slots)
                {
                    CustomerProfile p = s.Customers.ProfileFor(slot);
                    Assert.IsNotNull(p.PersonalityId);
                    Assert.IsNotNull(p.PersonalityName);
                    Assert.IsTrue(p.StartTrust.HasValue);
                    Assert.IsTrue(Enum.IsDefined(typeof(NegotiationLevel), p.Patience));
                    Assert.LessOrEqual(content.GetNpc(slot.NpcId).Customer.AvailableFromDay, day, "Cengiz/Berk gibi NPC'ler gününden önce gelmez");
                }

                Assert.IsTrue(s.Api.EndDay().IsSuccess);
            }
        }

        [Test]
        public void ACustomersBudget_IsValid_ForEveryInterestedCustomer()
        {
            CustomerView view;
            GameSession s = FindSessionWithACustomer(out view);
            CustomerSlot slot;
            Assert.IsTrue(s.Customers.TryGetWaiting(view.CustomerId, out slot));
            Esnaf.Domain.Products.ProductInstance instance = s.Store.Get(view.InstanceId);
            ProductDefinition definition = s.Content.GetProduct(instance.DefinitionId);
            double trueValue = new ValueCalculator(s.Content.ValueTables).TrueValue(instance, definition).Tl;

            double max = s.Customers.MaxFor(slot, instance, false);

            Assert.Greater(max, 0.0);
            Assert.LessOrEqual(max, Math.Floor(trueValue * s.Content.Customers.MaxRatioToTrueValue / 10.0 + 0.5) * 10.0 + 10.0);
            Assert.AreEqual(0.0, max % 10.0, "bütçe 10 TL'ye yuvarlıdır");
        }

        [Test]
        public void TheProfile_SurvivesSaveAndLoad_BecauseItIsDerivedFromSavedState()
        {
            CustomerView view;
            GameSession s = FindSessionWithACustomer(out view);

            Result<GameSession> restored = GameSession.Restore(s.Content, s.Capture());

            Assert.IsTrue(restored.IsSuccess, restored.Message);
            CustomerView again = restored.Value.Api.GetCustomers().Single(c => c.CustomerId == view.CustomerId);
            Assert.AreEqual(view.Profile.PersonalityId, again.Profile.PersonalityId);
            Assert.AreEqual(view.Profile.StartTrust, again.Profile.StartTrust);
            Assert.AreEqual(s.Api.GetStateDigest(), restored.Value.Api.GetStateDigest());
        }
    }
}
