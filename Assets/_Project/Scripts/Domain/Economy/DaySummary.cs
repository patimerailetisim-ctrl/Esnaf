using System.Collections.Generic;
using System.Collections.ObjectModel;
using Esnaf.Core;

namespace Esnaf.Domain.Economy
{
    /// <summary>
    /// Gün Sonu Özeti (GDD v0.3 2.1 + v0.2 9.4). Kayıtlardan HESAPLANIR, ayrıca saklanmaz. Değişmez.
    /// Gün 1'in "Para Özeti" bloğu: <see cref="OpeningCash"/>, <see cref="TotalIncome"/>, <see cref="TotalSpending"/>,
    /// <see cref="NetProfit"/>, <see cref="ClosingCash"/>. Diğer alanlar ayrıntı/tam özet içindir.
    /// Harcama alanları POZİTİF büyüklüklerdir.
    /// </summary>
    public sealed class DaySummary
    {
        public int Day { get; }
        public Money OpeningCash { get; }
        public Money ClosingCash { get; }

        /// <summary>O günün defter satırları (kayıt sırasıyla).</summary>
        public IReadOnlyList<TransactionRecord> Records { get; }

        public IReadOnlyList<SoldItemSummary> Sales { get; }

        public Money SalesIncome { get; }
        public Money PurchaseSpend { get; }

        /// <summary>O gün toptancıdan alınan aksesuar paketlerinin toplamı (wholesale_purchase; nakit çıkışı, telefon alışı <see cref="PurchaseSpend"/> değildir). Ürün örneği gerektirmez.</summary>
        public Money WholesaleSpend { get; }

        /// <summary>O gün ödenen tüm ekspertiz ücretleri (ürüne eklenenler ve boşa gidenler dahil; nakit çıkışı).</summary>
        public Money AppraisalSpend { get; }

        public Money RepairSpend { get; }
        public Money DailyExpense { get; }
        public Money InvestmentSpend { get; }
        public Money CapitalInflow { get; }

        /// <summary>Σ(satış − maliyet tabanı).</summary>
        public Money GrossProfit { get; }

        /// <summary>O gün gider yazılan ekspertiz ücretleri (ödeme günü değil, yazma günü).</summary>
        public Money WastedAppraisal { get; }

        public Money NetProfit { get; }

        /// <summary>Toplam gelir = satış geliri.</summary>
        public Money TotalIncome
        {
            get { return SalesIncome; }
        }

        /// <summary>Toplam gider = telefon alışı + toptan aksesuar alışı + ekspertiz + tamir + günlük gider (yatırım hariç: yatırım gider değildir).</summary>
        public Money TotalSpending
        {
            get { return PurchaseSpend + WholesaleSpend + AppraisalSpend + RepairSpend + DailyExpense; }
        }

        public IReadOnlyList<StockLine> Stock { get; }
        public Money StockCostBasis { get; }

        public int StockCount
        {
            get { return Stock.Count; }
        }

        /// <summary>Gün sonu toplam servet dökümü (nakit + stok maliyeti + varlıklar + bekleyen ekspertiz).</summary>
        public WealthBreakdown Wealth { get; }

        /// <summary>Servet farkı = net kâr + sermaye girişi (kimlik; canlı servetle testte doğrulanır).</summary>
        public Money WealthChange
        {
            get { return NetProfit + CapitalInflow; }
        }

        internal DaySummary(
            int day,
            Money openingCash,
            Money closingCash,
            IEnumerable<TransactionRecord> records,
            IEnumerable<SoldItemSummary> sales,
            Money salesIncome,
            Money purchaseSpend,
            Money wholesaleSpend,
            Money appraisalSpend,
            Money repairSpend,
            Money dailyExpense,
            Money investmentSpend,
            Money capitalInflow,
            Money grossProfit,
            Money wastedAppraisal,
            Money netProfit,
            IEnumerable<StockLine> stock,
            Money stockCostBasis,
            WealthBreakdown wealth)
        {
            Day = day;
            OpeningCash = openingCash;
            ClosingCash = closingCash;
            Records = new ReadOnlyCollection<TransactionRecord>(new List<TransactionRecord>(records));
            Sales = new ReadOnlyCollection<SoldItemSummary>(new List<SoldItemSummary>(sales));
            SalesIncome = salesIncome;
            PurchaseSpend = purchaseSpend;
            WholesaleSpend = wholesaleSpend;
            AppraisalSpend = appraisalSpend;
            RepairSpend = repairSpend;
            DailyExpense = dailyExpense;
            InvestmentSpend = investmentSpend;
            CapitalInflow = capitalInflow;
            GrossProfit = grossProfit;
            WastedAppraisal = wastedAppraisal;
            NetProfit = netProfit;
            Stock = new ReadOnlyCollection<StockLine>(new List<StockLine>(stock));
            StockCostBasis = stockCostBasis;
            Wealth = wealth;
        }
    }
}
