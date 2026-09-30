using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Esnaf.Domain.Appraisal
{
    /// <summary><see cref="AppraisalCalculator"/>'ın saf çıktısı: bulgular, aralıklar ve kartlar (kimlik/ücret/gün içermez).</summary>
    public sealed class AppraisalEvaluation
    {
        public IReadOnlyList<AttributeFinding> Findings { get; }
        public NumericRange BatteryRange { get; }
        public NumericRange BodyRange { get; }
        public MoneyRange ValueRange { get; }
        public IReadOnlyList<TrumpCard> Cards { get; }

        public AppraisalEvaluation(
            IEnumerable<AttributeFinding> findings,
            NumericRange batteryRange,
            NumericRange bodyRange,
            MoneyRange valueRange,
            IEnumerable<TrumpCard> cards)
        {
            if (findings == null)
            {
                throw new ArgumentNullException(nameof(findings));
            }

            if (cards == null)
            {
                throw new ArgumentNullException(nameof(cards));
            }

            Findings = new ReadOnlyCollection<AttributeFinding>(new List<AttributeFinding>(findings));
            BatteryRange = batteryRange;
            BodyRange = bodyRange;
            ValueRange = valueRange;
            Cards = new ReadOnlyCollection<TrumpCard>(new List<TrumpCard>(cards));
        }
    }
}
