using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Esnaf.Core;

namespace Esnaf.Domain.Economy
{
    /// <summary>
    /// Para defteri: yalnızca EKLEME yapılan (append-only), zamana göre sıralı defter. Nakit bakiyesi = Σ tutarlar (GDD I1).
    /// Her satırın türü <see cref="TransactionTypes"/>'tan gelir; işaret, yuvarlama ve ilişki kuralları burada zorlanır.
    /// Başarısız ekleme defteri HİÇ değiştirmez.
    /// </summary>
    public sealed class Ledger
    {
        public const string CashAccount = "cash";

        private readonly TransactionTypes _types;
        private readonly List<TransactionRecord> _records = new List<TransactionRecord>();
        private readonly ReadOnlyCollection<TransactionRecord> _view;
        private readonly HashSet<long> _writtenOff = new HashSet<long>();

        public Money Balance { get; private set; }
        public long LastId { get; private set; }

        /// <summary>Son satırın günü (satır yoksa 0).</summary>
        public int LastDay { get; private set; }

        public Ledger(TransactionTypes types)
        {
            if (types == null)
            {
                throw new ArgumentNullException(nameof(types));
            }

            _types = types;
            _view = new ReadOnlyCollection<TransactionRecord>(_records);
            Balance = Money.Zero;
        }

        public IReadOnlyList<TransactionRecord> Records
        {
            get { return _view; }
        }

        public int Count
        {
            get { return _records.Count; }
        }

        /// <param name="costBasis">Yalnızca satışta zorunlu (ve yalnızca satışta izinli).</param>
        /// <param name="relatedRecordId">Yalnızca gider yazma satırında zorunlu: yazılan ekspertiz satırı.</param>
        public Result<TransactionRecord> Append(
            string typeId,
            Money amount,
            int day,
            long? instanceId = null,
            string definitionId = null,
            string npcId = null,
            Money? costBasis = null,
            long? relatedRecordId = null,
            string memoKey = null,
            IEnumerable<string> memoArgs = null)
        {
            TransactionType type;
            if (!_types.TryGet(typeId, out type))
            {
                return Fail("ledger.unknown_type", "Unknown transaction type '" + typeId + "'.");
            }

            if (day < 0)
            {
                return Fail("ledger.day_invalid", "Day cannot be negative.");
            }

            if (day < LastDay)
            {
                return Fail("ledger.day_regression", "Day " + day + " is before the last recorded day " + LastDay + ".");
            }

            if (!SignMatches(type.Direction, amount))
            {
                return Fail("ledger.amount_sign", "Amount " + amount + " does not match direction " + type.Direction + " of '" + typeId + "'.");
            }

            if (!amount.IsRoundedTo10)
            {
                return Fail("ledger.amount_not_rounded", "Amount " + amount + " must be a multiple of 10 TL.");
            }

            if (type.ProfitEffect == ProfitEffect.Sale)
            {
                if (!costBasis.HasValue || !instanceId.HasValue)
                {
                    return Fail("ledger.sale_details_missing", "A sale needs an instance id and a cost basis.");
                }

                if (costBasis.Value.IsNegative || !costBasis.Value.IsRoundedTo10)
                {
                    return Fail("ledger.cost_basis_invalid", "Cost basis must be non-negative and a multiple of 10 TL.");
                }
            }
            else if (costBasis.HasValue)
            {
                return Fail("ledger.cost_basis_unexpected", "Only sales carry a cost basis.");
            }

            if (type.ProfitEffect == ProfitEffect.WriteOff)
            {
                TransactionRecord related;
                if (!relatedRecordId.HasValue
                    || !TryGetById(relatedRecordId.Value, out related)
                    || related.TypeId != TransactionTypeIds.Appraisal)
                {
                    return Fail("ledger.related_invalid", "A write-off must reference an existing appraisal row.");
                }

                if (_writtenOff.Contains(relatedRecordId.Value))
                {
                    return Fail("ledger.already_written_off", "Appraisal row " + relatedRecordId.Value + " is already written off.");
                }
            }
            else if (relatedRecordId.HasValue)
            {
                return Fail("ledger.related_unexpected", "Only write-off rows reference another row.");
            }

            Money newBalance;
            if (!Money.TryAdd(Balance, amount, out newBalance))
            {
                return Fail("ledger.overflow", "The balance would overflow.");
            }

            long id = LastId + 1;
            var record = new TransactionRecord(
                id,
                day,
                typeId,
                type.Category,
                CashAccount,
                amount,
                newBalance,
                instanceId,
                definitionId,
                npcId,
                costBasis,
                relatedRecordId,
                memoKey ?? type.DisplayKey,
                memoArgs);

            _records.Add(record);
            if (type.ProfitEffect == ProfitEffect.WriteOff)
            {
                _writtenOff.Add(relatedRecordId.Value);
            }

            Balance = newBalance;
            LastId = id;
            LastDay = day;
            return Result<TransactionRecord>.Ok(record);
        }

