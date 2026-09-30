using System;
using System.Collections.Generic;
using Esnaf.Core;

namespace Esnaf.Domain.Economy
{
    /// <summary>
    /// Gün özetini defter kayıtlarından hesaplar. Ekonomi sistemi stok/envanteri bilmez: stok satırlarını ve gün sonu servet dökümünü
    /// çağıran taraf (envanter/gün sistemi) verir. Özet hiçbir yerde saklanmaz.
    /// </summary>
    public sealed class DaySummaryBuilder
    {
        private readonly EconomyState _state;
        private readonly TransactionTypes _types;

        public DaySummaryBuilder(EconomyState state, TransactionTypes types)
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

        /// <summary>Verilen günün net kârı (işlemsiz gün için 0).</summary>
        public Money NetProfit(int day)
        {
            if (day < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(day), "Day cannot be negative.");
            }

            return DayFigures.Compute(_state.Ledger, _types, _state.Ledger.ForDay(day)).NetProfit;
        }

        public DaySummary Build(int day, IReadOnlyList<StockLine> stock, WealthBreakdown wealthAtEnd)
        {
            if (day < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(day), "Day cannot be negative.");
            }

            if (stock == null)
            {
                throw new ArgumentNullException(nameof(stock));
            }

            if (wealthAtEnd == null)
            {
                throw new ArgumentNullException(nameof(wealthAtEnd));
            }

            Ledger ledger = _state.Ledger;
            IReadOnlyList<TransactionRecord> records = ledger.ForDay(day);
            DayFigures figures = DayFigures.Compute(ledger, _types, records);

            Money salesIncome = Money.Zero;
            Money purchase = Money.Zero;
            Money appraisal = Money.Zero;
            Money repair = Money.Zero;
            Money expense = Money.Zero;
            Money investment = Money.Zero;
            Money capital = Money.Zero;
            foreach (TransactionRecord record in records)
            {
                switch (record.TypeId)
                {
                    case TransactionTypeIds.Sale:
                        salesIncome += record.Amount;
                        break;
                    case TransactionTypeIds.Purchase:
                        purchase += record.Amount.Abs();
                        break;
                    case TransactionTypeIds.Appraisal:
                        appraisal += record.Amount.Abs();
                        break;
                    case TransactionTypeIds.Repair:
                        repair += record.Amount.Abs();
                        break;
                    case TransactionTypeIds.DailyExpense:
                        expense += record.Amount.Abs();
                        break;
                    case TransactionTypeIds.Investment:
                        investment += record.Amount.Abs();
                        break;
                    case TransactionTypeIds.OpeningCapital:
                        capital += record.Amount;
                        break;
                }
            }

            Money stockCost = Money.Zero;
            foreach (StockLine line in stock)
            {
                stockCost += line.CostBasis;
            }

            return new DaySummary(
                day,
                ledger.BalanceBeforeDay(day),
                ledger.BalanceAfterDay(day),
                records,
                figures.Sales,
                salesIncome,
                purchase,
                appraisal,
                repair,
                expense,
                investment,
                capital,
                figures.GrossProfit,
                figures.WastedAppraisal,
                figures.NetProfit,
                stock,
                stockCost,
                wealthAtEnd);
        }
    }
}
