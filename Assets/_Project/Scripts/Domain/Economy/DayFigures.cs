using System.Collections.Generic;
using Esnaf.Core;

namespace Esnaf.Domain.Economy
{
    /// <summary>
    /// Bir günün kâr rakamları (GDD v0.2 9.2): Gün Net Kârı = Σ(satış − maliyet tabanı) − boşa ekspertiz − günlük gider.
    /// Gün özeti ve defter görünümü aynı hesabı paylaşır.
    /// </summary>
    internal sealed class DayFigures
    {
        public List<SoldItemSummary> Sales { get; } = new List<SoldItemSummary>();
        public Money GrossProfit { get; private set; }
        public Money WastedAppraisal { get; private set; }
        public Money Expense { get; private set; }

        public Money NetProfit
        {
            get { return GrossProfit - WastedAppraisal - Expense; }
        }

        public static DayFigures Compute(Ledger ledger, TransactionTypes types, IReadOnlyList<TransactionRecord> dayRecords)
        {
            var figures = new DayFigures();
            foreach (TransactionRecord record in dayRecords)
            {
                TransactionType type = types.Get(record.TypeId);
                switch (type.ProfitEffect)
                {
                    case ProfitEffect.Sale:
                        var sold = new SoldItemSummary(
                            record.Id, record.InstanceId.Value, record.DefinitionId, record.NpcId, record.Amount, record.SaleCostBasis.Value);
                        figures.Sales.Add(sold);
                        figures.GrossProfit += sold.Profit;
                        break;
                    case ProfitEffect.Expense:
                        figures.Expense += record.Amount.Abs();
                        break;
                    case ProfitEffect.WriteOff:
                        TransactionRecord appraisal;
                        if (ledger.TryGetById(record.RelatedRecordId.Value, out appraisal))
                        {
                            figures.WastedAppraisal += appraisal.Amount.Abs();
                        }

                        break;
                }
            }

            return figures;
        }
    }
}
