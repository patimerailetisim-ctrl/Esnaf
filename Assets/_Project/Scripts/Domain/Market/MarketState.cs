using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Esnaf.Domain.Market
{
    /// <summary>Şu an pazarda olan ilanların deposu (GDD v0.3 4.3). Ekleme sırası korunur; düz veri sınıfıdır.</summary>
    public sealed class MarketState
    {
        private readonly List<MarketListing> _listings = new List<MarketListing>();
        private readonly Dictionary<long, MarketListing> _byId = new Dictionary<long, MarketListing>();
        private readonly ReadOnlyCollection<MarketListing> _view;

        public MarketState()
        {
            _view = new ReadOnlyCollection<MarketListing>(_listings);
        }

        public int Count
        {
            get { return _listings.Count; }
        }

        public IReadOnlyList<MarketListing> Listings
        {
            get { return _view; }
        }

        public void Add(MarketListing listing)
        {
            if (listing == null)
            {
                throw new ArgumentNullException(nameof(listing));
            }

            if (_byId.ContainsKey(listing.ListingId))
            {
                throw new ArgumentException("Listing " + listing.ListingId + " is already on the market.", nameof(listing));
            }

            _byId.Add(listing.ListingId, listing);
            _listings.Add(listing);
        }

        public bool TryGet(long listingId, out MarketListing listing)
        {
            return _byId.TryGetValue(listingId, out listing);
        }

        /// <returns>İlan varsa silinir ve true; yoksa false.</returns>
        public bool Remove(long listingId)
        {
            MarketListing listing;
            if (!_byId.TryGetValue(listingId, out listing))
            {
                return false;
            }

            _byId.Remove(listingId);
            _listings.Remove(listing);
            return true;
        }
    }
}
