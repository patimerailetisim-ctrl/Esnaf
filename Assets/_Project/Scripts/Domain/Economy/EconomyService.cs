using System;
using System.Collections.Generic;
using Esnaf.Core;

namespace Esnaf.Domain.Economy
{
    /// <summary>
    /// Ekonomi sistemi: para hareketlerini deftere yazar ve bekleyen ekspertiz ücretlerini takip eder.
    /// Her işlem ya tamamen uygulanır ya da HİÇ uygulanmaz (başarısızlıkta defter, nakit ve varlıklar değişmez).
    /// Bildirim olayları (varsa) durum değişikliğinden SONRA yayınlanır; doğruluk olaylara bağlı değildir (GDD K5).
    ///
    /// Ekspertiz muhasebesi: ücret ödenince nakit hemen düşer ve ücret "bekleyen" olur (servete dahil, kâr etkisi yok).
    /// Ürün satın alınınca ücret ürünün maliyet tabanına eklenir; alınmazsa gider yazılır (gün net kârını o gün düşürür).
    /// </summary>
    public sealed class EconomyService
    {
        private readonly EconomyState _state;
        private readonly EconomyConstants _constants;
        private readonly IEventBus _events;

        public EconomyService(EconomyState state, EconomyConstants constants, IEventBus events)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (constants == null)
            {
                throw new ArgumentNullException(nameof(constants));
            }

            _state = state;
            _constants = constants;
            _events = events;
        }

        public Money Cash
        {
            get { return _state.Cash; }
        }

        /// <summary>Başlangıç sermayesini 0. güne yazar. Yalnızca boş defterde çalışır.</summary>
        public Result OpenBooks()
        {
            if (_state.Ledger.Count > 0)
            {
                return Result.Fail("books.already_open", "The ledger already has rows.");
            }

            Money oldCash = _state.Cash;
            Result<TransactionRecord> result = _state.Ledger.Append(TransactionTypeIds.OpeningCapital, _constants.OpeningCapital, 0);
            if (result.IsFailure)
            {
                return Result.Fail(result.ErrorCode, result.Message);
            }

            Publish(result.Value, oldCash);
            return Result.Ok();
        }

        public Result<TransactionRecord> RecordPurchase(long instanceId, string definitionId, string sellerNpcId, Money price, int day)
        {
            Result<TransactionRecord> check = CheckSpend(price);
            if (check.IsFailure)
            {
                return check;
            }

            Money oldCash = _state.Cash;
            Result<TransactionRecord> result = _state.Ledger.Append(
                TransactionTypeIds.Purchase, -price, day, instanceId, definitionId, sellerNpcId);
            if (result.IsSuccess)
            {
                Publish(result.Value, oldCash);
            }

            return result;
        }

        /// <summary>
        /// İçerik değişikliği iadesi (GDD v0.3 6.7): içerikte artık olmayan bir ürünün alış fiyatı kadar nakit geri verilir.
        /// Yalnızca kayıt yüklenirken kullanılır; kâr etkisi yoktur.
        /// </summary>
        public Result<TransactionRecord> RecordContentRefund(long instanceId, string definitionId, Money amount, int day)
        {
            if (!IsPositiveRounded(amount))
            {
                return Result<TransactionRecord>.Fail("amount.invalid", "A refund must be positive and a multiple of 10 TL.");
            }

            Money oldCash = _state.Cash;
            Result<TransactionRecord> result = _state.Ledger.Append(
                TransactionTypeIds.ContentRefund, amount, day, instanceId, definitionId);
            if (result.IsSuccess)
            {
                Publish(result.Value, oldCash);
            }

            return result;
        }

        public Result<TransactionRecord> RecordSale(long instanceId, string definitionId, string buyerNpcId, Money price, Money costBasis, int day)
        {
            if (!IsPositiveRounded(price) || costBasis.IsNegative || !costBasis.IsRoundedTo10)
            {
                return Result<TransactionRecord>.Fail("amount.invalid", "Sale price must be positive and cost basis non-negative, both multiples of 10 TL.");
            }

            Money oldCash = _state.Cash;
            Result<TransactionRecord> result = _state.Ledger.Append(
                TransactionTypeIds.Sale, price, day, instanceId, definitionId, buyerNpcId, costBasis);
            if (result.IsSuccess)
            {
                Publish(result.Value, oldCash);
            }

            return result;
        }

        public Result<TransactionRecord> RecordRepair(long instanceId, string definitionId, Money cost, int day)
        {
            Result<TransactionRecord> check = CheckSpend(cost);
            if (check.IsFailure)
            {
                return check;
            }

            Money oldCash = _state.Cash;
            Result<TransactionRecord> result = _state.Ledger.Append(TransactionTypeIds.Repair, -cost, day, instanceId, definitionId);
            if (result.IsSuccess)
            {
                Publish(result.Value, oldCash);
            }

            return result;
        }

        /// <summary>Yatırım (raf yükseltme, ekipman): nakit düşer, işletme varlığı artar; gider DEĞİLDİR.</summary>
        public Result<TransactionRecord> RecordInvestment(Money cost, int day, string memoKey = null)
        {
            Result<TransactionRecord> check = CheckSpend(cost);
            if (check.IsFailure)
            {
                return check;
            }

            Money oldCash = _state.Cash;
            Result<TransactionRecord> result = _state.Ledger.Append(TransactionTypeIds.Investment, -cost, day, memoKey: memoKey);
            if (result.IsSuccess)
            {
                _state.BusinessAssets += cost;
                Publish(result.Value, oldCash);
            }

            return result;
        }

