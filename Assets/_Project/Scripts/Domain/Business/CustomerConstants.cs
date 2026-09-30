using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Esnaf.Domain.Business
{
    /// <summary>
    /// Müşteri üretimi ve müşteri değeri sabitleri (economy_constants.json "customers"; GDD v0.2 6.2, 10.4, 11.2 ve v0.3 P4).
    /// Değişmez veridir; kodda sayı yoktur.
    /// </summary>
    public sealed class CustomerConstants
    {
        private readonly HashSet<string> _rich;

        /// <summary>Dükkân primi (başlangıç %5): M = V_müşteri × mRatio × (1 + prim).</summary>
        public double ShopPremium { get; }

        /// <summary>Primin itibarla çıkabileceği tavan (%12). İtibar sistemi Gün 11'dedir; bu gün yalnızca doğrulanır.</summary>
        public double ShopPremiumCap { get; }

        /// <summary>Müşteri Max'ı ≤ gerçek değer × bu oran (v0.2 10.4: "aşırı prim yok").</summary>
        public double MaxRatioToTrueValue { get; }

        public int CountBase { get; }
        public double CountPerShelfItem { get; }
        public int CountMax { get; }

        /// <summary>P4: günde en fazla <see cref="RichMaxPerDay"/> müşteri bu kişilerden gelebilir (zengin/koleksiyoncu).</summary>
        public IReadOnlyList<string> RichNpcIds { get; }

        public int RichMaxPerDay { get; }

        public CustomerConstants(
            double shopPremium,
            double shopPremiumCap,
            double maxRatioToTrueValue,
            int countBase,
            double countPerShelfItem,
            int countMax,
            IEnumerable<string> richNpcIds,
            int richMaxPerDay)
        {
            if (richNpcIds == null)
            {
                throw new ArgumentNullException(nameof(richNpcIds));
            }

            ShopPremium = shopPremium;
            ShopPremiumCap = shopPremiumCap;
            MaxRatioToTrueValue = maxRatioToTrueValue;
            CountBase = countBase;
            CountPerShelfItem = countPerShelfItem;
            CountMax = countMax;
            var list = new List<string>(richNpcIds);
            RichNpcIds = new ReadOnlyCollection<string>(list);
            _rich = new HashSet<string>(list, StringComparer.Ordinal);
            RichMaxPerDay = richMaxPerDay;
        }

        public bool IsRich(string npcId)
        {
            return npcId != null && _rich.Contains(npcId);
        }

        /// <summary>Günlük müşteri sayısı: <c>base + ⌈raf × oran⌉</c>, üst sınır <see cref="CountMax"/> (v0.2 10.4).</summary>
        public int ArrivalCount(int shelfItems)
        {
            int count = CountBase + (int)Math.Ceiling(CountPerShelfItem * shelfItems - 1e-9);
            return Math.Min(CountMax, count);
        }
    }
}
