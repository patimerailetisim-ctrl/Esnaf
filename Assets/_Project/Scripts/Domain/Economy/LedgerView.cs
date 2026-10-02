using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Esnaf.Core;

namespace Esnaf.Domain.Economy
{
    /// <summary>Tam Para Defteri ekranında bir günün bölümü (GDD v0.3 2.1, Gün 3).</summary>
    public sealed class LedgerDayGroup
    {
        public int Day { get; }
        public IReadOnlyList<TransactionRecord> Records { get; }
        public Money OpeningBalance { get; }
        public Money ClosingBalance { get; }

        /// <summary>O gün nakde giren toplam (pozitif tutarlar).</summary>
        public Money TotalIn { get; }

        /// <summary>O gün nakitten çıkan toplam (pozitif büyüklük).</summary>
        public Money TotalOut { get; }

        public Money NetProfit { get; }

        internal LedgerDayGroup(
            int day, IEnumerable<TransactionRecord> records, Money opening, Money closing, Money totalIn, Money totalOut, Money netProfit)
        {
            Day = day;
            Records = new ReadOnlyCollection<TransactionRecord>(new List<TransactionRecord>(records));
            OpeningBalance = opening;
            ClosingBalance = closing;
            TotalIn = totalIn;
            TotalOut = totalOut;
            NetProfit = netProfit;
        }
    }

    public enum AppraisalStatus
    {
        /// <summary>Ücret ödendi; ürün henüz alınmadı/vazgeçilmedi.</summary>
        Pending = 0,

        /// <summary>Ürün alındı: ücret ürünün maliyet tabanına eklendi.</summary>
        Capitalized = 1,

        /// <summary>Ürün alınmadı: ücret gider olarak yazıldı.</summary>
        WrittenOff = 2
    }

    /// <summary>"Neden bu kadar?" açıklaması için yapısal veri (metni arayüz üretir).</summary>
    public sealed class TransactionExplanation
    {
        public TransactionRecord Record { get; }

        /// <summary>Gider yazma satırında yazılan ekspertiz satırı.</summary>
        public TransactionRecord RelatedRecord { get; }

        /// <summary>Satışta satılan ürünün maliyet tabanı.</summary>
        public Money? CostBasis { get; }

        /// <summary>Satışta kâr/zarar (satış − maliyet tabanı).</summary>
        public Money? Profit { get; }

        /// <summary>Gider yazma satırında deftere gider olarak işlenen tutar.</summary>
        public Money? ExpenseAmount { get; }

        /// <summary>Ekspertiz satırında ücretin akıbeti.</summary>
        public AppraisalStatus? AppraisalStatus { get; }

        internal TransactionExplanation(
            TransactionRecord record,
            TransactionRecord relatedRecord,
            Money? costBasis,
            Money? profit,
            Money? expenseAmount,
            AppraisalStatus? appraisalStatus)
        {
            Record = record;
            RelatedRecord = relatedRecord;
            CostBasis = costBasis;
            Profit = profit;
            ExpenseAmount = expenseAmount;
            AppraisalStatus = appraisalStatus;
        }
    }

    /// <summary>
    /// Tam Para Defteri ekranının (Gün 3) alan verisi: günlere göre gruplanmış satırlar ve satır açıklamaları.
    /// Filtre ve sektör analizi MVP dışıdır. Görünüm defterden hesaplanır, ayrıca saklanmaz.
    /// </summary>
    public sealed class LedgerView
    {
        private readonly EconomyState _state;
        private readonly TransactionTypes _types;

        public LedgerView(EconomyState state, TransactionTypes types)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (types == null)
            {
                throw new ArgumentNullException(nameof(types));
            }

            _state = state;
            _types = types;
        }

        /// <summary>Yalnızca satırı olan günler. newestFirst: en yeni gün başta.</summary>
        public IReadOnlyList<LedgerDayGroup> GroupByDay(bool newestFirst)
        {
            Ledger ledger = _state.Ledger;
            var groups = new List<LedgerDayGroup>();
            int index = 0;
            while (index < ledger.Records.Count)
            {
                int day = ledger.Records[index].Day;
                var rows = new List<TransactionRecord>();
                Money totalIn = Money.Zero;
                Money totalOut = Money.Zero;
                while (index < ledger.Records.Count && ledger.Records[index].Day == day)
                {
                    TransactionRecord record = ledger.Records[index];
                    rows.Add(record);
                    if (record.Amount.IsPositive)
                    {
                        totalIn += record.Amount;
                    }
                    else if (record.Amount.IsNegative)
                    {
                        totalOut += record.Amount.Abs();
                    }

                    index++;
                }

                DayFigures figures = DayFigures.Compute(ledger, _types, rows);
                groups.Add(new LedgerDayGroup(
                    day, rows, ledger.BalanceBeforeDay(day), ledger.BalanceAfterDay(day), totalIn, totalOut, figures.NetProfit));
            }

            if (newestFirst)
            {
                groups.Reverse();
            }

            return new ReadOnlyCollection<LedgerDayGroup>(groups);
        }

        public Result<TransactionExplanation> Explain(long recordId)
        {
            Ledger ledger = _state.Ledger;
            TransactionRecord record;
            if (!ledger.TryGetById(recordId, out record))
            {
                return Result<TransactionExplanation>.Fail("ledger.unknown_record", "No ledger row with id " + recordId + ".");
            }

            TransactionType type = _types.Get(record.TypeId);
            TransactionRecord related = null;
            Money? costBasis = null;
            Money? profit = null;
            Money? expense = null;
            AppraisalStatus? status = null;

            if (type.ProfitEffect == ProfitEffect.Sale || type.ProfitEffect == ProfitEffect.AccessorySale)
            {
                costBasis = record.SaleCostBasis;
                profit = record.Amount - record.SaleCostBasis.Value;
            }
            else if (type.ProfitEffect == ProfitEffect.WriteOff)
            {
                ledger.TryGetById(record.RelatedRecordId.Value, out related);
                expense = related.Amount.Abs();
            }
            else if (record.TypeId == TransactionTypeIds.Appraisal)
            {
                if (ledger.IsWrittenOff(record.Id))
                {
                    status = AppraisalStatus.WrittenOff;
                }
                else if (record.InstanceId.HasValue && Contains(_state.GetPendingAppraisalRecordIds(record.InstanceId.Value), record.Id))
                {
                    status = AppraisalStatus.Pending;
                }
                else
                {
                    status = AppraisalStatus.Capitalized;
                }
            }

            return Result<TransactionExplanation>.Ok(new TransactionExplanation(record, related, costBasis, profit, expense, status));
        }

        private static bool Contains(IReadOnlyList<long> ids, long id)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == id)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
