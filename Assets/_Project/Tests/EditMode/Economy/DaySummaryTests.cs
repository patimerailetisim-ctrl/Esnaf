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
    public class DaySummaryTests
    {
        private static Money Tl(long value)
        {
            return Money.FromTl(value);
        }

        // ---------- Gün 1: "Para Özeti" (GDD v0.3 2.1 + hafta tablosu Gün 1) ----------

        [Test]
        public void DayOne_MoneySummary_MatchesTheWeekTable()
        {
            var h = new EconomyHarness();
            ProductInstance y5 = h.NewMarketInstance("phone.yildiz_y5");
            h.Buy(y5, 4800, 1);
            h.Sell(y5, 5750, 1);

            DaySummary s = h.CloseDay(1);

            Assert.AreEqual(1, s.Day);
            Assert.AreEqual(Tl(250000), s.OpeningCash, "Sabah nakit");
            Assert.AreEqual(Tl(5750), s.TotalIncome, "Toplam gelir (satış)");
            Assert.AreEqual(Tl(4800), s.TotalSpending, "Toplam gider (alış, ekspertiz, gider)");
            Assert.AreEqual(Tl(950), s.NetProfit, "Net kâr");
            Assert.AreEqual(Tl(250950), s.ClosingCash, "Akşam nakit");
            Assert.AreEqual(Tl(250950), s.Wealth.Total);
            Assert.AreEqual(Tl(950), s.WealthChange);
            Assert.AreEqual(0, s.StockCount);
            Assert.AreEqual(Money.Zero, s.StockCostBasis);
        }

        [Test]
        public void DayZero_IsTheOpeningCapital()
        {
            var h = new EconomyHarness();

            DaySummary s = h.CloseDay(0);

            Assert.AreEqual(Money.Zero, s.OpeningCash);
            Assert.AreEqual(Tl(250000), s.CapitalInflow);
            Assert.AreEqual(Tl(250000), s.ClosingCash);
            Assert.AreEqual(Money.Zero, s.NetProfit);
            Assert.AreEqual(Tl(250000), s.WealthChange, "Servet değişimi = net kâr + sermaye girişi");
        }

        [Test]
        public void DayWithoutRecords_HasEqualOpeningAndClosing_AndZeroProfit()
        {
            var h = new EconomyHarness();

            DaySummary s = h.CloseDay(2);

            Assert.AreEqual(Tl(250000), s.OpeningCash);
            Assert.AreEqual(Tl(250000), s.ClosingCash);
            Assert.AreEqual(Money.Zero, s.NetProfit);
            Assert.AreEqual(0, s.Records.Count);
            Assert.AreEqual(0, s.Sales.Count);
            Assert.AreEqual(Money.Zero, s.WealthChange);
        }

        // ---------- GDD v0.2 9.4: Gün 4 örneği ----------

        [Test]
        public void GddDayFourExample_ReproducesTheSummaryScreen()
        {
            // Sabah nakit 242.550, elde N3 Pro (maliyet 9.300): başlangıç sermayesi 251.850, Gün 3'te N3 Pro 9.100 + S1 200.
            var constants = new EconomyConstants(Tl(251850), 3, Tl(500), 6);
            var h = new EconomyHarness(constants);
            ProductInstance n3 = h.NewMarketInstance("phone.nova_n3_pro");
            h.PayAppraisal(n3, 200, 3);
            h.Buy(n3, 9100, 3);
            Assert.AreEqual(Tl(242550), h.Cash);

            ProductInstance y8 = h.NewMarketInstance("phone.yildiz_y8_plus");
            ProductInstance skipped = h.NewMarketInstance("phone.zirve_z5");
            h.PayAppraisal(y8, 200, 4);
            h.Buy(y8, 13600, 4);
            h.PayAppraisal(skipped, 200, 4);
            h.WriteOff(skipped, 4);
            h.Sell(n3, 10250, 4, "npc.selin");
            h.ChargeExpense(4);

            DaySummary s = h.CloseDay(4);

            Assert.AreEqual(Tl(242550), s.OpeningCash);
            Assert.AreEqual(Tl(238300), s.ClosingCash);
            Assert.AreEqual(Tl(10250), s.SalesIncome);
            Assert.AreEqual(Tl(13600), s.PurchaseSpend);
            Assert.AreEqual(Tl(400), s.AppraisalSpend);
            Assert.AreEqual(Tl(500), s.DailyExpense);
            Assert.AreEqual(Money.Zero, s.InvestmentSpend);
            Assert.AreEqual(Tl(950), s.GrossProfit);
            Assert.AreEqual(Tl(200), s.WastedAppraisal);
            Assert.AreEqual(Tl(250), s.NetProfit, "950 - 200 (boşa) - 500 (gider)");
            Assert.AreEqual(Tl(10250), s.TotalIncome);
            Assert.AreEqual(Tl(14500), s.TotalSpending);

            Assert.AreEqual(1, s.Sales.Count);
            SoldItemSummary sold = s.Sales[0];
            Assert.AreEqual("phone.nova_n3_pro", sold.DefinitionId);
            Assert.AreEqual(Tl(10250), sold.SalePrice);
            Assert.AreEqual(Tl(9300), sold.CostBasis);
            Assert.AreEqual(Tl(950), sold.Profit);
            Assert.AreEqual("npc.selin", sold.NpcId);

            Assert.AreEqual(1, s.StockCount);
            Assert.AreEqual(Tl(13800), s.StockCostBasis);
            Assert.AreEqual(y8.InstanceId, s.Stock[0].InstanceId);
            Assert.AreEqual(Tl(252100), s.Wealth.Total, "Toplam servet");
            Assert.AreEqual(Tl(250), s.WealthChange, "Servet farkı (+250)");
            Assert.AreEqual(6, s.Records.Count);
            Assert.AreEqual(
                new[] { "appraisal", "purchase", "appraisal", "wasted_appraisal", "sale", "daily_expense" },
                s.Records.Select(r => r.TypeId).ToArray());
        }

        [Test]
        public void Summary_ReconcilesCashExactly()
        {
            var h = new EconomyHarness();
            ProductInstance a = h.NewMarketInstance();
            ProductInstance b = h.NewMarketInstance();
            h.PayAppraisal(a, 600, 5);
            h.Buy(a, 16000, 5);
            h.Buy(b, 15390, 5);
            Assert.IsTrue(h.Shop.UpgradeCapacity(8, Tl(15000), 5).IsSuccess);
            h.Sell(b, 19920, 5);
            h.ChargeExpense(5);

            DaySummary s = h.CloseDay(5);

            long reconstructed = s.OpeningCash.Tl + s.TotalIncome.Tl - s.TotalSpending.Tl - s.InvestmentSpend.Tl + s.CapitalInflow.Tl;
            Assert.AreEqual(s.ClosingCash.Tl, reconstructed, "Sabah + gelir - gider - yatırım + sermaye = akşam");
            Assert.AreEqual(Tl(15000), s.InvestmentSpend);
            Assert.AreEqual(Tl(4530), s.GrossProfit);
            Assert.AreEqual(Tl(4030), s.NetProfit);
        }

        [Test]
        public void Repair_IsSpending_ButNotAnExpense_AndRaisesTheCostBasis()
        {
            var h = new EconomyHarness();
            ProductInstance phone = h.NewMarketInstance();
            h.Buy(phone, 10000, 4);
            Assert.IsTrue(h.Shop.AddRepairCost(phone.InstanceId, Tl(1800), 4).IsSuccess);
            h.Sell(phone, 14000, 4);

            DaySummary s = h.CloseDay(4);

            Assert.AreEqual(Tl(1800), s.RepairSpend);
            Assert.AreEqual(Tl(11800), s.TotalSpending, "Toplam gider = alış + onarım");
            Assert.AreEqual(Tl(2200), s.NetProfit, "14.000 - (10.000 + 1.800)");
            long reconstructed = s.OpeningCash.Tl + s.TotalIncome.Tl - s.TotalSpending.Tl;
            Assert.AreEqual(s.ClosingCash.Tl, reconstructed);
        }

        [Test]
        public void Summary_ListsEverySaleOfTheDay_InOrder()
        {
            var h = new EconomyHarness();
            ProductInstance a = h.NewMarketInstance("phone.a");
            ProductInstance b = h.NewMarketInstance("phone.b");
            h.Buy(a, 3700, 2);
            h.Buy(b, 5100, 2);
            h.Sell(a, 4400, 2);
            h.Sell(b, 5900, 2);

            DaySummary s = h.CloseDay(2);

            Assert.AreEqual(new[] { "phone.a", "phone.b" }, s.Sales.Select(x => x.DefinitionId).ToArray());
            Assert.AreEqual(new[] { 700L, 800L }, s.Sales.Select(x => x.Profit.Tl).ToArray());
            Assert.AreEqual(Tl(1500), s.GrossProfit);
            Assert.IsFalse(s.Sales is List<SoldItemSummary>);
            Assert.IsFalse(s.Records is List<TransactionRecord>);
        }

        [Test]
        public void LossMakingDay_IsShownAsNegativeProfit()
        {
            var h = new EconomyHarness();
            ProductInstance phone = h.NewMarketInstance();
            h.Buy(phone, 27000, 3);
            h.Sell(phone, 22000, 3);
            h.ChargeExpense(3);

            DaySummary s = h.CloseDay(3);

            Assert.AreEqual(Tl(-5000), s.GrossProfit);
            Assert.AreEqual(Tl(-5500), s.NetProfit);
            Assert.AreEqual(Tl(-5500), s.WealthChange);
        }

        [Test]
        public void WriteOff_IsRecognizedOnTheDayItIsWrittenOff_NotWhenTheFeeWasPaid()
        {
            var h = new EconomyHarness();
            ProductInstance phone = h.NewMarketInstance();
            h.PayAppraisal(phone, 300, 3);
            h.WriteOff(phone, 5);

            Assert.AreEqual(Money.Zero, h.Summaries.NetProfit(3), "Ödeme günü: henüz kayıp değil");
            Assert.AreEqual(Tl(-300), h.Summaries.NetProfit(5), "Yazma günü: gider");
            Assert.AreEqual(Tl(300), h.CloseDay(3).AppraisalSpend, "Nakit çıkışı ödeme gününde görünür");
            Assert.AreEqual(Tl(300), h.CloseDay(5).WastedAppraisal);
        }

        [Test]
        public void Build_RejectsBadArguments()
        {
            var h = new EconomyHarness();

            Assert.Throws<ArgumentOutOfRangeException>(() => h.Summaries.Build(-1, h.Shop.GetStockLines(), h.Wealth.Calculate()));
            Assert.Throws<ArgumentNullException>(() => h.Summaries.Build(1, null, h.Wealth.Calculate()));
            Assert.Throws<ArgumentNullException>(() => h.Summaries.Build(1, h.Shop.GetStockLines(), null));
            Assert.Throws<ArgumentNullException>(() => new DaySummaryBuilder(null, h.Types));
            Assert.Throws<ArgumentNullException>(() => new DaySummaryBuilder(h.State, null));
        }

        // ---------- Gün 3: tam defter görünümü için alan verisi ----------

        private static EconomyHarness ThreeDayBooks()
        {
            var h = new EconomyHarness();
            ProductInstance y5 = h.NewMarketInstance("phone.yildiz_y5");
            h.Buy(y5, 4800, 1);
            h.Sell(y5, 5750, 1);
            ProductInstance n1 = h.NewMarketInstance("phone.nova_n1_lite");
            h.Buy(n1, 3700, 2);
            h.Sell(n1, 4400, 2);
            ProductInstance a3 = h.NewMarketInstance("phone.samsun_vega_a3");
            h.PayAppraisal(a3, 200, 3);
            h.Buy(a3, 7900, 3);
            h.ChargeExpense(3);
            return h;
        }

        [Test]
        public void LedgerView_GroupsRowsByDay_OldestFirst()
        {
            EconomyHarness h = ThreeDayBooks();

            IReadOnlyList<LedgerDayGroup> groups = h.LedgerView.GroupByDay(false);

            Assert.AreEqual(new[] { 0, 1, 2, 3 }, groups.Select(g => g.Day).ToArray());
            Assert.AreEqual(1, groups[0].Records.Count, "Gün 0: başlangıç sermayesi");
            Assert.AreEqual(2, groups[1].Records.Count);
            Assert.AreEqual(2, groups[2].Records.Count);
            Assert.AreEqual(3, groups[3].Records.Count);
            Assert.IsFalse(groups is List<LedgerDayGroup>);
        }

        [Test]
        public void LedgerView_GroupTotals_AreConsistent()
        {
            EconomyHarness h = ThreeDayBooks();

            IReadOnlyList<LedgerDayGroup> groups = h.LedgerView.GroupByDay(false);

            LedgerDayGroup day1 = groups.Single(g => g.Day == 1);
            Assert.AreEqual(Tl(250000), day1.OpeningBalance);
            Assert.AreEqual(Tl(250950), day1.ClosingBalance);
            Assert.AreEqual(Tl(5750), day1.TotalIn);
            Assert.AreEqual(Tl(4800), day1.TotalOut);
            Assert.AreEqual(Tl(950), day1.NetProfit);

            LedgerDayGroup day3 = groups.Single(g => g.Day == 3);
            Assert.AreEqual(Tl(-500), day3.NetProfit, "Gün 3: yalnızca gider (ekspertiz ürüne eklendi)");
            Assert.AreEqual(
                groups.Last().ClosingBalance.Tl,
                groups.Sum(g => g.TotalIn.Tl - g.TotalOut.Tl),
                "Tüm girişler - tüm çıkışlar = son bakiye");

            for (int i = 1; i < groups.Count; i++)
            {
                Assert.AreEqual(groups[i - 1].ClosingBalance, groups[i].OpeningBalance, "Gün zinciri kopmamalı");
            }
        }

        [Test]
        public void LedgerView_NewestFirst_ReversesTheGroups()
        {
            EconomyHarness h = ThreeDayBooks();

            IReadOnlyList<LedgerDayGroup> newest = h.LedgerView.GroupByDay(true);

            Assert.AreEqual(new[] { 3, 2, 1, 0 }, newest.Select(g => g.Day).ToArray());
        }

        [Test]
        public void LedgerView_EmptyBooks_HaveNoGroups()
        {
            var h = new EconomyHarness(openBooks: false);

            Assert.AreEqual(0, h.LedgerView.GroupByDay(false).Count);
        }

        // ---------- "neden bu kadar?" açıklaması ----------

        [Test]
        public void Explain_Sale_ShowsCostBasisAndProfit()
        {
            var h = new EconomyHarness();
            ProductInstance phone = h.NewMarketInstance();
            h.PayAppraisal(phone, 1000, 1);
            h.Buy(phone, 27000, 1);
            SaleReceipt receipt = h.Sell(phone, 35000, 1);

            Result<TransactionExplanation> result = h.LedgerView.Explain(receipt.RecordId);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("sale", result.Value.Record.TypeId);
            Assert.AreEqual(Tl(28000), result.Value.CostBasis);
            Assert.AreEqual(Tl(7000), result.Value.Profit);
            Assert.IsNull(result.Value.AppraisalStatus);
        }

        [Test]
        public void Explain_Appraisal_ShowsItsFate()
        {
            var h = new EconomyHarness();
            ProductInstance pending = h.NewMarketInstance();
            ProductInstance bought = h.NewMarketInstance();
            ProductInstance wasted = h.NewMarketInstance();
            TransactionRecord pendingRow = h.Economy.PayAppraisal(pending.InstanceId, pending.DefinitionId, Tl(300), 1).Value;
            TransactionRecord boughtRow = h.Economy.PayAppraisal(bought.InstanceId, bought.DefinitionId, Tl(300), 1).Value;
            TransactionRecord wastedRow = h.Economy.PayAppraisal(wasted.InstanceId, wasted.DefinitionId, Tl(300), 1).Value;
            h.Buy(bought, 5000, 1);
            h.WriteOff(wasted, 1);

            Assert.AreEqual(AppraisalStatus.Pending, h.LedgerView.Explain(pendingRow.Id).Value.AppraisalStatus);
            Assert.AreEqual(AppraisalStatus.Capitalized, h.LedgerView.Explain(boughtRow.Id).Value.AppraisalStatus);
            Assert.AreEqual(AppraisalStatus.WrittenOff, h.LedgerView.Explain(wastedRow.Id).Value.AppraisalStatus);
        }

        [Test]
        public void Explain_WriteOff_PointsAtTheOriginalAppraisal()
        {
            var h = new EconomyHarness();
            ProductInstance phone = h.NewMarketInstance();
            TransactionRecord appraisal = h.Economy.PayAppraisal(phone.InstanceId, phone.DefinitionId, Tl(300), 1).Value;
            h.WriteOff(phone, 1);
            TransactionRecord writeOff = h.State.Ledger.Records.Last();

            TransactionExplanation e = h.LedgerView.Explain(writeOff.Id).Value;

            Assert.AreEqual(appraisal.Id, e.RelatedRecord.Id);
            Assert.AreEqual(Tl(300), e.ExpenseAmount);
        }

        [Test]
        public void Explain_UnknownRecord_Fails()
        {
            var h = new EconomyHarness();

            Assert.AreEqual("ledger.unknown_record", h.LedgerView.Explain(999).ErrorCode);
        }
    }
}
