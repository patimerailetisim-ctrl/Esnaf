using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Esnaf.Domain.Appraisal
{
    /// <summary>Ekspertiz kuralları: appraisal_levels.json (seviyeler, kontrol edilen nitelikler, risk kartı çarpanı). Değişmez tanım verisi.</summary>
    public sealed class AppraisalConfig
    {
        private readonly ReadOnlyCollection<AppraisalLevel> _levels;
        private readonly ReadOnlyCollection<AppraisalCheck> _checks;
        private readonly Dictionary<string, AppraisalLevel> _byId;

        public IReadOnlyList<AppraisalLevel> Levels
        {
            get { return _levels; }
        }

        public IReadOnlyList<AppraisalCheck> Checks
        {
            get { return _checks; }
        }

        /// <summary>Risk kartı: tahmini satış = değer × bu çarpan (v0.2 5.5: 1,02).</summary>
        public double ExpectedSaleFactor { get; }

        public AppraisalConfig(IEnumerable<AppraisalLevel> levels, IEnumerable<AppraisalCheck> checks, double expectedSaleFactor)
        {
            if (levels == null)
            {
                throw new ArgumentNullException(nameof(levels));
            }

            if (checks == null)
            {
                throw new ArgumentNullException(nameof(checks));
            }

            var levelList = new List<AppraisalLevel>(levels);
            _byId = new Dictionary<string, AppraisalLevel>(StringComparer.Ordinal);
            foreach (AppraisalLevel level in levelList)
            {
                if (!_byId.ContainsKey(level.Id))
                {
                    _byId.Add(level.Id, level);
                }
            }

            _levels = new ReadOnlyCollection<AppraisalLevel>(levelList);
            _checks = new ReadOnlyCollection<AppraisalCheck>(new List<AppraisalCheck>(checks));
            ExpectedSaleFactor = expectedSaleFactor;
        }

        public bool TryGetLevel(string id, out AppraisalLevel level)
        {
            if (id == null)
            {
                level = null;
                return false;
            }

            return _byId.TryGetValue(id, out level);
        }

        public AppraisalLevel GetLevel(string id)
        {
            AppraisalLevel level;
            if (!TryGetLevel(id, out level))
            {
                throw new KeyNotFoundException("Unknown appraisal level '" + id + "'.");
            }

            return level;
        }

        /// <summary>Seviyenin dosyadaki sırası; yoksa -1.</summary>
        public int IndexOf(string id)
        {
            for (int i = 0; i < _levels.Count; i++)
            {
                if (string.Equals(_levels[i].Id, id, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
