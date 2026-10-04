using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Accessories;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Game;
using Esnaf.Domain.Wholesale;
using Esnaf.Presentation;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Presentation
{
    /// <summary>Toptancı ve Aksesuar Stoğu ekranları (Gün 11.2.3): IGameApi komutları, UiFlow, Türkçe metinler, gün kilidi, hatalar, telefon rafından bağımsız 60 birim kapasite.</summary>
    public class UiFlowWholesaleTests
    {
        private const string Supplier = "supplier.ucuz_toptan";
        private const string Adapter = "accessory.charger_adapter";
        private const string Case = "accessory.phone_case";
        private const string Powerbank = "accessory.powerbank";

        private static GameSession New(ulong seed = 1UL)
        {
            return GameSession.NewGame(MarketHarness.RealContent(), seed);
        }

        private static UiFlow Flow(GameSession s)
        {
            return new UiFlow(s.Api, new ContentPresentation(s.Content), s.Bus);
        }

        private static UiFlow OpenWholesale(GameSession s)
        {
            UiFlow flow = Flow(s);
            Assert.IsTrue(flow.OpenWholesale());
            return flow;
        }

        // ---------- IGameApi ----------

        [Test]
        public void TheApi_ListsTheOffers_WithPackAndPrices_AndTheDayLock()
        {
            GameSession s = New();

            var offers = s.Api.GetWholesaleOffers();

            Assert.AreEqual(6, offers.Count);
            WholesaleOfferView adapter = offers.Single(o => o.AccessoryId == Adapter);
            Assert.AreEqual(Supplier, adapter.SupplierId);
            Assert.AreEqual("Ucuz Toptan", adapter.SupplierName);
            Assert.AreEqual("Şarj Adaptörü", adapter.AccessoryName);
            Assert.AreEqual(Money.FromTl(150), adapter.UnitCost);
            Assert.AreEqual(10, adapter.PackSize);
            Assert.AreEqual(Money.FromTl(1500), adapter.PackCost);
            Assert.IsTrue(adapter.IsAvailableToday);
            WholesaleOfferView power = offers.Single(o => o.AccessoryId == Powerbank);
            Assert.AreEqual(3, power.AvailableFromDay);
            Assert.IsFalse(power.IsAvailableToday, "1. günde powerbank kilitli");
        }

        [Test]
        public void TheApiOffers_UnlockWhenTheDayComes()
        {
            GameSession s = New();
            s.Api.EndDay();
            Assert.IsFalse(s.Api.GetWholesaleOffers().Single(o => o.AccessoryId == Powerbank).IsAvailableToday);

            s.Api.EndDay();

            Assert.AreEqual(3, s.Api.GetDay());
            Assert.IsTrue(s.Api.GetWholesaleOffers().Single(o => o.AccessoryId == Powerbank).IsAvailableToday);
        }

        [Test]
        public void BuyingThroughTheApi_UsesTheServiceAndTodaysDay()
        {
            GameSession s = New();
            Money cash = s.Api.GetCash();

            Result<WholesalePurchaseReceipt> r = s.Api.BuyWholesalePack(Supplier, Adapter);

            Assert.IsTrue(r.IsSuccess, r.ErrorCode);
            Assert.AreEqual(Money.FromTl(1500), r.Value.TotalCost);
            Assert.AreEqual(cash - Money.FromTl(1500), s.Api.GetCash());
            AccessoryStockView stock = s.Api.GetAccessoryStock();
            Assert.AreEqual(10, stock.TotalUnits);
            Assert.AreEqual(Money.FromTl(1500), stock.TotalCost);
            Assert.AreEqual(10, stock.Lines.Single().Quantity);
            Assert.AreEqual(1, s.EconomyState.Ledger.Records.Count(x => x.TypeId == TransactionTypeIds.WholesalePurchase));
        }

        [Test]
        public void TheApi_RefusesTheLockedOffer_AndChangesNothing()
        {
            GameSession s = New();
            string digest = s.Api.GetStateDigest();

            Result<WholesalePurchaseReceipt> r = s.Api.BuyWholesalePack(Supplier, Powerbank);

            Assert.AreEqual("wholesale.not_available_yet", r.ErrorCode);
            Assert.AreEqual(digest, s.Api.GetStateDigest());
        }

        [Test]
        public void TheApiStock_IsEmptyAtTheStart_WithTheSixtyUnitCapacity()
        {
            AccessoryStockView stock = New().Api.GetAccessoryStock();

            Assert.AreEqual(60, stock.Capacity);
            Assert.AreEqual(0, stock.TotalUnits);
            Assert.AreEqual(0, stock.Lines.Count);
        }

        // ---------- ekran navigasyonu ----------

        [Test]
        public void TheScreens_OpenFromTheListings_AndBackReturnsThere()
        {
            GameSession s = New();
            using (UiFlow flow = Flow(s))
            {
                Assert.IsTrue(flow.OpenWholesale());
                Assert.AreEqual(UiScreen.Wholesale, flow.CurrentScreen);
                Assert.IsNotNull(flow.WholesaleScreen);
                Assert.IsNull(flow.AccessoryStockScreen);
                flow.Back();
                Assert.AreEqual(UiScreen.Listings, flow.CurrentScreen);
                Assert.IsNull(flow.WholesaleScreen);

                Assert.IsTrue(flow.OpenAccessoryStock());
                Assert.AreEqual(UiScreen.AccessoryStock, flow.CurrentScreen);
                Assert.IsNotNull(flow.AccessoryStockScreen);
                Assert.IsNull(flow.WholesaleScreen);
                flow.Back();
                Assert.AreEqual(UiScreen.Listings, flow.CurrentScreen);
            }
        }

        [Test]
        public void TheTwoScreens_LinkToEachOther()
        {
            using (UiFlow flow = OpenWholesale(New()))
            {
                Assert.IsTrue(flow.OpenAccessoryStock());
                Assert.IsTrue(flow.OpenWholesale());
                Assert.AreEqual(UiScreen.Wholesale, flow.CurrentScreen);
            }
        }

        [Test]
        public void TheScreens_CannotBeOpenedFromElsewhere()
        {
            GameSession s = New();
            using (UiFlow flow = Flow(s))
            {
                flow.OpenListing(flow.Listings[0].ListingId);

                Assert.IsFalse(flow.OpenWholesale());
                Assert.IsFalse(flow.OpenAccessoryStock());
                Assert.AreEqual(UiScreen.Detail, flow.CurrentScreen);
            }
        }

        // ---------- toptancı ekranı ----------

        [Test]
        public void TheWholesaleScreen_ShowsEveryOfferWithItsLines()
        {
            using (UiFlow flow = OpenWholesale(New()))
            {
                WholesaleScreenViewModel screen = flow.WholesaleScreen;

                Assert.AreEqual("Toptancı", screen.Title);
                Assert.AreEqual("Ucuz Toptan", screen.SupplierName);
                Assert.AreEqual(6, screen.Offers.Count);
                WholesaleOfferRowViewModel row = screen.Offers.Single(o => o.AccessoryId == Adapter);
                Assert.AreEqual("Şarj Adaptörü", row.Name);
                Assert.AreEqual("Paket: 10 adet", row.PackLine);
                Assert.AreEqual("Birim maliyet: 150 ₺", row.UnitCostLine);
                Assert.AreEqual("Paket fiyatı: 1.500 ₺", row.PackPriceLine);
                Assert.AreEqual("Satın Al", row.ButtonText);
                Assert.IsTrue(row.IsButtonEnabled);
                Assert.IsNull(row.LockNote);
                Assert.AreEqual("Paket: 20 adet", screen.Offers.Single(o => o.AccessoryId == "accessory.charge_cable").PackLine);
                Assert.AreEqual("Paket fiyatı: 1.200 ₺", screen.Offers.Single(o => o.AccessoryId == "accessory.charge_cable").PackPriceLine);
                Assert.AreEqual("Stok: 0 / 60 adet", screen.StockLine);
                Assert.AreEqual("Nakit: 250.000 ₺", screen.CashLine);
            }
        }

        [Test]
        public void ALockedOffer_IsDisabled_WithTheOpeningDay()
        {
            using (UiFlow flow = OpenWholesale(New()))
            {
                WholesaleOfferRowViewModel row = flow.WholesaleScreen.Offers.Single(o => o.AccessoryId == Powerbank);

                Assert.IsTrue(row.IsLocked);
                Assert.IsFalse(row.IsButtonEnabled);
                Assert.AreEqual("Gün 3'te açılır", row.ButtonText);
                Assert.AreEqual("Bu ürün Gün 3'te açılacak.", row.LockNote);
            }
        }

        [Test]
        public void TheLockedOffer_Unlocks_OnTheDay()
        {
            GameSession s = New();
            using (UiFlow flow = OpenWholesale(s))
            {
                flow.Back();
                flow.EndDay();
                flow.EndDay();
                flow.OpenWholesale();

                WholesaleOfferRowViewModel row = flow.WholesaleScreen.Offers.Single(o => o.AccessoryId == Powerbank);

                Assert.IsFalse(row.IsLocked);
                Assert.IsTrue(row.IsButtonEnabled);
                Assert.AreEqual("Satın Al", row.ButtonText);
            }
        }

        // ---------- satın alma ----------

        [Test]
        public void ASuccessfulBuy_UpdatesTheScreenAndTheGame_WithAFeedbackMessage()
        {
            GameSession s = New();
            using (UiFlow flow = OpenWholesale(s))
            {
                Money cash = s.Api.GetCash();
                int raised = 0;
                flow.Changed += () => raised++;

                Result<WholesalePurchaseReceipt> r = flow.BuyWholesalePack(Supplier, Adapter);

                Assert.IsTrue(r.IsSuccess, r.ErrorCode);
                Assert.AreEqual("10 adet Şarj Adaptörü stoğa eklendi.", flow.StatusMessage);
                Assert.AreEqual("Stok: 10 / 60 adet", flow.WholesaleScreen.StockLine);
                Assert.AreEqual("Nakit: " + MoneyFormatter.Format(cash - Money.FromTl(1500)), flow.WholesaleScreen.CashLine);
                Assert.AreEqual(cash - Money.FromTl(1500), s.Api.GetCash());
                Assert.AreEqual(10, s.AccessoryStock.Quantity(Adapter));
                Assert.AreEqual(Money.FromTl(1500), s.AccessoryStock.TotalCost(Adapter));
                Assert.GreaterOrEqual(raised, 1);
                Assert.AreEqual(TurkishTexts.Cash(s.Api.GetCash()), flow.TopBar.CashText, "\u00FCst bar da g\u00FCncel");
            }
        }

        [Test]
        public void TheSameOffer_CanBeBoughtAgain()
        {
            GameSession s = New();
            using (UiFlow flow = OpenWholesale(s))
            {
                flow.BuyWholesalePack(Supplier, Case);
                flow.BuyWholesalePack(Supplier, Case);

                Assert.AreEqual(40, s.AccessoryStock.Quantity(Case));
                Assert.AreEqual("Stok: 40 / 60 adet", flow.WholesaleScreen.StockLine);
            }
        }

        [Test]
        public void ALockedBuy_SaysWhenItOpens_AndChangesNothing()
        {
            GameSession s = New();
            using (UiFlow flow = OpenWholesale(s))
            {
                string digest = s.Api.GetStateDigest();

                Result<WholesalePurchaseReceipt> r = flow.BuyWholesalePack(Supplier, Powerbank);

                Assert.AreEqual("wholesale.not_available_yet", r.ErrorCode);
                Assert.AreEqual("Bu ürün Gün 3'te açılacak.", flow.StatusMessage);
                Assert.AreEqual(digest, s.Api.GetStateDigest());
            }
        }

        [Test]
        public void NotEnoughCash_SaysSo_AndChangesNothing()
        {
            GameSession s = GameSession.NewGame(ContentVariants.WithOpeningCapital(1000), 1UL);
            using (UiFlow flow = OpenWholesale(s))
            {
                string digest = s.Api.GetStateDigest();

                Result<WholesalePurchaseReceipt> r = flow.BuyWholesalePack(Supplier, Adapter);

                Assert.AreEqual("cash.insufficient", r.ErrorCode);
                Assert.AreEqual("Bu paketi almak için yeterli paran yok.", flow.StatusMessage);
                Assert.AreEqual(digest, s.Api.GetStateDigest());
                Assert.AreEqual(0, s.AccessoryStock.TotalUnits);
            }
        }

        [Test]
        public void NotEnoughStockCapacity_SaysSo_AndChangesNothing()
        {
            GameSession s = New();
            using (UiFlow flow = OpenWholesale(s))
            {
                flow.BuyWholesalePack(Supplier, Case);
                flow.BuyWholesalePack(Supplier, Case);
                flow.BuyWholesalePack(Supplier, Case); // 60/60
                string digest = s.Api.GetStateDigest();

                Result<WholesalePurchaseReceipt> r = flow.BuyWholesalePack(Supplier, Adapter);

                Assert.AreEqual("stock.full", r.ErrorCode);
                Assert.AreEqual("Aksesuar stoğunda yeterli yer yok.", flow.StatusMessage);
                Assert.AreEqual(digest, s.Api.GetStateDigest());
                Assert.AreEqual("Stok: 60 / 60 adet", flow.WholesaleScreen.StockLine);
            }
        }

        [Test]
        public void AnUnknownOffer_SaysItIsGone()
        {
            using (UiFlow flow = OpenWholesale(New()))
            {
                flow.BuyWholesalePack("supplier.ghost", Adapter);

                Assert.AreEqual("Bu teklif artık yok.", flow.StatusMessage);
            }
        }

        [Test]
        public void Buying_OnlyWorksOnTheWholesaleScreen()
        {
            GameSession s = New();
            using (UiFlow flow = Flow(s))
            {
                Result<WholesalePurchaseReceipt> r = flow.BuyWholesalePack(Supplier, Adapter);

                Assert.AreEqual("ui.not_on_wholesale_screen", r.ErrorCode);
                Assert.AreEqual(0, s.AccessoryStock.TotalUnits);
            }
        }

        // ---------- stok ekranı ----------

        [Test]
        public void TheStockScreen_ShowsQuantity_AverageCost_TotalCost_AndCapacityUse()
        {
            GameSession s = New();
            s.Api.BuyWholesalePack(Supplier, Case);     // 20 × 70 = 1.400
            s.Api.BuyWholesalePack(Supplier, Adapter);  // 10 × 150 = 1.500
            using (UiFlow flow = Flow(s))
            {
                flow.OpenAccessoryStock();
                AccessoryStockScreenViewModel screen = flow.AccessoryStockScreen;

                Assert.AreEqual("Aksesuar Stoğu", screen.Title);
                Assert.AreEqual("Stok: 30 / 60 adet", screen.CapacityLine);
                Assert.AreEqual("Stok maliyeti: 2.900 ₺", screen.TotalCostLine);
                Assert.IsNull(screen.EmptyNote);
                AccessoryStockRowViewModel row = screen.Items.Single(i => i.AccessoryId == Case);
                Assert.AreEqual("Telefon Kılıfı", row.Name);
                Assert.AreEqual("Adet: 20", row.QuantityLine);
                Assert.AreEqual("Ortalama maliyet: 70 ₺", row.AverageCostLine);
                Assert.AreEqual("Toplam maliyet: 1.400 ₺", row.TotalCostLine);
                Assert.AreEqual("Kapasitenin %33'ı", row.ShareLine);
                Assert.AreEqual("Kapasitenin %17'si".Length > 0 ? "Kapasitenin %17'ı" : string.Empty, screen.Items.Single(i => i.AccessoryId == Adapter).ShareLine);
            }
        }

        [Test]
        public void AnEmptyStock_ExplainsItself()
        {
            using (UiFlow flow = Flow(New()))
            {
                flow.OpenAccessoryStock();

                Assert.AreEqual("Stok: 0 / 60 adet", flow.AccessoryStockScreen.CapacityLine);
                Assert.AreEqual(0, flow.AccessoryStockScreen.Items.Count);
                Assert.AreEqual(TurkishTexts.AccessoryStockEmpty, flow.AccessoryStockScreen.EmptyNote);
            }
        }

        // ---------- 60 birim, telefon rafından bağımsız ----------

        [Test]
        public void TheAccessoryCapacity_IsSixty_AndIndependentFromThePhoneShelf()
        {
            GameSession s = New();
            using (UiFlow flow = Flow(s))
            {
                string phoneButton = flow.ShelfButtonText;
                int phoneCapacity = s.InventoryState.Capacity;
                Assert.AreNotEqual(60, phoneCapacity, "telefon rafı başka bir sayı");

                flow.OpenWholesale();
                flow.BuyWholesalePack(Supplier, Case);
                flow.BuyWholesalePack(Supplier, Case);
                flow.BuyWholesalePack(Supplier, Case);
                flow.Back();

                Assert.AreEqual(60, s.AccessoryStock.TotalUnits);
                Assert.AreEqual(phoneButton, flow.ShelfButtonText, "telefon rafı düğmesi değişmez");
                Assert.AreEqual(0, s.Api.GetInventory().Count);
                Assert.AreEqual(phoneCapacity, s.InventoryState.Capacity);
                Assert.AreEqual("Aksesuar (60/60)", flow.AccessoryButtonText);
                StringAssert.DoesNotContain("Aksesuar", flow.ShelfButtonText);
            }
        }

        [Test]
        public void ThePhoneShelfBeingFull_DoesNotBlockAccessories()
        {
            GameSession s = New();
            Assert.IsTrue(s.Api.BuyListing(s.Api.GetListings()[0].ListingId).IsSuccess);

            Assert.IsTrue(s.Api.BuyWholesalePack(Supplier, Adapter).IsSuccess);
            Assert.AreEqual(1, s.Api.GetInventory().Count);
            Assert.AreEqual(10, s.Api.GetAccessoryStock().TotalUnits);
        }

        [Test]
        public void TheListingsButtons_ShowTheAccessoryStockAndStayCurrent()
        {
            GameSession s = New();
            using (UiFlow flow = Flow(s))
            {
                Assert.AreEqual("Aksesuar (0/60)", flow.AccessoryButtonText);

                flow.OpenWholesale();
                flow.BuyWholesalePack(Supplier, Adapter);
                flow.Back();

                Assert.AreEqual("Aksesuar (10/60)", flow.AccessoryButtonText);
            }
        }

        // ---------- mevcut sistemi koruma ----------

        [Test]
        public void TheScreens_DoNotUseRandomness_AndNeverTouchThePhoneSide()
        {
            GameSession s = New();
            var rng = s.Capture().Rng;
            var listings = s.Api.GetListings().Select(l => l.ListingId).ToArray();
            using (UiFlow flow = OpenWholesale(s))
            {
                flow.BuyWholesalePack(Supplier, Adapter);
                flow.OpenAccessoryStock();
                flow.OpenWholesale();
                flow.Back();
            }

            Assert.IsNull(DeepCompare.FirstDifference(rng, s.Capture().Rng));
            CollectionAssert.AreEqual(listings, s.Api.GetListings().Select(l => l.ListingId).ToArray());
        }

        // ---------- Telefon toptancısı (Gün 14) ----------

        [Test]
        public void TheScreen_ListsEveryPhoneOfferFromContent_WithTheSameRowShape_AndKeepsAccessoriesSeparate()
        {
            GameSession s = New();
            UiFlow flow = OpenWholesale(s);

            WholesaleScreenViewModel screen = flow.WholesaleScreen;

            Assert.AreEqual("Telefonlar", screen.PhonesHeader);
            Assert.AreEqual(s.Content.Wholesale.ProductOffers.Count, screen.PhoneOffers.Count);
            Assert.AreEqual(6, screen.Offers.Count, "aksesuar teklifleri ayrı kalır");
            foreach (PhoneOfferRowViewModel row in screen.PhoneOffers)
            {
                Assert.AreEqual("Sıfır", row.ConditionLine);
                StringAssert.EndsWith("/ adet", row.UnitCostLine);
                StringAssert.EndsWith(" adet", row.QuantityLine);
                StringAssert.StartsWith("Paket: ", row.PackPriceLine);
                StringAssert.EndsWith(" ADET AL", row.ButtonText);
                Assert.IsTrue(row.IsButtonEnabled);
            }
        }

        [Test]
        public void BuyingAPhonePack_ThroughTheFlow_AddsSeparateUnits_AndSaysSo()
        {
            GameSession s = New();
            UiFlow flow = OpenWholesale(s);
            PhoneWholesaleOfferView offer = s.Api.GetPhoneWholesaleOffers().First();
            long cash = s.Api.GetCash().Tl;

            Result<PhonePackReceipt> result = flow.BuyPhonePack(offer.SupplierId, offer.ProductId);

            Assert.IsTrue(result.IsSuccess, result.ErrorCode);
            Assert.AreEqual(cash - offer.PackCost.Tl, s.Api.GetCash().Tl);
            Assert.AreEqual(offer.PackSize, result.Value.InstanceIds.Count);
            StringAssert.Contains(offer.ProductName, flow.StatusMessage);
            StringAssert.Contains("rafa eklendi", flow.StatusMessage);
        }

        [Test]
        public void BuyingAPhonePack_WithoutCash_ShowsAReasonAndChangesNothing()
        {
            GameSession s = New();
            UiFlow flow = OpenWholesale(s);
            PhoneWholesaleOfferView priciest = s.Api.GetPhoneWholesaleOffers().OrderByDescending(o => o.PackCost.Tl).First();
            while (s.Api.GetCash().Tl >= priciest.PackCost.Tl && flow.BuyPhonePack(priciest.SupplierId, priciest.ProductId).IsSuccess)
            {
            }

            long cash = s.Api.GetCash().Tl;
            Result<PhonePackReceipt> result = flow.BuyPhonePack(priciest.SupplierId, priciest.ProductId);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(cash, s.Api.GetCash().Tl);
            Assert.IsNotEmpty(flow.StatusMessage);
        }

        [Test]
        public void BuyingAPhonePack_OutsideTheWholesaleScreen_IsRejected()
        {
            GameSession s = New();
            UiFlow flow = Flow(s);
            PhoneWholesaleOfferView offer = s.Api.GetPhoneWholesaleOffers().First();

            Assert.IsFalse(flow.BuyPhonePack(offer.SupplierId, offer.ProductId).IsSuccess);
        }
    }
}
