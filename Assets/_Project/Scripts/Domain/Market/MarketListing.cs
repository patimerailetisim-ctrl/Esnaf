using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Esnaf.Core;

namespace Esnaf.Domain.Market
{
    /// <summary>
    /// PAZAR İLANI (durum, kayda girer; GDD v0.3 4.3). Ürün örneğine yalnızca <see cref="InstanceId"/> ile bağlıdır.
    /// GİZLİ alanlar (<see cref="RejectPrice"/>, <see cref="BelievedValue"/>, fırsat/tuzak/jackpot işaretleri) yalnızca Domain içindir;
    /// UI'ya giden <c>ListingView</c> bunları taşımaz.
    /// </summary>
    public sealed class MarketListing
    {
        public long ListingId { get; }
        public long InstanceId { get; }
        public string SellerNpcId { get; }
        public Money AskingPrice { get; }

        /// <summary>Görünür etiketler: "box", "invoice", "urgent_sale".</summary>
        public IReadOnlyList<string> Tags { get; }

        public int DayListed { get; }

        /// <summary>Kalan ömür (gün); gün sonunda 1 azalır, 0'a inince ilan kalkar.</summary>
        public int RemainingDays { get; internal set; }

        /// <summary>Gizli: satıcının asla altına inmeyeceği fiyat (R).</summary>
        public Money RejectPrice { get; }

        /// <summary>Gizli: satıcının ürünün değeri sandığı tutar.</summary>
        public Money BelievedValue { get; }

        /// <summary>Gizli: makul fırsat (R ≤ oran × gerçek değer ve tuzak değil).</summary>
        public bool IsOpportunity { get; }

        /// <summary>Gizli: satıcı kusuru sakladı ve değeri ≥ oran × gerçek değer sanıyor.</summary>
        public bool IsTrap { get; }

        /// <summary>Gizli: ret oranı eşiğin altındaki satıcının ilanı (kotaya tabi).</summary>
        public bool IsJackpot { get; }

        /// <summary>Gün 1'in rehberli ilanı.</summary>
        public bool IsGuided { get; }

        public MarketListing(
            long listingId,
            long instanceId,
            string sellerNpcId,
            Money askingPrice,
            IEnumerable<string> tags,
            int dayListed,
            int remainingDays,
            Money rejectPrice,
            Money believedValue,
            bool isOpportunity,
            bool isTrap,
            bool isJackpot,
            bool isGuided)
        {
            if (tags == null)
            {
                throw new ArgumentNullException(nameof(tags));
            }

            ListingId = listingId;
            InstanceId = instanceId;
            SellerNpcId = sellerNpcId;
            AskingPrice = askingPrice;
            Tags = new ReadOnlyCollection<string>(new List<string>(tags));
            DayListed = dayListed;
            RemainingDays = remainingDays;
            RejectPrice = rejectPrice;
            BelievedValue = believedValue;
            IsOpportunity = isOpportunity;
            IsTrap = isTrap;
            IsJackpot = isJackpot;
            IsGuided = isGuided;
        }
    }
}