        public bool TryGetById(long id, out TransactionRecord record)
        {
            if (id >= 1 && id <= _records.Count)
            {
                record = _records[(int)(id - 1)];
                return true;
            }

            record = null;
            return false;
        }

        /// <summary>O güne ait satırlar (kayıt sırasıyla).</summary>
        public IReadOnlyList<TransactionRecord> ForDay(int day)
        {
            var list = new List<TransactionRecord>();
            for (int i = 0; i < _records.Count; i++)
            {
                if (_records[i].Day == day)
                {
                    list.Add(_records[i]);
                }
            }

            return new ReadOnlyCollection<TransactionRecord>(list);
        }

        /// <summary>Bu günden ÖNCEKİ son satırın sonrası bakiye (satır yoksa 0): "sabah nakit".</summary>
        public Money BalanceBeforeDay(int day)
        {
            for (int i = _records.Count - 1; i >= 0; i--)
            {
                if (_records[i].Day < day)
                {
                    return _records[i].BalanceAfter;
                }
            }

            return Money.Zero;
        }

        /// <summary>Bu gün dahil son satırın sonrası bakiye (satır yoksa 0): "akşam nakit".</summary>
        public Money BalanceAfterDay(int day)
        {
            for (int i = _records.Count - 1; i >= 0; i--)
            {
                if (_records[i].Day <= day)
                {
                    return _records[i].BalanceAfter;
                }
            }

            return Money.Zero;
        }

        /// <summary>Bu ekspertiz satırı gider olarak yazılmış mı?</summary>
        public bool IsWrittenOff(long appraisalRecordId)
        {
            return _writtenOff.Contains(appraisalRecordId);
        }

        /// <summary>Zinciri baştan doğrular: kimlikler ardışık, günler azalmıyor, bakiyeler tutarlı.</summary>
        public Result Verify()
        {
            long expectedId = 1;
            int lastDay = 0;
            long running = 0;
            for (int i = 0; i < _records.Count; i++)
            {
                TransactionRecord r = _records[i];
                if (r.Id != expectedId)
                {
                    return Result.Fail("ledger.corrupt", "Record #" + i + " has id " + r.Id + ", expected " + expectedId + ".");
                }

                if (r.Day < lastDay)
                {
                    return Result.Fail("ledger.corrupt", "Record " + r.Id + " goes back in time.");
                }

                running = checked(running + r.Amount.Tl);
                if (r.BalanceAfter.Tl != running)
                {
                    return Result.Fail("ledger.corrupt", "Record " + r.Id + " has an inconsistent balance.");
                }

                expectedId++;
                lastDay = r.Day;
            }

            if (running != Balance.Tl || LastId != _records.Count)
            {
                return Result.Fail("ledger.corrupt", "Ledger totals do not match its rows.");
            }

            return Result.Ok();
        }

        private static bool SignMatches(TransactionDirection direction, Money amount)
        {
            switch (direction)
            {
                case TransactionDirection.Inflow:
                    return amount.IsPositive;
                case TransactionDirection.Outflow:
                    return amount.IsNegative;
                default:
                    return amount.IsZero;
            }
        }

        private static Result<TransactionRecord> Fail(string code, string message)
        {
            return Result<TransactionRecord>.Fail(code, message);
        }
    }
}