        /// <summary>Ekspertiz ücretini öder; ücret, ürün alınana veya gider yazılana kadar "bekleyen" kalır.</summary>
        public Result<TransactionRecord> PayAppraisal(long instanceId, string definitionId, Money fee, int day)
        {
            Result<TransactionRecord> check = CheckSpend(fee);
            if (check.IsFailure)
            {
                return check;
            }

            Money oldCash = _state.Cash;
            Result<TransactionRecord> result = _state.Ledger.Append(TransactionTypeIds.Appraisal, -fee, day, instanceId, definitionId);
            if (result.IsSuccess)
            {
                _state.AddPendingAppraisal(instanceId, result.Value.Id);
                Publish(result.Value, oldCash);
            }

            return result;
        }

        public Money PendingAppraisalCost(long instanceId)
        {
            Money total = Money.Zero;
            foreach (long recordId in _state.GetPendingAppraisalRecordIds(instanceId))
            {
                TransactionRecord record;
                if (_state.Ledger.TryGetById(recordId, out record))
                {
                    total += record.Amount.Abs();
                }
            }

            return total;
        }

        /// <summary>Ürün satın alınırken çağrılır: bekleyen ücretleri döndürür (ürünün maliyet tabanına eklenir) ve listeden çıkarır.</summary>
        public Money CapitalizePendingAppraisals(long instanceId)
        {
            Money total = Money.Zero;
            foreach (long recordId in _state.TakePendingAppraisals(instanceId))
            {
                TransactionRecord record;
                if (_state.Ledger.TryGetById(recordId, out record))
                {
                    total += record.Amount.Abs();
                }
            }

            return total;
        }

        /// <summary>
        /// Ürün alınmayacaksa bekleyen ücretleri gider yazar (her ekspertiz satırı için nakitsiz bir "boşa ekspertiz" satırı).
        /// Gider, YAZILDIĞI günün net kârından düşer. Yazılacak bir şey yoksa "appraisal.nothing_pending".
        /// </summary>
        public Result<Money> WriteOffAppraisals(long instanceId, int day)
        {
            IReadOnlyList<long> pending = _state.GetPendingAppraisalRecordIds(instanceId);
            if (pending.Count == 0)
            {
                return Result<Money>.Fail("appraisal.nothing_pending", "No pending appraisal fee for instance " + instanceId + ".");
            }

            if (day < _state.Ledger.LastDay)
            {
                return Result<Money>.Fail("ledger.day_regression", "Day " + day + " is before the last recorded day.");
            }

            Money total = Money.Zero;
            var written = new List<TransactionRecord>();
            foreach (long appraisalId in _state.TakePendingAppraisals(instanceId))
            {
                TransactionRecord appraisal;
                _state.Ledger.TryGetById(appraisalId, out appraisal);
                Result<TransactionRecord> row = _state.Ledger.Append(
                    TransactionTypeIds.WastedAppraisal, Money.Zero, day, instanceId, appraisal.DefinitionId, relatedRecordId: appraisalId);
                written.Add(row.Value);
                total += appraisal.Amount.Abs();
            }

            foreach (TransactionRecord record in written)
            {
                Publish(record, _state.Cash);
            }

            return Result<Money>.Ok(total);
        }

        /// <summary>
        /// Günlük gideri işler (GDD: Gün 1–2'de 0, Gün 3'ten itibaren 500; sayılar veri dosyasından). Tutar 0 ise satır yazılmaz.
        /// Gider ZORUNLUDUR: nakit yetmese bile yazılır (nakit negatife düşebilir). İflas politikası GDD'de tanımlı değildir.
        /// Aynı gün için ikinci kez işlenmez ("expense.already_charged").
        /// </summary>
        public Result<Money> ChargeDailyExpense(int day)
        {
            if (day < 0)
            {
                return Result<Money>.Fail("ledger.day_invalid", "Day cannot be negative.");
            }

            Money amount = day >= _constants.DailyExpenseFromDay ? _constants.DailyExpenseAmount : Money.Zero;
            if (amount.IsZero)
            {
                return Result<Money>.Ok(Money.Zero);
            }

            foreach (TransactionRecord existing in _state.Ledger.ForDay(day))
            {
                if (existing.TypeId == TransactionTypeIds.DailyExpense)
                {
                    return Result<Money>.Fail("expense.already_charged", "The daily expense for day " + day + " was already charged.");
                }
            }

            Money oldCash = _state.Cash;
            Result<TransactionRecord> result = _state.Ledger.Append(TransactionTypeIds.DailyExpense, -amount, day);
            if (result.IsFailure)
            {
                return Result<Money>.Fail(result.ErrorCode, result.Message);
            }

            Publish(result.Value, oldCash);
            return Result<Money>.Ok(amount);
        }

        private Result<TransactionRecord> CheckSpend(Money amount)
        {
            if (!IsPositiveRounded(amount))
            {
                return Result<TransactionRecord>.Fail("amount.invalid", "Amount must be positive and a multiple of 10 TL.");
            }

            if (_state.Cash < amount)
            {
                return Result<TransactionRecord>.Fail("cash.insufficient", "Cash " + _state.Cash + " is not enough for " + amount + ".");
            }

            return Result<TransactionRecord>.Ok(null);
        }

        private static bool IsPositiveRounded(Money amount)
        {
            return amount.IsPositive && amount.IsRoundedTo10;
        }

        private void Publish(TransactionRecord record, Money oldCash)
        {
            if (_events == null)
            {
                return;
            }

            _events.Publish(new TransactionRecorded(record));
            if (_state.Cash != oldCash)
            {
                _events.Publish(new CashChanged(oldCash, _state.Cash));
            }
        }
    }
}
