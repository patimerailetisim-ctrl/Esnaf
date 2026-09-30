using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Esnaf.Core;

namespace Esnaf.Domain.Appraisal
{
    /// <summary>
    /// RİSK KARTI (GDD v0.2 5.5): ekspertiz sonrası, teklif için üç senaryo. Kesin cevap vermez; kârın ne kadar riskli olduğunu gösterir.
    /// <see cref="MissProbability"/> gerçek değerin aralık dışında kalma olasılığıdır (S2 ≈ %8).
    /// </summary>
    public sealed class RiskCard
    {
        public long AppraisalId { get; }
        public string LevelId { get; }
        public Money Offer { get; }

        /// <summary>Sırasıyla kötü, orta, iyi.</summary>
        public IReadOnlyList<RiskScenario> Scenarios { get; }

        public double MissProbability { get; }

        public RiskCard(long appraisalId, string levelId, Money offer, IEnumerable<RiskScenario> scenarios, double missProbability)
        {
            if (scenarios == null)
            {
                throw new ArgumentNullException(nameof(scenarios));
            }

            AppraisalId = appraisalId;
            LevelId = levelId;
            Offer = offer;
            Scenarios = new ReadOnlyCollection<RiskScenario>(new List<RiskScenario>(scenarios));
            MissProbability = missProbability;
        }
    }
}
