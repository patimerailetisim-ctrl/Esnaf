using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Esnaf.Core;
using Esnaf.Domain.Appraisal;
using Esnaf.Domain.Content;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Game;
using Esnaf.Domain.Inventory;
using Esnaf.Domain.Market;
using Esnaf.Domain.Negotiation;
using Esnaf.Domain.Products;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Negotiation
{
    /// <summary>Alış akışı (IGameApi üzerinden): pazarlıktan ürünün rafa girmesine kadar.</summary>
    public class BuyFlowTests
    {
        private static GameSession New(ulong seed = 42UL, IEventBus bus = null, ContentDatabase content = null)
        {
            return GameSession.NewGame(content ?? MarketHarness.RealContent(), seed, bus);
        }

        private static MarketListing Guided(GameSession s)
        {
            return s.Market.Listings.Single(l => l.IsGuided);
        }

        private static NegotiationView Start(GameSession s, long listingId)
        {
            Result<NegotiationView> r = s.Api.StartNegotiation(listingId);
            Assert.IsTrue(r.IsSuccess, r.ErrorCode);
            return r.Value;
        }

        private static NegotiationView Offer(GameSession s, long tl)
        {
            Result<NegotiationView> r = s.Api.MakeOffer(Money.FromTl(tl));
            Assert.IsTrue(r.IsSuccess, r.ErrorCode);
            return r.Value;
        }

        private static void PassDays(GameSession s, int days)
        {
            for (int i = 0; i < days; i++)
            {
                Assert.IsTrue(s.Api.EndDay().IsSuccess);
            }
        }

        /// <summary>Guided ilanın ürününe elle "ekspertiz sonucu" ekler (kart içeriği testte belirlenir).</summary>
        private static long InjectCard(GameSession s, long instanceId, string level, bool falseAlarm, long problem = 1000, double power = 0.7, long resultId = 900)
        {
            var card = new TrumpCard("screen", "appraisal.finding.screen_replaced", AppraisalConfidence.Medium, power, Money.FromTl(problem), falseAlarm);
            var finding = new AttributeFinding("screen", "appraisal.finding.screen_replaced", true, AppraisalConfidence.Medium, power, falseAlarm);
            s.Knowledge.Add(new AppraisalResult(
                resultId, instanceId, "phone.yildiz_y5", level, 1, Money.Zero, 1UL, new[] { finding },
                new NumericRange(80, 90), new NumericRange(95, 100), new MoneyRange(Money.FromTl(5000), Money.FromTl(5500)), new[] { card }));
            return resultId;
        }

        // ---------- başlangıç ----------

        [Test]
        public void Start_UnknownListing_Fails()
        {
            GameSession s = New();

            Result<NegotiationView> r = s.Api.StartNegotiation(999);

            Assert.AreEqual("listing.unknown", r.ErrorCode);
            Assert.IsNull(s.Api.GetNegotiation());
        }

        [Test]
        public void Start_OpensAnActiveNegotiationAtTheAskingPrice()
        {
            GameSession s = New();
            MarketListing g = Guided(s);

            NegotiationView v = Start(s, g.ListingId);

            Assert.AreEqual(g.ListingId, v.ListingId);
            Assert.AreEqual(g.InstanceId, v.InstanceId);
            Assert.AreEqual("npc.ayse", v.SellerNpcId);
            Assert.AreEqual(NegotiationPhase.Active, v.Phase);
            Assert.AreEqual(0, v.Round);
            Assert.AreEqual(Money.FromTl(5800), v.ShownPrice);
            Assert.IsTrue(v.DealPrice.IsZero);
            Assert.AreEqual(NegotiationLevel.Medium, v.Mood);
            Assert.AreEqual(NegotiationLevel.High, v.Patience);
            Assert.IsFalse(v.LastOfferInsulted);
            Assert.AreEqual(v.ListingId, s.Api.GetNegotiation().ListingId);
        }

        [Test]
        public void Start_CountsAnEncounterWithTheSeller()
        {
            GameSession s = New();

            Start(s, Guided(s).ListingId);

            Esnaf.Domain.Npc.NpcState state;
            Assert.IsTrue(s.Npcs.TryGet("npc.ayse", out state));
            Assert.AreEqual(1, state.EncounterCount);
        }

        [Test]
        public void OnlyOneNegotiationAtATime()
        {
            GameSession s = New();
            long first = s.Market.Listings[0].ListingId;
            long second = s.Market.Listings[1].ListingId;
            Start(s, first);

            Assert.AreEqual("negotiation.in_progress", s.Api.StartNegotiation(second).ErrorCode);
            Assert.AreEqual("negotiation.in_progress", s.Api.StartNegotiation(first).ErrorCode);
            Assert.AreEqual(first, s.Api.GetNegotiation().ListingId);
        }

        [Test]
        public void Commands_WithoutANegotiation_Fail()
        {
            GameSession s = New();

            Assert.AreEqual("negotiation.none", s.Api.MakeOffer(Money.FromTl(4000)).ErrorCode);
            Assert.AreEqual("negotiation.none", s.Api.MakeOfferWithCard(Money.FromTl(4000), 1, 0).ErrorCode);
            Assert.AreEqual("negotiation.none", s.Api.AcceptFinalPrice().ErrorCode);
            Assert.AreEqual("negotiation.none", s.Api.WalkAway().ErrorCode);
            Assert.IsNull(s.Api.GetNegotiation());
        }

        // ---------- teklifler (Ayşe Hanım, rehberli ilan; kesin değerler bağımsız referans modelinden) ----------

        [Test]
        public void GuidedSequence_FollowsTheReferenceModel_ThenTheFinalOfferIsAccepted()
        {
            GameSession s = New();
            MarketListing g = Guided(s);
            Start(s, g.ListingId);

            NegotiationView v1 = Offer(s, 4000);
            Assert.AreEqual(Money.FromTl(5390), v1.ShownPrice);
            Assert.IsTrue(v1.LastOfferInsulted);
            Assert.AreEqual(NegotiationLevel.Medium, v1.Patience);
            Assert.AreEqual(1, v1.Round);

            Assert.AreEqual(Money.FromTl(5130), Offer(s, 4300).ShownPrice);
            NegotiationView v3 = Offer(s, 4500);
            Assert.AreEqual(Money.FromTl(4980), v3.ShownPrice);
            Assert.AreEqual(NegotiationLevel.Low, v3.Patience);
            NegotiationView v4 = Offer(s, 4600);
            Assert.AreEqual(Money.FromTl(4890), v4.ShownPrice);
            Assert.AreEqual(NegotiationPhase.FinalOffer, v4.Phase);

            Assert.AreEqual("negotiation.final_offer_only", s.Api.MakeOffer(Money.FromTl(4890)).ErrorCode);
            Money cashBefore = s.Api.GetCash();

            Result<NegotiationView> accepted = s.Api.AcceptFinalPrice();

            Assert.IsTrue(accepted.IsSuccess, accepted.ErrorCode);
            Assert.AreEqual(NegotiationPhase.Deal, accepted.Value.Phase);
            Assert.AreEqual(Money.FromTl(4890), accepted.Value.DealPrice);
            Assert.AreEqual(cashBefore - Money.FromTl(4890), s.Api.GetCash());
            Assert.IsNull(s.Api.GetNegotiation());
            Assert.IsFalse(s.Market.TryGet(g.ListingId, out g));
        }

        [Test]
        public void ADealAtAHighOffer_IsClosedAtTheSellersPrice_NotAtTheOffer()
        {
            GameSession s = New();
            long listing = Guided(s).ListingId;
            long instance = Guided(s).InstanceId;
            Money cash = s.Api.GetCash();
            Start(s, listing);

            NegotiationView v = Offer(s, 5800);

            Assert.AreEqual(NegotiationPhase.Deal, v.Phase);
            Assert.AreEqual(Money.FromTl(5390), v.DealPrice);
            Assert.AreEqual(cash - Money.FromTl(5390), s.Api.GetCash());
            Assert.AreEqual(1, s.Api.GetInventory().Count);
            Assert.AreEqual(Money.FromTl(5390), s.Api.GetInventory()[0].CostBasis);
            Assert.AreEqual(instance, s.Api.GetInventory()[0].InstanceId);
            Assert.AreEqual(ProductLocation.Inventory, s.Store.Get(instance).Location);
            Assert.AreEqual(Money.FromTl(5390), s.Store.Get(instance).PurchasePrice);
            Assert.IsTrue(s.Npcs.HasSoldToPlayer("npc.ayse", instance));
            Assert.IsNull(s.Api.GetNegotiation());
        }

        [Test]
        public void APurchase_ChangesNeitherWealthNorTheLedgerInvariants()
        {
            GameSession s = New();
            Start(s, Guided(s).ListingId);
            Money wealthBefore = s.Wealth.Calculate().Total;

            Offer(s, 5800);

            Assert.AreEqual(wealthBefore, s.Wealth.Calculate().Total, "alış servet değiştirmez (nakit → stok)");
        }

        [TestCase(0L)]
        [TestCase(-10L)]
        [TestCase(4005L)]
        public void InvalidOffer_FailsAndKeepsTheNegotiationUntouched(long tl)
        {
            GameSession s = New();
            Start(s, Guided(s).ListingId);
            string digest = s.Api.GetStateDigest();

            Result<NegotiationView> r = s.Api.MakeOffer(Money.FromTl(tl));

            Assert.AreEqual("offer.invalid", r.ErrorCode);
            Assert.AreEqual(digest, s.Api.GetStateDigest());
        }

        // ---------- masadan kalkma ----------

        [Test]
        public void WalkAway_RemovesTheListingAndItsInstance_AndFreesTheTable()
        {
            GameSession s = New();
            MarketListing g = Guided(s);
            long instance = g.InstanceId;
            Money cash = s.Api.GetCash();
            Start(s, g.ListingId);
            Offer(s, 4000);

            Result<NegotiationView> r = s.Api.WalkAway();

            Assert.IsTrue(r.IsSuccess, r.ErrorCode);
            Assert.AreEqual(NegotiationPhase.Failed, r.Value.Phase);
            Assert.IsNull(s.Api.GetNegotiation());
            Assert.IsFalse(s.Market.TryGet(g.ListingId, out g));
            Assert.IsFalse(s.Store.TryGet(instance, out ProductInstance _));
            Assert.AreEqual(cash, s.Api.GetCash());
            Assert.AreEqual(0, s.Api.GetInventory().Count);
            Assert.IsTrue(s.Api.StartNegotiation(s.Market.Listings[0].ListingId).IsSuccess, "yeni pazarlık açılabilir");
        }

        [Test]
        public void WalkAway_WritesOffAPendingAppraisalFee()
        {
            GameSession s = New();
            PassDays(s, 2);
            MarketListing listing = s.Market.Listings.First(l => l.DayListed == 3);
            Result<AppraisalView> appraisal = s.Api.StartAppraisal(listing.ListingId, "s1");
            Money fee = appraisal.Value.Fee;
            Money wealthAfterFee = s.Wealth.Calculate().Total;
            Assert.AreEqual(fee, s.EconomyService.PendingAppraisalCost(listing.InstanceId));
            Start(s, listing.ListingId);

            Assert.IsTrue(s.Api.WalkAway().IsSuccess);

            Assert.IsTrue(s.EconomyService.PendingAppraisalCost(listing.InstanceId).IsZero);
            Assert.IsTrue(s.EconomyState.Ledger.Records.Any(r => r.TypeId == "wasted_appraisal" && r.InstanceId == listing.InstanceId));
        }

        // ---------- ekspertiz ücreti maliyete girer ----------

        [Test]
        public void Purchase_CapitalizesThePendingAppraisalFeeIntoTheCostBasis()
        {
            GameSession s = New();
            PassDays(s, 2);
            MarketListing listing = s.Market.Listings.First(l => l.DayListed == 3);
            Money fee = s.Api.StartAppraisal(listing.ListingId, "s1").Value.Fee;
            Start(s, listing.ListingId);
            Money ask = listing.AskingPrice;

            NegotiationView v = Offer(s, ask.Tl);

            Assert.AreEqual(NegotiationPhase.Deal, v.Phase);
            StockLine line = s.Api.GetInventory().Single();
            Assert.AreEqual(v.DealPrice + fee, line.CostBasis);
            Assert.IsTrue(s.EconomyService.PendingAppraisalCost(listing.InstanceId).IsZero);
        }

        // ---------- ön koşullar ----------

        [Test]
        public void Deal_WithInsufficientCash_IsRefused_AndTheNegotiationStaysOpen()
        {
            GameSession s = New();
            MarketListing g = Guided(s);
            Start(s, g.ListingId);
            Offer(s, 4000);
            Money leave = Money.FromTl(1000);
            Assert.IsTrue(s.EconomyService.RecordInvestment(s.Api.GetCash() - leave, 1, "ledger.memo.test").IsSuccess);
            string digest = s.Api.GetStateDigest();

            Result<NegotiationView> r = s.Api.MakeOffer(Money.FromTl(5800));

            Assert.AreEqual("cash.insufficient", r.ErrorCode);
            Assert.AreEqual(digest, s.Api.GetStateDigest());
            Assert.AreEqual(NegotiationPhase.Active, s.Api.GetNegotiation().Phase);
            Assert.AreEqual(0, s.Api.GetInventory().Count);
            Assert.IsTrue(s.Api.WalkAway().IsSuccess);
        }

        [Test]
        public void AcceptFinal_WithInsufficientCash_IsRefused()
        {
            GameSession s = New();
            Start(s, Guided(s).ListingId);
            foreach (long offer in new long[] { 4000, 4300, 4500, 4600 })
            {
                Offer(s, offer);
            }

            Assert.IsTrue(s.EconomyService.RecordInvestment(s.Api.GetCash() - Money.FromTl(1000), 1, "ledger.memo.test").IsSuccess);

            Result<NegotiationView> r = s.Api.AcceptFinalPrice();

            Assert.AreEqual("cash.insufficient", r.ErrorCode);
            Assert.AreEqual(NegotiationPhase.FinalOffer, s.Api.GetNegotiation().Phase);
        }

        [Test]
        public void FullShelf_BlocksStartingANegotiation()
        {
            string text = System.IO.File.ReadAllText(System.IO.Path.Combine(TestPaths.ContentDataDirectory(), ContentFileNames.EconomyConstants));
            var source = new DictionaryContentSource();
            foreach (string file in System.IO.Directory.GetFiles(TestPaths.ContentDataDirectory(), "*.json"))
            {
                source.Add(System.IO.Path.GetFileName(file), System.IO.File.ReadAllText(file));
            }

            Assert.IsTrue(text.Contains("\"initialShelfCapacity\": 15"));
            source.Add(ContentFileNames.EconomyConstants, text.Replace("\"initialShelfCapacity\": 15", "\"initialShelfCapacity\": 1"));
            ContentLoadResult load = ContentDatabase.Load(source);
            Assert.IsNotNull(load.Database, load.FormatIssues());
            GameSession s = New(content: load.Database);
            Start(s, Guided(s).ListingId);
            Offer(s, 5800);
            Assert.AreEqual(1, s.Api.GetInventory().Count);

            Result<NegotiationView> r = s.Api.StartNegotiation(s.Market.Listings[0].ListingId);

            Assert.AreEqual("inventory.full", r.ErrorCode);
            Assert.IsNull(s.Api.GetNegotiation());
        }

        [Test]
        public void EndDay_IsBlockedWhileANegotiationIsOpen()
        {
            GameSession s = New();
            Start(s, Guided(s).ListingId);
            string digest = s.Api.GetStateDigest();

            Result<DayEndReport> r = s.Api.EndDay();

            Assert.AreEqual("negotiation.in_progress", r.ErrorCode);
            Assert.AreEqual(1, s.Api.GetDay());
            Assert.AreEqual(digest, s.Api.GetStateDigest());
            Assert.IsTrue(s.Api.WalkAway().IsSuccess);
            Assert.IsTrue(s.Api.EndDay().IsSuccess);
            Assert.AreEqual(2, s.Api.GetDay());
        }

        // ---------- koz kartları ----------

        [Test]
        public void Card_LowersTheSellersReject_AndCannotBePlayedTwice()
        {
            GameSession s = New();
            MarketListing g = Guided(s);
            long appraisalId = InjectCard(s, g.InstanceId, "s1", falseAlarm: false);
            NegotiationView start = Start(s, g.ListingId);
            Assert.AreEqual(1, start.Cards.Count);
            Assert.IsFalse(start.Cards[0].IsUsed);
            Assert.AreEqual(appraisalId, start.Cards[0].AppraisalId);
            Assert.AreEqual(0, start.Cards[0].CardIndex);
            Assert.AreEqual(Money.FromTl(1000), start.Cards[0].ProblemValue);

            Result<NegotiationView> r = s.Api.MakeOfferWithCard(Money.FromTl(4200), appraisalId, 0);

            Assert.IsTrue(r.IsSuccess, r.ErrorCode);
            Assert.AreEqual(Money.FromTl(5110), r.Value.ShownPrice);
            Assert.AreEqual(NegotiationLevel.Medium, r.Value.Mood);
            Assert.IsTrue(r.Value.Cards[0].IsUsed);
            string digest = s.Api.GetStateDigest();

            Result<NegotiationView> again = s.Api.MakeOfferWithCard(Money.FromTl(4500), appraisalId, 0);

            Assert.AreEqual("card.already_used", again.ErrorCode);
            Assert.AreEqual(digest, s.Api.GetStateDigest());
            Assert.AreEqual(Money.FromTl(4690), Offer(s, 4500).ShownPrice, "kart R'yi düşürmüş kalır (4120)");
        }

        [Test]
        public void ProfessionalReportCard_PersuadesMore()
        {
            GameSession s = New();
            MarketListing g = Guided(s);
            long id = InjectCard(s, g.InstanceId, "s3", falseAlarm: false, problem: 1000, power: 1.0);
            Start(s, g.ListingId);

            NegotiationView v = s.Api.MakeOfferWithCard(Money.FromTl(4200), id, 0).Value;

            Assert.AreEqual(Money.FromTl(4920), v.ShownPrice);
        }

        [Test]
        public void FalseAlarmCard_CostsTrustAndPatience_WithoutLoweringTheReject()
        {
            GameSession s = New();
            MarketListing g = Guided(s);
            long id = InjectCard(s, g.InstanceId, "s1", falseAlarm: true);
            Start(s, g.ListingId);

            NegotiationView v = s.Api.MakeOfferWithCard(Money.FromTl(4200), id, 0).Value;

            Assert.AreEqual(Money.FromTl(5420), v.ShownPrice);
            Assert.AreEqual(NegotiationLevel.Medium, v.Patience);
            Assert.AreEqual(Money.FromTl(5170), Offer(s, 4600).ShownPrice);
        }

        [Test]
        public void Card_ViewNeverRevealsWhetherACardIsAFalseAlarm()
        {
            GameSession s = New();
            MarketListing g = Guided(s);
            InjectCard(s, g.InstanceId, "s1", falseAlarm: true, resultId: 900);
            NegotiationView v = Start(s, g.ListingId);

            foreach (PropertyInfo p in typeof(NegotiationCardView).GetProperties())
            {
                StringAssert.DoesNotContain("False", p.Name);
                StringAssert.DoesNotContain("Valid", p.Name);
            }

            Assert.AreEqual(1, v.Cards.Count);
        }

        [Test]
        public void UnknownCards_AreRejected_WithoutChangingState()
        {
            GameSession s = New();
            MarketListing g = Guided(s);
            long id = InjectCard(s, g.InstanceId, "s1", falseAlarm: false);
            MarketListing other = s.Market.Listings.First(l => l.ListingId != g.ListingId);
            long otherId = InjectCard(s, other.InstanceId, "s1", falseAlarm: false, resultId: 901);
            Start(s, g.ListingId);
            string digest = s.Api.GetStateDigest();

            Assert.AreEqual("card.unknown", s.Api.MakeOfferWithCard(Money.FromTl(4200), 12345, 0).ErrorCode);
            Assert.AreEqual("card.unknown", s.Api.MakeOfferWithCard(Money.FromTl(4200), id, 1).ErrorCode);
            Assert.AreEqual("card.unknown", s.Api.MakeOfferWithCard(Money.FromTl(4200), id, -1).ErrorCode);
            Assert.AreEqual("card.unknown", s.Api.MakeOfferWithCard(Money.FromTl(4200), otherId, 0).ErrorCode, "başka ürünün kartı");
            Assert.AreEqual(digest, s.Api.GetStateDigest());
        }

        // ---------- gizlilik (K3) ----------

        [Test]
        public void Views_CarryNoHiddenNegotiationState()
        {
            var forbidden = new[] { "Reject", "Trust", "Floor", "Believed", "Persuasion", "Urgency", "FalseAlarm", "IsValid", "Multiplier" };
            foreach (Type type in new[] { typeof(NegotiationView), typeof(NegotiationCardView) })
            {
                foreach (PropertyInfo p in type.GetProperties())
                {
                    foreach (string word in forbidden)
                    {
                        StringAssert.DoesNotContain(word, p.Name, type.Name);
                    }
                }
            }

            CollectionAssert.AreEquivalent(
                new[] { "ListingId", "InstanceId", "SellerNpcId", "Phase", "Round", "ShownPrice", "DealPrice", "Mood", "Patience", "LastOfferInsulted", "Cards" },
                typeof(NegotiationView).GetProperties().Select(p => p.Name).ToArray());
            Assert.AreEqual(typeof(NegotiationLevel), typeof(NegotiationView).GetProperty("Mood").PropertyType);
            Assert.AreEqual(typeof(NegotiationLevel), typeof(NegotiationView).GetProperty("Patience").PropertyType);
        }

        [Test]
        public void Mood_IsHighFromSeventy_AndLowBelowForty()
        {
            GameSession s = New();
            MarketListing g = Guided(s);
            Start(s, g.ListingId);
            for (int i = 0; i < 4; i++)
            {
                Offer(s, 4610); // 0,97 × 4750 = 4607,5 → yakın teklif, her tur güven +5
            }

            Assert.AreEqual(NegotiationLevel.High, s.Api.GetNegotiation().Mood, "güven 70");

            GameSession t = New();
            MarketListing h = Guided(t);
            long first = InjectCard(t, h.InstanceId, "s1", falseAlarm: true, resultId: 900);
            long second = InjectCard(t, h.InstanceId, "s2", falseAlarm: true, resultId: 901);
            Start(t, h.ListingId);
            Assert.AreEqual(NegotiationLevel.Medium, t.Api.MakeOfferWithCard(Money.FromTl(4200), first, 0).Value.Mood, "güven 40 alt sınırın altında değil");
            Assert.AreEqual(NegotiationLevel.Low, t.Api.MakeOfferWithCard(Money.FromTl(4200), second, 0).Value.Mood, "güven 30");
        }

        // ---------- olaylar ----------

        [Test]
        public void Events_AreNotificationsPublishedInOrder_AfterAPurchase()
        {
            var bus = new EventBus();
            var log = new List<string>();
            bus.Subscribe<NegotiationStarted>(e => log.Add("started"));
            bus.Subscribe<ItemAddedToShelf>(e => log.Add("shelf"));
            bus.Subscribe<OfferMade>(e => log.Add("offer:" + e.Round + ":" + e.Phase));
            bus.Subscribe<ListingPurchased>(e => log.Add("purchased:" + e.Price.Tl));
            bus.Subscribe<NegotiationEnded>(e => log.Add("ended:" + e.Phase));
            GameSession s = New(bus: bus);
            Start(s, Guided(s).ListingId);

            Offer(s, 5800);

            CollectionAssert.AreEqual(new[] { "started", "shelf", "offer:1:Deal", "purchased:5390", "ended:Deal" }, log);
        }

        [Test]
        public void Events_AfterAWalkAway()
        {
            var bus = new EventBus();
            var log = new List<string>();
            bus.Subscribe<NegotiationStarted>(e => log.Add("started"));
            bus.Subscribe<OfferMade>(e => log.Add("offer:" + e.Round + ":" + e.Phase + ":" + e.Insulted));
            bus.Subscribe<ListingPurchased>(e => log.Add("purchased"));
            bus.Subscribe<NegotiationEnded>(e => log.Add("ended:" + e.Phase + ":" + e.DealPrice.Tl));
            GameSession s = New(bus: bus);
            Start(s, Guided(s).ListingId);
            Offer(s, 4000);

            s.Api.WalkAway();

            CollectionAssert.AreEqual(new[] { "started", "offer:1:Active:True", "ended:Failed:0" }, log);
        }

        // ---------- determinizm (I6) ----------

        private static string Play(ulong seed)
        {
            GameSession s = New(seed);
            for (int day = 0; day < 6; day++)
            {
                foreach (long id in s.Market.Listings.Select(l => l.ListingId).ToList())
                {
                    MarketListing listing;
                    if (!s.Market.TryGet(id, out listing) || s.Api.GetInventory().Count >= 5)
                    {
                        continue;
                    }

                    if (s.Api.StartNegotiation(id).IsFailure)
                    {
                        continue;
                    }

                    long offer = Money.RoundTo10((long)(listing.AskingPrice.Tl * 0.8));
                    for (int i = 0; i < 8; i++)
                    {
                        Result<NegotiationView> r = s.Api.MakeOffer(Money.FromTl(offer));
                        if (r.IsFailure || r.Value.Phase == NegotiationPhase.Deal)
                        {
                            break;
                        }

                        if (r.Value.Phase == NegotiationPhase.FinalOffer)
                        {
                            if (day % 2 == 0)
                            {
                                s.Api.AcceptFinalPrice();
                            }

                            break;
                        }

                        offer += 100;
                    }

                    if (s.Api.GetNegotiation() != null)
                    {
                        s.Api.WalkAway();
                    }
                }

                s.Api.EndDay();
            }

            return s.Api.GetStateDigest();
        }

        [Test]
        public void SameSeedSameCommands_GiveTheSameDigest_AndDifferentSeedsDiffer()
        {
            Assert.AreEqual(Play(7UL), Play(7UL));
            Assert.AreNotEqual(Play(7UL), Play(8UL));
        }

        [Test]
        public void Digest_ChangesWithTheNegotiationState()
        {
            GameSession s = New();
            long listing = Guided(s).ListingId;
            string idle = s.Api.GetStateDigest();

            Start(s, listing);
            string started = s.Api.GetStateDigest();
            Offer(s, 4000);
            string afterOffer = s.Api.GetStateDigest();

            Assert.AreNotEqual(idle, started);
            Assert.AreNotEqual(started, afterOffer);
        }

        [Test]
        public void DigestDescription_HasANegotiationLineOnlyWhileNegotiating()
        {
            GameSession s = New();
            Assert.IsFalse(GameStateDigest.Describe(s).Contains("\nG|"));

            Start(s, Guided(s).ListingId);

            Assert.IsTrue(GameStateDigest.Describe(s).Contains("\nG|"));
        }

        private static string Bits(double v)
        {
            return BitConverter.DoubleToInt64Bits(v).ToString("x16", System.Globalization.CultureInfo.InvariantCulture);
        }

        [Test]
        public void DigestLine_ListsEveryNegotiationField_InAFixedFormat()
        {
            GameSession s = New();
            MarketListing g = Guided(s);
            long id = InjectCard(s, g.InstanceId, "s1", falseAlarm: false);
            Start(s, g.ListingId);
            Assert.IsTrue(s.Api.MakeOfferWithCard(Money.FromTl(4200), id, 0).IsSuccess);

            string line = GameStateDigest.Describe(s).Split('\n').Single(l => l.StartsWith("G|"));

            // Ayşe Hanım, rehberli ilan: R 4750 → kart sonrası 4120 (bağımsız referans), fiyat 5110,0 (5110,x), güven 60, sabır 3
            string expected = "G|" + g.ListingId + "|" + g.InstanceId + "|npc.ayse|0|1|3|60|"
                + Bits(4120.0) + "|" + Bits(s.TradeState.Current.State.Price) + "|5110|0|0|" + id + ":0|"
                + Bits(4750.0) + "|" + Bits(3432.0) + "|4|50|5800|" + Bits(0.3) + "|" + Bits(0.9) + "|" + Bits(1.0) + "|1";
            Assert.AreEqual(expected, line);
        }

        [Test]
        public void TheNegotiationDoesNotDisturbTheMarketRandomStream()
        {
            GameSession a = New(11UL);
            GameSession b = New(11UL);
            Start(a, Guided(a).ListingId);
            Assert.IsTrue(a.Api.WalkAway().IsSuccess);
            Assert.IsTrue(b.Api.EndDay().IsSuccess);
            Assert.IsTrue(a.Api.EndDay().IsSuccess);

            // Aynı tohumla, pazarlık yapılan ve yapılmayan oyunda ertesi günün yeni ilanları aynı olmalı (ilan akışı ayrı).
            var listA = a.Api.GetListings().Where(v => v.DayListed == 2).Select(v => v.DefinitionId + ":" + v.AskingPrice.Tl).ToList();
            var listB = b.Api.GetListings().Where(v => v.DayListed == 2).Select(v => v.DefinitionId + ":" + v.AskingPrice.Tl).ToList();
            CollectionAssert.AreEqual(listB, listA);
        }

        // ---------- değişmezler (rastgele oyun) ----------

        [Test]
        public void Fuzz_EveryDealRespectsTheRejectPrice_AndMoneyIsConserved()
        {
            for (ulong seed = 1; seed <= 40; seed++)
            {
                GameSession s = New(seed);
                for (int day = 0; day < 8; day++)
                {
                    foreach (long id in s.Market.Listings.Select(l => l.ListingId).ToList())
                    {
                        MarketListing listing;
                        if (!s.Market.TryGet(id, out listing) || s.Api.GetInventory().Count >= 5 || s.Api.StartNegotiation(id).IsFailure)
                        {
                            continue;
                        }

                        Money cashBefore = s.Api.GetCash();
                        Money wealthBefore = s.Wealth.Calculate().Total;
                        long offer = Money.RoundTo10((long)(listing.RejectPrice.Tl * (0.85 + 0.02 * (int)(seed % 10))));
                        NegotiationView last = null;
                        for (int i = 0; i < 10; i++)
                        {
                            Result<NegotiationView> r = s.Api.MakeOffer(Money.FromTl(offer));
                            if (r.IsFailure)
                            {
                                break;
                            }

                            last = r.Value;
                            if (last.Phase != NegotiationPhase.Active)
                            {
                                break;
                            }

                            offer += 50;
                        }

                        if (last != null && last.Phase == NegotiationPhase.FinalOffer)
                        {
                            last = s.Api.AcceptFinalPrice().Value;
                        }

                        if (last != null && last.Phase == NegotiationPhase.Deal)
                        {
                            Assert.GreaterOrEqual(last.DealPrice.Tl, (long)Math.Floor(listing.RejectPrice.Tl * 0.97), "ruh hali ret fiyatını en fazla %3 oynatır (I4)");
                            Assert.LessOrEqual(last.DealPrice.Tl, listing.AskingPrice.Tl, "asla istenen fiyatın üstü değil");
                            Assert.AreEqual(cashBefore - last.DealPrice, s.Api.GetCash());
                            Assert.AreEqual(wealthBefore, s.Wealth.Calculate().Total);
                        }
                        else if (s.Api.GetNegotiation() != null)
                        {
                            s.Api.WalkAway();
                        }

                        Assert.IsNull(s.Api.GetNegotiation());
                    }

                    Assert.IsTrue(s.Api.EndDay().IsSuccess);
                }
            }
        }
    }
}
