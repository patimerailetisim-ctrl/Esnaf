using System;
using System.Collections.Generic;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Economy;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Economy
{
    public class LedgerTests
    {
        private static Ledger NewLedger()
        {
            return new Ledger(ContentFixtures.Types());
        }

        private static Money Tl(long value)
        {
            return Money.FromTl(value);
        }

        private static TransactionRecord Ok(Result<TransactionRecord> result)
        {
            Assert.IsTrue(result.IsSuccess, "Append başarısız: " + result);
            return result.Value;
        }

        private static void AssertUnchanged(Ledger ledger, int count, Money balance, long lastId)
        {
            Assert.AreEqual(count, ledger.Count);
            Assert.AreEqual(balance, ledger.Balance);
            Assert.AreEqual(lastId, ledger.LastId);
        }

        // ---------- temel ----------

        [Test]
        public void NewLedger_IsEmpty()
        {
            Ledger ledger = NewLedger();

            Assert.AreEqual(0, ledger.Count);
            Assert.AreEqual(Money.Zero, ledger.Balance);
            Assert.AreEqual(0L, ledger.LastId);
            Assert.AreEqual(0, ledger.Records.Count);
            Assert.IsTrue(ledger.Verify().IsSuccess);
        }

        [Test]
        public void Constructor_RejectsNullTypes()
        {
            Assert.Throws<ArgumentNullException>(() => new Ledger(null));
        }

        [Test]
        public void Append_AssignsSequentialIds_AndRunningBalance()
        {
            Ledger ledger = NewLedger();

            TransactionRecord a = Ok(ledger.Append("opening_capital", Tl(250000), 0));
            TransactionRecord b = Ok(ledger.Append("purchase", Tl(-27000), 1, instanceId: 5, definitionId: "phone.x"));
            TransactionRecord c = Ok(ledger.Append("sale", Tl(35000), 1, instanceId: 5, definitionId: "phone.x", costBasis: Tl(27000)));

            Assert.AreEqual(new[] { 1L, 2L, 3L }, new[] { a.Id, b.Id, c.Id });
            Assert.AreEqual(Tl(250000), a.BalanceAfter);
            Assert.AreEqual(Tl(223000), b.BalanceAfter);
            Assert.AreEqual(Tl(258000), c.BalanceAfter);
            Assert.AreEqual(Tl(258000), ledger.Balance);
            Assert.AreEqual(3L, ledger.LastId);
            Assert.AreEqual(3, ledger.Count);
        }

        [Test]
        public void Append_CopiesTypeCategory_AndDefaultsAccountAndMemoKey()
        {
            Ledger ledger = NewLedger();

            TransactionRecord r = Ok(ledger.Append("investment", Tl(-15000), 5));

            Assert.AreEqual(TransactionCategory.Investment, r.Category);
            Assert.AreEqual("cash", r.Account);
            Assert.AreEqual("ledger.type.investment", r.MemoKey, "Varsayılan açıklama anahtarı tür anahtarıdır");
            Assert.AreEqual(0, r.MemoArgs.Count);
            Assert.AreEqual("investment", r.TypeId);
            Assert.AreEqual(5, r.Day);
        }

        [Test]
        public void Append_StoresReferencesAndMemo()
        {
            Ledger ledger = NewLedger();

            TransactionRecord r = Ok(ledger.Append(
                "purchase", Tl(-9100), 3, instanceId: 42, definitionId: "phone.nova_n3_pro", npcId: "npc.kemal",
                memoKey: "ledger.memo.custom", memoArgs: new[] { "a", "b" }));

            Assert.AreEqual(42L, r.InstanceId);
            Assert.AreEqual("phone.nova_n3_pro", r.DefinitionId);
            Assert.AreEqual("npc.kemal", r.NpcId);
            Assert.AreEqual("ledger.memo.custom", r.MemoKey);
            Assert.AreEqual(new[] { "a", "b" }, r.MemoArgs.ToArray());
            Assert.IsFalse(r.MemoArgs is List<string>);
            Assert.IsFalse(r.MemoArgs is string[]);
            Assert.IsNull(r.SaleCostBasis);
            Assert.IsNull(r.RelatedRecordId);
        }

        [Test]
        public void Records_AreReadOnly_AndImmutable()
        {
            Ledger ledger = NewLedger();
            Ok(ledger.Append("opening_capital", Tl(250000), 0));

            Assert.IsFalse(ledger.Records is List<TransactionRecord>);
            Assert.IsFalse(ledger.Records is TransactionRecord[]);
            Assert.IsNull(typeof(TransactionRecord).GetProperty("Amount").SetMethod, "TransactionRecord değişmez olmalı");
            Assert.IsNull(typeof(TransactionRecord).GetProperty("Day").SetMethod);
        }

        // ---------- tür ve işaret kuralları ----------

        [Test]
        public void Append_UnknownType_Fails_AndLeavesLedgerUntouched()
        {
            Ledger ledger = NewLedger();
            Ok(ledger.Append("opening_capital", Tl(250000), 0));

            Result<TransactionRecord> result = ledger.Append("loan_disbursement", Tl(1000), 1);

            Assert.IsTrue(result.IsFailure);
            Assert.AreEqual("ledger.unknown_type", result.ErrorCode);
            AssertUnchanged(ledger, 1, Tl(250000), 1);
        }

        [TestCase("purchase", 1000L)]       // çıkış türü pozitif tutar
        [TestCase("purchase", 0L)]          // çıkış türü sıfır tutar
        [TestCase("sale", -1000L)]          // giriş türü negatif tutar
        [TestCase("opening_capital", 0L)]   // giriş türü sıfır tutar
        [TestCase("wasted_appraisal", 100L)]
        [TestCase("wasted_appraisal", -100L)]
        public void Append_AmountSign_MustMatchTypeDirection(string typeId, long amount)
        {
            Ledger ledger = NewLedger();

            Result<TransactionRecord> result = ledger.Append(typeId, Tl(amount), 1, instanceId: 1, costBasis: typeId == "sale" ? Tl(100) : (Money?)null);

            Assert.IsTrue(result.IsFailure);
            Assert.AreEqual("ledger.amount_sign", result.ErrorCode);
            AssertUnchanged(ledger, 0, Money.Zero, 0);
        }

        [TestCase(1005L)]
        [TestCase(-1L)]
        public void Append_AmountsMustBeRoundedToTenTl(long amount)
        {
            Ledger ledger = NewLedger();

            Result<TransactionRecord> result = ledger.Append(amount > 0 ? "opening_capital" : "purchase", Tl(amount), 1);

            Assert.AreEqual("ledger.amount_not_rounded", result.ErrorCode);
            AssertUnchanged(ledger, 0, Money.Zero, 0);
        }

        // ---------- gün kuralları ----------

        [Test]
        public void Append_NegativeDay_Fails()
        {
            Ledger ledger = NewLedger();

            Assert.AreEqual("ledger.day_invalid", ledger.Append("opening_capital", Tl(1000), -1).ErrorCode);
        }

        [Test]
        public void Append_DayCannotGoBackwards_ButSameDayIsFine()
        {
            Ledger ledger = NewLedger();
            Ok(ledger.Append("opening_capital", Tl(250000), 0));
            Ok(ledger.Append("investment", Tl(-1000), 5));
            Ok(ledger.Append("investment", Tl(-1000), 5));

            Result<TransactionRecord> result = ledger.Append("investment", Tl(-1000), 4);

            Assert.AreEqual("ledger.day_regression", result.ErrorCode);
            AssertUnchanged(ledger, 3, Tl(248000), 3);
            Ok(ledger.Append("investment", Tl(-1000), 6));
        }

        // ---------- satış / maliyet tabanı ----------

        [Test]
        public void Sale_RequiresCostBasisAndInstance()
        {
            Ledger ledger = NewLedger();

            Assert.AreEqual("ledger.sale_details_missing", ledger.Append("sale", Tl(5000), 1, instanceId: 1).ErrorCode);
            Assert.AreEqual("ledger.sale_details_missing", ledger.Append("sale", Tl(5000), 1, costBasis: Tl(4000)).ErrorCode);
            AssertUnchanged(ledger, 0, Money.Zero, 0);
        }

        [TestCase(-10L)]
        [TestCase(4005L)]
        public void Sale_CostBasis_MustBeNonNegativeAndRounded(long costBasis)
        {
            Ledger ledger = NewLedger();

            Assert.AreEqual("ledger.cost_basis_invalid", ledger.Append("sale", Tl(5000), 1, instanceId: 1, costBasis: Tl(costBasis)).ErrorCode);
        }

        [Test]
        public void ZeroCostBasis_IsAllowed()
        {
            Ledger ledger = NewLedger();

            TransactionRecord r = Ok(ledger.Append("sale", Tl(5000), 1, instanceId: 1, costBasis: Tl(0)));

            Assert.AreEqual(Tl(0), r.SaleCostBasis);
        }

        [Test]
        public void CostBasis_IsOnlyAllowedOnSales()
        {
            Ledger ledger = NewLedger();

            Assert.AreEqual("ledger.cost_basis_unexpected", ledger.Append("purchase", Tl(-5000), 1, costBasis: Tl(4000)).ErrorCode);
        }

        // ---------- ekspertiz gider yazma (write-off) ----------

        [Test]
        public void WrittenOffAppraisal_LinksToTheOriginalRow_WithZeroCash()
        {
            Ledger ledger = NewLedger();
            Ok(ledger.Append("opening_capital", Tl(250000), 0));
            TransactionRecord appraisal = Ok(ledger.Append("appraisal", Tl(-300), 1, instanceId: 9));

            TransactionRecord writeOff = Ok(ledger.Append("wasted_appraisal", Tl(0), 1, instanceId: 9, relatedRecordId: appraisal.Id));

            Assert.AreEqual(appraisal.Id, writeOff.RelatedRecordId);
            Assert.AreEqual(Tl(249700), writeOff.BalanceAfter, "Yazma satırı nakdi değiştirmez");
            Assert.AreEqual(Tl(249700), ledger.Balance);
        }

        [Test]
        public void WriteOff_RequiresARelatedAppraisalRow()
        {
            Ledger ledger = NewLedger();
            TransactionRecord purchase = Ok(ledger.Append("purchase", Tl(-1000), 1));

            Assert.AreEqual("ledger.related_invalid", ledger.Append("wasted_appraisal", Tl(0), 1).ErrorCode, "ilişki yok");
            Assert.AreEqual("ledger.related_invalid", ledger.Append("wasted_appraisal", Tl(0), 1, relatedRecordId: 999).ErrorCode, "bilinmeyen satır");
            Assert.AreEqual("ledger.related_invalid", ledger.Append("wasted_appraisal", Tl(0), 1, relatedRecordId: purchase.Id).ErrorCode, "ekspertiz olmayan satır");
        }

        [Test]
        public void SameAppraisal_CannotBeWrittenOffTwice()
        {
            Ledger ledger = NewLedger();
            TransactionRecord appraisal = Ok(ledger.Append("appraisal", Tl(-300), 1, instanceId: 9));
            Ok(ledger.Append("wasted_appraisal", Tl(0), 1, instanceId: 9, relatedRecordId: appraisal.Id));

            Result<TransactionRecord> again = ledger.Append("wasted_appraisal", Tl(0), 2, instanceId: 9, relatedRecordId: appraisal.Id);

            Assert.AreEqual("ledger.already_written_off", again.ErrorCode);
        }

        [Test]
        public void RelatedRecord_IsOnlyAllowedOnWriteOffs()
        {
            Ledger ledger = NewLedger();
            TransactionRecord appraisal = Ok(ledger.Append("appraisal", Tl(-300), 1, instanceId: 9));

            Assert.AreEqual("ledger.related_unexpected", ledger.Append("purchase", Tl(-1000), 1, relatedRecordId: appraisal.Id).ErrorCode);
        }

        // ---------- sorgular ----------

        [Test]
        public void TryGetById_And_ForDay_And_Balances()
        {
            Ledger ledger = NewLedger();
            Ok(ledger.Append("opening_capital", Tl(250000), 0));
            Ok(ledger.Append("purchase", Tl(-4800), 1, instanceId: 1));
            Ok(ledger.Append("sale", Tl(5750), 1, instanceId: 1, costBasis: Tl(4800)));
            Ok(ledger.Append("purchase", Tl(-3700), 2, instanceId: 2));
            Ok(ledger.Append("investment", Tl(-15000), 4));

            TransactionRecord found;
            Assert.IsTrue(ledger.TryGetById(2, out found));
            Assert.AreEqual("purchase", found.TypeId);
            Assert.IsFalse(ledger.TryGetById(99, out found));
            Assert.IsFalse(ledger.TryGetById(0, out found));

            Assert.AreEqual(new[] { 2L, 3L }, ledger.ForDay(1).Select(r => r.Id).ToArray());
            Assert.AreEqual(0, ledger.ForDay(3).Count, "İşlemsiz gün");
            Assert.AreEqual(1, ledger.ForDay(0).Count);

            Assert.AreEqual(Money.Zero, ledger.BalanceBeforeDay(0));
            Assert.AreEqual(Tl(250000), ledger.BalanceBeforeDay(1));
            Assert.AreEqual(Tl(250950), ledger.BalanceAfterDay(1));
            Assert.AreEqual(Tl(250950), ledger.BalanceBeforeDay(2));
            Assert.AreEqual(Tl(247250), ledger.BalanceAfterDay(2));
            Assert.AreEqual(Tl(247250), ledger.BalanceBeforeDay(4));
            Assert.AreEqual(Tl(247250), ledger.BalanceAfterDay(3), "İşlemsiz gün: bakiye değişmez");
            Assert.AreEqual(Tl(232250), ledger.BalanceAfterDay(4));
            Assert.AreEqual(Tl(232250), ledger.BalanceAfterDay(99));
        }

        [Test]
        public void Verify_DetectsAConsistentChain()
        {
            Ledger ledger = NewLedger();
            Ok(ledger.Append("opening_capital", Tl(250000), 0));
            Ok(ledger.Append("investment", Tl(-15000), 5));

            Assert.IsTrue(ledger.Verify().IsSuccess);
        }

        [Test]
        public void Append_BalanceOverflow_IsReported_WithoutChangingTheLedger()
        {
            Ledger ledger = NewLedger();
            Ok(ledger.Append("opening_capital", Money.FromTl(long.MaxValue - 7), 0));

            Result<TransactionRecord> result = ledger.Append("opening_capital", Tl(100), 0);

            Assert.IsTrue(result.IsFailure);
            Assert.AreEqual("ledger.overflow", result.ErrorCode);
            AssertUnchanged(ledger, 1, Money.FromTl(long.MaxValue - 7), 1);
        }
    }
}
