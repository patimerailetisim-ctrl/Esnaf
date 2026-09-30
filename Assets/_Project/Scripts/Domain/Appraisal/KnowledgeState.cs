using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Esnaf.Domain.Appraisal
{
    /// <summary>
    /// Oyuncunun "bildikleri" (GDD v0.3 4.3): ürün → ekspertiz sonuçları. Gerçek nitelikler burada değil, <c>ProductInstance</c>'tadır.
    /// Aynı ürün + aynı seviye için yalnızca BİR sonuç tutulur (I5: kilitli sonuç).
    /// </summary>
    public sealed class KnowledgeState
    {
        private readonly List<AppraisalResult> _all = new List<AppraisalResult>();
        private readonly Dictionary<string, AppraisalResult> _byInstanceAndLevel = new Dictionary<string, AppraisalResult>(StringComparer.Ordinal);
        private readonly Dictionary<long, AppraisalResult> _byId = new Dictionary<long, AppraisalResult>();

        /// <summary>Tüm sonuçlar, kimlik (yapılış) sırasıyla, salt okunur kopya.</summary>
        public IReadOnlyList<AppraisalResult> All
        {
            get { return new ReadOnlyCollection<AppraisalResult>(new List<AppraisalResult>(_all)); }
        }

        public void Add(AppraisalResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            string key = Key(result.InstanceId, result.LevelId);
            if (_byInstanceAndLevel.ContainsKey(key))
            {
                throw new ArgumentException("Instance " + result.InstanceId + " already has a locked " + result.LevelId + " result.", nameof(result));
            }

            if (_byId.ContainsKey(result.ResultId))
            {
                throw new ArgumentException("Appraisal result " + result.ResultId + " already exists.", nameof(result));
            }

            _byInstanceAndLevel.Add(key, result);
            _byId.Add(result.ResultId, result);
            _all.Add(result);
        }

        public bool TryGet(long instanceId, string levelId, out AppraisalResult result)
        {
            return _byInstanceAndLevel.TryGetValue(Key(instanceId, levelId), out result);
        }

        public bool TryGetById(long resultId, out AppraisalResult result)
        {
            return _byId.TryGetValue(resultId, out result);
        }

        /// <summary>Bir ürünün tüm sonuçları, kimlik sırasıyla, salt okunur kopya.</summary>
        public IReadOnlyList<AppraisalResult> ForInstance(long instanceId)
        {
            return new ReadOnlyCollection<AppraisalResult>(_all.Where(r => r.InstanceId == instanceId).ToList());
        }

        private static string Key(long instanceId, string levelId)
        {
            return instanceId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + levelId;
        }
    }
}
