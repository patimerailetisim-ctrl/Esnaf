using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Esnaf.Core;

namespace Esnaf.Domain.Market
{
    /// <summary>
    /// UI'ya giden, DEĞİŞMEZ ilan görünümü (K3: sorgu = salt okunur kopya). Yalnızca oyuncunun görebileceği bilgiyi taşır;
    /// gizli alanlar (ret fiyatı, satıcının inandığı değer, fırsat/tuzak/jackpot işaretleri, niteliklerin gerçek değerleri)
    /// bu tipe ASLA eklenmez (bir test üyeleri denetler).
    /// </summary>
    public sealed class ListingView
    {
        public long ListingId { get; }
        public long InstanceId { get; }
        public string DefinitionId { get; }
        public int StorageGb { get; }
        public int AgeMonths { get; }
        public bool HasBox { get; }
        public bool HasInvoice { get; }
        public string SellerNpcId { get; }
        public Money AskingPrice { get; }
        public int DayListed { get; }
        public int RemainingDays { get; }
        public IReadOnlyList<string> Tags { get; }

        public ListingView(
            long listingId,
            long instanceId,
            string definitionId,
            int storageGb,
            int ageMonths,
            bool hasBox,
            bool hasInvoice,
            string sellerNpcId,
            Money askingPrice,
            int dayListed,
            int remainingDays,
            IEnumerable<string> tags)
        {
            if (tags == null)
            {
                throw new ArgumentNullException(nameof(tags));
            }

            ListingId = listingId;
            InstanceId = instanceId;
            DefinitionId = definitionId;
            StorageGb = storageGb;
            AgeMonths = ageMonths;
            HasBox = hasBox;
            HasInvoice = hasInvoice;
            SellerNpcId = sellerNpcId;
            AskingPrice = askingPrice;
            DayListed = dayListed;
            RemainingDays = remainingDays;
            Tags = new ReadOnlyCollection<string>(new List<string>(tags));
        }
    }
}
