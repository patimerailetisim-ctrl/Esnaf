using System;
using System.Collections.Generic;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Market;
using NUnit.Framework;

namespace Esnaf.Tests.Market
{
    public class MarketStateTests
    {
        private static MarketListing Listing(long id, long instanceId = 100, int remaining = 3)
        {
            return new MarketListing(
                id, instanceId + id, "npc.kemal", Money.FromTl(6000), new[] { "box" }, 2, remaining,
                Money.FromTl(5000), Money.FromTl(5500), true, false, true, false);
        }

        [Test]
        public void Listing_ExposesEveryConstructorValue()
        {
            var listing = new MarketListing(
                5, 42, "npc.selin", Money.FromTl(18400), new[] { "box", "invoice" }, 3, 4,
                Money.FromTl(14350), Money.FromTl(17500), true, false, true, true);

            Assert.AreEqual(5, listing.ListingId);
            Assert.AreEqual(42, listing.InstanceId);
            Assert.AreEqual("npc.selin", listing.SellerNpcId);
            Assert.AreEqual(Money.FromTl(18400), listing.AskingPrice);
            CollectionAssert.AreEqual(new[] { "box", "invoice" }, listing.Tags.ToArray());
            Assert.AreEqual(3, listing.DayListed);
            Assert.AreEqual(4, listing.RemainingDays);
            Assert.AreEqual(Money.FromTl(14350), listing.RejectPrice);
            Assert.AreEqual(Money.FromTl(17500), listing.BelievedValue);
            Assert.IsTrue(listing.IsOpportunity);
            Assert.IsFalse(listing.IsTrap);
            Assert.IsTrue(listing.IsJackpot);
            Assert.IsTrue(listing.IsGuided);
        }

        [Test]
        public void Listing_TagsAreACopy_AndReadOnly()
        {
            var tags = new List<string> { "box" };
            var listing = new MarketListing(1, 1, "npc.a", Money.FromTl(100), tags, 1, 2, Money.FromTl(50), Money.FromTl(80), false, false, false, false);

            tags.Add("invoice");

            CollectionAssert.AreEqual(new[] { "box" }, listing.Tags.ToArray());
            Assert.IsFalse(listing.Tags is List<string>);
        }

        [Test]
        public void EmptyState_HasNoListings()
        {
            var market = new MarketState();
            MarketListing found;

            Assert.AreEqual(0, market.Count);
            Assert.AreEqual(0, market.Listings.Count);
            Assert.IsFalse(market.TryGet(1, out found));
            Assert.IsNull(found);
        }

        [Test]
        public void Add_KeepsInsertionOrder_AndAllowsLookup()
        {
            var market = new MarketState();

            market.Add(Listing(3));
            market.Add(Listing(1));
            market.Add(Listing(2));

            CollectionAssert.AreEqual(new long[] { 3, 1, 2 }, market.Listings.Select(l => l.ListingId).ToArray());
            Assert.AreEqual(3, market.Count);
            MarketListing found;
            Assert.IsTrue(market.TryGet(1, out found));
            Assert.AreEqual(1, found.ListingId);
            Assert.IsFalse(market.TryGet(9, out found));
        }

        [Test]
        public void Add_DuplicateListingId_Throws()
        {
            var market = new MarketState();
            market.Add(Listing(1));

            Assert.Throws<ArgumentException>(() => market.Add(Listing(1)));
            Assert.AreEqual(1, market.Count);
        }

        [Test]
        public void Add_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new MarketState().Add(null));
        }

        [Test]
        public void Remove_DeletesOnlyThatListing()
        {
            var market = new MarketState();
            market.Add(Listing(1));
            market.Add(Listing(2));
            market.Add(Listing(3));

            Assert.IsTrue(market.Remove(2));
            Assert.IsFalse(market.Remove(2), "ikinci silme");
            Assert.IsFalse(market.Remove(99));

            CollectionAssert.AreEqual(new long[] { 1, 3 }, market.Listings.Select(l => l.ListingId).ToArray());
            MarketListing found;
            Assert.IsFalse(market.TryGet(2, out found));
            Assert.IsTrue(market.TryGet(3, out found));
        }

        [Test]
        public void Listings_IsAReadOnlyView()
        {
            var market = new MarketState();
            market.Add(Listing(1));

            Assert.IsFalse(market.Listings is List<MarketListing>);
        }
    }
}
