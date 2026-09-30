using System;
using System.Collections.Generic;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Content;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Market;
using Esnaf.Domain.Products;
using Esnaf.Domain.Time;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Time
{
    public class DayEndStepsTests
    {
        // ---------- Adım 2: günlük gider ----------

        [Test]
        public void DailyExpenseStep_HasTheFixedIdentity()
        {
            var step = new DailyExpenseStep(new EconomyHarness().Economy);

            Assert.AreEqual("daily_expense", step.Id);
            Assert.AreEqual(2, step.Order);
        }

        [Test]
        public void DailyExpenseStep_DayThree_ChargesFiveHundred()
        {
            var h = new EconomyHarness();
            var step = new DailyExpenseStep(h.Economy);
            var ctx = new DayEndContext(3);

            Result result = step.Execute(ctx);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(Money.FromTl(500), ctx.ExpenseCharged);
            Assert.AreEqual(Money.FromTl(249500), h.Cash, "GDD T4: Gün 3 sonu nakit 249.500");
        }

        [TestCase(1)]
        [TestCase(2)]
        public void DailyExpenseStep_DaysOneAndTwo_ChargeNothing(int day)
        {
            var h = new EconomyHarness();
            var step = new DailyExpenseStep(h.Economy);
            var ctx = new DayEndContext(day);

            Assert.IsTrue(step.Execute(ctx).IsSuccess);

            Assert.AreEqual(Money.Zero, ctx.ExpenseCharged);
            Assert.AreEqual(Money.FromTl(250000), h.Cash);
            Assert.AreEqual(1, h.State.Ledger.Count, "yalnızca başlangıç sermayesi satırı");
        }

        [Test]
        public void DailyExpenseStep_TwiceOnTheSameDay_FailsTheSecondTime()
        {
            var h = new EconomyHarness();
            var step = new DailyExpenseStep(h.Economy);
            step.Execute(new DayEndContext(3));

            Result second = step.Execute(new DayEndContext(3));

            Assert.IsTrue(second.IsFailure);
            Assert.AreEqual("expense.already_charged", second.ErrorCode);
            Assert.AreEqual(Money.FromTl(249500), h.Cash);
        }

        [Test]
        public void DailyExpenseStep_RejectsNulls()
        {
            Assert.Throws<ArgumentNullException>(() => new DailyExpenseStep(null));
            Assert.Throws<ArgumentNullException>(() => new DailyExpenseStep(new EconomyHarness().Economy).Execute(null));
        }

        // ---------- Adım 4: ilan ömrü ----------

        private sealed class ExpiryRig
        {
            public readonly MarketState Market = new MarketState();
            public readonly InstanceStore Store = new InstanceStore();
            public readonly EventBus Bus = new EventBus();
            public readonly EconomyHarness Bank = new EconomyHarness();
            public readonly ListingExpiryStep Step;
            public readonly List<string> Events = new List<string>();

            public ExpiryRig()
            {
                Step = new ListingExpiryStep(Market, Store, Bank.Economy, Bus);
                Bus.Subscribe<ListingExpired>(e =>
                {
                    MarketListing still;
                    ProductInstance inst;
                    Events.Add(
                        "expired:" + e.ListingId + ":" + e.InstanceId
                        + ":marketHas=" + Market.TryGet(e.ListingId, out still)
                        + ":storeHas=" + Store.TryGet(e.InstanceId, out inst));
                });
            }

            public void Add(long listingId, int remaining, ProductLocation location = ProductLocation.Market)
            {
                long instanceId = listingId + 100;
                Store.Add(new ProductInstance { InstanceId = instanceId, DefinitionId = "phone.x", Location = location });
                Market.Add(new MarketListing(
                    listingId, instanceId, "npc.a", Money.FromTl(1000), new string[0], 1, remaining,
                    Money.FromTl(800), Money.FromTl(900), false, false, false, false));
            }
        }

        [Test]
        public void ListingExpiryStep_HasTheFixedIdentity()
        {
            var rig = new ExpiryRig();

            Assert.AreEqual("listing_expiry", rig.Step.Id);
            Assert.AreEqual(4, rig.Step.Order);
        }

        [Test]
        public void ListingExpiryStep_DecrementsAll_AndRemovesTheExpired()
        {
            var rig = new ExpiryRig();
            rig.Add(1, 1);
            rig.Add(2, 3);
            rig.Add(3, 1);
            rig.Add(4, 2);
            var ctx = new DayEndContext(2);

            Result result = rig.Step.Execute(ctx);

            Assert.IsTrue(result.IsSuccess);
            CollectionAssert.AreEqual(new long[] { 1, 3 }, ctx.ExpiredListingIds.ToArray(), "ilan sırasıyla");
            CollectionAssert.AreEqual(new long[] { 2, 4 }, rig.Market.Listings.Select(l => l.ListingId).ToArray());
            MarketListing two;
            MarketListing four;
            rig.Market.TryGet(2, out two);
            rig.Market.TryGet(4, out four);
            Assert.AreEqual(2, two.RemainingDays);
            Assert.AreEqual(1, four.RemainingDays);
        }

        [Test]
        public void ListingExpiryStep_RemovesTheMarketInstance_ButKeepsTheOnesTheShopOwns()
        {
            var rig = new ExpiryRig();
            rig.Add(1, 1);
            rig.Add(2, 1, ProductLocation.Inventory);

            rig.Step.Execute(new DayEndContext(2));

            ProductInstance instance;
            Assert.IsFalse(rig.Store.TryGet(101, out instance), "pazarda kalan örnek silinir");
            Assert.IsTrue(rig.Store.TryGet(102, out instance), "alınmış örnek silinmez");
            Assert.AreEqual(ProductLocation.Inventory, instance.Location);
            Assert.AreEqual(0, rig.Market.Count);
        }

        [Test]
        public void ListingExpiryStep_PublishesEventsAfterTheStateChange_InOrder()
        {
            var rig = new ExpiryRig();
            rig.Add(1, 1);
            rig.Add(2, 5);
            rig.Add(3, 1);

            rig.Step.Execute(new DayEndContext(2));

            CollectionAssert.AreEqual(
                new[] { "expired:1:101:marketHas=False:storeHas=False", "expired:3:103:marketHas=False:storeHas=False" }, rig.Events);
        }

        [Test]
        public void ListingExpiryStep_NothingExpiring_PublishesNothing()
        {
            var rig = new ExpiryRig();
            rig.Add(1, 2);
            var ctx = new DayEndContext(2);

            rig.Step.Execute(ctx);

            Assert.AreEqual(0, rig.Events.Count);
            Assert.AreEqual(0, ctx.ExpiredListingIds.Count);
        }

        [Test]
        public void ListingExpiryStep_ListingWithNoDaysLeft_IsRemoved()
        {
            var rig = new ExpiryRig();
            rig.Add(1, 0);

            rig.Step.Execute(new DayEndContext(2));

            Assert.AreEqual(0, rig.Market.Count);
        }

        [Test]
        public void ListingExpiryStep_WorksWithoutAnEventBus()
        {
            var market = new MarketState();
            var store = new InstanceStore();
            store.Add(new ProductInstance { InstanceId = 101, DefinitionId = "phone.x", Location = ProductLocation.Market });
            market.Add(new MarketListing(1, 101, "npc.a", Money.FromTl(1000), new string[0], 1, 1, Money.FromTl(800), Money.FromTl(900), false, false, false, false));
            var step = new ListingExpiryStep(market, store, new EconomyHarness().Economy, null);

            Assert.IsTrue(step.Execute(new DayEndContext(2)).IsSuccess);
            Assert.AreEqual(0, market.Count);
        }

        // Boşa ekspertiz: ürünü alınmadan kalkan ilanın bekleyen ekspertiz ücreti gider yazılır (Gün 6, v0.2 5.1)

        [Test]
        public void ListingExpiryStep_WritesOffPendingAppraisalFees_OfExpiringListings()
        {
            var rig = new ExpiryRig();
            rig.Add(1, 1);
            rig.Add(2, 5);
            Assert.IsTrue(rig.Bank.Economy.PayAppraisal(101, "phone.x", Tl(200), 3).IsSuccess);
            Assert.IsTrue(rig.Bank.Economy.PayAppraisal(101, "phone.x", Tl(600), 3).IsSuccess);
            Assert.IsTrue(rig.Bank.Economy.PayAppraisal(102, "phone.x", Tl(300), 3).IsSuccess);

            rig.Step.Execute(new DayEndContext(3));

            Assert.AreEqual(Esnaf.Core.Money.Zero, rig.Bank.Economy.PendingAppraisalCost(101), "kalkan ilanın ücretleri gider yazıldı");
            Assert.AreEqual(Tl(300), rig.Bank.Economy.PendingAppraisalCost(102), "ilanı süren ürünün ücreti bekler");
            Assert.AreEqual(2, rig.Bank.State.Ledger.Records.Count(r => r.TypeId == "wasted_appraisal"));
            Assert.IsTrue(rig.Bank.State.Ledger.Records.Where(r => r.TypeId == "wasted_appraisal").All(r => r.Day == 3 && r.Amount.IsZero));
            Assert.AreEqual(Tl(-800), rig.Bank.Summaries.NetProfit(3), "boşa ekspertiz günün net kârından düşer");
        }

        [Test]
        public void ListingExpiryStep_DoesNotWriteOffFees_OfItemsTheShopBought()
        {
            var rig = new ExpiryRig();
            rig.Add(1, 1, ProductLocation.Inventory);
            Assert.IsTrue(rig.Bank.Economy.PayAppraisal(101, "phone.x", Tl(200), 3).IsSuccess);

            rig.Step.Execute(new DayEndContext(3));

            Assert.AreEqual(0, rig.Bank.State.Ledger.Records.Count(r => r.TypeId == "wasted_appraisal"));
            Assert.AreEqual(Tl(200), rig.Bank.Economy.PendingAppraisalCost(101));
        }

        [Test]
        public void ListingExpiryStep_ExpiringListingWithoutFees_WritesNothing()
        {
            var rig = new ExpiryRig();
            rig.Add(1, 1);

            Assert.IsTrue(rig.Step.Execute(new DayEndContext(3)).IsSuccess);

            Assert.AreEqual(1, rig.Bank.State.Ledger.Count, "yalnızca başlangıç sermayesi");
        }

        private static Esnaf.Core.Money Tl(long tl)
        {
            return Esnaf.Core.Money.FromTl(tl);
        }

        [Test]
        public void ListingExpiryStep_RejectsNulls()
        {
            var economy = new EconomyHarness().Economy;
            Assert.Throws<ArgumentNullException>(() => new ListingExpiryStep(null, new InstanceStore(), economy, null));
            Assert.Throws<ArgumentNullException>(() => new ListingExpiryStep(new MarketState(), null, economy, null));
            Assert.Throws<ArgumentNullException>(() => new ListingExpiryStep(new MarketState(), new InstanceStore(), null, null));
            Assert.Throws<ArgumentNullException>(() => new ExpiryRig().Step.Execute(null));
        }

        // ---------- Adım 7: yeni gün ----------

        private sealed class NewDayRig
        {
            public readonly MarketHarness Harness;
            public readonly TimeState Time;
            public readonly MarketState Market = new MarketState();
            public readonly EventBus Bus = new EventBus();
            public readonly List<string> Events = new List<string>();
            public readonly NewDayStep Step;

            public NewDayRig(int startDay = 1, ContentDatabase content = null)
            {
                Harness = new MarketHarness(5UL, content);
                Time = new TimeState(startDay, 5UL);
                Step = new NewDayStep(Time, Market, Harness.Generator, Harness.Rng, Bus);
                Bus.Subscribe<DayEnded>(e => Events.Add("ended:" + e.Day));
                Bus.Subscribe<DayStarted>(e => Events.Add("started:" + e.Day));
                Bus.Subscribe<ListingsGenerated>(e => Events.Add("listings:" + e.Day + ":" + string.Join(",", e.ListingIds)));
            }
        }

        [Test]
        public void NewDayStep_HasTheFixedIdentity()
        {
            var rig = new NewDayRig();

            Assert.AreEqual("new_day", rig.Step.Id);
            Assert.AreEqual(7, rig.Step.Order);
        }

        [Test]
        public void NewDayStep_AdvancesTheDay_AndOpensTheMarketForIt()
        {
            var rig = new NewDayRig();
            var ctx = new DayEndContext(1);

            Result result = rig.Step.Execute(ctx);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(2, rig.Time.Day);
            Assert.AreEqual(2, ctx.NewDay);
            Assert.AreEqual(5, rig.Market.Count, "Gün 2: 5 ilan");
            CollectionAssert.AreEqual(rig.Market.Listings.Select(l => l.ListingId).ToArray(), ctx.NewListingIds.ToArray());
            Assert.IsTrue(rig.Market.Listings.All(l => l.DayListed == 2));
        }

        [Test]
        public void NewDayStep_PublishesDayEnded_DayStarted_ListingsGenerated_InThatOrder()
        {
            var rig = new NewDayRig();

            rig.Step.Execute(new DayEndContext(1));

            Assert.AreEqual(3, rig.Events.Count);
            Assert.AreEqual("ended:1", rig.Events[0]);
            Assert.AreEqual("started:2", rig.Events[1]);
            string ids = string.Join(",", rig.Market.Listings.Select(l => l.ListingId));
            Assert.AreEqual("listings:2:" + ids, rig.Events[2]);
        }

        [Test]
        public void NewDayStep_UsesOnlyTheMarketStream()
        {
            var rig = new NewDayRig();

            rig.Step.Execute(new DayEndContext(1));

            CollectionAssert.AreEqual(new[] { "market" }, rig.Harness.Rng.Capture().Keys.ToArray());
        }

        [Test]
        public void NewDayStep_DayMismatch_FailsWithoutChangingAnything()
        {
            var rig = new NewDayRig(startDay: 5);

            Result result = rig.Step.Execute(new DayEndContext(1));

            Assert.IsTrue(result.IsFailure);
            Assert.AreEqual("time.day_mismatch", result.ErrorCode);
            Assert.AreEqual(5, rig.Time.Day);
            Assert.AreEqual(0, rig.Market.Count);
            Assert.AreEqual(0, rig.Events.Count);
        }

        [Test]
        public void NewDayStep_WhenGenerationFails_LeavesTimeMarketAndEventsUntouched()
        {
            ContentDatabase content = MarketHarness.FixtureContent(
                ContentFixtures.EconomyConstantsJson.Replace("\"minPerDay\": 2", "\"minPerDay\": 9"));
            var rig = new NewDayRig(1, content);

            Result result = rig.Step.Execute(new DayEndContext(1));

            Assert.IsTrue(result.IsFailure);
            Assert.AreEqual("market.generation_failed", result.ErrorCode);
            Assert.AreEqual(1, rig.Time.Day, "gün ilerlememeli");
            Assert.AreEqual(0, rig.Market.Count);
            Assert.AreEqual(0, rig.Events.Count);
            Assert.AreEqual(0, rig.Harness.Store.Count);
        }

        [Test]
        public void OpenFirstDay_OpensTheCurrentDay_WithoutAdvancing()
        {
            var rig = new NewDayRig();

            Result result = rig.Step.OpenFirstDay();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, rig.Time.Day);
            Assert.AreEqual(3, rig.Market.Count);
            Assert.AreEqual(1, rig.Market.Listings.Count(l => l.IsGuided));
            Assert.AreEqual(2, rig.Events.Count);
            Assert.AreEqual("started:1", rig.Events[0]);
            StringAssert.StartsWith("listings:1:", rig.Events[1]);
        }

        [Test]
        public void OpenFirstDay_WhenGenerationFails_ReportsIt()
        {
            ContentDatabase content = MarketHarness.FixtureContent(
                ContentFixtures.EconomyConstantsJson.Replace("\"minPerDay\": 2", "\"minPerDay\": 9"));
            var rig = new NewDayRig(2, content);

            Result result = rig.Step.OpenFirstDay();

            Assert.IsTrue(result.IsFailure);
            Assert.AreEqual("market.generation_failed", result.ErrorCode);
            Assert.AreEqual(0, rig.Events.Count);
        }

        [Test]
        public void NewDayStep_RejectsNulls()
        {
            var rig = new NewDayRig();
            var h = rig.Harness;

            Assert.Throws<ArgumentNullException>(() => new NewDayStep(null, rig.Market, h.Generator, h.Rng, rig.Bus));
            Assert.Throws<ArgumentNullException>(() => new NewDayStep(rig.Time, null, h.Generator, h.Rng, rig.Bus));
            Assert.Throws<ArgumentNullException>(() => new NewDayStep(rig.Time, rig.Market, null, h.Rng, rig.Bus));
            Assert.Throws<ArgumentNullException>(() => new NewDayStep(rig.Time, rig.Market, h.Generator, null, rig.Bus));
            Assert.Throws<ArgumentNullException>(() => rig.Step.Execute(null));
        }
    }
}
