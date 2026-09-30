using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Esnaf.Core;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Game;
using Esnaf.Domain.Market;
using Esnaf.Domain.Products;
using Esnaf.Domain.Time;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Game
{
    public class GameApiTests
    {
        private static GameSession New(ulong seed = 42UL, IEventBus bus = null)
        {
            return GameSession.NewGame(MarketHarness.RealContent(), seed, bus);
        }

        // ---------- sorgular ----------

        [Test]
        public void Queries_AtTheStart()
        {
            IGameApi api = New().Api;

            Assert.AreEqual(1, api.GetDay());
            Assert.AreEqual(Money.FromTl(250000), api.GetCash());
            Assert.AreEqual(3, api.GetListings().Count);
            Assert.AreEqual(0, api.GetInventory().Count);
        }

        [Test]
        public void GetListings_ShowsOnlyPublicInformation()
        {
            GameSession s = New();
            MarketListing guided = s.Market.Listings.Single(l => l.IsGuided);

            ListingView view = s.Api.GetListings().Single(v => v.ListingId == guided.ListingId);

            Assert.AreEqual(guided.InstanceId, view.InstanceId);
            Assert.AreEqual("phone.yildiz_y5", view.DefinitionId);
            Assert.AreEqual(64, view.StorageGb);
            Assert.AreEqual(24, view.AgeMonths);
            Assert.IsFalse(view.HasBox);
            Assert.IsFalse(view.HasInvoice);
            Assert.AreEqual("npc.ayse", view.SellerNpcId);
            Assert.AreEqual(Money.FromTl(5800), view.AskingPrice);
            Assert.AreEqual(1, view.DayListed);
            Assert.AreEqual(guided.RemainingDays, view.RemainingDays);
            Assert.AreEqual(0, view.Tags.Count);
        }

        [Test]
        public void GetListings_MirrorsBoxInvoiceAndTagsOfEveryListing()
        {
            GameSession s = New(7UL);
            for (int i = 0; i < 12; i++)
            {
                Assert.IsTrue(s.Api.EndDay().IsSuccess);
            }

            IReadOnlyList<ListingView> views = s.Api.GetListings();

            Assert.AreEqual(s.Market.Count, views.Count);
            for (int i = 0; i < views.Count; i++)
            {
                MarketListing listing = s.Market.Listings[i];
                ProductInstance instance = s.Store.Get(listing.InstanceId);
                Assert.AreEqual(listing.ListingId, views[i].ListingId, "aynı sırada");
                Assert.AreEqual(instance.DefinitionId, views[i].DefinitionId);
                Assert.AreEqual(instance.StorageGb, views[i].StorageGb);
                Assert.AreEqual(instance.AgeMonths, views[i].AgeMonths);
                Assert.AreEqual(instance.GetFlag("box"), views[i].HasBox);
                Assert.AreEqual(instance.GetFlag("invoice"), views[i].HasInvoice);
                Assert.AreEqual(listing.AskingPrice, views[i].AskingPrice);
                Assert.AreEqual(listing.DayListed, views[i].DayListed);
                Assert.AreEqual(listing.RemainingDays, views[i].RemainingDays);
                CollectionAssert.AreEqual(listing.Tags.ToArray(), views[i].Tags.ToArray());
            }
        }

        [Test]
        public void ListingView_HasNoHiddenMembers()
        {
            string[] properties = typeof(ListingView).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            string[] fields = typeof(ListingView).GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Select(f => f.Name).ToArray();

            CollectionAssert.AreEqual(
                new[]
                {
                    "AgeMonths", "AskingPrice", "DayListed", "DefinitionId", "HasBox", "HasInvoice", "InstanceId",
                    "ListingId", "RemainingDays", "SellerNpcId", "StorageGb", "Tags"
                },
                properties,
                "ListingView'e gizli bilgi (R, inandığı değer, fırsat/tuzak) eklenmemeli");
            Assert.AreEqual(0, fields.Length, "public alan olmamalı");
        }

        [Test]
        public void GetListings_ReturnsACopy_NotTheLiveState()
        {
            GameSession s = New();

            IReadOnlyList<ListingView> before = s.Api.GetListings();
            Assert.IsTrue(s.Api.EndDay().IsSuccess);

            Assert.AreEqual(3, before.Count, "eski görünüm değişmemeli");
            Assert.AreNotEqual(before.Count, s.Api.GetListings().Count);
            Assert.IsFalse(before is List<ListingView>);
            Assert.AreNotSame(s.Api.GetListings(), s.Api.GetListings());
        }

        [Test]
        public void ListingView_TagsAreReadOnly()
        {
            GameSession s = New();

            Assert.IsFalse(s.Api.GetListings()[0].Tags is List<string>);
        }

        [Test]
        public void GetInventory_ListsOwnedItemsWithTheirCostBasis()
        {
            GameSession s = New();
            ProductInstance phone = s.Store.Get(s.Market.Listings[0].InstanceId);
            Assert.IsTrue(s.InventoryService.Acquire(phone.InstanceId, Money.FromTl(4800), 1).IsSuccess);

            IReadOnlyList<StockLine> stock = s.Api.GetInventory();

            Assert.AreEqual(1, stock.Count);
            Assert.AreEqual(phone.InstanceId, stock[0].InstanceId);
            Assert.AreEqual(Money.FromTl(4800), stock[0].CostBasis);
            Assert.AreEqual(Money.FromTl(245200), s.Api.GetCash());
        }

        [Test]
        public void GetTodaySummary_AtTheStart_ShowsAnUntouchedDayOne()
        {
            DaySummary today = New().Api.GetTodaySummary();

            Assert.AreEqual(1, today.Day);
            Assert.AreEqual(Money.FromTl(250000), today.OpeningCash, "sabah nakit = başlangıç sermayesi");
            Assert.AreEqual(Money.FromTl(250000), today.ClosingCash);
            Assert.AreEqual(Money.Zero, today.NetProfit);
            Assert.AreEqual(0, today.Records.Count);
            Assert.AreEqual(Money.FromTl(250000), today.Wealth.Total);
        }

        [Test]
        public void GetTodaySummary_FollowsTheDaySoFar()
        {
            GameSession s = New();
            ProductInstance phone = s.Store.Get(s.Market.Listings[0].InstanceId);
            Assert.IsTrue(s.InventoryService.Acquire(phone.InstanceId, Money.FromTl(4800), 1).IsSuccess);

            DaySummary today = s.Api.GetTodaySummary();

            Assert.AreEqual(Money.FromTl(4800), today.PurchaseSpend);
            Assert.AreEqual(Money.FromTl(245200), today.ClosingCash);
            Assert.AreEqual(1, today.StockCount);
            Assert.AreEqual(Money.FromTl(4800), today.StockCostBasis);
        }

        [Test]
        public void GetTodaySummary_AfterEndDay_StartsTheNewDayFromYesterdaysClosingCash()
        {
            GameSession s = New();
            s.Api.EndDay();
            s.Api.EndDay();
            DayEndReport day3 = s.Api.EndDay().Value;

            DaySummary today = s.Api.GetTodaySummary();

            Assert.AreEqual(4, today.Day);
            Assert.AreEqual(day3.Summary.ClosingCash, today.OpeningCash);
            Assert.AreEqual(Money.FromTl(249500), today.OpeningCash);
            Assert.AreEqual(0, today.Records.Count);
        }

        [Test]
        public void Queries_DoNotChangeTheState()
        {
            GameSession s = New();
            string before = GameStateDigest.Describe(s);

            s.Api.GetDay();
            s.Api.GetCash();
            s.Api.GetListings();
            s.Api.GetInventory();
            s.Api.GetTodaySummary();
            s.Api.GetStateDigest();

            Assert.AreEqual(before, GameStateDigest.Describe(s));
        }

        // ---------- EndDay: "Günü Bitir" ----------

        [Test]
        public void EndDay_AdvancesTheDay_AndRefreshesTheListings()
        {
            GameSession s = New();
            IReadOnlyList<long> before = s.Api.GetListings().Select(v => v.ListingId).ToArray();

            Result<DayEndReport> result = s.Api.EndDay();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(2, s.Api.GetDay());
            IReadOnlyList<ListingView> after = s.Api.GetListings();
            Assert.AreEqual(3 + 5, after.Count, "Gün 1 ilanları (ömür ≥ 2) durur + Gün 2'nin 5 ilanı");
            CollectionAssert.AreEqual(before.ToArray(), after.Take(3).Select(v => v.ListingId).ToArray(), "eski ilanlar önce");
            Assert.IsTrue(after.Skip(3).All(v => v.DayListed == 2));
        }

        [Test]
        public void EndDay_Report_DescribesTheEndedDay()
        {
            GameSession s = New();

            DayEndReport report = s.Api.EndDay().Value;

            Assert.AreEqual(1, report.EndedDay);
            Assert.AreEqual(2, report.NewDay);
            Assert.AreEqual(Money.Zero, report.ExpenseCharged);
            Assert.AreEqual(0, report.ExpiredListingIds.Count, "Gün 1 ilanlarının ömrü en az 2");
            Assert.AreEqual(5, report.NewListingIds.Count);
            CollectionAssert.AreEqual(new[] { "missed_customers", "daily_expense", "listing_expiry", "demand_update", "new_day" }, report.ExecutedSteps.ToArray());
            Assert.AreEqual(1, report.Summary.Day);
            Assert.AreEqual(Money.FromTl(250000), report.Summary.ClosingCash);
            Assert.AreEqual(Money.Zero, report.Summary.NetProfit);
        }

        [Test]
        public void EndDay_DaysOneAndTwo_ChargeNoExpense_DayThreeCharges500()
        {
            GameSession s = New();

            s.Api.EndDay();
            Assert.AreEqual(Money.FromTl(250000), s.Api.GetCash(), "Gün 1 sonu gider 0");
            s.Api.EndDay();
            Assert.AreEqual(Money.FromTl(250000), s.Api.GetCash(), "Gün 2 sonu gider 0 (GDD T4)");
            DayEndReport day3 = s.Api.EndDay().Value;

            Assert.AreEqual(Money.FromTl(249500), s.Api.GetCash(), "GDD T4: Gün 3 sonu nakit 249.500");
            Assert.AreEqual(Money.FromTl(500), day3.ExpenseCharged);
            Assert.AreEqual(Money.FromTl(-500), day3.Summary.NetProfit, "GDD T4: gün net kârı −500");
            Assert.AreEqual(3, day3.EndedDay);
            Assert.AreEqual(4, day3.NewDay);
            Assert.AreEqual(4, s.Api.GetDay());
        }

        [Test]
        public void EndDay_ListingsLiveExactlyTheirLifetime()
        {
            GameSession s = New(5UL);
            var born = new Dictionary<long, KeyValuePair<int, int>>(); // listingId -> (gün, ömür)

            for (int day = 1; day <= 40; day++)
            {
                foreach (ListingView v in s.Api.GetListings())
                {
                    if (!born.ContainsKey(v.ListingId))
                    {
                        MarketListing l = s.Market.Listings.Single(x => x.ListingId == v.ListingId);
                        Assert.AreEqual(day, v.DayListed);
                        born[v.ListingId] = new KeyValuePair<int, int>(v.DayListed, l.RemainingDays);
                    }
                }

                HashSet<long> visible = new HashSet<long>(s.Api.GetListings().Select(v => v.ListingId));
                foreach (KeyValuePair<long, KeyValuePair<int, int>> pair in born)
                {
                    int listed = pair.Value.Key;
                    int life = pair.Value.Value;
                    bool shouldBeVisible = day >= listed && day <= listed + life - 1;
                    Assert.AreEqual(shouldBeVisible, visible.Contains(pair.Key), "ilan " + pair.Key + " gün " + day + " (doğum " + listed + ", ömür " + life + ")");
                }

                Assert.IsTrue(s.Api.EndDay().IsSuccess);
            }
        }

        [Test]
        public void EndDay_ExpiredListings_LeaveNoOrphanInstances()
        {
            GameSession s = New(6UL);

            for (int day = 1; day <= 200; day++)
            {
                s.Api.EndDay();

                var listingInstanceIds = new HashSet<long>(s.Market.Listings.Select(l => l.InstanceId));
                Assert.AreEqual(s.Market.Count, listingInstanceIds.Count, "her ilan ayrı ürün (gün " + day + ")");
                int onMarket = 0;
                foreach (ProductInstance instance in s.Store.All)
                {
                    Assert.AreEqual(ProductLocation.Market, instance.Location);
                    Assert.IsTrue(listingInstanceIds.Contains(instance.InstanceId), "sahipsiz pazar ürünü #" + instance.InstanceId + " gün " + day);
                    onMarket++;
                }

                Assert.AreEqual(s.Market.Count, onMarket);
            }
        }

        [Test]
        public void EndDay_KeepsAnItemTheShopBought_EvenWhenItsListingExpires()
        {
            GameSession s = New(8UL);
            MarketListing target = s.Market.Listings[0];
            long instanceId = target.InstanceId;
            Assert.IsTrue(s.InventoryService.Acquire(instanceId, Money.FromTl(1000), 1).IsSuccess);

            for (int day = 1; day <= 6; day++)
            {
                Assert.IsTrue(s.Api.EndDay().IsSuccess);
            }

            ProductInstance owned;
            Assert.IsTrue(s.Store.TryGet(instanceId, out owned));
            Assert.AreEqual(ProductLocation.Inventory, owned.Location);
            Assert.IsTrue(s.InventoryState.Contains(instanceId));
            MarketListing gone;
            Assert.IsFalse(s.Market.TryGet(target.ListingId, out gone), "ilanın kendisi kalkmış olmalı");
        }

        [Test]
        public void EndDay_PublishesEventsInThePipelineOrder()
        {
            var bus = new EventBus();
            var events = new List<string>();
            GameSession s = New(11UL, bus);
            bus.Subscribe<TransactionRecorded>(e => events.Add("tx:" + e.Record.TypeId));
            bus.Subscribe<CashChanged>(e => events.Add("cash:" + e.OldCash.Tl + ">" + e.NewCash.Tl));
            bus.Subscribe<ListingExpired>(e => events.Add("expired"));
            bus.Subscribe<DayEnded>(e => events.Add("ended:" + e.Day));
            bus.Subscribe<DayStarted>(e => events.Add("started:" + e.Day));
            bus.Subscribe<ListingsGenerated>(e => events.Add("listings:" + e.Day));

            // Gün 1–2: gider yok
            s.Api.EndDay();
            s.Api.EndDay();
            events.Clear();

            // Gün 3: gider (adım 2) → ilan ömürleri (adım 4) → gün bitti/başladı/ilanlar (adım 7)
            s.Api.EndDay();

            Assert.AreEqual("tx:daily_expense", events[0], "adım 2: gider satırı");
            Assert.AreEqual("cash:250000>249500", events[1], "adım 2: nakit değişimi");
            int ended = events.IndexOf("ended:3");
            Assert.GreaterOrEqual(ended, 2);
            Assert.IsTrue(events.Skip(2).Take(ended - 2).All(e => e == "expired"), "adım 4: araya yalnızca ilan-kalktı olayları girer");
            Assert.AreEqual("started:4", events[ended + 1], "adım 7: yeni gün");
            Assert.AreEqual("listings:4", events[ended + 2], "adım 7: yeni ilanlar");
            Assert.AreEqual(ended + 3, events.Count, "ilanlar son olay");
        }

        [Test]
        public void EndDay_LongRun_KeepsCountingDays()
        {
            GameSession s = New(12UL);

            for (int i = 0; i < 50; i++)
            {
                Assert.IsTrue(s.Api.EndDay().IsSuccess);
            }

            Assert.AreEqual(51, s.Api.GetDay());
            Assert.AreEqual(Money.FromTl(250000 - 500 * 48), s.Api.GetCash(), "Gün 3–50 arası 48 gün × 500");
        }

        [Test]
        public void EndDay_WhenTheMarketCannotBeOpened_FailsAndReports()
        {
            // Gün 2'nin fırsat kotası imkânsız: Gün 1 sonunda pazar açılamaz.
            var content = MarketHarness.FixtureContent(
                ContentFixtures.EconomyConstantsJson.Replace("\"minPerDay\": 2", "\"minPerDay\": 9"));
            GameSession s = GameSession.NewGame(content, 2UL);

            Result<DayEndReport> result = s.Api.EndDay();

            Assert.IsTrue(result.IsFailure);
            Assert.AreEqual("market.generation_failed", result.ErrorCode);
            Assert.AreEqual(1, s.Api.GetDay(), "gün ilerlememeli");
        }
    }
}
