using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Esnaf.Domain.Market
{
    /// <summary>Bildirim: yeni günün ilanları üretildi ve pazara eklendi.</summary>
    public sealed class ListingsGenerated
    {
        public int Day { get; }
        public IReadOnlyList<long> ListingIds { get; }

        public ListingsGenerated(int day, IEnumerable<long> listingIds)
        {
            if (listingIds == null)
            {
                throw new ArgumentNullException(nameof(listingIds));
            }

            Day = day;
            ListingIds = new ReadOnlyCollection<long>(new List<long>(listingIds));
        }
    }
}
