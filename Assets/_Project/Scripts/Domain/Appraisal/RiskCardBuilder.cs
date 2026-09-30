using System;
using Esnaf.Core;

namespace Esnaf.Domain.Appraisal
{
    /// <summary>Bir ekspertiz sonucundan ve bir teklif tutarından risk kartı üretir (GDD v0.2 5.5).</summary>
    public sealed class RiskCardBuilder
    {
        private readonly AppraisalConfig _config;

        public RiskCardBuilder(AppraisalConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            _config = config;
        }

        public Result<RiskCard> Build(AppraisalResult result, Money offer)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (!offer.IsPositive || !offer.IsRoundedTo10)
            {
                return Result<RiskCard>.Fail("offer.invalid", "The offer must be positive and a multiple of 10 TL.");
            }

            AppraisalLevel level;
            if (!_config.TryGetLevel(result.LevelId, out level))
            {
                return Result<RiskCard>.Fail("appraisal.level_unknown", "Unknown appraisal level '" + result.LevelId + "'.");
            }

            if (result.ValueRange == null)
            {
                return Result<RiskCard>.Fail("appraisal.no_value_range", "Level " + result.LevelId + " gives no value range.");
            }

            var scenarios = new[]
            {
                Scenario("bad", result.ValueRange.Min, offer),
                Scenario("mid", result.ValueRange.Midpoint, offer),
                Scenario("good", result.ValueRange.Max, offer)
            };
            double miss = 1.0 - (level.CoverageEstimate ?? 1.0);
            return Result<RiskCard>.Ok(new RiskCard(result.ResultId, result.LevelId, offer, scenarios, miss));
        }

        private RiskScenario Scenario(string name, Money trueValue, Money offer)
        {
            Money sale = Money.FromDoubleRoundedTo10(trueValue.Tl * _config.ExpectedSaleFactor);
            return new RiskScenario(name, trueValue, sale, sale - offer);
        }
    }
}
