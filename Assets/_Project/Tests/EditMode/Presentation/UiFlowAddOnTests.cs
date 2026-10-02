using System;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Accessories;
using Esnaf.Domain.Business;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Game;
using Esnaf.Domain.Market;
using Esnaf.Domain.Negotiation;
using Esnaf.Presentation;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Presentation
{
    /// <summary>Aksesuar ek satışı paneli (UiFlow, Gün 11.3.3): telefon satışı bittikten sonra açılır; hepsi IGameApi üstünde, UI'da kural yoktur.</summary>
    public class UiFlowAddOnTests
    {
        private const string Supplier = "supplier.ucuz_toptan";
        private const string Case = "accessory.phone_case";
        private const string Adapter = "accessory.charger_adapter";

        private static GameSession SessionWithCustomer(out CustomerView customer)
        {
            for (ulong seed = 1; seed <= 200; seed++)
            {
                GameSession s = GameSession.NewGame(MarketHarness.RealContent(), seed);
                MarketListing g = s.Market.Listings.Single(l => l.IsGuided);
                Assert.IsTrue(s.Api.StartNegotiation(g.ListingId).IsSuccess);
                Assert.IsTrue(s.Api.MakeOffer(Money.FromTl(5800)).IsSuccess);
                Assert.IsTrue(s.Api.SetPrice(g.InstanceId, Money.FromTl(5900)).IsSuccess);
                if (s.Api.GetCustomers().Count > 0)
                {
                    customer = s.Api.GetCustomers()[0];
                    return s;
                }
            }

            throw new InvalidOperationException("No seed gives a customer.");
        }

        private static UiFlow Flow(GameSession s)
        {
            return new UiFlow(s.Api, new ContentPresentation(s.Content), s.Bus);
        }

        /// <summary>Gerçek akış: müşteriyle konuş, 10 ₺ iste, anlaşma. Ekran "Done" (anlaşma) halinde kalır.</summary>
        private static UiFlow DealDone(GameSession s, CustomerView customer)
        {
            UiFlow flow = Flow(s);
            Assert.IsTrue(flow.OpenCustomers());
            Assert.IsTrue(flow.StartSale(customer.CustomerId).IsSuccess);
            Assert.IsTrue(flow.SaleGreet());
            flow.AdjustSalePrice(-100000);
            Result<SaleView> deal = flow.SaleAsk();
            Assert.IsTrue(deal.IsSuccess, deal.ErrorCode);
            Assert.AreEqual(NegotiationPhase.Deal, deal.Value.Phase);
            return flow;
        }

        private static int Count(GameSession s, string typeId)
        {
            return s.EconomyState.Ledger.Records.Count(r => r.TypeId == typeId);
        }

        // ---------- A ----------

        [Test]
        public void AfterThePhoneSale_TheAddOnPanelOpens_WithTheTitleAndSubtitle()
        {
            CustomerView c;
            GameSession s = SessionWithCustomer(out c);
            using (UiFlow flow = DealDone(s, c))
            {
                SaleScreenViewModel screen = flow.SaleScreen;

                Assert.AreEqual(SaleMode.Done, screen.Mode);
                Assert.IsNotNull(screen.AddOn);
                Assert.AreEqual("Yanında bir aksesuar ister misiniz?", screen.AddOn.Title);
                Assert.AreEqual("Bu satışa aksesuar ekleyebilirsiniz.", screen.AddOn.Subtitle);
                Assert.AreEqual(s.Api.GetAccessoryAddOns().PhoneSaleRecordId, screen.AddOn.PhoneSaleRecordId);
            }
        }

        [Test]
        public void TheAddOnPanel_IsNotShown_InTheLobby_WhileTalking_OrAfterAFailedSale()
        {
            CustomerView c;
            GameSession s = SessionWithCustomer(out c);
            using (UiFlow flow = Flow(s))
            {
                flow.OpenCustomers();
                Assert.IsNull(flow.SaleScreen.AddOn, "lobi");
                flow.StartSale(c.CustomerId);
                Assert.IsNull(flow.SaleScreen.AddOn, "konuşma");
                Assert.IsTrue(flow.SaleLetGo().IsSuccess);
                Assert.AreEqual(SaleMode.Done, flow.SaleScreen.Mode);
                Assert.IsNull(flow.SaleScreen.AddOn, "müşteri gitti, telefon satılmadı");
                Assert.IsFalse(flow.SaleAddAccessory(Case).IsSuccess);
            }
        }

        // ---------- B, I ----------

        [Test]
        public void TheCards_ShowTheRealCatalog_WithNamePriceAndStock_AndOutOfStockCardsAreDisabled()
        {
            CustomerView c;
            GameSession s = SessionWithCustomer(out c);
            Assert.IsTrue(s.WholesaleService.BuyPack(Supplier, Case, 1).IsSuccess);
            using (UiFlow flow = DealDone(s, c))
            {
                AddOnPanelViewModel panel = flow.SaleScreen.AddOn;
                AccessoryAddOnView api = s.Api.GetAccessoryAddOns();

                Assert.AreEqual(api.Options.Count, panel.Cards.Count);
                AddOnCardViewModel cases = panel.Cards.Single(x => x.AccessoryId == Case);
                Assert.AreEqual(api.Options.Single(o => o.AccessoryId == Case).AccessoryName, cases.Name);
                Assert.AreEqual(MoneyFormatter.Format(Money.FromTl(160)), cases.PriceLine);
                Assert.AreEqual("Stok: 20", cases.StockLine);
                Assert.IsTrue(cases.IsButtonEnabled);
                Assert.AreEqual("Ekle", cases.ButtonText);

                AddOnCardViewModel adapter = panel.Cards.Single(x => x.AccessoryId == Adapter);
                Assert.AreEqual(0, adapter.InStock);
                Assert.IsFalse(adapter.IsButtonEnabled);
                Assert.AreEqual("Stokta yok", adapter.StockLine);
                Assert.AreEqual("Stokta yok", adapter.ButtonText);
            }
        }

        [Test]
        public void AnOutOfStockAccessory_CannotBeSold_AndTheRefusalComesFromTheGame()
        {
            CustomerView c;
            GameSession s = SessionWithCustomer(out c);
            using (UiFlow flow = DealDone(s, c))
            {
                Money cash = s.Api.GetCash();
                string digest = s.Api.GetStateDigest();

                Result<AccessorySaleReceipt> r = flow.SaleAddAccessory(Case);

                Assert.AreEqual("stock.insufficient", r.ErrorCode);
                Assert.AreEqual(cash, s.Api.GetCash());
                Assert.AreEqual(digest, s.Api.GetStateDigest());
                Assert.IsTrue(flow.SaleScreen.AddOn.FeedbackIsError);
                Assert.AreEqual("Stokta yok.", flow.SaleScreen.AddOn.Feedback);
            }
        }

        // ---------- C, D ----------

        [Test]
        public void AddingAnAccessory_SellsThroughTheApi_StockDrops_AndTheCardRefreshes()
        {
            CustomerView c;
            GameSession s = SessionWithCustomer(out c);
            Assert.IsTrue(s.WholesaleService.BuyPack(Supplier, Case, 1).IsSuccess);
            using (UiFlow flow = DealDone(s, c))
            {
                Money cash = s.Api.GetCash();
                int rows = Count(s, TransactionTypeIds.AccessorySale);

                Result<AccessorySaleReceipt> r = flow.SaleAddAccessory(Case);

                Assert.IsTrue(r.IsSuccess, r.ErrorCode);
                Assert.AreEqual(19, s.AccessoryStock.Quantity(Case));
                Assert.AreEqual(cash + Money.FromTl(160), s.Api.GetCash());
                Assert.AreEqual(rows + 1, Count(s, TransactionTypeIds.AccessorySale));
                AddOnPanelViewModel panel = flow.SaleScreen.AddOn;
                Assert.AreEqual("Stok: 19", panel.Cards.Single(x => x.AccessoryId == Case).StockLine);
                Assert.IsFalse(panel.FeedbackIsError);
                StringAssert.Contains("eklendi", panel.Feedback);
                StringAssert.Contains(panel.Cards.Single(x => x.AccessoryId == Case).Name, panel.Feedback);
            }
        }

        // ---------- E, J ----------

        [Test]
        public void SeveralAccessories_CanBeAdded_AndTheTotalsFollowTheGame()
        {
            CustomerView c;
            GameSession s = SessionWithCustomer(out c);
            Assert.IsTrue(s.WholesaleService.BuyPack(Supplier, Case, 1).IsSuccess);
            Assert.IsTrue(s.WholesaleService.BuyPack(Supplier, Adapter, 1).IsSuccess);
            using (UiFlow flow = DealDone(s, c))
            {
                Money phone = flow.SaleScreen.AddOn.PhonePrice;
                Money phoneProfit = flow.SaleScreen.AddOn.PhoneProfit;

                Assert.IsTrue(flow.SaleAddAccessory(Case).IsSuccess);
                Assert.IsTrue(flow.SaleAddAccessory(Case).IsSuccess);
                Assert.IsTrue(flow.SaleAddAccessory(Adapter).IsSuccess);

                AddOnPanelViewModel panel = flow.SaleScreen.AddOn;
                AccessoryAddOnView api = s.Api.GetAccessoryAddOns();
                Assert.AreEqual(3, panel.AddOnsSold);
                Assert.AreEqual(Money.FromTl(160 + 160 + 250), panel.AccessoriesPrice);
                Assert.AreEqual(phone, panel.PhonePrice);
                Assert.AreEqual(phone + panel.AccessoriesPrice, panel.TotalPrice);
                Assert.AreEqual(api.AccessoryRevenue, panel.AccessoriesPrice);
                Assert.AreEqual(phoneProfit, panel.PhoneProfit);
                Assert.AreEqual(api.AccessoryProfit, panel.AccessoryProfit);
                Assert.AreEqual(api.TotalProfit, panel.TotalProfit);
                Assert.AreEqual(s.AccessoryAddOns.TotalProfitOfSale(panel.PhoneSaleRecordId).Value, panel.TotalProfit);
                StringAssert.Contains(MoneyFormatter.Format(panel.TotalPrice), panel.TotalLine);
                StringAssert.StartsWith("Telefon:", panel.PhoneLine);
                StringAssert.StartsWith("Aksesuarlar:", panel.AccessoriesLine);
                StringAssert.StartsWith("Toplam:", panel.TotalLine);
                StringAssert.StartsWith("Telefon kârı:", panel.PhoneProfitLine);
                StringAssert.StartsWith("Aksesuar kârı:", panel.AccessoryProfitLine);
                StringAssert.StartsWith("Toplam kâr:", panel.TotalProfitLine);
            }
        }

        // ---------- F ----------

        [Test]
        public void TheSaleCanBeFinished_WithoutAnyAccessory()
        {
            CustomerView c;
            GameSession s = SessionWithCustomer(out c);
            using (UiFlow flow = DealDone(s, c))
            {
                int accessoryRows = Count(s, TransactionTypeIds.AccessorySale);

                Assert.AreEqual("Devam Et / Satışı Bitir", flow.SaleScreen.AddOn.FinishButtonText);
                Assert.IsTrue(flow.SaleNext());

                Assert.AreEqual(SaleMode.Lobby, flow.SaleScreen.Mode);
                Assert.IsNull(flow.SaleScreen.AddOn);
                Assert.AreEqual(accessoryRows, Count(s, TransactionTypeIds.AccessorySale));
            }
        }

        [Test]
        public void AfterFinishing_NoMoreAccessoryCanBeAddedFromTheUi()
        {
            CustomerView c;
            GameSession s = SessionWithCustomer(out c);
            Assert.IsTrue(s.WholesaleService.BuyPack(Supplier, Case, 1).IsSuccess);
            using (UiFlow flow = DealDone(s, c))
            {
                flow.SaleNext();

                Assert.AreEqual("ui.no_addon_panel", flow.SaleAddAccessory(Case).ErrorCode);
                Assert.AreEqual(20, s.AccessoryStock.Quantity(Case));
            }
        }

        // ---------- G, H, K ----------

        [Test]
        public void TheAddOn_NeverChangesThePhoneSale_OrRepeatsItsTransaction()
        {
            CustomerView c;
            GameSession s = SessionWithCustomer(out c);
            Assert.IsTrue(s.WholesaleService.BuyPack(Supplier, Case, 1).IsSuccess);
            using (UiFlow flow = DealDone(s, c))
            {
                SaleScreenViewModel before = flow.SaleScreen;
                long recordId = before.AddOn.PhoneSaleRecordId;
                TransactionRecord sale;
                s.EconomyState.Ledger.TryGetById(recordId, out sale);
                int phoneSales = Count(s, TransactionTypeIds.Sale);

                flow.SaleAddAccessory(Case);
                flow.SaleAddAccessory(Case);

                SaleScreenViewModel after = flow.SaleScreen;
                TransactionRecord now;
                s.EconomyState.Ledger.TryGetById(recordId, out now);
                Assert.AreEqual(phoneSales, Count(s, TransactionTypeIds.Sale), "telefon transaction'ı tekrar oluşmadı");
                Assert.AreEqual(sale.Amount, now.Amount, "telefon fiyatı aynı");
                Assert.AreEqual(sale.SaleCostBasis, now.SaleCostBasis);
                Assert.AreEqual(recordId, after.AddOn.PhoneSaleRecordId);
                Assert.AreEqual(before.AddOn.PhonePrice, after.AddOn.PhonePrice);
                Assert.AreEqual(before.AddOn.PhoneProfit, after.AddOn.PhoneProfit);
                // K: müşteri/telefon bilgisi aynı satışla aynı kalır.
                Assert.AreEqual(before.CustomerName, after.CustomerName);
                Assert.AreEqual(before.NpcId, after.NpcId);
                Assert.AreEqual(before.ModelTitle, after.ModelTitle);
                Assert.AreEqual(before.DefinitionId, after.DefinitionId);
                Assert.AreEqual(before.Title, after.Title);
                Assert.AreEqual(before.CustomerLine, after.CustomerLine);
                Assert.AreEqual(c.NpcId, after.NpcId);
                Assert.AreEqual(c.NpcId, s.Api.GetAccessoryAddOns().BuyerNpcId);
            }
        }

        [Test]
        public void ThePhoneSaleResultScreen_StillShowsTheDealAndOnlyTheContinueReply()
        {
            CustomerView c;
            GameSession s = SessionWithCustomer(out c);
            using (UiFlow flow = DealDone(s, c))
            {
                SaleScreenViewModel screen = flow.SaleScreen;

                Assert.AreEqual(SaleMode.Done, screen.Mode);
                CollectionAssert.AreEqual(new[] { SaleReplyKind.Continue }, screen.Replies.Select(r => r.Kind).ToArray());
                Assert.AreEqual(TurkishTexts.SaleDeal(screen.AddOn.PhonePrice), screen.Title);
            }
        }

        // ---------- RNG ----------

        [Test]
        public void TheAddOnUi_UsesNoRandomness()
        {
            CustomerView c;
            GameSession s = SessionWithCustomer(out c);
            Assert.IsTrue(s.WholesaleService.BuyPack(Supplier, Case, 1).IsSuccess);
            using (UiFlow flow = DealDone(s, c))
            {
                var rng = s.Capture().Rng;

                flow.SaleAddAccessory(Case);
                flow.SaleAddAccessory(Adapter);
                flow.SaleNext();

                Assert.IsNull(DeepCompare.FirstDifference(rng, s.Capture().Rng));
            }
        }

        [Test]
        public void TheAddOnTexts_AreFullTurkish_AndErrorsMapToReadableMessages()
        {
            foreach (string code in new[] { "stock.insufficient", "addon.no_sale", "addon.sale_closed", "accessory.unknown", "amount.invalid" })
            {
                StringAssert.DoesNotContain("Bir sorun", TurkishTexts.AddOnError(code), code);
            }
        }
    }
}
