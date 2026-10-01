using System.Collections.Generic;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Game;
using Esnaf.Domain.Inventory;
using Esnaf.Domain.Market;
using Esnaf.Domain.Negotiation;
using Esnaf.Domain.Npc;
using Esnaf.Domain.Products;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Negotiation
{
    /// <summary>
    /// Pazarlıksız alış (Gün 10 Adım 6, kullanıcı kararı): IGameApi.BuyListing ilanı İSTENEN fiyattan satın alır.
    /// Alış hattı pazarlıktakiyle aynıdır (InventoryService.Acquire): defter, raf, ekspertiz maliyeti, olaylar, NPC kaydı.
    /// </summary>
    public class BuyListingTests
    {
        private static GameSession New(ulong seed = 42UL, IEventBus bus = null, Esnaf.Domain.Content.ContentDatabase content = null)
        {
            return GameSession.NewGame(content ?? MarketHarness.RealContent(), seed, bus);
        }

        private static MarketListing First(GameSession s)
        {
            return s.Market.Listings.First();
        }

        private static void PassDays(GameSession s, int days)
        {
            for (int i = 0; i < days; i++)
            {
                Assert.IsTrue(s.Api.EndDay().IsSuccess);
            }
        }

        [Test]
        public void BuyListing_BuysAtTheAskingPrice_AndPutsTheItemOnTheShelf()
        {
            GameSession s = New();
            MarketListing listing = First(s);
            Money cash = s.Api.GetCash();

            Result<Money> result = s.Api.BuyListing(listing.ListingId);

            Assert.IsTrue(result.IsSuccess, result.ErrorCode);
            Assert.AreEqual(listing.AskingPrice, result.Value);
            Assert.AreEqual(cash - listing.AskingPrice, s.Api.GetCash());
            StockLine line = s.Api.GetInventory().Single();
            Assert.AreEqual(listing.InstanceId, line.InstanceId);
            Assert.AreEqual(listing.AskingPrice, line.CostBasis);
            ProductInstance instance = s.Store.Get(listing.InstanceId);
            Assert.AreEqual(ProductLocation.Inventory, instance.Location);
            Assert.AreEqual(listing.AskingPrice, instance.PurchasePrice);
            Assert.IsFalse(s.Api.GetListings().Any(l => l.ListingId == listing.ListingId), "ilan pazardan kalktı");
            Assert.IsNull(s.Api.GetNegotiation());
            Assert.IsTrue(s.EconomyState.Ledger.Verify().IsSuccess);
        }

        [Test]
        public void TheLedgerRecordsOnePurchaseAtTheAskingPrice_AndTheDaySummaryShowsTheSpend()
        {
            GameSession s = New();
            MarketListing listing = First(s);

            s.Api.BuyListing(listing.ListingId);

            TransactionRecord purchase = s.EconomyState.Ledger.Records.Single(r => r.TypeId == TransactionTypeIds.Purchase);
            Assert.AreEqual(-listing.AskingPrice, purchase.Amount, "defterde çıkış eksi işaretlidir");
            Assert.AreEqual(listing.AskingPrice, s.Api.GetTodaySummary().PurchaseSpend);
        }

        [Test]
        public void PaidAppraisalFees_JoinTheCostBasis_AsInTheNegotiationPath_ButNotTheCash()
        {
            GameSession s = New();
            PassDays(s, 2);
            MarketListing listing = First(s);
            var appraisal = s.Api.StartAppraisal(listing.ListingId, "s1");
            Assert.IsTrue(appraisal.IsSuccess, appraisal.ErrorCode);
            Money fee = appraisal.Value.Fee;
            Assert.IsTrue(fee.IsPositive);
            Money cash = s.Api.GetCash();

            Assert.IsTrue(s.Api.BuyListing(listing.ListingId).IsSuccess);

            Assert.AreEqual(cash - listing.AskingPrice, s.Api.GetCash(), "ekspertiz ücreti zaten ödenmişti");
            Assert.AreEqual(listing.AskingPrice + fee, s.Api.GetInventory().Single().CostBasis, "bekleyen ücret maliyete eklenir");
        }

        [Test]
        public void TheEvents_AnnounceThePurchase_AfterTheStateChanged_AndNeverANegotiation()
        {
            var bus = new EventBus();
            var purchased = new List<ListingPurchased>();
            var shelved = 0;
            var negotiationEvents = 0;
            bool listingGoneWhenAnnounced = false;
            bool onShelfWhenAnnounced = false;
            GameSession s = New(42UL, bus);
            MarketListing listing = First(s);
            bus.Subscribe<ListingPurchased>(e =>
            {
                purchased.Add(e);
                listingGoneWhenAnnounced = !s.Market.Listings.Any(l => l.ListingId == e.ListingId);
                onShelfWhenAnnounced = s.InventoryState.Contains(e.InstanceId);
            });
            bus.Subscribe<ItemAddedToShelf>(e => shelved++);
            bus.Subscribe<NegotiationStarted>(e => negotiationEvents++);
            bus.Subscribe<OfferMade>(e => negotiationEvents++);
            bus.Subscribe<NegotiationEnded>(e => negotiationEvents++);

            s.Api.BuyListing(listing.ListingId);

            ListingPurchased evt = purchased.Single();
            Assert.AreEqual(listing.ListingId, evt.ListingId);
            Assert.AreEqual(listing.InstanceId, evt.InstanceId);
            Assert.AreEqual(listing.SellerNpcId, evt.SellerNpcId);
            Assert.AreEqual(listing.AskingPrice, evt.Price);
            Assert.IsTrue(listingGoneWhenAnnounced);
            Assert.IsTrue(onShelfWhenAnnounced);
            Assert.AreEqual(1, shelved);
            Assert.AreEqual(0, negotiationEvents);
        }

        [Test]
        public void TheSellerIsRemembered_AsHavingSoldToThePlayer()
        {
            GameSession s = New();
            MarketListing listing = First(s);

            s.Api.BuyListing(listing.ListingId);

            Assert.IsTrue(s.Npcs.HasSoldToPlayer(listing.SellerNpcId, listing.InstanceId));
        }

        [Test]
        public void ADirectBuy_IsNotAnEncounterWithTheSeller()
        {
            GameSession s = New();
            MarketListing listing = First(s);
            NpcState before;
            int encountersBefore = s.Npcs.TryGet(listing.SellerNpcId, out before) ? before.EncounterCount : 0;

            s.Api.BuyListing(listing.ListingId);

            NpcState after;
            int encountersAfter = s.Npcs.TryGet(listing.SellerNpcId, out after) ? after.EncounterCount : 0;
            Assert.AreEqual(encountersBefore, encountersAfter, "pazarl\u0131k yok: kar\u015F\u0131la\u015Fma say\u0131lmaz");
        }

        [Test]
        public void ABoughtListing_CannotBeBoughtAgain_AndNothingChanges()
        {
            GameSession s = New();
            MarketListing listing = First(s);
            s.Api.BuyListing(listing.ListingId);
            string digest = s.Api.GetStateDigest();

            Result<Money> again = s.Api.BuyListing(listing.ListingId);

            Assert.IsTrue(again.IsFailure);
            Assert.AreEqual("listing.unknown", again.ErrorCode);
            Assert.AreEqual(digest, s.Api.GetStateDigest());
            Assert.AreEqual(1, s.Api.GetInventory().Count);
        }

        [TestCase(-1L)]
        [TestCase(0L)]
        [TestCase(99999L)]
        public void AnUnknownListing_IsRefused_AndNothingChanges(long id)
        {
            GameSession s = New();
            string digest = s.Api.GetStateDigest();

            Result<Money> result = s.Api.BuyListing(id);

            Assert.IsTrue(result.IsFailure);
            Assert.AreEqual("listing.unknown", result.ErrorCode);
            Assert.AreEqual(digest, s.Api.GetStateDigest());
        }

        [Test]
        public void NotEnoughCash_IsRefused_AndTheListingStaysOnTheMarket()
        {
            GameSession s = New(42UL, null, ContentVariants.WithOpeningCapital(1000));
            MarketListing listing = First(s);
            Assert.Greater(listing.AskingPrice.Tl, 1000L);
            string digest = s.Api.GetStateDigest();

            Result<Money> result = s.Api.BuyListing(listing.ListingId);

            Assert.IsTrue(result.IsFailure);
            Assert.AreEqual("cash.insufficient", result.ErrorCode);
            Assert.AreEqual(digest, s.Api.GetStateDigest(), "atomik: hiçbir şey değişmez");
            Assert.AreEqual(ProductLocation.Market, s.Store.Get(listing.InstanceId).Location);
            Assert.IsTrue(s.Api.GetListings().Any(l => l.ListingId == listing.ListingId));
            Assert.AreEqual(0, s.Api.GetInventory().Count);
        }

        [Test]
        public void AFullShelf_RefusesTheNextPurchase_AndNothingChanges()
        {
            GameSession s = New();
            for (int day = 0; day < 10 && s.Api.GetInventory().Count < s.InventoryState.Capacity; day++)
            {
                foreach (ListingView listing in s.Api.GetListings().ToList())
                {
                    if (s.Api.GetInventory().Count < s.InventoryState.Capacity)
                    {
                        Assert.IsTrue(s.Api.BuyListing(listing.ListingId).IsSuccess);
                    }
                }

                if (s.Api.GetInventory().Count < s.InventoryState.Capacity)
                {
                    PassDays(s, 1);
                }
            }

            Assert.AreEqual(s.InventoryState.Capacity, s.Api.GetInventory().Count, "raf doldu");
            PassDays(s, 1);
            long next = s.Api.GetListings()[0].ListingId;
            string digest = s.Api.GetStateDigest();

            Result<Money> result = s.Api.BuyListing(next);

            Assert.IsTrue(result.IsFailure);
            Assert.AreEqual("inventory.full", result.ErrorCode);
            Assert.AreEqual(digest, s.Api.GetStateDigest());
        }

        [Test]
        public void WhileANegotiationIsOpen_NoListingCanBeBoughtDirectly_AndTheNegotiationGoesOn()
        {
            GameSession s = New();
            IReadOnlyList<ListingView> listings = s.Api.GetListings();
            Assert.IsTrue(s.Api.StartNegotiation(listings[0].ListingId).IsSuccess);
            string digest = s.Api.GetStateDigest();

            foreach (ListingView listing in listings)
            {
                Result<Money> result = s.Api.BuyListing(listing.ListingId);
                Assert.IsTrue(result.IsFailure);
                Assert.AreEqual("negotiation.in_progress", result.ErrorCode);
            }

            Assert.AreEqual(digest, s.Api.GetStateDigest());
            Assert.IsNotNull(s.Api.GetNegotiation());
        }

        [Test]
        public void SeveralPurchasesInADay_AddUp()
        {
            GameSession s = New();
            Money cash = s.Api.GetCash();
            Money spent = Money.Zero;

            foreach (ListingView listing in s.Api.GetListings().ToList())
            {
                Result<Money> result = s.Api.BuyListing(listing.ListingId);
                Assert.IsTrue(result.IsSuccess, result.ErrorCode);
                spent += result.Value;
            }

            Assert.AreEqual(cash - spent, s.Api.GetCash());
            Assert.AreEqual(3, s.Api.GetInventory().Count);
            Assert.AreEqual(0, s.Api.GetListings().Count);
            Assert.IsTrue(s.EconomyState.Ledger.Verify().IsSuccess);
        }

        [Test]
        public void TheShelfGrowing_RaisesTheArrivingCustomers_AsAfterAnyPurchase()
        {
            GameSession s = New();
            int before = s.Capture().Customers.Arrived;
            Assert.AreEqual(s.Content.Customers.ArrivalCount(0), before);

            foreach (ListingView listing in s.Api.GetListings().ToList())
            {
                Assert.IsTrue(s.Api.BuyListing(listing.ListingId).IsSuccess);
            }

            int expected = s.Content.Customers.ArrivalCount(3);
            Assert.Greater(expected, before, "varsay\u0131m: 3 \u00FCr\u00FCnle gelen m\u00FC\u015Fteri say\u0131s\u0131 artar");
            Assert.AreEqual(expected, s.Capture().Customers.Arrived);
        }

        [Test]
        public void ABuy_DoesNotTouchTheRandomStreams()
        {
            GameSession s = New();
            var before = s.Capture().Rng;

            s.Api.BuyListing(First(s).ListingId);

            Assert.IsNull(DeepCompare.FirstDifference(before, s.Capture().Rng));
        }

        [Test]
        public void TheDayCanEnd_AfterABuy_AndTheBoughtItemAgesOnTheShelf()
        {
            GameSession s = New();
            long instanceId = First(s).InstanceId;
            s.Api.BuyListing(First(s).ListingId);

            Assert.IsTrue(s.Api.EndDay().IsSuccess);

            Assert.AreEqual(instanceId, s.Api.GetInventory().Single().InstanceId);
            Assert.AreEqual(2, s.Api.GetDay());
        }

        [Test]
        public void TheSameCommands_GiveTheSameState_AndTheStateSurvivesSaveAndLoad()
        {
            GameSession a = New(7UL);
            GameSession b = New(7UL);
            string before = a.Api.GetStateDigest();

            a.Api.BuyListing(First(a).ListingId);
            b.Api.BuyListing(First(b).ListingId);

            Assert.AreEqual(a.Api.GetStateDigest(), b.Api.GetStateDigest());
            Assert.AreNotEqual(before, a.Api.GetStateDigest());
            Result<GameSession> restored = GameSession.Restore(MarketHarness.RealContent(), a.Capture());
            Assert.IsTrue(restored.IsSuccess, restored.Message);
            Assert.AreEqual(a.Api.GetStateDigest(), restored.Value.Api.GetStateDigest());
            Assert.IsTrue(a.Api.EndDay().IsSuccess);
            Assert.IsTrue(restored.Value.Api.EndDay().IsSuccess);
            Assert.AreEqual(a.Api.GetStateDigest(), restored.Value.Api.GetStateDigest(), "kayıttan sonra devam aynı");
        }
    }
}
