using System.Collections.Generic;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Inventory;
using Esnaf.Domain.Products;
using NUnit.Framework;

namespace Esnaf.Tests.Support
{
    /// <summary>
    /// Ekonomi + envanter testleri için hazır düzenek: fixture JSON'larından kurulan türler/sabitler, boş defter,
    /// servisler ve olay yolu. Kural mantığı içermez; yalnızca kurulum ve kısa yollar sağlar.
    /// </summary>
    public sealed class EconomyHarness
    {
        private long _nextInstanceId = 1;

        public TransactionTypes Types { get; }
        public EconomyConstants Constants { get; }
        public EconomyState State { get; }
        public EventBus Bus { get; }
        public EconomyService Economy { get; }
        public InstanceStore Store { get; }
        public InventoryState Inventory { get; }
        public InventoryService Shop { get; }
        public WealthCalculator Wealth { get; }
        public DaySummaryBuilder Summaries { get; }
        public LedgerView LedgerView { get; }

        public EconomyHarness(EconomyConstants constants = null, bool openBooks = true)
        {
            Types = ContentFixtures.Types();
            Constants = constants ?? ContentFixtures.Constants();
            State = new EconomyState(Types);
            Bus = new EventBus();
            Economy = new EconomyService(State, Constants, Bus);
            Store = new InstanceStore();
            Inventory = new InventoryState(Constants.InitialShelfCapacity);
            Shop = new InventoryService(Inventory, Store, Economy, Bus);
            Wealth = new WealthCalculator(new IWealthContributor[]
            {
                new CashWealthContributor(State),
                new StockWealthContributor(Inventory, Store),
                new BusinessAssetsWealthContributor(State),
                new PendingAppraisalWealthContributor(State)
            });
            Summaries = new DaySummaryBuilder(State, Types);
            LedgerView = new LedgerView(State, Types);

            if (openBooks)
            {
                Result opened = Economy.OpenBooks();
                Assert.IsTrue(opened.IsSuccess, "Harness: defter açılamadı");
            }
        }

        public Money Cash
        {
            get { return Economy.Cash; }
        }

        /// <summary>Pazarda (Market) duran yeni bir ürün örneği üretir ve depoya koyar.</summary>
        public ProductInstance NewMarketInstance(string definitionId = "phone.test_one", string sellerNpcId = null)
        {
            var instance = new ProductInstance
            {
                InstanceId = _nextInstanceId++,
                DefinitionId = definitionId,
                StorageGb = 128,
                AgeMonths = 12,
                SellerNpcId = sellerNpcId,
                Location = ProductLocation.Market
            };
            Store.Add(instance);
            return instance;
        }

        public void Buy(ProductInstance instance, long price, int day)
        {
            Result result = Shop.Acquire(instance.InstanceId, Money.FromTl(price), day);
            Assert.IsTrue(result.IsSuccess, "Harness: alım başarısız: " + result);
        }

        public SaleReceipt Sell(ProductInstance instance, long price, int day, string buyer = null)
        {
            Result<SaleReceipt> result = Shop.Sell(instance.InstanceId, Money.FromTl(price), day, buyer);
            Assert.IsTrue(result.IsSuccess, "Harness: satış başarısız: " + result);
            return result.Value;
        }

        public void PayAppraisal(ProductInstance instance, long fee, int day)
        {
            Result<TransactionRecord> result = Economy.PayAppraisal(instance.InstanceId, instance.DefinitionId, Money.FromTl(fee), day);
            Assert.IsTrue(result.IsSuccess, "Harness: ekspertiz ödenemedi: " + result);
        }

        public void WriteOff(ProductInstance instance, int day)
        {
            Result<Money> result = Economy.WriteOffAppraisals(instance.InstanceId, day);
            Assert.IsTrue(result.IsSuccess, "Harness: ekspertiz gideri işlenemedi: " + result);
        }

        public void ChargeExpense(int day)
        {
            Result<Money> result = Economy.ChargeDailyExpense(day);
            Assert.IsTrue(result.IsSuccess, "Harness: günlük gider işlenemedi: " + result);
        }

        public DaySummary CloseDay(int day)
        {
            return Summaries.Build(day, Shop.GetStockLines(), Wealth.Calculate());
        }

        public Money TotalWealth()
        {
            return Wealth.Calculate().Total;
        }

        public Money StockCost()
        {
            return Money.FromTl(Shop.GetStockLines().Sum(l => l.CostBasis.Tl));
        }

        /// <summary>
        /// GDD değişmezleri: I1 (nakit = başlangıç + Σ defter), I2 (servet kimliği: servet = başlangıç + Σ gün net kârı),
        /// I3 (raf ≤ kapasite), I8 (ürün tek konumda), defter zinciri tutarlı.
        /// </summary>
        public void AssertInvariants(Money openingCapital, int lastDay, string context = "")
        {
            // I1: nakit = başlangıç sermayesi + Σ(defterdeki diğer tüm tutarlar)
            long otherRows = 0;
            long allRows = 0;
            int openingRows = 0;
            foreach (TransactionRecord record in State.Ledger.Records)
            {
                allRows += record.Amount.Tl;
                if (record.TypeId == TransactionTypeIds.OpeningCapital)
                {
                    openingRows++;
                    Assert.AreEqual(openingCapital, record.Amount, "Başlangıç sermayesi satırı " + context);
                }
                else
                {
                    otherRows += record.Amount.Tl;
                }
            }

            Assert.AreEqual(1, openingRows, "Tam bir başlangıç sermayesi satırı olmalı " + context);
            Assert.AreEqual(openingCapital.Tl + otherRows, Economy.Cash.Tl, "I1 nakit = 250.000 + Σ defter " + context);
            Assert.AreEqual(allRows, State.Ledger.Balance.Tl, "I1 defter bakiyesi " + context);
            Assert.IsTrue(State.Ledger.Verify().IsSuccess, "Defter zinciri " + context);

            // I2 (kimlik): servet = başlangıç sermayesi + Σ(gün net kârı)
            long cumulativeNet = 0;
            for (int day = 0; day <= lastDay; day++)
            {
                cumulativeNet += Summaries.NetProfit(day).Tl;
            }

            Assert.AreEqual(openingCapital.Tl + cumulativeNet, TotalWealth().Tl, "I2 servet kimliği " + context);

            // I3
            Assert.LessOrEqual(Inventory.Count, Inventory.Capacity, "I3 raf doluluğu " + context);

            // I8
            var ids = new HashSet<long>(Inventory.ItemIds);
            Assert.AreEqual(Inventory.Count, ids.Count, "Rafta yinelenen ürün " + context);
            foreach (ProductInstance instance in Store.All)
            {
                bool onShelf = ids.Contains(instance.InstanceId);
                Assert.AreEqual(instance.Location == ProductLocation.Inventory, onShelf, "I8 konum/raf uyumu #" + instance.InstanceId + " " + context);
            }
        }
    }
}
