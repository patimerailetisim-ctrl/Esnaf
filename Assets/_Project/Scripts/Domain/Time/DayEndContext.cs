using System;
using System.Collections.Generic;
using Esnaf.Core;

namespace Esnaf.Domain.Time
{
    /// <summary>Bir "Günü Bitir" çalışmasının ortak veri torbası: hangi gün bitiyor ve adımların ürettiği sonuçlar.</summary>
    public sealed class DayEndContext
    {
        /// <summary>Biten gün.</summary>
        public int Day { get; }

        /// <summary>Yeni gün (adım 7 doldurur; öncesinde 0).</summary>
        public int NewDay { get; set; }

        /// <summary>Adım 2: o gün işlenen günlük gider (0 olabilir).</summary>
        public Money ExpenseCharged { get; set; }

        /// <summary>Adım 4: süresi dolup kaldırılan ilanlar (ilan sırasıyla).</summary>
        public IList<long> ExpiredListingIds { get; } = new List<long>();

        /// <summary>Adım 7: yeni günün ilanları.</summary>
        public IList<long> NewListingIds { get; } = new List<long>();

        /// <summary>Başarıyla tamamlanan adımlar, çalışma sırasıyla (boru hattı doldurur).</summary>
        public IList<string> ExecutedStepIds { get; } = new List<string>();

        public DayEndContext(int day)
        {
            if (day < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(day), "Day must be at least 1.");
            }

            Day = day;
        }
    }
}
