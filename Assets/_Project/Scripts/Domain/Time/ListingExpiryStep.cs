using System;
using System.Collections.Generic;
using Esnaf.Core;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Market;
using Esnaf.Domain.Products;

namespace Esnaf.Domain.Time
{
    /// <summary>
    /// Gün sonu adım 4: tüm ilanların ömrü 1 azalır; ömrü biten ilanlar kaldırılır (GDD v0.2 Bölüm 1).
    /// İlanın ürünü hâlâ PAZAR konumundaysa depodan silinir (sahipsiz örnek kalmaz); dükkânın satın aldığı ürün silinmez.
    /// Ürünü alınmadan kalkan ilanın bekleyen ekspertiz ücreti, kalktığı günün defterine "boşa ekspertiz" gideri olarak yazılır (GDD v0.2 5.1).
    /// <see cref="ListingExpired"/> olayları tüm durum değişikliğinden SONRA, ilan sırasıyla yayınlanır.
    /// </summary>
    public sealed class ListingExpiryStep : IDayEndStep
    {
        private readonly MarketState _market;
        private readonly InstanceStore _store;
        private readonly EconomyService _economy;
        private readonly IEventBus _events;

        public string Id
        {
            get { return "listing_expiry"; }
        }

        public int Order
        {
            get { return DayEndOrder.ListingExpiry; }
        }

        public ListingExpiryStep(MarketState market, InstanceStore store, EconomyService economy, IEventBus events)
        {
            if (market == null)
            {
                throw new ArgumentNullException(nameof(market));
            }

            if (store == null)
            {
                throw new ArgumentNullException(nameof(store));
            }

            if (economy == null)
            {
                throw new ArgumentNullException(nameof(economy));
            }

            _market = market;
            _store = store;
            _economy = economy;
            _events = events;
        }

        public Result Execute(DayEndContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var expired = new List<MarketListing>();
            foreach (MarketListing listing in new List<MarketListing>(_market.Listings))
            {
                listing.RemainingDays -= 1;
                if (listing.RemainingDays <= 0)
                {
                    expired.Add(listing);
                }
            }

            foreach (MarketListing listing in expired)
            {
                _market.Remove(listing.ListingId);
                ProductInstance instance;
                if (_store.TryGet(listing.InstanceId, out instance) && instance.Location == ProductLocation.Market)
                {
                    // "appraisal.nothing_pending" normaldir (ekspertiz yapılmamış).
                    _economy.WriteOffAppraisals(listing.InstanceId, context.Day);
                    _store.Remove(listing.InstanceId);
                }

                context.ExpiredListingIds.Add(listing.ListingId);
            }

            if (_events != null)
            {
                foreach (MarketListing listing in expired)
                {
                    _events.Publish(new ListingExpired(listing.ListingId, listing.InstanceId));
                }
            }

            return Result.Ok();
        }
    }
}
