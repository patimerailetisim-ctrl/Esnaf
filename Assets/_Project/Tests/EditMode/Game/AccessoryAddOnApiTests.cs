using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Accessories;
using Esnaf.Domain.Business;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Game;
using Esnaf.Domain.Negotiation;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Game
{
    /// <summary>
    /// Aksesuar ek satışı IGameApi üzerinden (Gün 11.3.2), GERÇEK müşteri satış akışının (StartSale / AskPrice → Anlaşma) ardından:
    /// fırsat görünümü, satış, ikinci aksesuar, stok yok, tamamlanmamış/başka gün, telefon satışının tekrarlanmaması, RNG, toplam kâr.
    /// </summary>
    public class AccessoryAddOnApiTests
    {
        private const string Supplier = "supplier.ucuz_toptan";
        private const string Case = "accessory.phone_case";          // 160, 20 × 70 paket
        private const string Adapter = "accessory.charger_adapter";   // 250, 10 × 150 paket

        private sealed class Played
        {
            public GameSession Session;
            public CustomerView Customer;
            public SaleView Deal;
            public long SaleRecordId;
        }

        private static GameSession SessionWithCustomer(out CustomerView customer)
        {
            for (ulong seed = 1; seed < 200; seed++)
            {
                GameSession s = GameSession.NewGame(MarketHarness.RealContent(), seed);
                var guided = s.Market.Listings.Single(l => l.IsGuided);
                s.Api.StartNegotiation(guided.ListingId);
                s.Api.MakeOffer(Money.FromTl(5800));
                s.Api.SetPrice(guided.InstanceId, Money.FromTl(5900));
                if (s.Api.GetCustomers().Count > 0)
                {
                    customer = s.Api.GetCustomers()[0];
                    return s;
                }
            }

            throw new System.InvalidOperationException("No seed gives a customer.");
        }

        private static Played FullSale(bool stockCase = true, bool stockAdapter = false)
        {
            CustomerView customer;
            GameSession s = SessionWithCustomer(out customer);
            if (stockCase)
            {
                Assert.IsTrue(s.WholesaleService.BuyPack(Supplier, Case, 1).IsSuccess);
            }

            if (stockAdapter)
            {
                Assert.IsTrue(s.WholesaleService.BuyPack(Supplier, Adapter, 1).IsSuccess);
            }

            Assert.IsTrue(s.Api.StartSale(customer.CustomerId).IsSuccess);
            Result<SaleView> deal = s.Api.AskPrice(Money.FromTl(10));
            Assert.IsTrue(deal.IsSuccess, deal.ErrorCode);
            Assert.AreEqual(NegotiationPhase.Deal, deal.Value.Phase);
            return new Played
            {
                Session = s,
                Customer = customer,
                Deal = deal.Value,
                SaleRecordId = s.EconomyState.Ledger.Records.Last(r => r.TypeId == TransactionTypeIds.Sale).Id
            };
        }

        private static int Count(GameSession s, string typeId)
        {
            return s.EconomyState.Ledger.Records.Count(r => r.TypeId == typeId);
        }

        // ---------- A: tam telefon satışından sonra ----------

        [Test]
        public void AfterACompletedPhoneSale_TheAddOnOpportunityIsReachable_WithTheSaleRecordId()
        {
            Played p = FullSale();

            AccessoryAddOnView view = p.Session.Api.GetAccessoryAddOns();

            Assert.IsTrue(view.HasPhoneSale);
            Assert.AreEqual(p.SaleRecordId, view.PhoneSaleRecordId, "phoneSaleRecordId API'den taşınır");
            Assert.AreEqual(p.Customer.NpcId, view.BuyerNpcId);
            Assert.AreEqual(p.Deal.DealPrice, view.PhoneSalePrice);
            Assert.AreEqual(6, view.Options.Count);
            AccessoryAddOnOptionView cases = view.Options.Single(o => o.AccessoryId == Case);
            Assert.AreEqual(Money.FromTl(160), cases.RetailPrice);
            Assert.AreEqual(20, cases.InStock);
            Assert.IsTrue(cases.IsAvailable);
            Assert.IsFalse(view.Options.Single(o => o.AccessoryId == Adapter).IsAvailable, "stokta yok");
            Assert.AreEqual(0, view.AddOnsSold);
            Assert.AreEqual(view.PhoneProfit, view.TotalProfit);
        }

        [Test]
        public void ASingleAccessory_CanBeSold_StockDropsByOne_CashRisesByTheRetailPrice_AndTheLedgerRowAppears()
        {
            Played p = FullSale();
            GameSession s = p.Session;
            Money cash = s.Api.GetCash();
            int units = s.AccessoryStock.Quantity(Case);

            Result<AccessorySaleReceipt> r = s.Api.SellAccessoryAddOn(Case);

            Assert.IsTrue(r.IsSuccess, r.ErrorCode);
            Assert.AreEqual(units - 1, s.AccessoryStock.Quantity(Case), "stok −1");
            Assert.AreEqual(cash + Money.FromTl(160), s.Api.GetCash(), "nakit + retailPrice");
            Assert.AreEqual(1, Count(s, TransactionTypeIds.AccessorySale), "accessory_sale kaydı");
            Assert.AreEqual(p.SaleRecordId, r.Value.PhoneSaleRecordId);
            Assert.AreEqual(Money.FromTl(70), r.Value.CostBasis);
            Assert.AreEqual(Money.FromTl(90), r.Value.Profit);
        }

        // ---------- B: ikinci aksesuar ----------

        [Test]
        public void ASecondAccessory_CanBeSoldToTheSameSale()
        {
            Played p = FullSale(stockCase: true, stockAdapter: true);
            GameSession s = p.Session;

            Assert.IsTrue(s.Api.SellAccessoryAddOn(Case).IsSuccess);
            Result<AccessorySaleReceipt> second = s.Api.SellAccessoryAddOn(Adapter);

            Assert.IsTrue(second.IsSuccess, second.ErrorCode);
            Assert.AreEqual(2, Count(s, TransactionTypeIds.AccessorySale));
            Assert.AreEqual(p.SaleRecordId, second.Value.PhoneSaleRecordId);
            Assert.AreEqual(2, s.Api.GetAccessoryAddOns().AddOnsSold);
        }

        [Test]
        public void TheSameAccessory_CanBeSoldTwice_WhileStockLasts()
        {
            Played p = FullSale();

            Assert.IsTrue(p.Session.Api.SellAccessoryAddOn(Case).IsSuccess);
            Assert.IsTrue(p.Session.Api.SellAccessoryAddOn(Case).IsSuccess);

            Assert.AreEqual(18, p.Session.AccessoryStock.Quantity(Case));
        }

        // ---------- C: stok yok ----------

        [Test]
        public void WithoutStock_TheApiRefuses_AndNothingChanges()
        {
            Played p = FullSale(stockCase: false);
            GameSession s = p.Session;
            string digest = s.Api.GetStateDigest();
            Money cash = s.Api.GetCash();
            int rows = s.EconomyState.Ledger.Count;

            Result<AccessorySaleReceipt> r = s.Api.SellAccessoryAddOn(Case);

            Assert.AreEqual("stock.insufficient", r.ErrorCode);
            Assert.AreEqual(digest, s.Api.GetStateDigest());
            Assert.AreEqual(cash, s.Api.GetCash());
            Assert.AreEqual(rows, s.EconomyState.Ledger.Count);
            Assert.AreEqual(0, s.AccessoryStock.TotalUnits, "negatif stok yok");
        }

        [Test]
        public void AnUnknownAccessory_IsRefused()
        {
            Played p = FullSale();

            Assert.AreEqual("accessory.unknown", p.Session.Api.SellAccessoryAddOn("accessory.ghost").ErrorCode);
        }

        // ---------- D: telefon satışı tamamlanmadan ----------

        [Test]
        public void WithoutAPhoneSale_NoAddOnIsPossible_AndTheViewSaysSo()
        {
            CustomerView customer;
            GameSession s = SessionWithCustomer(out customer);
            s.WholesaleService.BuyPack(Supplier, Case, 1);
            string digest = s.Api.GetStateDigest();

            AccessoryAddOnView view = s.Api.GetAccessoryAddOns();
            Result<AccessorySaleReceipt> r = s.Api.SellAccessoryAddOn(Case);

            Assert.IsFalse(view.HasPhoneSale);
            Assert.AreEqual(0, view.Options.Count);
            Assert.AreEqual(0L, view.PhoneSaleRecordId);
            Assert.AreEqual("addon.no_sale", r.ErrorCode);
            Assert.AreEqual(digest, s.Api.GetStateDigest());
        }

        [Test]
        public void DuringAnOpenSale_BeforeTheDeal_NoAddOnIsPossible()
        {
            CustomerView customer;
            GameSession s = SessionWithCustomer(out customer);
            s.WholesaleService.BuyPack(Supplier, Case, 1);
            Assert.IsTrue(s.Api.StartSale(customer.CustomerId).IsSuccess);
            Assert.IsNotNull(s.Api.GetSale(), "satış sürüyor");
            string digest = s.Api.GetStateDigest();

            Assert.AreEqual("addon.no_sale", s.Api.SellAccessoryAddOn(Case).ErrorCode);
            Assert.IsFalse(s.Api.GetAccessoryAddOns().HasPhoneSale);
            Assert.AreEqual(digest, s.Api.GetStateDigest());
        }

        [Test]
        public void ACustomerWhoLeavesWithoutBuying_GivesNoAddOn()
        {
            CustomerView customer;
            GameSession s = SessionWithCustomer(out customer);
            s.WholesaleService.BuyPack(Supplier, Case, 1);
            s.Api.StartSale(customer.CustomerId);
            s.Api.LetCustomerGo();

            Assert.IsFalse(s.Api.GetAccessoryAddOns().HasPhoneSale);
            Assert.AreEqual("addon.no_sale", s.Api.SellAccessoryAddOn(Case).ErrorCode);
        }

        // ---------- E: farklı gün ----------

        [Test]
        public void OnAnotherDay_TheEarlierPhoneSaleOffersNoAddOn()
        {
            Played p = FullSale();
            GameSession s = p.Session;
            Assert.IsTrue(s.Api.EndDay().IsSuccess);
            Assert.AreEqual(2, s.Api.GetDay());
            string digest = s.Api.GetStateDigest();

            Assert.IsFalse(s.Api.GetAccessoryAddOns().HasPhoneSale);
            Assert.AreEqual("addon.no_sale", s.Api.SellAccessoryAddOn(Case).ErrorCode);
            Assert.AreEqual(digest, s.Api.GetStateDigest());
        }

        [Test]
        public void TheDomainService_StillRefusesAnOldSale_ByItsRecordId()
        {
            Played p = FullSale();
            GameSession s = p.Session;
            s.Api.EndDay();

            Result<AccessorySaleReceipt> r = s.AccessoryAddOns.SellAddOn(p.SaleRecordId, Case, s.Api.GetDay());

            Assert.AreEqual("addon.sale_closed", r.ErrorCode);
        }

        // ---------- F, G: telefon satışı değişmez ----------

        [Test]
        public void TheAddOn_NeverRepeatsThePhoneSale_OrChangesItsPriceOrRow()
        {
            Played p = FullSale(stockCase: true, stockAdapter: true);
            GameSession s = p.Session;
            TransactionRecord before;
            s.EconomyState.Ledger.TryGetById(p.SaleRecordId, out before);
            int phoneSales = Count(s, TransactionTypeIds.Sale);
            int phoneRows = s.Api.GetInventory().Count;
            int saleCountToday = s.Api.GetTodaySummary().Sales.Count;

            s.Api.SellAccessoryAddOn(Case);
            s.Api.SellAccessoryAddOn(Adapter);

            Assert.AreEqual(phoneSales, Count(s, TransactionTypeIds.Sale), "telefon satışı ikinci kez yazılmadı");
            Assert.AreEqual(saleCountToday, s.Api.GetTodaySummary().Sales.Count);
            Assert.AreEqual(phoneRows, s.Api.GetInventory().Count, "telefon rafi değişmedi");
            TransactionRecord after;
            s.EconomyState.Ledger.TryGetById(p.SaleRecordId, out after);
            Assert.AreEqual(before.Amount, after.Amount, "telefon satış fiyatı aynı");
            Assert.AreEqual(before.SaleCostBasis, after.SaleCostBasis);
            Assert.AreEqual(before.BalanceAfter, after.BalanceAfter);
            Assert.AreEqual(p.Deal.DealPrice, after.Amount, "pazarlık sonucu aynı");
        }

        [Test]
        public void TheAccessoryPrice_IsTheFixedRetailPrice_EvenForADifferentPhoneNegotiation()
        {
            Played p = FullSale();

            Result<AccessorySaleReceipt> r = p.Session.Api.SellAccessoryAddOn(Case);

            Assert.AreEqual(p.Session.Content.Accessories.Definitions.Single(d => d.Id == Case).RetailPrice, r.Value.SalePrice);
        }

        // ---------- I, J: RNG ve toplam kâr ----------

        [Test]
        public void TheAddOnApi_DoesNotUseRandomness()
        {
            Played p = FullSale();
            var rng = p.Session.Capture().Rng;

            p.Session.Api.GetAccessoryAddOns();
            p.Session.Api.SellAccessoryAddOn(Case);
            p.Session.Api.SellAccessoryAddOn("accessory.ghost");

            Assert.IsNull(DeepCompare.FirstDifference(rng, p.Session.Capture().Rng));
        }

        [Test]
        public void TheTotalProfit_IsPhoneProfitPlusAllAddOnProfits()
        {
            Played p = FullSale(stockCase: true, stockAdapter: true);
            GameSession s = p.Session;
            Money phoneProfit = s.Api.GetAccessoryAddOns().PhoneProfit;

            AccessorySaleReceipt a = s.Api.SellAccessoryAddOn(Case).Value;
            AccessorySaleReceipt b = s.Api.SellAccessoryAddOn(Adapter).Value;
            AccessoryAddOnView view = s.Api.GetAccessoryAddOns();

            Assert.AreEqual(2, view.AddOnsSold);
            Assert.AreEqual(a.Profit + b.Profit, view.AccessoryProfit);
            Assert.AreEqual(phoneProfit + a.Profit + b.Profit, view.TotalProfit);
            Assert.AreEqual(view.TotalProfit, s.AccessoryAddOns.TotalProfitOfSale(p.SaleRecordId).Value, "görünüm ile servis aynı sonucu verir");
        }

        [Test]
        public void TheAddOnStateSurvivesSaveAndLoad_WithoutAnyNewSavedState()
        {
            Played p = FullSale();
            p.Session.Api.SellAccessoryAddOn(Case);
            GameSnapshot snap = p.Session.Capture();

            Result<GameSession> back = GameSession.Restore(MarketHarness.RealContent(), snap);

            Assert.IsTrue(back.IsSuccess, back.ErrorCode + ": " + back.Message);
            AccessoryAddOnView before = p.Session.Api.GetAccessoryAddOns();
            AccessoryAddOnView after = back.Value.Api.GetAccessoryAddOns();
            Assert.AreEqual(before.PhoneSaleRecordId, after.PhoneSaleRecordId);
            Assert.AreEqual(before.AddOnsSold, after.AddOnsSold);
            Assert.AreEqual(before.TotalProfit, after.TotalProfit);
            Assert.AreEqual(p.Session.Api.GetStateDigest(), back.Value.Api.GetStateDigest());
        }

        [Test]
        public void ThePhoneSaleFlow_ItselfIsUnchanged_ForTheCustomerView()
        {
            Played p = FullSale();

            Assert.AreEqual(NegotiationPhase.Deal, p.Deal.Phase);
            Assert.IsNull(p.Session.Api.GetSale(), "satış tamamlandı");
            Assert.AreEqual(0, p.Session.Api.GetInventory().Count, "telefon satıldı");
            Assert.IsTrue(p.Session.Api.GetCash() > Money.Zero);
        }
    }
}
