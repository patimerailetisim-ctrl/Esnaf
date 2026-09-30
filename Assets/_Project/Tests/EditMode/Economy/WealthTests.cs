using System;
using System.Collections.Generic;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Inventory;
using Esnaf.Domain.Products;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Economy
{
    public class WealthTests
    {
        private static Money Tl(long value)
        {
            return Money.FromTl(value);
        }

        private sealed class FixedContributor : IWealthContributor
        {
            private readonly Money _value;

            public FixedContributor(string key, Money value)
            {
                Key = key;
                _value = value;
            }

            public string Key { get; }

            public Money GetValue()
            {
                return _value;
            }
        }

        [Test]
        public void Calculator_SumsContributorsInOrder_WithNamedLines()
        {
            var calculator = new WealthCalculator(new IWealthContributor[]
            {
                new FixedContributor("wealth.a", Tl(1000)),
                new FixedContributor("wealth.b", Tl(250)),
                new FixedContributor("wealth.c", Tl(0))
            });

            WealthBreakdown breakdown = calculator.Calculate();

            Assert.AreEqual(Tl(1250), breakdown.Total);
            Assert.AreEqual(new[] { "wealth.a", "wealth.b", "wealth.c" }, breakdown.Lines.Select(l => l.Key).ToArray());
            Assert.AreEqual(Tl(250), breakdown.GetAmount("wealth.b"));
            Assert.AreEqual(Money.Zero, breakdown.GetAmount("wealth.missing"));
            Assert.IsFalse(breakdown.Lines is List<WealthLine>);
        }

        [Test]
        public void Calculator_WithNoContributors_IsZero()
        {
            Assert.AreEqual(Money.Zero, new WealthCalculator(new IWealthContributor[0]).Calculate().Total);
        }

        [Test]
        public void Calculator_RejectsNullOrDuplicateKeys()
        {
            Assert.Throws<ArgumentNullException>(() => new WealthCalculator(null));
            Assert.Throws<ArgumentException>(() => new WealthCalculator(new IWealthContributor[]
            {
                new FixedContributor("wealth.a", Tl(1)), new FixedContributor("wealth.a", Tl(2))
            }));
        }

        [Test]
        public void NegativeContributor_ReducesWealth_ThisIsTheDebtExtensionPoint()
        {
            // Kredi/borç MVP'de yok. Ama borç, ileride yalnızca NEGATİF katkı veren bir katkıcı olarak eklenebilmeli (GDD v0.3 2.3).
            var h = new EconomyHarness();
            var withLiability = new WealthCalculator(new IWealthContributor[]
            {
                new CashWealthContributor(h.State),
                new FixedContributor("wealth.liabilities", Tl(-40000))
            });

            Assert.AreEqual(Tl(210000), withLiability.Calculate().Total);
        }

        [Test]
        public void StandardContributors_ReportCashStockAssetsAndPendingSeparately()
        {
            var h = new EconomyHarness();
            ProductInstance bought = h.NewMarketInstance();
            ProductInstance pending = h.NewMarketInstance();
            h.PayAppraisal(bought, 200, 1);
            h.Buy(bought, 9100, 1);                 // stok maliyeti 9.300
            h.PayAppraisal(pending, 600, 1);        // bekleyen ekspertiz 600
            Assert.IsTrue(h.Shop.UpgradeCapacity(8, Tl(15000), 1).IsSuccess);

            WealthBreakdown w = h.Wealth.Calculate();

            Assert.AreEqual(Tl(250000 - 200 - 9100 - 600 - 15000), w.GetAmount(WealthKeys.Cash));
            Assert.AreEqual(Tl(9300), w.GetAmount(WealthKeys.Stock));
            Assert.AreEqual(Tl(15000), w.GetAmount(WealthKeys.BusinessAssets));
            Assert.AreEqual(Tl(600), w.GetAmount(WealthKeys.PendingAppraisals));
            Assert.AreEqual(Tl(250000), w.Total, "Hiçbir kâr/zarar oluşmadı: servet başlangıç sermayesine eşit");
        }

        [Test]
        public void Wealth_AfterASale_RisesByTheProfit()
        {
            var h = new EconomyHarness();
            ProductInstance phone = h.NewMarketInstance();
            h.Buy(phone, 27000, 1);
            Money before = h.TotalWealth();

            h.Sell(phone, 35000, 1);

            Assert.AreEqual(Tl(250000), before);
            Assert.AreEqual(Tl(258000), h.TotalWealth());
        }

        [Test]
        public void Wealth_AfterWasteAndExpense_DropsByExactlyThoseAmounts()
        {
            var h = new EconomyHarness();
            ProductInstance phone = h.NewMarketInstance();
            h.PayAppraisal(phone, 300, 3);
            Assert.AreEqual(Tl(250000), h.TotalWealth(), "Bekleyen ekspertiz henüz kayıp değil");

            h.WriteOff(phone, 3);
            Assert.AreEqual(Tl(249700), h.TotalWealth());

            h.ChargeExpense(3);
            Assert.AreEqual(Tl(249200), h.TotalWealth());
        }

        [Test]
        public void StockContributor_ValuesItemsAtCostBasis_NotAtTrueValue()
        {
            var h = new EconomyHarness();
            ProductInstance phone = h.NewMarketInstance();
            h.Buy(phone, 27000, 1);

            Assert.AreEqual(Tl(27000), new StockWealthContributor(h.Inventory, h.Store).GetValue());
            Assert.AreEqual(WealthKeys.Stock, new StockWealthContributor(h.Inventory, h.Store).Key);
        }

        [Test]
        public void Contributors_RejectNullArguments()
        {
            var h = new EconomyHarness();

            Assert.Throws<ArgumentNullException>(() => new CashWealthContributor(null));
            Assert.Throws<ArgumentNullException>(() => new BusinessAssetsWealthContributor(null));
            Assert.Throws<ArgumentNullException>(() => new PendingAppraisalWealthContributor(null));
            Assert.Throws<ArgumentNullException>(() => new StockWealthContributor(null, h.Store));
            Assert.Throws<ArgumentNullException>(() => new StockWealthContributor(h.Inventory, null));
        }
    }
}
