using System;
using System.Collections.Generic;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Content;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Inventory;
using Esnaf.Domain.Products;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Economy
{
    public class InventoryServiceTests
    {
        private static Money Tl(long value)
        {
            return Money.FromTl(value);
        }

        // ---------- GDD v0.3 9.3 senaryoları (yalnızca aritmetik/defter doğruluğu; denge testi DEĞİL) ----------

        [Test]
        public void T1_BuyThenSell_27000_To_35000()
        {
            var h = new EconomyHarness();
            ProductInstance phone = h.NewMarketInstance();

            Assert.AreEqual(Tl(250000), h.Cash);
            Assert.AreEqual(0, h.Inventory.Count);

            h.Buy(phone, 27000, 1);

            Assert.AreEqual(Tl(223000), h.Cash);
            Assert.AreEqual(1, h.Inventory.Count);
            Assert.AreEqual(Tl(27000), phone.CostBasis);
            Assert.AreEqual(Tl(27000), h.StockCost());

            SaleReceipt receipt = h.Sell(phone, 35000, 1);

            Assert.AreEqual(Tl(258000), h.Cash);
            Assert.AreEqual(0, h.Inventory.Count);
            Assert.AreEqual(Tl(8000), receipt.Profit);
            Assert.AreEqual(Tl(8000), h.Summaries.NetProfit(1));
            h.AssertInvariants(Tl(250000), 1);
        }

        [Test]
        public void T2_WithAppraisal_CostBasisIncludesTheFee()
        {
            var h = new EconomyHarness();
            ProductInstance phone = h.NewMarketInstance();

            h.PayAppraisal(phone, 1000, 1);
            h.Buy(phone, 27000, 1);

            Assert.AreEqual(Tl(222000), h.Cash);
            Assert.AreEqual(Tl(28000), phone.CostBasis, "Maliyet tabanı = alış + ekspertiz");
            Assert.AreEqual(Tl(27000), phone.PurchasePrice, "Alış fiyatı ekspertizsiz kalır");
            Assert.AreEqual(Money.Zero, h.Economy.PendingAppraisalCost(phone.InstanceId));

            SaleReceipt receipt = h.Sell(phone, 35000, 1);

            Assert.AreEqual(Tl(257000), h.Cash);
            Assert.AreEqual(Tl(7000), receipt.Profit);
            Assert.AreEqual(Tl(7000), h.Summaries.NetProfit(1));
            h.AssertInvariants(Tl(250000), 1);
        }

        [Test]
        public void T3_WastedAppraisal_IsAnExpense_WhenTheItemIsNotBought()
        {
            var h = new EconomyHarness();
            ProductInstance phone = h.NewMarketInstance();

            h.PayAppraisal(phone, 300, 1);
            h.WriteOff(phone, 1);

            Assert.AreEqual(Tl(249700), h.Cash);
            Assert.AreEqual(0, h.Inventory.Count);
            Assert.AreEqual(Tl(-300), h.Summaries.NetProfit(1));
            Assert.AreEqual(ProductLocation.Market, phone.Location);
            h.AssertInvariants(Tl(250000), 1);
        }

        [Test]
        public void T4_DailyExpense_DayTwoZero_DayThreeFiveHundred()
        {
            var h = new EconomyHarness();

            h.ChargeExpense(2);
            Assert.AreEqual(Tl(250000), h.Cash);
            Assert.AreEqual(Money.Zero, h.Summaries.NetProfit(2));

            h.ChargeExpense(3);
            Assert.AreEqual(Tl(249500), h.Cash);
            Assert.AreEqual(Tl(-500), h.Summaries.NetProfit(3));
            h.AssertInvariants(Tl(250000), 3);
        }

        [Test]
        public void T5_ShelfUpgrade_IsAnInvestment_NotAnExpense()
        {
            var h = new EconomyHarness();
            Money wealthBefore = h.TotalWealth();

            Result result = h.Shop.UpgradeCapacity(8, Tl(15000), 5);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(Tl(235000), h.Cash);
            Assert.AreEqual(8, h.Inventory.Capacity);
            Assert.AreEqual(wealthBefore, h.TotalWealth(), "Yatırım serveti değiştirmez");
            Assert.AreEqual(Money.Zero, h.Summaries.NetProfit(5), "Yatırım net kârı etkilemez");
            Assert.AreEqual(Tl(15000), h.State.BusinessAssets);
            h.AssertInvariants(Tl(250000), 5);
        }

        // ---------- alış ----------

        [Test]
        public void Acquire_SetsTheInstanceState()
        {
            var h = new EconomyHarness();
            ProductInstance phone = h.NewMarketInstance("phone.elma_e13_pro", sellerNpcId: "npc.kemal");

            Assert.IsTrue(h.Shop.Acquire(phone.InstanceId, Tl(27000), 4).IsSuccess);

            Assert.AreEqual(ProductLocation.Inventory, phone.Location);
            Assert.AreEqual(Tl(27000), phone.PurchasePrice);
            Assert.AreEqual(Tl(27000), phone.CostBasis);
            Assert.AreEqual(4, phone.AcquiredDay);
            CollectionAssert.AreEqual(new[] { phone.InstanceId }, h.Inventory.ItemIds.ToArray());

            TransactionRecord record = h.State.Ledger.Records.Last();
            Assert.AreEqual("purchase", record.TypeId);
            Assert.AreEqual("npc.kemal", record.NpcId, "Satıcı örnekten alınır");
            Assert.AreEqual("phone.elma_e13_pro", record.DefinitionId);
        }

        [Test]
        public void Acquire_UnknownInstance_Fails()
        {
            var h = new EconomyHarness();

            Assert.AreEqual("instance.unknown", h.Shop.Acquire(999, Tl(1000), 1).ErrorCode);
            Assert.AreEqual(1, h.State.Ledger.Count);
        }

        [Test]
        public void Acquire_InstanceNotOnTheMarket_Fails_SoAnItemIsNeverBoughtTwice()
        {
            var h = new EconomyHarness();
            ProductInstance phone = h.NewMarketInstance();
            h.Buy(phone, 5000, 1);
            Money cash = h.Cash;

            Assert.AreEqual("instance.not_on_market", h.Shop.Acquire(phone.InstanceId, Tl(5000), 1).ErrorCode);
            Assert.AreEqual(cash, h.Cash);
            Assert.AreEqual(1, h.Inventory.Count);

            h.Sell(phone, 6000, 1);
            Assert.AreEqual("instance.not_on_market", h.Shop.Acquire(phone.InstanceId, Tl(5000), 1).ErrorCode, "Satılan ürün geri alınamaz");
        }

        [TestCase(0L)]
        [TestCase(-1000L)]
        [TestCase(1005L)]
        public void Acquire_InvalidPrice_Fails(long price)
        {
            var h = new EconomyHarness();
            ProductInstance phone = h.NewMarketInstance();

            Assert.AreEqual("price.invalid", h.Shop.Acquire(phone.InstanceId, Tl(price), 1).ErrorCode);
            Assert.AreEqual(ProductLocation.Market, phone.Location);
            Assert.AreEqual(1, h.State.Ledger.Count);
        }

        [Test]
        public void Acquire_InsufficientCash_IsAtomic()
        {
            var h = new EconomyHarness();
            ProductInstance phone = h.NewMarketInstance();
            h.PayAppraisal(phone, 300, 1);

            Result result = h.Shop.Acquire(phone.InstanceId, Tl(250000), 1);

            Assert.AreEqual("cash.insufficient", result.ErrorCode);
            Assert.AreEqual(ProductLocation.Market, phone.Location);
            Assert.AreEqual(Money.Zero, phone.CostBasis);
            Assert.IsNull(phone.AcquiredDay);
            Assert.AreEqual(0, h.Inventory.Count);
            Assert.AreEqual(Tl(300), h.Economy.PendingAppraisalCost(phone.InstanceId), "Bekleyen ekspertiz korunmalı");
        }

        [Test]
        public void Acquire_FullShelf_Fails_AndRespectsCapacity_I3()
        {
            var h = new EconomyHarness();
            for (int i = 0; i < 6; i++)
            {
                h.Buy(h.NewMarketInstance(), 1000, 1);
            }

            Assert.AreEqual(6, h.Inventory.Count);
            ProductInstance extra = h.NewMarketInstance();
            Money cash = h.Cash;

            Assert.AreEqual("inventory.full", h.Shop.Acquire(extra.InstanceId, Tl(1000), 1).ErrorCode);
            Assert.AreEqual(6, h.Inventory.Count);
            Assert.AreEqual(cash, h.Cash);
            Assert.AreEqual(ProductLocation.Market, extra.Location);
            h.AssertInvariants(Tl(250000), 1);
        }

        [Test]
        public void ShelfFreesUpAfterASale()
        {
            var h = new EconomyHarness();
            var items = new List<ProductInstance>();
            for (int i = 0; i < 6; i++)
            {
                ProductInstance p = h.NewMarketInstance();
                h.Buy(p, 1000, 1);
                items.Add(p);
            }

            h.Sell(items[2], 1500, 1);
            ProductInstance next = h.NewMarketInstance();

            Assert.IsTrue(h.Shop.Acquire(next.InstanceId, Tl(1000), 1).IsSuccess);
            Assert.AreEqual(6, h.Inventory.Count);
        }

        [Test]
        public void Acquire_CapitalizesOnlyThatInstancesAppraisals()
        {
            var h = new EconomyHarness();
            ProductInstance a = h.NewMarketInstance();
            ProductInstance b = h.NewMarketInstance();
            h.PayAppraisal(a, 300, 1);
            h.PayAppraisal(b, 1000, 1);

            h.Buy(a, 5000, 1);

            Assert.AreEqual(Tl(5300), a.CostBasis);
            Assert.AreEqual(Tl(1000), h.Economy.PendingAppraisalCost(b.InstanceId));
        }

        // ---------- satış ----------

        [Test]
        public void Sell_MovesTheInstanceToSold_AndRecordsCostBasisSnapshot()
        {
            var h = new EconomyHarness();
            ProductInstance phone = h.NewMarketInstance("phone.nova_n3_pro");
            h.PayAppraisal(phone, 200, 3);
            h.Buy(phone, 9100, 3);

            Result<SaleReceipt> result = h.Shop.Sell(phone.InstanceId, Tl(10600), 4, "npc.selin");

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(Tl(10600), result.Value.SalePrice);
            Assert.AreEqual(Tl(9300), result.Value.CostBasis);
            Assert.AreEqual(Tl(1300), result.Value.Profit);
            Assert.AreEqual(ProductLocation.Sold, phone.Location);
            Assert.AreEqual(0, h.Inventory.Count);

            TransactionRecord record = h.State.Ledger.Records.Last();
            Assert.AreEqual(record.Id, result.Value.RecordId);
            Assert.AreEqual("sale", record.TypeId);
            Assert.AreEqual(Tl(9300), record.SaleCostBasis);
            Assert.AreEqual("npc.selin", record.NpcId);
            Assert.AreEqual("phone.nova_n3_pro", record.DefinitionId);
        }

        [Test]
        public void Sell_BelowCost_IsALoss()
        {
            var h = new EconomyHarness();
            ProductInstance phone = h.NewMarketInstance();
            h.Buy(phone, 27000, 1);

            SaleReceipt receipt = h.Sell(phone, 22000, 1);

            Assert.AreEqual(Tl(-5000), receipt.Profit);
            Assert.IsTrue(receipt.Profit.IsNegative);
            Assert.AreEqual(Tl(-5000), h.Summaries.NetProfit(1));
        }

        [Test]
        public void Sell_NotInInventory_Fails()
        {
            var h = new EconomyHarness();
            ProductInstance phone = h.NewMarketInstance();

            Assert.AreEqual("instance.not_in_inventory", h.Shop.Sell(phone.InstanceId, Tl(1000), 1).ErrorCode, "Pazardaki ürün satılamaz");
            Assert.AreEqual("instance.unknown", h.Shop.Sell(999, Tl(1000), 1).ErrorCode);
            Assert.AreEqual(1, h.State.Ledger.Count);
        }

        [Test]
        public void Sell_TwiceIsImpossible()
        {
            var h = new EconomyHarness();
            ProductInstance phone = h.NewMarketInstance();
            h.Buy(phone, 5000, 1);
            h.Sell(phone, 6000, 1);
            Money cash = h.Cash;

            Assert.AreEqual("instance.not_in_inventory", h.Shop.Sell(phone.InstanceId, Tl(6000), 1).ErrorCode);
            Assert.AreEqual(cash, h.Cash);
        }

        [TestCase(0L)]
        [TestCase(-1000L)]
        [TestCase(1005L)]
        public void Sell_InvalidPrice_Fails(long price)
        {
            var h = new EconomyHarness();
            ProductInstance phone = h.NewMarketInstance();
            h.Buy(phone, 5000, 1);

            Assert.AreEqual("price.invalid", h.Shop.Sell(phone.InstanceId, Tl(price), 1).ErrorCode);
            Assert.AreEqual(ProductLocation.Inventory, phone.Location);
            Assert.AreEqual(1, h.Inventory.Count);
        }

        // ---------- tamir maliyeti (yalnızca muhasebe) ----------

        [Test]
        public void AddRepairCost_IncreasesCostBasis_AndReducesCash()
        {
            var h = new EconomyHarness();
            ProductInstance phone = h.NewMarketInstance("phone.nova_n3_pro");
            h.PayAppraisal(phone, 200, 7);
            h.Buy(phone, 4800, 7);

            Result result = h.Shop.AddRepairCost(phone.InstanceId, Tl(1800), 7);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(Tl(6800), phone.CostBasis, "4.800 alış + 200 ekspertiz + 1.800 tamir");
            Assert.AreEqual(Tl(250000 - 200 - 4800 - 1800), h.Cash);
            Assert.AreEqual(Tl(4800), phone.PurchasePrice);
            Assert.AreEqual("repair", h.State.Ledger.Records.Last().TypeId);
            Assert.AreEqual(Money.Zero, h.Summaries.NetProfit(7), "Tamir maliyeti ürüne eklenir, gider değildir");
        }

        [Test]
        public void AddRepairCost_ItemNotInInventory_Fails()
        {
            var h = new EconomyHarness();
            ProductInstance phone = h.NewMarketInstance();

            Assert.AreEqual("instance.not_in_inventory", h.Shop.AddRepairCost(phone.InstanceId, Tl(1000), 1).ErrorCode);
            Assert.AreEqual("instance.unknown", h.Shop.AddRepairCost(999, Tl(1000), 1).ErrorCode);
        }

        [TestCase(0L)]
        [TestCase(-1L)]
        [TestCase(1005L)]
        public void AddRepairCost_InvalidCost_Fails(long cost)
        {
            var h = new EconomyHarness();
            ProductInstance phone = h.NewMarketInstance();
            h.Buy(phone, 5000, 1);

            Assert.AreEqual("price.invalid", h.Shop.AddRepairCost(phone.InstanceId, Tl(cost), 1).ErrorCode);
            Assert.AreEqual(Tl(5000), phone.CostBasis);
        }

        [Test]
        public void AddRepairCost_InsufficientCash_IsAtomic()
        {
            var h = new EconomyHarness();
            ProductInstance phone = h.NewMarketInstance();
            h.Buy(phone, 5000, 1);
            Assert.IsTrue(h.Economy.RecordInvestment(h.Cash - Tl(100), 1).IsSuccess);

            Assert.AreEqual("cash.insufficient", h.Shop.AddRepairCost(phone.InstanceId, Tl(1000), 1).ErrorCode);
            Assert.AreEqual(Tl(5000), phone.CostBasis);
        }

        // ---------- kapasite yükseltme ----------

        [Test]
        public void UpgradeCapacity_MustBeLargerThanCurrent()
        {
            var h = new EconomyHarness();

            Assert.AreEqual("capacity.not_larger", h.Shop.UpgradeCapacity(6, Tl(1000), 5).ErrorCode);
            Assert.AreEqual("capacity.not_larger", h.Shop.UpgradeCapacity(3, Tl(1000), 5).ErrorCode);
            Assert.AreEqual(6, h.Inventory.Capacity);
            Assert.AreEqual(1, h.State.Ledger.Count);
        }

        [TestCase(0L)]
        [TestCase(-15000L)]
        [TestCase(15005L)]
        public void UpgradeCapacity_InvalidCost_Fails(long cost)
        {
            var h = new EconomyHarness();

            Assert.AreEqual("price.invalid", h.Shop.UpgradeCapacity(8, Tl(cost), 5).ErrorCode);
            Assert.AreEqual(6, h.Inventory.Capacity);
        }

        [Test]
        public void UpgradeCapacity_InsufficientCash_IsAtomic()
        {
            var h = new EconomyHarness();

            Assert.AreEqual("cash.insufficient", h.Shop.UpgradeCapacity(8, Tl(250010), 5).ErrorCode);
            Assert.AreEqual(6, h.Inventory.Capacity);
            Assert.AreEqual(Money.Zero, h.State.BusinessAssets);
        }

        [Test]
        public void UpgradedCapacity_AllowsMoreItems()
        {
            var h = new EconomyHarness();
            for (int i = 0; i < 6; i++)
            {
                h.Buy(h.NewMarketInstance(), 1000, 1);
            }

            Assert.IsTrue(h.Shop.UpgradeCapacity(8, Tl(15000), 5).IsSuccess);

            h.Buy(h.NewMarketInstance(), 1000, 5);
            h.Buy(h.NewMarketInstance(), 1000, 5);
            Assert.AreEqual(8, h.Inventory.Count);
            Assert.AreEqual("inventory.full", h.Shop.Acquire(h.NewMarketInstance().InstanceId, Tl(1000), 5).ErrorCode);
            h.AssertInvariants(Tl(250000), 5);
        }

        // ---------- olaylar ----------

        [Test]
        public void Acquire_PublishesItemAddedToShelf_OnlyOnSuccess()
        {
            var h = new EconomyHarness();
            var added = new List<ItemAddedToShelf>();
            h.Bus.Subscribe<ItemAddedToShelf>(e => added.Add(e));
            ProductInstance phone = h.NewMarketInstance();

            h.Shop.Acquire(phone.InstanceId, Tl(0), 1);
            h.Shop.Acquire(999, Tl(1000), 1);
            Assert.AreEqual(0, added.Count);

            h.Shop.Acquire(phone.InstanceId, Tl(5000), 1);

            Assert.AreEqual(1, added.Count);
            Assert.AreEqual(phone.InstanceId, added[0].InstanceId);
        }

        // ---------- stok satırları / raf durumu ----------

        [Test]
        public void GetStockLines_ListsEveryShelfItemWithItsCostBasis()
        {
            var h = new EconomyHarness();
            ProductInstance a = h.NewMarketInstance("phone.nova_n3_pro");
            ProductInstance b = h.NewMarketInstance("phone.yildiz_y8_plus");
            h.Buy(a, 9100, 3);
            h.PayAppraisal(b, 200, 4);
            h.Buy(b, 13600, 4);

            IReadOnlyList<StockLine> lines = h.Shop.GetStockLines();

            Assert.AreEqual(2, lines.Count);
            Assert.AreEqual(a.InstanceId, lines[0].InstanceId);
            Assert.AreEqual("phone.nova_n3_pro", lines[0].DefinitionId);
            Assert.AreEqual(Tl(9100), lines[0].CostBasis);
            Assert.AreEqual(Tl(13800), lines[1].CostBasis);
            Assert.IsFalse(lines is List<StockLine>);
        }

        [Test]
        public void InventoryState_ItemIds_AreReadOnly_AndCapacityIsValidated()
        {
            var state = new InventoryState(6);

            Assert.IsFalse(state.ItemIds is List<long>);
            Assert.IsFalse(state.ItemIds is long[]);
            Assert.Throws<ArgumentOutOfRangeException>(() => new InventoryState(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new InventoryState(-2));
        }

        [Test]
        public void Constructor_RejectsNullArguments()
        {
            var h = new EconomyHarness();

            Assert.Throws<ArgumentNullException>(() => new InventoryService(null, h.Store, h.Economy, null));
            Assert.Throws<ArgumentNullException>(() => new InventoryService(h.Inventory, null, h.Economy, null));
            Assert.Throws<ArgumentNullException>(() => new InventoryService(h.Inventory, h.Store, null, null));
        }

        // ---------- ürün örneği + değer sistemi ile bağ ----------

        [Test]
        public void GddExampleInstance_OverpaidWithoutAppraisal_LinksLedgerAndValueSystem()
        {
            // GDD v0.3 4.4: ekranı değişmiş E13 Pro, oyuncu ekspertizsiz 27.000 ödemiş; gerçek değer 22.170 (22.310 değil: kasa 82).
            var h = new EconomyHarness();
            ProductDefinition e13 = ContentFixtures.E13Pro();
            ProductInstance phone = ContentFixtures.Instance(e13, 18, 78, "replaced_aftermarket", 82, "ok");
            phone.InstanceId = 1042;
            phone.SellerNpcId = "npc.kemal";
            phone.Location = ProductLocation.Market;
            h.Store.Add(phone);
            var calculator = new ValueCalculator(ContentFixtures.Tables());

            Assert.AreEqual(Tl(22170), calculator.TrueValue(phone, e13));
            Assert.IsTrue(h.Shop.Acquire(phone.InstanceId, Tl(27000), 4).IsSuccess);

            Assert.AreEqual("phone.elma_e13_pro", phone.DefinitionId, "Örnek tanımı yalnızca ID ile gösterir");
            Assert.AreEqual(Tl(27000), phone.CostBasis);
            Assert.AreEqual(Tl(4830), phone.CostBasis - calculator.TrueValue(phone, e13), "Fazla ödeme = maliyet - gerçek değer");
            Assert.AreEqual(Tl(223000), h.Cash);

            SaleReceipt receipt = h.Sell(phone, 30000, 6, "npc.berk");
            Assert.AreEqual(Tl(3000), receipt.Profit, "Kâr, gerçek değere göre değil maliyete göre hesaplanır");
            Assert.AreEqual(Tl(22170), calculator.TrueValue(phone, e13), "Değer hesabı ledger'dan etkilenmez");
            h.AssertInvariants(Tl(250000), 6);
        }

        [Test]
        public void GeneratedInstances_WorkWithTheShop()
        {
            ContentLoadResult content = ContentDatabase.Load(new DirectoryContentSource(TestPaths.ContentDataDirectory()));
            Assert.IsTrue(content.IsSuccess, content.FormatIssues());
            var generator = new InstanceGenerator(content.Database.ConditionProfiles, new IdGenerator());
            var calculator = new ValueCalculator(content.Database.ValueTables);
            var rng = new PcgRandom(77UL, 1UL);
            var h = new EconomyHarness();
            ProductDefinition def = content.Database.GetProduct("phone.elma_e13_pro");

            ProductInstance phone = generator.Generate(def, 1, rng);
            h.Store.Add(phone);
            Money value = calculator.TrueValue(phone, def);
            Money price = Money.FromTlRoundedTo10(value.Tl * 9 / 10);

            Assert.IsTrue(h.Shop.Acquire(phone.InstanceId, price, 1).IsSuccess);
            Assert.AreEqual(price, phone.CostBasis);
            Assert.AreEqual(def.Id, phone.DefinitionId);
            Assert.IsTrue(calculator.TrueValue(phone, def) == value);
            h.AssertInvariants(Tl(250000), 1);
        }
    }

    public class InstanceStoreTests
    {
        [Test]
        public void Add_Get_TryGet_Count()
        {
            var store = new InstanceStore();
            var a = new ProductInstance { InstanceId = 5, DefinitionId = "phone.a" };

            store.Add(a);

            Assert.AreEqual(1, store.Count);
            Assert.AreSame(a, store.Get(5));
            ProductInstance found;
            Assert.IsTrue(store.TryGet(5, out found));
            Assert.AreSame(a, found);
            Assert.IsFalse(store.TryGet(6, out found));
            Assert.IsNull(found);
            Assert.Throws<KeyNotFoundException>(() => store.Get(6));
        }

        [Test]
        public void Add_DuplicateOrInvalidId_Throws()
        {
            var store = new InstanceStore();
            store.Add(new ProductInstance { InstanceId = 5 });

            Assert.Throws<ArgumentException>(() => store.Add(new ProductInstance { InstanceId = 5 }));
            Assert.Throws<ArgumentException>(() => store.Add(new ProductInstance { InstanceId = 0 }));
            Assert.Throws<ArgumentException>(() => store.Add(new ProductInstance { InstanceId = -3 }));
            Assert.Throws<ArgumentNullException>(() => store.Add(null));
            Assert.AreEqual(1, store.Count);
        }

        [Test]
        public void All_IsInInsertionOrder_AndReadOnly()
        {
            var store = new InstanceStore();
            store.Add(new ProductInstance { InstanceId = 3 });
            store.Add(new ProductInstance { InstanceId = 1 });
            store.Add(new ProductInstance { InstanceId = 2 });

            Assert.AreEqual(new[] { 3L, 1L, 2L }, store.All.Select(i => i.InstanceId).ToArray());
            Assert.IsFalse(store.All is List<ProductInstance>);
            Assert.IsFalse(store.All is ProductInstance[]);
        }
    }
}
