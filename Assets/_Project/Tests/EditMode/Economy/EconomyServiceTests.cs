using System;
using System.Collections.Generic;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Economy;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Economy
{
    public class EconomyServiceTests
    {
        private static Money Tl(long value)
        {
            return Money.FromTl(value);
        }

        private static EconomyHarness Books()
        {
            return new EconomyHarness();
        }

        // ---------- açılış ----------

        [Test]
        public void OpenBooks_RecordsTheOpeningCapital_OnDayZero()
        {
            var h = new EconomyHarness(openBooks: false);
            Assert.AreEqual(Money.Zero, h.Cash);

            Result result = h.Economy.OpenBooks();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(Tl(250000), h.Cash);
            Assert.AreEqual(1, h.State.Ledger.Count);
            TransactionRecord r = h.State.Ledger.Records[0];
            Assert.AreEqual("opening_capital", r.TypeId);
            Assert.AreEqual(0, r.Day);
            Assert.AreEqual(Tl(250000), r.Amount);
            Assert.AreEqual(TransactionCategory.Capital, r.Category);
        }

        [Test]
        public void OpenBooks_CannotBeCalledTwice()
        {
            EconomyHarness h = Books();

            Result result = h.Economy.OpenBooks();

            Assert.AreEqual("books.already_open", result.ErrorCode);
            Assert.AreEqual(1, h.State.Ledger.Count);
            Assert.AreEqual(Tl(250000), h.Cash);
        }

        [Test]
        public void Operations_BeforeBooksAreOpened_ArePossibleOnlyWithCash()
        {
            var h = new EconomyHarness(openBooks: false);

            Result<TransactionRecord> result = h.Economy.RecordPurchase(1, "phone.x", null, Tl(1000), 1);

            Assert.AreEqual("cash.insufficient", result.ErrorCode);
        }

        // ---------- alış ----------

        [Test]
        public void RecordPurchase_ReducesCash_AndTagsTheRecord()
        {
            EconomyHarness h = Books();

            Result<TransactionRecord> result = h.Economy.RecordPurchase(7, "phone.elma_e13_pro", "npc.kemal", Tl(27000), 4);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(Tl(223000), h.Cash);
            TransactionRecord r = result.Value;
            Assert.AreEqual("purchase", r.TypeId);
            Assert.AreEqual(Tl(-27000), r.Amount);
            Assert.AreEqual(7L, r.InstanceId);
            Assert.AreEqual("phone.elma_e13_pro", r.DefinitionId);
            Assert.AreEqual("npc.kemal", r.NpcId);
            Assert.AreEqual(4, r.Day);
            Assert.AreEqual(Tl(223000), r.BalanceAfter);
        }

        [TestCase(0L)]
        [TestCase(-1000L)]
        [TestCase(1005L)]
        public void RecordPurchase_InvalidAmount_FailsWithoutChange(long price)
        {
            EconomyHarness h = Books();

            Result<TransactionRecord> result = h.Economy.RecordPurchase(1, "phone.x", null, Tl(price), 1);

            Assert.AreEqual("amount.invalid", result.ErrorCode);
            Assert.AreEqual(1, h.State.Ledger.Count);
            Assert.AreEqual(Tl(250000), h.Cash);
        }

        [Test]
        public void RecordPurchase_InsufficientCash_FailsWithoutChange()
        {
            EconomyHarness h = Books();

            Result<TransactionRecord> result = h.Economy.RecordPurchase(1, "phone.x", null, Tl(250010), 1);

            Assert.AreEqual("cash.insufficient", result.ErrorCode);
            Assert.AreEqual(1, h.State.Ledger.Count);
            Assert.AreEqual(Tl(250000), h.Cash);
        }

        [Test]
        public void RecordPurchase_ExactCash_LeavesZero()
        {
            EconomyHarness h = Books();

            Assert.IsTrue(h.Economy.RecordPurchase(1, "phone.x", null, Tl(250000), 1).IsSuccess);
            Assert.AreEqual(Money.Zero, h.Cash);
            Assert.IsFalse(h.Cash.IsNegative);
        }

        // ---------- satış ----------

        [Test]
        public void RecordSale_IncreasesCash_AndStoresTheCostBasisSnapshot()
        {
            EconomyHarness h = Books();

            Result<TransactionRecord> result = h.Economy.RecordSale(7, "phone.x", "npc.berk", Tl(35000), Tl(27000), 6);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(Tl(285000), h.Cash);
            Assert.AreEqual("sale", result.Value.TypeId);
            Assert.AreEqual(Tl(35000), result.Value.Amount);
            Assert.AreEqual(Tl(27000), result.Value.SaleCostBasis);
            Assert.AreEqual("npc.berk", result.Value.NpcId);
        }

        [TestCase(0L)]
        [TestCase(-5000L)]
        [TestCase(5005L)]
        public void RecordSale_InvalidPrice_Fails(long price)
        {
            EconomyHarness h = Books();

            Assert.AreEqual("amount.invalid", h.Economy.RecordSale(1, "phone.x", null, Tl(price), Tl(1000), 1).ErrorCode);
            Assert.AreEqual(1, h.State.Ledger.Count);
        }

        [TestCase(-10L)]
        [TestCase(1005L)]
        public void RecordSale_InvalidCostBasis_Fails(long costBasis)
        {
            EconomyHarness h = Books();

            Assert.AreEqual("amount.invalid", h.Economy.RecordSale(1, "phone.x", null, Tl(5000), Tl(costBasis), 1).ErrorCode);
            Assert.AreEqual(1, h.State.Ledger.Count);
        }

        // ---------- tamir, yatırım ----------

        [Test]
        public void RecordRepair_ReducesCash_AsARepairRow()
        {
            EconomyHarness h = Books();

            Result<TransactionRecord> result = h.Economy.RecordRepair(9, "phone.nova_n3_pro", Tl(1800), 7);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(Tl(248200), h.Cash);
            Assert.AreEqual("repair", result.Value.TypeId);
            Assert.AreEqual(9L, result.Value.InstanceId);
        }

        [Test]
        public void RecordInvestment_ReducesCash_AndBuildsBusinessAssets()
        {
            EconomyHarness h = Books();

            Assert.IsTrue(h.Economy.RecordInvestment(Tl(15000), 5, "ledger.memo.shelf_upgrade").IsSuccess);
            Assert.IsTrue(h.Economy.RecordInvestment(Tl(12000), 6).IsSuccess);

            Assert.AreEqual(Tl(223000), h.Cash);
            Assert.AreEqual(Tl(27000), h.State.BusinessAssets);
            TransactionRecord first = h.State.Ledger.Records[1];
            Assert.AreEqual("investment", first.TypeId);
            Assert.AreEqual(TransactionCategory.Investment, first.Category);
            Assert.AreEqual("ledger.memo.shelf_upgrade", first.MemoKey);
        }

        [Test]
        public void RecordInvestment_InsufficientCash_FailsWithoutChange()
        {
            EconomyHarness h = Books();

            Result<TransactionRecord> result = h.Economy.RecordInvestment(Tl(250010), 5);

            Assert.AreEqual("cash.insufficient", result.ErrorCode);
            Assert.AreEqual(Money.Zero, h.State.BusinessAssets);
            Assert.AreEqual(1, h.State.Ledger.Count);
        }

        // ---------- ekspertiz ----------

        [Test]
        public void PayAppraisal_ReducesCash_AndKeepsTheFeePending()
        {
            EconomyHarness h = Books();

            Result<TransactionRecord> result = h.Economy.PayAppraisal(9, "phone.elma_e13_pro", Tl(1000), 3);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(Tl(249000), h.Cash);
            Assert.AreEqual("appraisal", result.Value.TypeId);
            Assert.AreEqual(9L, result.Value.InstanceId);
            Assert.AreEqual(Tl(1000), h.Economy.PendingAppraisalCost(9));
            Assert.AreEqual(Money.Zero, h.Economy.PendingAppraisalCost(10));
        }

        [Test]
        public void PayAppraisal_SeveralLevels_AccumulatePending()
        {
            EconomyHarness h = Books();

            h.Economy.PayAppraisal(9, "phone.x", Tl(300), 3);
            h.Economy.PayAppraisal(9, "phone.x", Tl(1000), 3);

            Assert.AreEqual(Tl(1300), h.Economy.PendingAppraisalCost(9));
            Assert.AreEqual(2, h.State.GetPendingAppraisalRecordIds(9).Count);
        }

        [TestCase(0L)]
        [TestCase(-300L)]
        [TestCase(305L)]
        public void PayAppraisal_InvalidFee_Fails(long fee)
        {
            EconomyHarness h = Books();

            Assert.AreEqual("amount.invalid", h.Economy.PayAppraisal(9, "phone.x", Tl(fee), 3).ErrorCode);
            Assert.AreEqual(Money.Zero, h.Economy.PendingAppraisalCost(9));
            Assert.AreEqual(1, h.State.Ledger.Count);
        }

        [Test]
        public void PayAppraisal_InsufficientCash_FailsWithoutPending()
        {
            EconomyHarness h = Books();
            Assert.IsTrue(h.Economy.RecordInvestment(Tl(249900), 1).IsSuccess);

            Result<TransactionRecord> result = h.Economy.PayAppraisal(9, "phone.x", Tl(300), 2);

            Assert.AreEqual("cash.insufficient", result.ErrorCode);
            Assert.AreEqual(Money.Zero, h.Economy.PendingAppraisalCost(9));
        }

        [Test]
        public void CapitalizePending_ReturnsTheTotal_AndClearsIt()
        {
            EconomyHarness h = Books();
            h.Economy.PayAppraisal(9, "phone.x", Tl(300), 3);
            h.Economy.PayAppraisal(9, "phone.x", Tl(1000), 3);

            Money total = h.Economy.CapitalizePendingAppraisals(9);

            Assert.AreEqual(Tl(1300), total);
            Assert.AreEqual(Money.Zero, h.Economy.PendingAppraisalCost(9));
            Assert.AreEqual(Money.Zero, h.Economy.CapitalizePendingAppraisals(9), "İkinci çağrı sıfır");
            Assert.AreEqual(Tl(248700), h.Cash, "Nakit değişmez, ücret zaten ödenmişti");
        }

        [Test]
        public void WriteOff_CreatesOneZeroRowPerPendingAppraisal_AndKeepsCashUnchanged()
        {
            EconomyHarness h = Books();
            TransactionRecord first = h.Economy.PayAppraisal(9, "phone.x", Tl(300), 3).Value;
            TransactionRecord second = h.Economy.PayAppraisal(9, "phone.x", Tl(1000), 3).Value;
            Money cashBefore = h.Cash;

            Result<Money> result = h.Economy.WriteOffAppraisals(9, 4);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(Tl(1300), result.Value);
            Assert.AreEqual(cashBefore, h.Cash);
            Assert.AreEqual(Money.Zero, h.Economy.PendingAppraisalCost(9));

            List<TransactionRecord> writeOffs = h.State.Ledger.Records.Where(r => r.TypeId == "wasted_appraisal").ToList();
            Assert.AreEqual(2, writeOffs.Count);
            Assert.AreEqual(new[] { first.Id, second.Id }, writeOffs.Select(r => r.RelatedRecordId.Value).ToArray());
            Assert.IsTrue(writeOffs.All(r => r.Amount.IsZero && r.Day == 4 && r.InstanceId == 9));
        }

        [Test]
        public void WriteOff_NothingPending_Fails()
        {
            EconomyHarness h = Books();

            Assert.AreEqual("appraisal.nothing_pending", h.Economy.WriteOffAppraisals(9, 4).ErrorCode);
            h.Economy.PayAppraisal(9, "phone.x", Tl(300), 3);
            h.Economy.CapitalizePendingAppraisals(9);
            Assert.AreEqual("appraisal.nothing_pending", h.Economy.WriteOffAppraisals(9, 4).ErrorCode, "Ürüne eklenen ücret yazılamaz");
        }

        [Test]
        public void WriteOff_CannotBeAppliedTwice()
        {
            EconomyHarness h = Books();
            h.Economy.PayAppraisal(9, "phone.x", Tl(300), 3);
            Assert.IsTrue(h.Economy.WriteOffAppraisals(9, 3).IsSuccess);

            Assert.AreEqual("appraisal.nothing_pending", h.Economy.WriteOffAppraisals(9, 3).ErrorCode);
            Assert.AreEqual(1, h.State.Ledger.Records.Count(r => r.TypeId == "wasted_appraisal"));
        }

        // ---------- günlük gider (T4) ----------

        [TestCase(1)]
        [TestCase(2)]
        public void DailyExpense_IsZeroBeforeDayThree_AndWritesNoRow(int day)
        {
            EconomyHarness h = Books();

            Result<Money> result = h.Economy.ChargeDailyExpense(day);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(Money.Zero, result.Value);
            Assert.AreEqual(Tl(250000), h.Cash);
            Assert.AreEqual(1, h.State.Ledger.Count);
        }

        [TestCase(3)]
        [TestCase(4)]
        [TestCase(30)]
        public void DailyExpense_Is500FromDayThree(int day)
        {
            EconomyHarness h = Books();

            Result<Money> result = h.Economy.ChargeDailyExpense(day);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(Tl(500), result.Value);
            Assert.AreEqual(Tl(249500), h.Cash);
            TransactionRecord r = h.State.Ledger.Records.Last();
            Assert.AreEqual("daily_expense", r.TypeId);
            Assert.AreEqual(Tl(-500), r.Amount);
            Assert.AreEqual(TransactionCategory.Expense, r.Category);
            Assert.AreEqual(day, r.Day);
        }

        [Test]
        public void DailyExpense_CannotBeChargedTwiceForTheSameDay()
        {
            EconomyHarness h = Books();
            Assert.IsTrue(h.Economy.ChargeDailyExpense(3).IsSuccess);

            Result<Money> again = h.Economy.ChargeDailyExpense(3);

            Assert.AreEqual("expense.already_charged", again.ErrorCode);
            Assert.AreEqual(Tl(249500), h.Cash);
            Assert.IsTrue(h.Economy.ChargeDailyExpense(4).IsSuccess);
            Assert.AreEqual(Tl(249000), h.Cash);
        }

        [Test]
        public void DailyExpense_IsMandatory_SoCashMayGoNegative_ButPurchasesStillCannot()
        {
            EconomyHarness h = Books();
            Assert.IsTrue(h.Economy.RecordInvestment(Tl(249800), 1).IsSuccess);
            Assert.AreEqual(Tl(200), h.Cash);

            Assert.IsTrue(h.Economy.ChargeDailyExpense(3).IsSuccess);

            Assert.AreEqual(Tl(-300), h.Cash);
            Assert.AreEqual("cash.insufficient", h.Economy.RecordPurchase(1, "phone.x", null, Tl(10), 3).ErrorCode);
        }

        [Test]
        public void DailyExpense_UsesTheConstants_NotHardcodedNumbers()
        {
            var constants = new EconomyConstants(Tl(100000), 2, Tl(700), 4);
            var h = new EconomyHarness(constants);

            Assert.AreEqual(Money.Zero, h.Economy.ChargeDailyExpense(1).Value);
            Assert.AreEqual(Tl(700), h.Economy.ChargeDailyExpense(2).Value);
            Assert.AreEqual(Tl(99300), h.Cash);
        }

        // ---------- olaylar (yalnızca bildirim) ----------

        [Test]
        public void SuccessfulRecords_PublishTransactionRecordedAndCashChanged()
        {
            EconomyHarness h = Books();
            var recorded = new List<TransactionRecorded>();
            var cash = new List<CashChanged>();
            h.Bus.Subscribe<TransactionRecorded>(e => recorded.Add(e));
            h.Bus.Subscribe<CashChanged>(e => cash.Add(e));

            h.Economy.RecordPurchase(1, "phone.x", null, Tl(27000), 1);

            Assert.AreEqual(1, recorded.Count);
            Assert.AreEqual("purchase", recorded[0].Record.TypeId);
            Assert.AreEqual(1, cash.Count);
            Assert.AreEqual(Tl(250000), cash[0].OldCash);
            Assert.AreEqual(Tl(223000), cash[0].NewCash);
        }

        [Test]
        public void FailedOperations_PublishNothing()
        {
            EconomyHarness h = Books();
            int events = 0;
            h.Bus.Subscribe<TransactionRecorded>(_ => events++);
            h.Bus.Subscribe<CashChanged>(_ => events++);

            h.Economy.RecordPurchase(1, "phone.x", null, Tl(999999990), 1);
            h.Economy.RecordPurchase(1, "phone.x", null, Tl(0), 1);
            h.Economy.WriteOffAppraisals(1, 1);

            Assert.AreEqual(0, events);
        }

        [Test]
        public void ZeroCashWriteOff_PublishesTransactionRecordedButNoCashChange()
        {
            EconomyHarness h = Books();
            h.Economy.PayAppraisal(9, "phone.x", Tl(300), 3);
            int recorded = 0;
            int cash = 0;
            h.Bus.Subscribe<TransactionRecorded>(_ => recorded++);
            h.Bus.Subscribe<CashChanged>(_ => cash++);

            h.Economy.WriteOffAppraisals(9, 3);

            Assert.AreEqual(1, recorded);
            Assert.AreEqual(0, cash);
        }

        [Test]
        public void StateIsCommitted_EvenIfAnEventHandlerThrows()
        {
            EconomyHarness h = Books();
            h.Bus.Subscribe<TransactionRecorded>(_ => throw new InvalidOperationException("boom"));

            Assert.Throws<InvalidOperationException>(() => h.Economy.RecordPurchase(1, "phone.x", null, Tl(27000), 1));

            Assert.AreEqual(Tl(223000), h.Cash, "Olay yayını durum değişikliğinden SONRA yapılmalı (K5)");
            Assert.AreEqual(2, h.State.Ledger.Count);
        }

        [Test]
        public void WorksWithoutAnEventBus()
        {
            var types = ContentFixtures.Types();
            var state = new EconomyState(types);
            var economy = new EconomyService(state, ContentFixtures.Constants(), null);

            Assert.IsTrue(economy.OpenBooks().IsSuccess);
            Assert.IsTrue(economy.RecordPurchase(1, "phone.x", null, Tl(1000), 1).IsSuccess);
            Assert.AreEqual(Tl(249000), economy.Cash);
        }

        [Test]
        public void Constructor_RejectsNullArguments()
        {
            var state = new EconomyState(ContentFixtures.Types());

            Assert.Throws<ArgumentNullException>(() => new EconomyService(null, ContentFixtures.Constants(), null));
            Assert.Throws<ArgumentNullException>(() => new EconomyService(state, null, null));
            Assert.Throws<ArgumentNullException>(() => new EconomyState(null));
        }
    }
}
