using Esnaf.Core;

namespace Esnaf.Domain.Appraisal
{
    /// <summary>Değer aralığı (TL, uçlar dahil).</summary>
    public sealed class MoneyRange
    {
        public Money Min { get; }
        public Money Max { get; }

        public MoneyRange(Money min, Money max)
        {
            Min = min;
            Max = max;
        }

        public bool Contains(Money value)
        {
            return value >= Min && value <= Max;
        }

        /// <summary>Orta nokta, 10 TL'ye yuvarlı (28.000–31.000 → 29.500).</summary>
        public Money Midpoint
        {
            get { return Money.FromDoubleRoundedTo10((Min.Tl + Max.Tl) / 2.0); }
        }
    }
}
