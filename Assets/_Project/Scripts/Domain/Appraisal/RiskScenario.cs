using Esnaf.Core;

namespace Esnaf.Domain.Appraisal
{
    /// <summary>Risk kartındaki bir senaryo: gerçek değer bu olsaydı beklenen satış ve kâr (GDD v0.2 5.5).</summary>
    public sealed class RiskScenario
    {
        /// <summary>"bad", "mid" veya "good".</summary>
        public string Name { get; }

        public Money TrueValue { get; }

        /// <summary>Beklenen satış = değer × 1,02 (10 TL'ye yuvarlı).</summary>
        public Money ExpectedSale { get; }

        /// <summary>Beklenen satış − teklif. Ekspertiz ücreti dahil DEĞİLDİR (batık maliyet; ürün maliyetine ayrıca eklenir).</summary>
        public Money Profit { get; }

        public RiskScenario(string name, Money trueValue, Money expectedSale, Money profit)
        {
            Name = name;
            TrueValue = trueValue;
            ExpectedSale = expectedSale;
            Profit = profit;
        }
    }
}
