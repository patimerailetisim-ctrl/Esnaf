using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Game;
using Esnaf.Domain.Market;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Economy
{
    /// <summary>Günlük özette toptan aksesuar alışı (Day 11.2.3): ayrı ve açık kalem, toplam harcamada, defterle tutarlı, ürün örneği gerektirmez, telefon toplamlarını bozmaz.</summary>
    public class WholesaleDaySummaryTests
    {
        private const string Supplier = "supplier.ucuz_toptan";
        private const string Adapter = "accessory.charger_adapter"; // 1.500
        private const string Case = "accessory.phone_case";          // 1.400

        private static GameSession New()
        {
            return GameSession.NewGame(MarketHarness.RealContent(), 1UL);
        }

        private static DaySummary Summary(GameSession s)
        {
            return s.Api.GetTodaySummary();
        }

        [Test]
        public void AWholesaleBuy_ShowsAsItsOwnLine_AndNotAsAPhonePurchase()
        {
            GameSession s = New();

            s.WholesaleService.BuyPack(Supplier, Adapter, 1);
            DaySummary sum = Summary(s);

            Assert.AreEqual(Money.FromTl(1500), sum.WholesaleSpend);
            Assert.AreEqual(Money.Zero, sum.PurchaseSpend, "telefon alışı değişmez");
        }

        [Test]
        public void SeveralBuys_AreSummedAcrossTheDay()
        {
            GameSession s = New();

            s.WholesaleService.BuyPack(Supplier, Adapter, 1);
            s.WholesaleService.BuyPack(Supplier, Case, 1);
            s.WholesaleService.BuyPack(Supplier, Case, 1);

            Assert.AreEqual(Money.FromTl(1500 + 1400 + 1400), Summary(s).WholesaleSpend);
        }

        [Test]
        public void TheTotalSpending_IncludesWholesale_AndMatchesTheLedgerOutflow()
        {
            GameSession s = New();
            s.WholesaleService.BuyPack(Supplier, Adapter, 1);

            DaySummary sum = Summary(s);

            Assert.AreEqual(sum.PurchaseSpend + sum.WholesaleSpend + sum.AppraisalSpend + sum.RepairSpend + sum.DailyExpense, sum.TotalSpending);
            Assert.AreEqual(Money.FromTl(1500), sum.TotalSpending);
            long ledgerOutflow = -s.EconomyState.Ledger.Records
                .Where(r => r.Day == 1 && r.TypeId != "opening_capital")
                .Sum(r => r.Amount.Tl);
            Assert.AreEqual(ledgerOutflow, sum.TotalSpending.Tl, "defter çıkışıyla tutarlı");
        }

        [Test]
        public void TheCashIdentity_HoldsWithWholesale()
        {
            GameSession s = New();
            s.WholesaleService.BuyPack(Supplier, Adapter, 1);
            s.WholesaleService.BuyPack(Supplier, Case, 1);

            DaySummary sum = Summary(s);

            Assert.AreEqual(sum.OpeningCash + sum.CapitalInflow + sum.TotalIncome - sum.TotalSpending - sum.InvestmentSpend, sum.ClosingCash);
        }

        [Test]
        public void TheLedgerRows_AppearInTheDaysRecords_WithoutAnInstanceId()
        {
            GameSession s = New();

            s.WholesaleService.BuyPack(Supplier, Adapter, 1);
            DaySummary sum = Summary(s);

            TransactionRecord row = sum.Records.Single(r => r.TypeId == TransactionTypeIds.WholesalePurchase);
            Assert.IsNull(row.InstanceId);
            Assert.AreEqual(Money.FromTl(-1500), row.Amount);
            Assert.AreEqual(0, sum.Sales.Count, "aksesuar alışı satış listesine girmez");
        }

        [Test]
        public void TheNetProfit_IsNotAffected_TheCostGoesToStock()
        {
            GameSession s = New();
            Money before = Summary(s).NetProfit;

            s.WholesaleService.BuyPack(Supplier, Adapter, 1);

            Assert.AreEqual(before, Summary(s).NetProfit);
            Assert.AreEqual(Money.Zero, Summary(s).GrossProfit);
        }

        [Test]
        public void TheWealthChange_StaysConsistent_BecauseTheStockIsInWealth()
        {
            GameSession s = New();
            Money wealthBefore = s.Wealth.Calculate().Total;

            s.WholesaleService.BuyPack(Supplier, Adapter, 1);
            DaySummary sum = Summary(s);

            Assert.AreEqual(s.Wealth.Calculate().Total - wealthBefore, sum.WealthChange - (sum.CapitalInflow - sum.CapitalInflow), "servet değişimi = net kâr");
            Assert.AreEqual(Money.FromTl(1500), sum.Wealth.GetAmount(WealthKeys.AccessoryStock));
        }

        [Test]
        public void ThePhoneSummaries_AreUnchanged_ByAWholesaleBuyOnTheSameDay()
        {
            GameSession plain = New();
            GameSession mixed = New();
            MarketListing guided = plain.Market.Listings.Single(l => l.IsGuided);
            MarketListing guided2 = mixed.Market.Listings.Single(l => l.IsGuided);
            Assert.IsTrue(plain.Api.BuyListing(guided.ListingId).IsSuccess);
            Assert.IsTrue(mixed.Api.BuyListing(guided2.ListingId).IsSuccess);
            mixed.WholesaleService.BuyPack(Supplier, Case, 1);

            DaySummary a = Summary(plain);
            DaySummary b = Summary(mixed);

            Assert.AreEqual(a.PurchaseSpend, b.PurchaseSpend, "telefon alış toplamı aynı");
            Assert.AreEqual(a.StockCostBasis, b.StockCostBasis, "telefon stok maliyeti aynı");
            Assert.AreEqual(a.StockCount, b.StockCount);
            Assert.AreEqual(a.NetProfit, b.NetProfit);
            Assert.AreEqual(Money.Zero, a.WholesaleSpend);
            Assert.AreEqual(Money.FromTl(1400), b.WholesaleSpend);
            Assert.AreEqual(a.TotalSpending + Money.FromTl(1400), b.TotalSpending);
        }

        [Test]
        public void ADayWithoutWholesale_HasZeroWholesaleSpend()
        {
            Assert.AreEqual(Money.Zero, Summary(New()).WholesaleSpend);
        }

        [Test]
        public void TheDayEndReport_CarriesTheSameLine_AndTheNextDayStartsAtZero()
        {
            GameSession s = New();
            s.WholesaleService.BuyPack(Supplier, Adapter, 1);

            var report = s.Api.EndDay().Value;

            Assert.AreEqual(Money.FromTl(1500), report.Summary.WholesaleSpend);
            Assert.AreEqual(Money.Zero, Summary(s).WholesaleSpend, "yeni günün özeti sıfırdan");
        }
    }
}
