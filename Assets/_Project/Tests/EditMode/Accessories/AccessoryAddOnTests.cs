using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Accessories;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Game;
using Esnaf.Domain.Inventory;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Accessories
{
    /// <summary>
    /// Telefon satışına aksesuar ek satışı (Gün 11.3.1), gerçek içerik ve gerçek oturum üstünde: stoktan 1 birim, sabit retailPrice, gerçek maliyet, kâr,
    /// aynı güne bağlı telefon satışı, atomiklik, günlük özet, servet, RNG ve telefon tarafının değişmemesi. Gün 11.3.4'ten beri YALNIZCA müşterinin istediği aksesuar
    /// satılır: bu yüzden testler, talebi gereken aksesuarları içeren bir telefon satışı bulur (satış tutarı değiştirilerek; talep tutardan da türer).
    /// </summary>
    public class AccessoryAddOnTests
    {
        private const string Case = "accessory.phone_case";      // retailPrice 160
        private const string Adapter = "accessory.charger_adapter"; // retailPrice 250
        private const string Supplier = "supplier.ucuz_toptan";

        private static GameSession New()
        {
            return GameSession.NewGame(MarketHarness.RealContent(), 1UL);
        }

        // Bir telefon alır ve Gün 1'de 'price'a satar; satışın defter satırını döndürür.
        private static SaleReceipt PhoneSale(GameSession s, Money price, string buyerNpcId = "npc.kemal")
        {
            Assert.IsTrue(s.Api.BuyListing(s.Api.GetListings()[0].ListingId).IsSuccess);
            long instanceId = s.Api.GetInventory()[0].InstanceId;
            Result<SaleReceipt> sold = s.InventoryService.Sell(instanceId, price, s.Time.Day, buyerNpcId);
            Assert.IsTrue(sold.IsSuccess, sold.ErrorCode);
            return sold.Value;
        }

        private static readonly System.Collections.Generic.Dictionary<string, long> PriceCache = new System.Collections.Generic.Dictionary<string, long>();

        // Müşterinin İSTEDİĞİ aksesuarlar 'needed' kümesini kapsayan bir telefon satışı yapar (tutar 10 ₺ adımlarıyla aranır; talep tutardan da türer).
        private static SaleReceipt RequestedSale(out GameSession s, out Money price, params string[] needed)
        {
            string key = string.Join(",", needed);
            long cachedTl;
            if (PriceCache.TryGetValue(key, out cachedTl))
            {
                s = New();
                price = Money.FromTl(cachedTl);
                return PhoneSale(s, price);
            }

            for (long tl = 20000; tl < 120000; tl += 10)
            {
                GameSession candidate = New();
                Money tryPrice = Money.FromTl(tl);
                SaleReceipt sale = PhoneSale(candidate, tryPrice);
                System.Collections.Generic.IReadOnlyList<string> request = candidate.AccessoryAddOns.RequestedAccessories(sale.RecordId);
                if (needed.All(request.Contains))
                {
                    PriceCache[key] = tl;
                    s = candidate;
                    price = tryPrice;
                    return sale;
                }
            }

            throw new System.InvalidOperationException("No sale price gives a request containing " + key);
        }

        private sealed class Snap
        {
            public Money Cash;
            public int Ledger;
            public int Units;
            public Money StockCost;
            public string Digest;

            public static Snap Of(GameSession s)
            {
                return new Snap
                {
                    Cash = s.EconomyService.Cash,
                    Ledger = s.EconomyState.Ledger.Count,
                    Units = s.AccessoryStock.TotalUnits,
                    StockCost = s.AccessoryStock.StockCost,
                    Digest = s.Api.GetStateDigest()
                };
            }
        }

        private static void AssertUnchanged(Snap before, GameSession s)
        {
            Snap after = Snap.Of(s);
            Assert.AreEqual(before.Cash, after.Cash, "nakit");
            Assert.AreEqual(before.Ledger, after.Ledger, "defter");
            Assert.AreEqual(before.Units, after.Units, "stok birimi");
            Assert.AreEqual(before.StockCost, after.StockCost, "stok maliyeti");
            Assert.AreEqual(before.Digest, after.Digest, "durum özeti");
        }

        // ---------- başarılı ek satış ----------

        [Test]
        public void WithOneUnitInStock_TheAddOnSucceeds_AndTheStockDropsByOne()
        {
            GameSession s;
            Money price;
            SaleReceipt phone = RequestedSale(out s, out price, Case);
            Assert.IsTrue(s.AccessoryStock.Add(Case, 1, Money.FromTl(70)).IsSuccess);

            Result<AccessorySaleReceipt> r = s.AccessoryAddOns.SellAddOn(phone.RecordId, Case, s.Time.Day);

            Assert.IsTrue(r.IsSuccess, r.ErrorCode);
            Assert.AreEqual(0, s.AccessoryStock.Quantity(Case));
            Assert.AreEqual(0, s.AccessoryStock.TotalUnits);
            Assert.AreEqual(Money.Zero, s.AccessoryStock.TotalCost(Case));
        }

        [Test]
        public void TheCash_RisesByTheFixedRetailPrice()
        {
            GameSession s;
            Money price;
            SaleReceipt phone = RequestedSale(out s, out price, Case);
            s.AccessoryStock.Add(Case, 1, Money.FromTl(70));
            Money cash = s.EconomyService.Cash;

            Result<AccessorySaleReceipt> r = s.AccessoryAddOns.SellAddOn(phone.RecordId, Case, 1);

            Assert.AreEqual(Money.FromTl(160), r.Value.SalePrice, "retailPrice sabit, pazarlık yok");
            Assert.AreEqual(cash + Money.FromTl(160), s.EconomyService.Cash);
            Assert.AreEqual(s.EconomyService.Cash, s.EconomyState.Ledger.Balance);
        }

        [Test]
        public void TheCostBasis_IsTheRealProportionalCostFromTheStock()
        {
            GameSession s;
            Money price;
            SaleReceipt phone = RequestedSale(out s, out price, Case);
            Assert.IsTrue(s.WholesaleService.BuyPack(Supplier, Case, 1).IsSuccess); // 20 × 70 = 1.400

            Result<AccessorySaleReceipt> r = s.AccessoryAddOns.SellAddOn(phone.RecordId, Case, 1);

            Assert.AreEqual(Money.FromTl(70), r.Value.CostBasis, "1.400 / 20");
            Assert.AreEqual(19, s.AccessoryStock.Quantity(Case));
            Assert.AreEqual(Money.FromTl(1330), s.AccessoryStock.TotalCost(Case), "kalan maliyet 19 × 70");
        }

        [Test]
        public void ANonRoundCost_IsKeptExactly_AndTheProfitUsesIt()
        {
            GameSession s;
            Money price;
            SaleReceipt phone = RequestedSale(out s, out price, Case);
            s.AccessoryStock.Add(Case, 3, Money.FromTl(100)); // 33,33 ₺/adet

            AccessorySaleReceipt first = s.AccessoryAddOns.SellAddOn(phone.RecordId, Case, 1).Value;

            Assert.AreEqual(Money.FromTl(33), first.CostBasis, "100 / 3, aşağı yuvarlı");
            Assert.AreEqual(Money.FromTl(127), first.Profit, "160 − 33");
            Assert.AreEqual(2, s.AccessoryStock.Quantity(Case));
            Assert.AreEqual(Money.FromTl(67), s.AccessoryStock.TotalCost(Case), "maliyet kaybolmaz: kalan 100 − 33");
        }

        [Test]
        public void TheAccessoryProfit_IsRetailMinusTheRealCost()
        {
            GameSession s;
            Money price;
            SaleReceipt phone = RequestedSale(out s, out price, Adapter);
            s.WholesaleService.BuyPack(Supplier, Adapter, 1); // 10 × 150

            Result<AccessorySaleReceipt> r = s.AccessoryAddOns.SellAddOn(phone.RecordId, Adapter, 1);

            Assert.AreEqual(Money.FromTl(250), r.Value.SalePrice);
            Assert.AreEqual(Money.FromTl(150), r.Value.CostBasis);
            Assert.AreEqual(Money.FromTl(100), r.Value.Profit);
        }

        [Test]
        public void TheCombinedResult_IsPhoneProfitPlusAccessoryProfit()
        {
            GameSession s;
            Money price;
            SaleReceipt phone = RequestedSale(out s, out price, Adapter, Case);
            s.WholesaleService.BuyPack(Supplier, Adapter, 1);
            s.WholesaleService.BuyPack(Supplier, Case, 1);

            AccessorySaleReceipt a = s.AccessoryAddOns.SellAddOn(phone.RecordId, Adapter, 1).Value;
            AccessorySaleReceipt b = s.AccessoryAddOns.SellAddOn(phone.RecordId, Case, 1).Value;

            Assert.AreEqual(phone.Profit, a.PhoneProfit);
            Assert.AreEqual(phone.Profit + a.Profit, a.CombinedProfit);
            Assert.AreEqual(Money.FromTl(100), a.Profit);
            Assert.AreEqual(Money.FromTl(90), b.Profit, "160 − 70");
            Assert.AreEqual(phone.Profit + a.Profit + b.Profit, s.AccessoryAddOns.TotalProfitOfSale(phone.RecordId).Value, "tüm ek satışlar defterden toplanır");
        }

        [Test]
        public void ThePhoneSalesIncomeAndProfit_StayOnThePhoneSystem_AndTheAddOnGetsItsOwnLedgerRow()
        {
            GameSession s;
            Money price;
            SaleReceipt phone = RequestedSale(out s, out price, Case);
            s.AccessoryStock.Add(Case, 1, Money.FromTl(70));
            int rows = s.EconomyState.Ledger.Count;

            Result<AccessorySaleReceipt> r = s.AccessoryAddOns.SellAddOn(phone.RecordId, Case, 1);

            Assert.AreEqual(rows + 1, s.EconomyState.Ledger.Count, "tek defter kaydı");
            TransactionRecord row;
            Assert.IsTrue(s.EconomyState.Ledger.TryGetById(r.Value.LedgerRecordId, out row));
            Assert.AreEqual(TransactionTypeIds.AccessorySale, row.TypeId);
            Assert.AreEqual(Money.FromTl(160), row.Amount);
            Assert.AreEqual(Money.FromTl(70), row.SaleCostBasis);
            Assert.AreEqual(Case, row.DefinitionId);
            Assert.AreEqual("npc.kemal", row.NpcId, "alıcı telefonu alan müşteri");
            Assert.AreEqual(phone.RecordId.ToString(), row.MemoArgs.Single(), "telefon satışına bağlı");
            Assert.IsNull(row.InstanceId);
            TransactionRecord phoneRow;
            s.EconomyState.Ledger.TryGetById(phone.RecordId, out phoneRow);
            Assert.AreEqual(price, phoneRow.Amount, "telefon satışı değişmedi");
        }

        // ---------- stok yok / bağlı satış yok ----------

        [Test]
        public void WithNoStock_TheAddOnFails_AndNothingChanges()
        {
            GameSession s;
            Money price;
            SaleReceipt phone = RequestedSale(out s, out price, Case);
            Snap before = Snap.Of(s);

            Result<AccessorySaleReceipt> r = s.AccessoryAddOns.SellAddOn(phone.RecordId, Case, 1);

            Assert.IsTrue(r.IsFailure);
            Assert.AreEqual("stock.insufficient", r.ErrorCode);
            AssertUnchanged(before, s);
        }

        [Test]
        public void TheStock_NeverGoesNegative_WhenTheRequestedItemIsOutOfStock()
        {
            GameSession s;
            Money price;
            SaleReceipt phone = RequestedSale(out s, out price, Adapter, Case);
            s.AccessoryStock.Add(Case, 1, Money.FromTl(70));

            Assert.IsTrue(s.AccessoryAddOns.SellAddOn(phone.RecordId, Case, 1).IsSuccess);
            Snap before = Snap.Of(s);

            Assert.AreEqual("stock.insufficient", s.AccessoryAddOns.SellAddOn(phone.RecordId, Adapter, 1).ErrorCode);
            AssertUnchanged(before, s);
            Assert.AreEqual(0, s.AccessoryStock.Quantity(Case));
            Assert.AreEqual(0, s.AccessoryStock.TotalUnits);
        }

        [Test]
        public void AnAccessoryCannotBeSoldAlone_ItNeedsAPhoneSale()
        {
            GameSession s = New();
            s.AccessoryStock.Add(Case, 1, Money.FromTl(70));
            Snap before = Snap.Of(s);

            Assert.AreEqual("sale.unknown", s.AccessoryAddOns.SellAddOn(987654L, Case, 1).ErrorCode, "olmayan satış");
            Assert.AreEqual("sale.unknown", s.AccessoryAddOns.SellAddOn(1L, Case, 1).ErrorCode, "1 numaralı satır açılış sermayesi");
            Assert.AreEqual("sale.unknown", s.AccessoryAddOns.SellAddOn(0L, Case, 1).ErrorCode);
            AssertUnchanged(before, s);
        }

        [Test]
        public void AnAddOnRowItself_IsNotAPhoneSale()
        {
            GameSession s;
            Money price;
            SaleReceipt phone = RequestedSale(out s, out price, Case);
            s.AccessoryStock.Add(Case, 2, Money.FromTl(140));
            long addOnRow = s.AccessoryAddOns.SellAddOn(phone.RecordId, Case, 1).Value.LedgerRecordId;
            Snap before = Snap.Of(s);

            Assert.AreEqual("sale.unknown", s.AccessoryAddOns.SellAddOn(addOnRow, Case, 1).ErrorCode);
            AssertUnchanged(before, s);
        }

        [Test]
        public void TheAddOn_MustHappenOnTheDayOfThePhoneSale()
        {
            GameSession s;
            Money price;
            SaleReceipt phone = RequestedSale(out s, out price, Case);
            s.AccessoryStock.Add(Case, 1, Money.FromTl(70));
            Snap before = Snap.Of(s);

            Result<AccessorySaleReceipt> r = s.AccessoryAddOns.SellAddOn(phone.RecordId, Case, 2);

            Assert.AreEqual("addon.sale_closed", r.ErrorCode);
            AssertUnchanged(before, s);
        }

        [Test]
        public void AnUnknownAccessory_IsRefused_AndNothingChanges()
        {
            GameSession s = New();
            SaleReceipt phone = PhoneSale(s, Money.FromTl(20000));
            Snap before = Snap.Of(s);

            Assert.AreEqual("accessory.unknown", s.AccessoryAddOns.SellAddOn(phone.RecordId, "accessory.ghost", 1).ErrorCode);
            Assert.AreEqual("accessory.unknown", s.AccessoryAddOns.SellAddOn(phone.RecordId, null, 1).ErrorCode);
            AssertUnchanged(before, s);
        }

        [Test]
        public void IfTheLedgerRefuses_TheStockIsPutBackExactly()
        {
            GameSession s;
            Money price;
            SaleReceipt phone = RequestedSale(out s, out price, Case);
            s.WholesaleService.BuyPack(Supplier, Case, 3); // defterin son günü 3
            Snap before = Snap.Of(s);

            // Telefon satışı Gün 1'de; defter Gün 3'te: satış günü eşleşir ama defter geri güne yazamaz.
            Result<AccessorySaleReceipt> r = s.AccessoryAddOns.SellAddOn(phone.RecordId, Case, 1);

            Assert.AreEqual("ledger.day_regression", r.ErrorCode);
            AssertUnchanged(before, s);
            Assert.AreEqual(20, s.AccessoryStock.Quantity(Case));
            Assert.AreEqual(Money.FromTl(1400), s.AccessoryStock.TotalCost(Case), "maliyet birebir geri geldi");
        }

        // ---------- günlük özet, servet, defter ----------

        [Test]
        public void TheDaySummary_ShowsTheAddOnIncomeAndProfit_AndTheTotalsAgree()
        {
            GameSession s;
            Money price;
            SaleReceipt phone = RequestedSale(out s, out price, Adapter);
            s.WholesaleService.BuyPack(Supplier, Adapter, 1);
            AccessorySaleReceipt a = s.AccessoryAddOns.SellAddOn(phone.RecordId, Adapter, 1).Value;

            DaySummary sum = s.Api.GetTodaySummary();

            Assert.AreEqual(price, sum.SalesIncome, "telefon satış geliri ayrı");
            Assert.AreEqual(Money.FromTl(250), sum.AccessorySalesIncome);
            Assert.AreEqual(price + Money.FromTl(250), sum.TotalIncome);
            Assert.AreEqual(a.Profit, sum.AccessoryProfit);
            Assert.AreEqual(phone.Profit + a.Profit, sum.GrossProfit, "brüt kâr = telefon + aksesuar");
            Assert.AreEqual(1, sum.Sales.Count, "aksesuar satış listesine girmez");
            Assert.AreEqual(sum.OpeningCash + sum.CapitalInflow + sum.TotalIncome - sum.TotalSpending - sum.InvestmentSpend, sum.ClosingCash, "nakit kimliği");
        }

        [Test]
        public void TheWealthChange_OfTheAddOn_EqualsItsProfit()
        {
            GameSession s;
            Money price;
            SaleReceipt phone = RequestedSale(out s, out price, Adapter);
            s.WholesaleService.BuyPack(Supplier, Adapter, 1);
            Money wealthBefore = s.Wealth.Calculate().Total;

            AccessorySaleReceipt a = s.AccessoryAddOns.SellAddOn(phone.RecordId, Adapter, 1).Value;

            Assert.AreEqual(a.Profit, s.Wealth.Calculate().Total - wealthBefore);
            DaySummary sum = s.Api.GetTodaySummary();
            Assert.AreEqual(sum.NetProfit + sum.CapitalInflow, sum.WealthChange);
        }

        [Test]
        public void TheLedgerExplanation_ShowsCostBasisAndProfit()
        {
            GameSession s;
            Money price;
            SaleReceipt phone = RequestedSale(out s, out price, Case);
            s.AccessoryStock.Add(Case, 1, Money.FromTl(70));
            AccessorySaleReceipt a = s.AccessoryAddOns.SellAddOn(phone.RecordId, Case, 1).Value;

            var explanation = s.LedgerView.Explain(a.LedgerRecordId);

            Assert.IsTrue(explanation.IsSuccess);
            Assert.AreEqual(Money.FromTl(70), explanation.Value.CostBasis);
            Assert.AreEqual(Money.FromTl(90), explanation.Value.Profit);
        }

        // ---------- kayıt ----------

        [Test]
        public void TheAddOn_SurvivesSaveAndLoad_AndTheDigestRoundTrips()
        {
            GameSession s;
            Money price;
            SaleReceipt phone = RequestedSale(out s, out price, Case);
            s.WholesaleService.BuyPack(Supplier, Case, 1);
            s.AccessoryAddOns.SellAddOn(phone.RecordId, Case, 1);

            Result<GameSession> back = GameSession.Restore(MarketHarness.RealContent(), s.Capture());

            Assert.IsTrue(back.IsSuccess, back.ErrorCode + ": " + back.Message);
            Assert.AreEqual(s.Api.GetStateDigest(), back.Value.Api.GetStateDigest());
            Assert.AreEqual(19, back.Value.AccessoryStock.Quantity(Case));
            Assert.AreEqual(Money.FromTl(1330), back.Value.AccessoryStock.TotalCost(Case));
            Assert.AreEqual(s.EconomyService.Cash, back.Value.EconomyService.Cash);
            Assert.AreEqual(s.AccessoryAddOns.TotalProfitOfSale(phone.RecordId).Value, back.Value.AccessoryAddOns.TotalProfitOfSale(phone.RecordId).Value);
            Assert.AreEqual(Money.FromTl(160), back.Value.Api.GetTodaySummary().AccessorySalesIncome);
        }

        // ---------- mevcut sistemi koruma ----------

        [Test]
        public void TheAddOn_DoesNotUseRandomness_TheRngStaysUntouched()
        {
            GameSession s;
            Money price;
            SaleReceipt phone = RequestedSale(out s, out price, Case);
            s.AccessoryStock.Add(Case, 2, Money.FromTl(140));
            var rng = s.Capture().Rng;

            s.AccessoryAddOns.SellAddOn(phone.RecordId, Case, 1);
            s.AccessoryAddOns.SellAddOn(phone.RecordId, Case, 1);
            s.AccessoryAddOns.SellAddOn(phone.RecordId, Case, 1); // reddedilen de rastgelelik tüketmez

            Assert.IsNull(DeepCompare.FirstDifference(rng, s.Capture().Rng));
        }

        [Test]
        public void TheAccessoryCapacity_StaysSixty_AndTheSoldUnitFreesItsPlace()
        {
            GameSession s;
            Money price;
            SaleReceipt phone = RequestedSale(out s, out price, Case);
            s.WholesaleService.BuyPack(Supplier, Case, 1);
            s.WholesaleService.BuyPack(Supplier, Case, 1);
            s.WholesaleService.BuyPack(Supplier, Case, 1); // 60/60
            Assert.AreEqual(0, s.AccessoryStock.FreeUnits);

            s.AccessoryAddOns.SellAddOn(phone.RecordId, Case, 1);

            Assert.AreEqual(60, s.AccessoryStock.Capacity);
            Assert.AreEqual(1, s.AccessoryStock.FreeUnits);
            Assert.AreEqual("stock.full", s.WholesaleService.BuyPack(Supplier, Case, 1).ErrorCode, "20'lik paket 1 boş yere sığmaz");
        }

        [Test]
        public void ThePhoneCustomerSaleFlow_IsUnchanged_AndARequestedAddOnCanFollowARealCustomerDeal()
        {
            // Gerçek müşteri akışı: StartSale / AskPrice (anlaşma); müşteri kılıf istedi → telefon satışının defter satırına ek satış.
            AddOnDeal deal = AddOnDeals.Requesting(Case);
            GameSession s = deal.Session;
            s.WholesaleService.BuyPack(Supplier, Case, 1);

            Result<AccessorySaleReceipt> r = s.AccessoryAddOns.SellAddOn(deal.SaleRecordId, Case, 1);

            Assert.IsTrue(r.IsSuccess, r.ErrorCode);
            Assert.AreEqual(deal.Customer.NpcId, s.EconomyState.Ledger.Records.Single(x => x.TypeId == TransactionTypeIds.AccessorySale).NpcId);
        }

        // ---------- müşteri talebi (Gün 11.3.4) ----------

        [Test]
        public void IfTheCustomerAskedForNothing_NoAccessoryCanBeSold_EvenWithStock()
        {
            AddOnDeal deal = AddOnDeals.NoRequest();
            GameSession s = deal.Session;
            foreach (AccessoryDefinition d in s.Content.Accessories.Definitions)
            {
                Assert.IsTrue(s.AccessoryStock.Add(d.Id, 2, Money.FromTl(100)).IsSuccess);
            }

            Snap before = Snap.Of(s);

            foreach (AccessoryDefinition d in s.Content.Accessories.Definitions)
            {
                Assert.AreEqual("addon.not_requested", s.AccessoryAddOns.SellAddOn(deal.SaleRecordId, d.Id, 1).ErrorCode, d.Id);
            }

            AssertUnchanged(before, s);
        }

        [Test]
        public void AnAccessoryTheCustomerDidNotAskFor_IsRefused_AndOnlyTheRequestedOnesAreSold()
        {
            AddOnDeal deal = AddOnDeals.WithCount(2);
            GameSession s = deal.Session;
            foreach (AccessoryDefinition d in s.Content.Accessories.Definitions)
            {
                s.AccessoryStock.Add(d.Id, 1, Money.FromTl(50));
            }

            Snap before = Snap.Of(s);
            foreach (AccessoryDefinition d in s.Content.Accessories.Definitions.Where(d => !deal.Requested.Contains(d.Id)))
            {
                Assert.AreEqual("addon.not_in_request", s.AccessoryAddOns.SellAddOn(deal.SaleRecordId, d.Id, 1).ErrorCode, d.Id);
            }

            AssertUnchanged(before, s);
            foreach (string id in deal.Requested)
            {
                Assert.IsTrue(s.AccessoryAddOns.SellAddOn(deal.SaleRecordId, id, 1).IsSuccess, id);
            }

            Assert.AreEqual(2, s.AccessoryAddOns.AddOnsOf(deal.SaleRecordId).Count);
        }

        [Test]
        public void ARequestedAccessory_IsOneUnit_TheSameOneCannotBeSoldTwice()
        {
            GameSession s;
            Money price;
            SaleReceipt phone = RequestedSale(out s, out price, Case);
            s.AccessoryStock.Add(Case, 5, Money.FromTl(350));
            Assert.IsTrue(s.AccessoryAddOns.SellAddOn(phone.RecordId, Case, 1).IsSuccess);
            Snap before = Snap.Of(s);

            Assert.AreEqual("addon.request_limit", s.AccessoryAddOns.SellAddOn(phone.RecordId, Case, 1).ErrorCode);
            AssertUnchanged(before, s);
        }

        [Test]
        public void AtMostFiveAccessories_CanEverBeSoldToOneSale()
        {
            AddOnDeal deal = AddOnDeals.WithCount(5);
            GameSession s = deal.Session;
            foreach (AccessoryDefinition d in s.Content.Accessories.Definitions)
            {
                s.AccessoryStock.Add(d.Id, 3, Money.FromTl(150));
            }

            int sold = 0;
            foreach (AccessoryDefinition d in s.Content.Accessories.Definitions)
            {
                sold += s.AccessoryAddOns.SellAddOn(deal.SaleRecordId, d.Id, 1).IsSuccess ? 1 : 0;
            }

            Assert.AreEqual(5, deal.Requested.Count);
            Assert.AreEqual(5, sold, "6 aksesuardan yalnızca istenen 5'i satılır");
            Assert.AreEqual(5, s.AccessoryAddOns.AddOnsOf(deal.SaleRecordId).Count);
        }

        [Test]
        public void ARequestOfThree_CanBePartlySold_AndTheRestStaysUnsold()
        {
            AddOnDeal deal = AddOnDeals.WithCount(3);
            GameSession s = deal.Session;
            foreach (string id in deal.Requested)
            {
                s.AccessoryStock.Add(id, 2, Money.FromTl(100));
            }

            Assert.IsTrue(s.AccessoryAddOns.SellAddOn(deal.SaleRecordId, deal.Requested[0], 1).IsSuccess);

            Assert.AreEqual(1, s.AccessoryAddOns.AddOnsOf(deal.SaleRecordId).Count);
            Assert.AreEqual(2, s.AccessoryStock.Quantity(deal.Requested[1]), "satılmayan istek stokta kalır");
        }

        [Test]
        public void TheServiceChecksItsArguments()
        {
            GameSession s = New();

            Assert.Throws<System.ArgumentNullException>(() => new AccessoryAddOnService(null, s.AccessoryStock, s.EconomyService, s.EconomyState));
            Assert.Throws<System.ArgumentNullException>(() => new AccessoryAddOnService(s.Content.Accessories, null, s.EconomyService, s.EconomyState));
            Assert.Throws<System.ArgumentNullException>(() => new AccessoryAddOnService(s.Content.Accessories, s.AccessoryStock, null, s.EconomyState));
            Assert.Throws<System.ArgumentNullException>(() => new AccessoryAddOnService(s.Content.Accessories, s.AccessoryStock, s.EconomyService, null));
        }

        [Test]
        public void TotalProfitOfSale_RefusesANonSaleRow()
        {
            GameSession s = New();

            Assert.AreEqual("sale.unknown", s.AccessoryAddOns.TotalProfitOfSale(1L).ErrorCode);
            Assert.AreEqual("sale.unknown", s.AccessoryAddOns.TotalProfitOfSale(555L).ErrorCode);
        }
    }
}
