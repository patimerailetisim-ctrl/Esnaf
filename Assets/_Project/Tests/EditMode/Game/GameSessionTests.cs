using System;
using System.Collections.Generic;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Content;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Game;
using Esnaf.Domain.Market;
using Esnaf.Domain.Products;
using Esnaf.Domain.Time;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Game
{
    public class GameSessionTests
    {
        private static GameSession New(ulong seed = 42UL, IEventBus bus = null)
        {
            return GameSession.NewGame(MarketHarness.RealContent(), seed, bus);
        }

        [Test]
        public void NewGame_StartsOnDayOne_WithTheOpeningCapital()
        {
            GameSession s = New();

            Assert.AreEqual(1, s.Time.Day);
            Assert.AreEqual(42UL, s.Time.MasterSeed);
            Assert.AreEqual(Money.FromTl(250000), s.EconomyState.Cash, "GDD karar 2: 250.000 TL");
            Assert.AreEqual(1, s.EconomyState.Ledger.Count);
            Assert.AreEqual(0, s.EconomyState.Ledger.Records[0].Day, "sermaye 0. güne yazılır");
            Assert.AreEqual(6, s.InventoryState.Capacity, "başlangıç raf kapasitesi 6");
            Assert.AreEqual(0, s.InventoryState.Count);
            Assert.AreEqual(Money.FromTl(250000), s.Wealth.Calculate().Total);
        }

        [Test]
        public void NewGame_SeedsTheRandomStreams_WithTheGameSeed()
        {
            GameSession s = New(42UL);

            Assert.AreEqual(42UL, s.Rng.MasterSeed);
            var reference = new RngStreams(42UL).Get("market");
            Assert.AreEqual(reference.NextUInt(), new RngStreams(s.Time.MasterSeed).Get("market").NextUInt());
            var h = new MarketHarness(42UL);
            Assert.AreEqual(
                string.Join(",", h.Generate(1).Select(l => l.AskingPrice.Tl + "/" + l.RejectPrice.Tl)),
                string.Join(",", s.Market.Listings.Select(l => l.AskingPrice.Tl + "/" + l.RejectPrice.Tl)),
                "oturumun Gün 1 ilanları, aynı tohumlu bağımsız üretimle birebir aynı");
        }

        [Test]
        public void NewGame_OpensTheMarketForDayOne_WithTheGuidedListing()
        {
            GameSession s = New();

            Assert.AreEqual(3, s.Market.Count);
            Assert.AreEqual(1, s.Market.Listings.Count(l => l.IsGuided));
            Assert.AreEqual(3, s.Store.Count);
            Assert.AreEqual(3, s.InstanceIds.LastIssued);
            Assert.AreEqual(3, s.ListingIds.LastIssued);
            Assert.IsTrue(s.Store.All.All(i => i.Location == ProductLocation.Market));
        }

        [Test]
        public void NewGame_PublishesDayStartedAndListingsGenerated_ToTheGivenBus()
        {
            var bus = new EventBus();
            var events = new List<string>();
            bus.Subscribe<DayStarted>(e => events.Add("started:" + e.Day));
            bus.Subscribe<ListingsGenerated>(e => events.Add("listings:" + e.Day + ":" + e.ListingIds.Count));
            bus.Subscribe<TransactionRecorded>(e => events.Add("tx:" + e.Record.TypeId));

            GameSession s = New(42UL, bus);

            Assert.AreSame(bus, s.Bus);
            CollectionAssert.AreEqual(new[] { "tx:opening_capital", "started:1", "listings:1:3" }, events);
        }

        [Test]
        public void NewGame_WithoutABus_CreatesItsOwn()
        {
            GameSession s = New();

            Assert.IsNotNull(s.Bus);
        }

        [Test]
        public void NewGame_ExposesTheApi_AndItsWiring()
        {
            GameSession s = New();

            Assert.IsNotNull(s.Api);
            Assert.AreSame(MarketHarness.RealContent(), s.Content);
            Assert.IsNotNull(s.Rng);
            Assert.IsNotNull(s.Npcs);
            Assert.IsNotNull(s.EconomyService);
            Assert.IsNotNull(s.InventoryService);
            Assert.IsNotNull(s.Summaries);
            Assert.IsNotNull(s.LedgerView);
            Assert.IsNotNull(s.ListingGenerator);
        }

        [Test]
        public void NewGame_WiresTheDayEndPipeline_InTheFixedOrder()
        {
            GameSession s = New();

            CollectionAssert.AreEqual(new[] { "missed_customers", "daily_expense", "listing_expiry", "demand_update", "new_day", "auto_save" }, s.DayEnd.Steps.Select(x => x.Id).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2, 4, 5, 7, 8 }, s.DayEnd.Steps.Select(x => x.Order).ToArray());
        }

        [Test]
        public void NewGame_WiresEveryWealthContributor()
        {
            GameSession s = New();
            ProductInstance guided = s.Store.All.First();
            Assert.IsTrue(s.InventoryService.Acquire(guided.InstanceId, Money.FromTl(1000), 1).IsSuccess);
            Assert.IsTrue(s.EconomyService.PayAppraisal(s.Store.All.Last().InstanceId, s.Store.All.Last().DefinitionId, Money.FromTl(200), 1).IsSuccess);
            Assert.IsTrue(s.InventoryService.UpgradeCapacity(8, Money.FromTl(15000), 1).IsSuccess);

            WealthBreakdown wealth = s.Wealth.Calculate();

            Assert.AreEqual(Money.FromTl(250000), wealth.Total, "alış, ekspertiz ve yatırım servet değişimi yaratmaz");
            Assert.AreEqual(Money.FromTl(250000 - 1000 - 200 - 15000), s.EconomyState.Cash);
        }

        [Test]
        public void NewGame_RejectsANullContent()
        {
            Assert.Throws<ArgumentNullException>(() => GameSession.NewGame(null, 1UL));
        }

        [Test]
        public void TwoSessions_AreCompletelyIndependent()
        {
            GameSession a = New(1UL);
            GameSession b = New(1UL);

            Assert.IsTrue(a.Api.EndDay().IsSuccess);
            Assert.IsTrue(a.Api.EndDay().IsSuccess);

            Assert.AreEqual(3, a.Api.GetDay());
            Assert.AreEqual(1, b.Api.GetDay(), "tekil (singleton) durum olmamalı");
            Assert.AreEqual(3, b.Market.Count);
            Assert.AreNotEqual(a.Api.GetStateDigest(), b.Api.GetStateDigest());
        }

        [Test]
        public void Sessions_WithTheSameSeed_StartIdentical()
        {
            Assert.AreEqual(New(9UL).Api.GetStateDigest(), New(9UL).Api.GetStateDigest());
            Assert.AreNotEqual(New(9UL).Api.GetStateDigest(), New(10UL).Api.GetStateDigest());
        }

        [Test]
        public void NewGame_WithFixtureContent_Works()
        {
            GameSession s = GameSession.NewGame(MarketHarness.FixtureContent(), 3UL);

            Assert.AreEqual(3, s.Market.Count);
            Assert.AreEqual(Money.FromTl(250000), s.EconomyState.Cash);
        }
    }
}
