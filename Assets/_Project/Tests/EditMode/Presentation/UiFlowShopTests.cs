using System.IO;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Business;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Game;
using Esnaf.Presentation;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Presentation
{
    /// <summary>
    /// Gün 13.4 — Dükkan ana ekranı (UiFlow.ShopScreen) ve kalıcı alt navigasyon (UiFlow.Nav): HUD (gün/saat/kasa), telefon rafı (satışta / satış dışı, görsel, fiyat, kapasite 15),
    /// aksesuar rafı (60 kapasite), aktif müşteri, mevcut Raf paneline ve satış akışına bağlantı. Yeni ekonomi/müşteri mantığı yok.
    /// </summary>
    public class UiFlowShopTests
    {
        private const ulong Seed = 20260101UL;
        private const string Supplier = "supplier.ucuz_toptan";
        private const string Adapter = "accessory.charger_adapter";

        private static GameSession New(ulong seed = Seed)
        {
            return GameSession.NewGame(MarketHarness.RealContent(), seed);
        }

        private static UiFlow ShopFlow(GameSession s)
        {
            var flow = new UiFlow(s.Api, new ContentPresentation(s.Content), s.Bus);
            flow.GoToShop();
            return flow;
        }

        private static void BuyAndPrice(GameSession s, int count, int pricedCount)
        {
            foreach (var l in s.Api.GetListings().Take(count).ToList())
            {
                Assert.IsTrue(s.Api.BuyListing(l.ListingId).IsSuccess);
            }

            var stock = s.Api.GetInventory();
            for (int i = 0; i < pricedCount; i++)
            {
                Assert.IsTrue(s.Api.SetPrice(stock[i].InstanceId, Money.FromTl((stock[i].CostBasis.Tl + 9) / 10 * 10)).IsSuccess);
            }
        }

        // ---------- HUD ----------

        [Test]
        public void TheHud_ShowsDayClockAndCash_AndFollowsThem()
        {
            GameSession s = New();
            using (UiFlow flow = ShopFlow(s))
            {
                Assert.AreEqual(TurkishTexts.Day(1), flow.TopBar.DayText);
                Assert.AreEqual("09:00", flow.TopBar.ClockText);
                Assert.AreEqual(TurkishTexts.Cash(s.Api.GetCash()), flow.TopBar.CashText);

                flow.Tick(10.0 / UiFlow.GameMinutesPerRealSecond * 3); // ~30 oyun dk
                s.Api.AdvanceTime(0);
                flow.Refresh();

                Assert.AreEqual(s.Api.GetClock().Text, flow.TopBar.ClockText);
                StringAssert.StartsWith("09:", flow.TopBar.ClockText);
            }
        }

        // ---------- telefon rafı ----------

        [Test]
        public void ThePhoneShelf_ShowsEveryPhone_WithModelImagePriceAndCapacityOutOfFifteen()
        {
            GameSession s = New();
            BuyAndPrice(s, 3, 2);
            using (UiFlow flow = ShopFlow(s))
            {
                ShopScreenViewModel shop = flow.ShopScreen;
                var stock = s.Api.GetInventory();

                Assert.AreEqual("RAF  3/15", shop.ShelfHeader);
                Assert.AreEqual(2, shop.SellablePhones.Count);
                Assert.AreEqual(1, shop.OffSalePhones.Count);
                Assert.IsNull(shop.ShelfEmptyNote);
                for (int i = 0; i < 2; i++)
                {
                    ShopPhoneViewModel tile = shop.SellablePhones.Single(p => p.InstanceId == stock[i].InstanceId);
                    Assert.AreEqual(stock[i].DefinitionId, tile.DefinitionId, "doğru telefon görseli (model kimliği)");
                    Assert.AreEqual(flow.Content.ModelName(stock[i].DefinitionId), tile.Model);
                    Assert.AreEqual(MoneyFormatter.Format(stock[i].ListPrice), tile.PriceText, "satış fiyatı");
                    Assert.IsTrue(tile.IsSellable);
                }

                ShopPhoneViewModel off = shop.OffSalePhones.Single();
                Assert.AreEqual(stock[2].InstanceId, off.InstanceId);
                Assert.AreEqual(TurkishTexts.ShopOffSale, off.PriceText, "fiyatsız: satışta değil");
                Assert.IsFalse(off.IsSellable);
                Assert.AreEqual(stock[2].DefinitionId, off.DefinitionId);
            }
        }

        [Test]
        public void EveryShelfPhonesImage_ExistsInThePhoneCatalog()
        {
            GameSession s = New();
            BuyAndPrice(s, 3, 3);
            string phones = Path.Combine(Directory.GetParent(Directory.GetParent(TestPaths.ContentDataDirectory()).FullName).FullName, "Art", "Phones");
            var folders = Directory.GetDirectories(phones).Select(d => Path.GetFileName(d).ToLowerInvariant()).ToList();
            using (UiFlow flow = ShopFlow(s))
            {
                foreach (ShopPhoneViewModel p in flow.ShopScreen.SellablePhones)
                {
                    CollectionAssert.Contains(folders, PhoneAssetNaming.ModelKey(p.DefinitionId));
                }
            }
        }

        [Test]
        public void AnEmptyShelf_ShowsAnExplainingNote()
        {
            GameSession s = New();
            using (UiFlow flow = ShopFlow(s))
            {
                ShopScreenViewModel shop = flow.ShopScreen;

                Assert.AreEqual("RAF  0/15", shop.ShelfHeader);
                Assert.AreEqual(0, shop.SellablePhones.Count + shop.OffSalePhones.Count);
                Assert.AreEqual(TurkishTexts.ShopShelfEmpty, shop.ShelfEmptyNote);
                StringAssert.Contains("İlanlar", shop.ShelfEmptyNote);
            }
        }

        // ---------- telefona dokunma → mevcut raf paneli ----------

        [Test]
        public void TappingAPhone_OpensTheExistingShelfPricingPanel_AndBackReturnsToTheShop()
        {
            GameSession s = New();
            BuyAndPrice(s, 2, 1);
            using (UiFlow flow = ShopFlow(s))
            {
                long id = s.Api.GetInventory()[1].InstanceId; // fiyatsız

                Assert.IsTrue(flow.OpenShelfItemFromShop(id));

                Assert.AreEqual(UiScreen.Shelf, flow.CurrentScreen, "mevcut Raf ekranı");
                Assert.IsNotNull(flow.ShelfScreen.Editor, "fiyat paneli o telefonla açık");
                Assert.AreEqual(id, flow.ShelfScreen.Editor.InstanceId);
                Assert.IsNull(flow.ShopScreen, "Dükkan ekranı gizli");
                flow.SetShelfPrice(s.Api.GetInventory()[1].CostBasis.Tl + 500);
                Assert.IsTrue(flow.SaveShelfPrice().IsSuccess, "mevcut fiyatlandırma çalışır");
                Assert.IsTrue(s.Api.GetInventory()[1].ListPrice.IsPositive);

                flow.Back();
                flow.Back();

                Assert.AreEqual(UiScreen.Shop, flow.CurrentScreen, "Geri Dükkan'a döner");
                Assert.AreEqual(2, flow.ShopScreen.SellablePhones.Count, "yeni fiyatlı telefon Dükkan'da satışta");
            }
        }

        [Test]
        public void ThePhoneTap_IsRefusedOffTheShopScreen_OrForAnUnknownPhone()
        {
            GameSession s = New();
            BuyAndPrice(s, 1, 1);
            using (UiFlow flow = new UiFlow(s.Api, new ContentPresentation(s.Content), s.Bus))
            {
                long id = s.Api.GetInventory()[0].InstanceId;
                Assert.IsFalse(flow.OpenShelfItemFromShop(id), "İlanlar ekranındayken");
                flow.GoToShop();
                Assert.IsFalse(flow.OpenShelfItemFromShop(987654L));
                Assert.AreEqual(UiScreen.Shop, flow.CurrentScreen);
            }
        }

        // ---------- aksesuarlar ----------

        [Test]
        public void TheAccessoryShelf_ShowsTheExistingStock_WithTheSixtyCapacity()
        {
            GameSession s = New();
            Assert.IsTrue(s.Api.BuyWholesalePack(Supplier, Adapter).IsSuccess);
            using (UiFlow flow = ShopFlow(s))
            {
                ShopScreenViewModel shop = flow.ShopScreen;
                var stock = s.Api.GetAccessoryStock();

                Assert.AreEqual(60, stock.Capacity, "60 kapasite sistemi aynı");
                Assert.AreEqual(TurkishTexts.ShopAccessoryHeader(stock.TotalUnits, 60), shop.AccessoryHeader);
                StringAssert.EndsWith("/60", shop.AccessoryHeader);
                Assert.AreEqual(stock.Lines.Count, shop.Accessories.Count);
                ShopAccessoryViewModel row = shop.Accessories.Single(a => a.AccessoryId == Adapter);
                Assert.AreEqual(stock.Lines.Single(l => l.AccessoryId == Adapter).AccessoryName, row.Name);
                Assert.AreEqual(stock.Lines.Single(l => l.AccessoryId == Adapter).Quantity.ToString(), row.QuantityText);
                Assert.IsNull(shop.AccessoriesEmptyNote);
            }
        }

        [Test]
        public void AnEmptyAccessoryStock_ShowsAnExplainingNote()
        {
            GameSession s = New();
            using (UiFlow flow = ShopFlow(s))
            {
                Assert.AreEqual(0, flow.ShopScreen.Accessories.Count);
                Assert.AreEqual(TurkishTexts.ShopAccessoriesEmpty, flow.ShopScreen.AccessoriesEmptyNote);
                StringAssert.Contains("Toptancı", flow.ShopScreen.AccessoriesEmptyNote);
                StringAssert.EndsWith("0/60", flow.ShopScreen.AccessoryHeader);
            }
        }

        // ---------- aktif müşteri ----------

        // Rafında müşterinin ilgilendiği telefon olan oturum; ilk müşteri geldiğinde döner.
        private static GameSession WithArrivedCustomer(out QueuedCustomer first)
        {
            for (ulong seed = 1; seed <= 400; seed++)
            {
                GameSession s = New(seed);
                BuyAndPrice(s, 1, 1);
                first = s.CustomerQueue.PlanFor(1)[0];
                s.Api.AdvanceTime(first.ArrivalMinute - s.Api.GetClock().MinuteOfDay);
                CustomerView v = s.Api.GetActiveCustomer();
                if (v != null && v.InstanceId != 0)
                {
                    return s;
                }
            }

            throw new System.InvalidOperationException("No seed.");
        }

        [Test]
        public void TheActiveCustomer_IsShownInTheShop_WithNameInterestAndAGoButton()
        {
            QueuedCustomer first;
            GameSession s = WithArrivedCustomer(out first);
            using (UiFlow flow = ShopFlow(s))
            {
                ShopCustomerViewModel c = flow.ShopScreen.Customer;

                Assert.IsNotNull(c);
                Assert.AreEqual(first.CustomerId, c.CustomerId, "doğru aktif müşteri");
                Assert.AreEqual(first.NpcId, c.NpcId);
                Assert.AreEqual(flow.Content.CustomerName(first.CustomerId, first.NpcId), c.Name, "mevcut ad/portre sistemi");
                Assert.AreEqual(TurkishTexts.ShopCustomerTitle(c.Name), c.Title);
                Assert.IsTrue(c.CanGo);
                Assert.AreEqual("Müşteriye Git", c.GoButtonText);
            }
        }

        [Test]
        public void WithoutACustomer_TheShopSaysSo()
        {
            GameSession s = New();
            BuyAndPrice(s, 1, 1);
            using (UiFlow flow = ShopFlow(s))
            {
                Assert.IsNull(flow.ShopScreen.Customer);
                Assert.AreEqual(TurkishTexts.ShopNoCustomer, flow.ShopScreen.CustomerNote);
            }
        }

        [Test]
        public void GoToActiveCustomer_OpensTheExistingSaleFlow()
        {
            QueuedCustomer first;
            GameSession s = WithArrivedCustomer(out first);
            using (UiFlow flow = ShopFlow(s))
            {
                Result go = flow.GoToActiveCustomer();

                Assert.IsTrue(go.IsSuccess, go.ErrorCode);
                Assert.AreEqual(UiScreen.Sale, flow.CurrentScreen);
                Assert.AreEqual(first.CustomerId, s.Api.GetSale().CustomerId, "mevcut satış açıldı");
                Assert.IsNull(flow.ShopScreen);
            }
        }

        [Test]
        public void GoToActiveCustomer_WithoutAnyone_ChangesNothing()
        {
            GameSession s = New();
            using (UiFlow flow = ShopFlow(s))
            {
                Assert.AreEqual("ui.no_active_customer", flow.GoToActiveCustomer().ErrorCode);
                Assert.AreEqual(UiScreen.Shop, flow.CurrentScreen);
            }
        }

        // ---------- müşteri bildirimi çakışmaz ----------

        [Test]
        public void TheCustomerNotice_CoexistsWithTheShop_WithoutMovingThePlayer()
        {
            QueuedCustomer first;
            GameSession probe = WithArrivedCustomer(out first);
            GameSession s = New(probe.Time.MasterSeed);
            BuyAndPrice(s, 1, 1);
            using (UiFlow flow = ShopFlow(s))
            {
                for (int i = 0; i < 400 && flow.Notices.Count == 0; i++)
                {
                    flow.Tick(1.0 / UiFlow.GameMinutesPerRealSecond);
                }

                Assert.AreEqual(1, flow.Notices.Count, "13.1 bildirimi Dükkan'da da doğar");
                Assert.AreEqual(UiScreen.Shop, flow.CurrentScreen, "oyuncu Dükkan'dan çıkarılmaz");
                Assert.IsNotNull(flow.ShopScreen.Customer, "Dükkan aynı müşteriyi gösterir");
                Assert.AreEqual(flow.Notices[0].CustomerId, flow.ShopScreen.Customer.CustomerId, "bildirim ve Dükkan kartı aynı müşteri");

                Assert.IsTrue(flow.DismissNotice(first.CustomerId));
                Assert.IsNotNull(flow.ShopScreen.Customer, "bildirimi kapatmak müşteriyi mağazadan çıkarmaz");

                Assert.AreEqual(UiScreen.Shop, flow.CurrentScreen);
            }
        }

        [Test]
        public void TheNoticesGoToCustomer_WorksFromTheShop()
        {
            QueuedCustomer first;
            GameSession probe = WithArrivedCustomer(out first);
            GameSession s = New(probe.Time.MasterSeed);
            BuyAndPrice(s, 1, 1);
            using (UiFlow flow = ShopFlow(s))
            {
                for (int i = 0; i < 400 && flow.Notices.Count == 0; i++)
                {
                    flow.Tick(1.0 / UiFlow.GameMinutesPerRealSecond);
                }

                Assert.IsTrue(flow.GoToCustomer(flow.Notices[0].CustomerId).IsSuccess);

                Assert.AreEqual(UiScreen.Sale, flow.CurrentScreen);
            }
        }

        // ---------- alt navigasyon ----------

        [Test]
        public void TheNavBar_HasTheFourTabs_AndTheActiveOneFollowsTheScreen()
        {
            GameSession s = New();
            using (UiFlow flow = ShopFlow(s))
            {
                CollectionAssert.AreEqual(new[] { "Dükkan", "Toptancı", "İlanlar", "Profil" }, flow.Nav.Items.Select(i => i.Label).ToArray());
                Assert.AreEqual(NavTab.Shop, flow.Nav.Active);
                Assert.IsTrue(flow.Nav.Items.Single(i => i.Tab == NavTab.Shop).IsActive);
                Assert.AreEqual(1, flow.Nav.Items.Count(i => i.IsActive));

                flow.GoToWholesale();
                Assert.AreEqual(NavTab.Wholesale, flow.Nav.Active);
                flow.OpenAccessoryStock();
                Assert.AreEqual(NavTab.Wholesale, flow.Nav.Active, "aksesuar stoğu Toptancı sekmesinde");
                flow.GoToListings();
                Assert.AreEqual(NavTab.Listings, flow.Nav.Active);
                flow.OpenListing(s.Api.GetListings()[0].ListingId);
                Assert.AreEqual(NavTab.Listings, flow.Nav.Active, "ilan detayı İlanlar sekmesinde");
                flow.GoToProfile();
                Assert.AreEqual(NavTab.Profile, flow.Nav.Active);
            }
        }

        [Test]
        public void TheNavTabs_GoToTheRightScreens_FromAnyScreen()
        {
            GameSession s = New();
            BuyAndPrice(s, 1, 1);
            using (UiFlow flow = new UiFlow(s.Api, new ContentPresentation(s.Content), s.Bus))
            {
                foreach (NavTab from in new[] { NavTab.Listings, NavTab.Wholesale, NavTab.Profile, NavTab.Shop })
                {
                    flow.GoToTab(from);
                    flow.GoToTab(NavTab.Shop);
                    Assert.AreEqual(UiScreen.Shop, flow.CurrentScreen);
                    flow.GoToTab(from);
                    flow.GoToTab(NavTab.Wholesale);
                    Assert.AreEqual(UiScreen.Wholesale, flow.CurrentScreen);
                    flow.GoToTab(from);
                    flow.GoToTab(NavTab.Listings);
                    Assert.AreEqual(UiScreen.Listings, flow.CurrentScreen);
                    flow.GoToTab(from);
                    flow.GoToTab(NavTab.Profile);
                    Assert.AreEqual(UiScreen.Profile, flow.CurrentScreen);
                }
            }
        }

        [Test]
        public void TheNavBar_WorksFromDeepScreens_AndKeepsARunningNegotiation()
        {
            GameSession s = New();
            using (UiFlow flow = new UiFlow(s.Api, new ContentPresentation(s.Content), s.Bus))
            {
                flow.OpenListing(s.Api.GetListings()[0].ListingId);
                flow.OpenNegotiation();
                Assert.AreEqual(UiScreen.Negotiation, flow.CurrentScreen);

                flow.GoToShop();

                Assert.AreEqual(UiScreen.Shop, flow.CurrentScreen);
                Assert.IsNotNull(s.Api.GetNegotiation(), "süren pazarlık oyunda açık kalır (Geri'deki gibi)");
                flow.GoToListings();
                Assert.AreEqual(UiScreen.Listings, flow.CurrentScreen);
            }
        }

        [Test]
        public void TheProfileTab_IsAPlaceholderForNow_AndTheShopStaysStableOnTicks()
        {
            GameSession s = New();
            using (UiFlow flow = ShopFlow(s))
            {
                flow.GoToProfile();
                Assert.AreEqual(UiScreen.Profile, flow.CurrentScreen);
                Assert.IsNull(flow.ShopScreen);
                flow.Tick(1.0);
                Assert.AreEqual(UiScreen.Profile, flow.CurrentScreen, "saat ilerlemek ekranı değiştirmez");
            }
        }

        // ---------- yeniden çizim ve oyun durumu ----------

        [Test]
        public void TheShop_IsRedrawnOnlyWhenItsContentChanges()
        {
            GameSession s = New();
            BuyAndPrice(s, 1, 1);
            using (UiFlow flow = ShopFlow(s))
            {
                int changed = 0;
                flow.Changed += () => changed++;

                flow.Tick(1.0 / UiFlow.GameMinutesPerRealSecond);
                flow.Tick(1.0 / UiFlow.GameMinutesPerRealSecond);

                Assert.AreEqual(0, changed, "içerik değişmedikçe yeniden kurulmaz");

                var l = s.Api.GetListings()[0];
                s.Api.BuyListing(l.ListingId);
                flow.Tick(1.0 / UiFlow.GameMinutesPerRealSecond);

                Assert.GreaterOrEqual(changed, 1, "raf değişince Dükkan yeniden çizilir");
            }
        }

        [Test]
        public void TheShopAndNavigation_DoNotChangeTheGameState_OrTheRngStreams()
        {
            GameSession s = New();
            BuyAndPrice(s, 2, 1);
            string digest = s.Api.GetStateDigest();
            var rng = s.Capture().Rng;
            using (UiFlow flow = ShopFlow(s))
            {
                flow.GoToWholesale();
                flow.GoToListings();
                flow.GoToProfile();
                flow.GoToShop();
                flow.OpenShelfItemFromShop(s.Api.GetInventory()[1].InstanceId);
                flow.Back();
                flow.Back();
            }

            Assert.AreEqual(digest, s.Api.GetStateDigest());
            Assert.IsNull(DeepCompare.FirstDifference(rng, s.Capture().Rng));
        }
    }
}
