using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Esnaf.Domain.Wholesale
{
    /// <summary>Toptancı teklifleri (wholesale.json), dosyadaki sırayla düz liste. Dosya yoksa <see cref="Empty"/>. Satın alma servisi bu adımda yoktur.</summary>
    public sealed class WholesaleCatalog
    {
        public static readonly WholesaleCatalog Empty = new WholesaleCatalog(new WholesaleOffer[0]);

        public IReadOnlyList<WholesaleOffer> Offers { get; }

        public bool IsEmpty
        {
            get { return Offers.Count == 0; }
        }

        public WholesaleCatalog(IEnumerable<WholesaleOffer> offers)
        {
            if (offers == null)
            {
                throw new ArgumentNullException(nameof(offers));
            }

            Offers = new ReadOnlyCollection<WholesaleOffer>(new List<WholesaleOffer>(offers));
        }

        public bool TryGet(string supplierId, string accessoryId, out WholesaleOffer offer)
        {
            foreach (WholesaleOffer candidate in Offers)
            {
                if (candidate.SupplierId == supplierId && candidate.AccessoryId == accessoryId)
                {
                    offer = candidate;
                    return true;
                }
            }

            offer = null;
            return false;
        }

        /// <summary>Verilen günde geçerli teklifler.</summary>
        public IReadOnlyList<WholesaleOffer> AvailableOn(int day)
        {
            var list = new List<WholesaleOffer>();
            foreach (WholesaleOffer offer in Offers)
            {
                if (offer.AvailableFromDay <= day)
                {
                    list.Add(offer);
                }
            }

            return list;
        }
    }
}
