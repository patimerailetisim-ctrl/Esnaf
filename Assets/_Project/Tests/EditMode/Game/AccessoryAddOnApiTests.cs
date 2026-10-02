using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Accessories;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Game;
using Esnaf.Domain.Negotiation;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Game
{
    /// <summary>
    /// Aksesuar ek satışı IGameApi üzerinden (Gün 11.3.2, Gün 11.3.4'te müşteri talebine bağlandı), GERÇEK müşteri satış akışının
    /// (StartSale / AskPrice → Anlaşma) ardından: talep, fırsat görünümü, satış, kısmi satış, stok yok, istemediği aksesuar, tamamlanmamış/başka gün,
    /// telefon satışının tekrarlanmaması, RNG, toplam kâr.
    /// </summary>
    public class AccessoryAddOnApiTests
    {
        private const string Supplier = "supplier.ucuz_toptan";
        private const string Case = "accessory.phone_case";          // 160, 20 × 70 paket
        private const string Adapter = "accessory.charger_adapter";   // 250, 10 × 150 paket

        private static int Count(GameSession s, string typeId)
        {
            return s.EconomyState.Ledger.Records.Count(r => r.TypeId == typeId);
        }

        private static void Stock(GameSession s, params string[] ids)
        {
            foreach (string id in ids)
            {
                // Paket gibi: kılıf 20 × 70, adaptör 10 × 150, diğerleri 10 × 50 (her aksesuar toptancıdan her gün alınamaz; stok doğrudan konur).
                int quantity = id == Case ? 20 : 10;
                Money cost = id == Case ? Money.FromTl(1400) : id == Adapter ? Money.FromTl(1500) : Money.FromTl(500);
                Assert.IsTrue(s.AccessoryStock.Add(id, quantity, cost).IsSuccess, id);
            }
        }

        // ---------- talep yok → ek satış yok ----------

        [Test]
        public void IfTheCustomerAskedForNothing_NoAddOnOpportunityExists_AndTheApiRefusesEveryAccessory()
        {
            AddOnDeal deal = AddOnDeals.NoRequest();
            GameSession s = deal.Session;
            Stock(s, Case, Adapter);
            string digest = s.Api.GetStateDigest();

            AccessoryAddOnView view = s.Api.GetAccessoryAddOns();

            Assert.IsTrue(view.HasPhoneSale, "telefon satışı var");
            Assert.IsFalse(view.HasRequest);
            Assert.AreEqual(0, view.Options.Count, "müşteri istemediyse hiçbir seçenek yok");
            Assert.AreEqual("addon.not_requested", s.Api.SellAccessoryAddOn(Case).ErrorCode);
            Assert.AreEqual("addon.not_requested", s.Api.SellAccessoryAddOn(Adapter).ErrorCode);
            Assert.AreEqual(digest, s.Api.GetStateDigest());
            Assert.AreEqual(0, Count(s, TransactionTypeIds.AccessorySale));
        }

        // ---------- talep var ----------

        [Test]
        public void IfTheCustomerAsked_TheViewListsExactlyTheRequestedAccessories_WithTheSaleRecordId()
        {
            AddOnDeal deal = AddOnDeals.WithCount(3);
            GameSession s = deal.Session;

            AccessoryAddOnView view = s.Api.GetAccessoryAddOns();

            Assert.IsTrue(view.HasPhoneSale);
            Assert.IsTrue(view.HasRequest);
            Assert.AreEqual(deal.SaleRecordId, view.PhoneSaleRecordId, "phoneSaleRecordId API'den taşınır");
            Assert.AreEqual(deal.Customer.NpcId, view.BuyerNpcId);
            Assert.AreEqual(deal.Deal.DealPrice, view.PhoneSalePrice);
            CollectionAssert.AreEquivalent(deal.Requested.ToArray(), view.Options.Select(o => o.AccessoryId).ToArray());
            Assert.AreEqual(3, view.Options.Count);
            Assert.IsTrue(view.Options.All(o => !o.IsAdded));
            Assert.AreEqual(0, view.AddOnsSold);
            Assert.AreEqual(view.PhoneProfit, view.TotalProfit);
            Assert.AreEqual(Money.Zero, view.AccessoryRevenue);
        }

        [Test]
        public void ARequestedAccessory_CanBeSold_StockDropsByOne_CashRisesByTheRetailPrice_AndTheLedgerRowAppears()
        {
            AddOnDeal deal = AddOnDeals.Requesting(Case);
            GameSession s = deal.Session;
            Stock(s, Case);
            Money cash = s.Api.GetCash();
            int units = s.AccessoryStock.Quantity(Case);

            Result<AccessorySaleReceipt> r = s.Api.SellAccessoryAddOn(Case);

            Assert.IsTrue(r.IsSuccess, r.ErrorCode);
            Assert.AreEqual(units - 1, s.AccessoryStock.Quantity(Case), "stok −1");
            Assert.AreEqual(cash + Money.FromTl(160), s.Api.GetCash(), "nakit + retailPrice");
            Assert.AreEqual(1, Count(s, TransactionTypeIds.AccessorySale), "accessory_sale kaydı");
            Assert.AreEqual(deal.SaleRecordId, r.Value.PhoneSaleRecordId);
            Assert.AreEqual(Money.FromTl(70), r.Value.CostBasis);
            Assert.AreEqual(Money.FromTl(90), r.Value.Profit);
            Assert.IsTrue(s.Api.GetAccessoryAddOns().Options.Single(o => o.AccessoryId == Case).IsAdded);
        }

        [Test]
        public void TheRequestedAccessoriesCanBeSoldOneByOne_AndTheProgressFollows()
        {
            AddOnDeal deal = AddOnDeals.WithCount(4);
            GameSession s = deal.Session;
            Stock(s, deal.Requested.ToArray());

            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(i, s.Api.GetAccessoryAddOns().AddOnsSold);
                Assert.IsTrue(s.Api.SellAccessoryAddOn(deal.Requested[i]).IsSuccess);
            }

            AccessoryAddOnView view = s.Api.GetAccessoryAddOns();
            Assert.AreEqual(4, view.AddOnsSold);
            Assert.IsTrue(view.Options.All(o => o.IsAdded && !o.IsAvailable), "4/4: artık ekleme yok");
            Assert.AreEqual(4, Count(s, TransactionTypeIds.AccessorySale));
        }

        [Test]
        public void OnlyAPartOfTheRequest_CanBeSold_AndTheSaleStillStands()
        {
            AddOnDeal deal = AddOnDeals.WithCount(3);
            GameSession s = deal.Session;
            Stock(s, deal.Requested.ToArray());

            Assert.IsTrue(s.Api.SellAccessoryAddOn(deal.Requested[0]).IsSuccess);

            AccessoryAddOnView view = s.Api.GetAccessoryAddOns();
            Assert.AreEqual(1, view.AddOnsSold);
            Assert.AreEqual(2, view.Options.Count(o => o.IsAvailable));
            Assert.AreEqual(1, Count(s, TransactionTypeIds.AccessorySale));
            Assert.AreEqual(1, Count(s, TransactionTypeIds.Sale));
        }

        [Test]
        public void AnAccessoryTheCustomerDidNotAskFor_IsRefusedByTheApi_EvenIfInStock()
        {
            AddOnDeal deal = AddOnDeals.WithCount(1);
            GameSession s = deal.Session;
            Stock(s, Case, Adapter);
            string other = new[] { Case, Adapter }.FirstOrDefault(id => id != deal.Requested[0]) ?? Case;
            string digest = s.Api.GetStateDigest();

            Result<AccessorySaleReceipt> r = s.Api.SellAccessoryAddOn(other);

            Assert.AreEqual("addon.not_in_request", r.ErrorCode);
            Assert.AreEqual(digest, s.Api.GetStateDigest());
            Assert.AreEqual(1, s.Api.GetAccessoryAddOns().Options.Count);
        }

        [Test]
        public void TheSameRequestedAccessory_CannotBeSoldTwice()
        {
            AddOnDeal deal = AddOnDeals.Requesting(Case);
            GameSession s = deal.Session;
            Stock(s, Case);
            Assert.IsTrue(s.Api.SellAccessoryAddOn(Case).IsSuccess);
            string digest = s.Api.GetStateDigest();

            Assert.AreEqual("addon.request_limit", s.Api.SellAccessoryAddOn(Case).ErrorCode);

            Assert.AreEqual(digest, s.Api.GetStateDigest());
            Assert.AreEqual(19, s.AccessoryStock.Quantity(Case));
        }

        [Test]
        public void NeverMoreThanFiveAccessories_CanBeSoldToOneSale()
        {
            AddOnDeal deal = AddOnDeals.WithCount(5);
            GameSession s = deal.Session;
            foreach (AccessoryDefinition d in s.Content.Accessories.Definitions)
            {
                s.AccessoryStock.Add(d.Id, 2, Money.FromTl(100));
            }

            int sold = s.Content.Accessories.Definitions.Count(d => s.Api.SellAccessoryAddOn(d.Id).IsSuccess);

            Assert.AreEqual(5, sold);
            Assert.AreEqual(5, s.Api.GetAccessoryAddOns().Options.Count);
        }

        // ---------- stok yok ----------

        [Test]
        public void ARequestedAccessoryWithoutStock_IsShownButRefused_AndNothingChanges()
        {
            AddOnDeal deal = AddOnDeals.Requesting(Case);
            GameSession s = deal.Session;
            string digest = s.Api.GetStateDigest();
            Money cash = s.Api.GetCash();
            int rows = s.EconomyState.Ledger.Count;

            AccessoryAddOnOptionView option = s.Api.GetAccessoryAddOns().Options.Single(o => o.AccessoryId == Case);
            Result<AccessorySaleReceipt> r = s.Api.SellAccessoryAddOn(Case);

            Assert.AreEqual(0, option.InStock);
            Assert.IsFalse(option.IsAvailable);
            Assert.AreEqual("stock.insufficient", r.ErrorCode);
            Assert.AreEqual(digest, s.Api.GetStateDigest());
            Assert.AreEqual(cash, s.Api.GetCash());
            Assert.AreEqual(rows, s.EconomyState.Ledger.Count);
            Assert.AreEqual(0, s.AccessoryStock.TotalUnits, "negatif stok yok");
        }

        [Test]
        public void AnUnknownAccessory_IsRefused()
        {
            AddOnDeal deal = AddOnDeals.WithCount(2);

            Assert.AreEqual("accessory.unknown", deal.Session.Api.SellAccessoryAddOn("accessory.ghost").ErrorCode);
        }

        // ---------- telefon satışı tamamlanmadan / başka gün ----------

        [Test]
        public void WithoutAPhoneSale_NoAddOnIsPossible_AndTheViewSaysSo()
        {
            GameSession s = GameSession.NewGame(MarketHarness.RealContent(), 1UL);
            Stock(s, Case);
            string digest = s.Api.GetStateDigest();

            AccessoryAddOnView view = s.Api.GetAccessoryAddOns();
            Result<AccessorySaleReceipt> r = s.Api.SellAccessoryAddOn(Case);

            Assert.IsFalse(view.HasPhoneSale);
            Assert.IsFalse(view.HasRequest);
            Assert.AreEqual(0, view.Options.Count);
            Assert.AreEqual(0L, view.PhoneSaleRecordId);
            Assert.AreEqual("addon.no_sale", r.ErrorCode);
            Assert.AreEqual(digest, s.Api.GetStateDigest());
        }

        [Test]
        public void DuringAnOpenSale_BeforeTheDeal_NoAddOnIsPossible()
        {
            AddOnDeal done = AddOnDeals.Requesting(Case);
            // aynı tohumla, ama anlaşmadan ÖNCE: müşteri satışı başlatıldı, sürüyor.
            GameSession s = AddOnDeals.Prepare(done.Seed, done.Bumps);
            var guided = s.Market.Listings.Single(l => l.IsGuided);
            s.Api.StartNegotiation(guided.ListingId);
            s.Api.MakeOffer(Money.FromTl(5800));
            s.Api.SetPrice(guided.InstanceId, Money.FromTl(5900));
            Stock(s, Case);
            Assert.IsTrue(s.Api.StartSale(s.Api.GetCustomers()[0].CustomerId).IsSuccess);
            Assert.IsNotNull(s.Api.GetSale(), "satış sürüyor");
            string digest = s.Api.GetStateDigest();

            Assert.AreEqual("addon.no_sale", s.Api.SellAccessoryAddOn(Case).ErrorCode);
            Assert.IsFalse(s.Api.GetAccessoryAddOns().HasPhoneSale);
            Assert.AreEqual(digest, s.Api.GetStateDigest());
        }

        [Test]
        public void OnAnotherDay_TheEarlierPhoneSaleOffersNoAddOn()
        {
            AddOnDeal deal = AddOnDeals.Requesting(Case);
            GameSession s = deal.Session;
            Stock(s, Case);
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
            AddOnDeal deal = AddOnDeals.Requesting(Case);
            GameSession s = deal.Session;
            s.Api.EndDay();

            Result<AccessorySaleReceipt> r = s.AccessoryAddOns.SellAddOn(deal.SaleRecordId, Case, s.Api.GetDay());

            Assert.AreEqual("addon.sale_closed", r.ErrorCode);
        }

        // ---------- telefon satışı değişmez ----------

        [Test]
        public void TheAddOn_NeverRepeatsThePhoneSale_OrChangesItsPriceOrRow()
        {
            AddOnDeal deal = AddOnDeals.WithCount(2);
            GameSession s = deal.Session;
            Stock(s, deal.Requested.ToArray());
            TransactionRecord before;
            s.EconomyState.Ledger.TryGetById(deal.SaleRecordId, out before);
            int phoneSales = Count(s, TransactionTypeIds.Sale);
            int phoneRows = s.Api.GetInventory().Count;
            int saleCountToday = s.Api.GetTodaySummary().Sales.Count;

            foreach (string id in deal.Requested)
            {
                s.Api.SellAccessoryAddOn(id);
            }

            Assert.AreEqual(phoneSales, Count(s, TransactionTypeIds.Sale), "telefon satışı ikinci kez yazılmadı");
            Assert.AreEqual(saleCountToday, s.Api.GetTodaySummary().Sales.Count);
            Assert.AreEqual(phoneRows, s.Api.GetInventory().Count, "telefon rafı değişmedi");
            TransactionRecord after;
            s.EconomyState.Ledger.TryGetById(deal.SaleRecordId, out after);
            Assert.AreEqual(before.Amount, after.Amount, "telefon satış fiyatı aynı");
            Assert.AreEqual(before.SaleCostBasis, after.SaleCostBasis);
            Assert.AreEqual(before.BalanceAfter, after.BalanceAfter);
            Assert.AreEqual(deal.Deal.DealPrice, after.Amount, "pazarlık sonucu aynı");
        }

        [Test]
        public void TheAccessoryPrice_IsTheFixedRetailPrice()
        {
            AddOnDeal deal = AddOnDeals.Requesting(Case);
            Stock(deal.Session, Case);

            Result<AccessorySaleReceipt> r = deal.Session.Api.SellAccessoryAddOn(Case);

            Assert.AreEqual(deal.Session.Content.Accessories.Definitions.Single(d => d.Id == Case).RetailPrice, r.Value.SalePrice);
        }

        // ---------- RNG, toplam kâr, kayıt ----------

        [Test]
        public void TheAddOnApi_DoesNotUseRandomness()
        {
            AddOnDeal deal = AddOnDeals.Requesting(Case);
            GameSession s = deal.Session;
            Stock(s, Case);
            var rng = s.Capture().Rng;

            s.Api.GetAccessoryAddOns();
            s.Api.SellAccessoryAddOn(Case);
            s.Api.SellAccessoryAddOn("accessory.ghost");

            Assert.IsNull(DeepCompare.FirstDifference(rng, s.Capture().Rng));
        }

        [Test]
        public void TheTotalProfitAndRevenue_AreThePhoneProfitPlusAllAddOns()
        {
            AddOnDeal deal = AddOnDeals.WithCount(2);
            GameSession s = deal.Session;
            Stock(s, deal.Requested.ToArray());
            Money phoneProfit = s.Api.GetAccessoryAddOns().PhoneProfit;

            AccessorySaleReceipt a = s.Api.SellAccessoryAddOn(deal.Requested[0]).Value;
            AccessorySaleReceipt b = s.Api.SellAccessoryAddOn(deal.Requested[1]).Value;
            AccessoryAddOnView view = s.Api.GetAccessoryAddOns();

            Assert.AreEqual(2, view.AddOnsSold);
            Assert.AreEqual(a.SalePrice + b.SalePrice, view.AccessoryRevenue);
            Assert.AreEqual(a.Profit + b.Profit, view.AccessoryProfit);
            Assert.AreEqual(phoneProfit + a.Profit + b.Profit, view.TotalProfit);
            Assert.AreEqual(view.TotalProfit, s.AccessoryAddOns.TotalProfitOfSale(deal.SaleRecordId).Value);
        }

        [Test]
        public void TheAddOnState_SurvivesSaveAndLoad_AndTheRequestIsTheSame()
        {
            AddOnDeal deal = AddOnDeals.Requesting(Case);
            Stock(deal.Session, Case);
            deal.Session.Api.SellAccessoryAddOn(Case);

            Result<GameSession> back = GameSession.Restore(MarketHarness.RealContent(), deal.Session.Capture());

            Assert.IsTrue(back.IsSuccess, back.ErrorCode + ": " + back.Message);
            AccessoryAddOnView before = deal.Session.Api.GetAccessoryAddOns();
            AccessoryAddOnView after = back.Value.Api.GetAccessoryAddOns();
            Assert.AreEqual(before.PhoneSaleRecordId, after.PhoneSaleRecordId);
            Assert.AreEqual(before.AddOnsSold, after.AddOnsSold);
            Assert.AreEqual(before.TotalProfit, after.TotalProfit);
            CollectionAssert.AreEqual(before.Options.Select(o => o.AccessoryId).ToArray(), after.Options.Select(o => o.AccessoryId).ToArray());
            Assert.AreEqual(deal.Session.Api.GetStateDigest(), back.Value.Api.GetStateDigest());
        }

        [Test]
        public void ThePhoneSaleFlow_ItselfIsUnchanged_ForTheCustomerView()
        {
            AddOnDeal deal = AddOnDeals.NoRequest();

            Assert.AreEqual(NegotiationPhase.Deal, deal.Deal.Phase);
            Assert.IsNull(deal.Session.Api.GetSale(), "satış tamamlandı");
            Assert.AreEqual(0, deal.Session.Api.GetInventory().Count, "telefon satıldı");
            Assert.IsTrue(deal.Session.Api.GetCash() > Money.Zero);
        }
    }
}
