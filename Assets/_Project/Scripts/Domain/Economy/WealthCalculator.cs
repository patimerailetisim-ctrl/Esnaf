using System;
using System.Collections.Generic;
using Esnaf.Core;

namespace Esnaf.Domain.Economy
{
    /// <summary>Servet = katkıcıların toplamı (GDD I2: nakit + stok maliyeti + varlıklar). Katkıcıların sırası döküm sırasıdır.</summary>
    public sealed class WealthCalculator
    {
        private readonly List<IWealthContributor> _contributors;

        public WealthCalculator(IEnumerable<IWealthContributor> contributors)
        {
            if (contributors == null)
            {
                throw new ArgumentNullException(nameof(contributors));
            }

            _contributors = new List<IWealthContributor>(contributors);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (IWealthContributor contributor in _contributors)
            {
                if (contributor == null || string.IsNullOrEmpty(contributor.Key) || !keys.Add(contributor.Key))
                {
                    throw new ArgumentException("Wealth contributors need unique, non-empty keys.", nameof(contributors));
                }
            }
        }

        public WealthBreakdown Calculate()
        {
            var lines = new List<WealthLine>(_contributors.Count);
            foreach (IWealthContributor contributor in _contributors)
            {
                lines.Add(new WealthLine(contributor.Key, contributor.GetValue()));
            }

            return new WealthBreakdown(lines);
        }
    }
}
