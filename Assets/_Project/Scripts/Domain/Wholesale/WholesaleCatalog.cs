using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Esnaf.Domain.Wholesale
{
    /// <summary>
    /// Toptancı teklifleri (wholesale.json). <see cref="Offers"/> AKSESUAR tekliflerini (dosyadaki sırayla), <see cref="ProductOffers"/> ürün/telefon tekliflerini, <see cref="AllOffers"/> ikisini birden
    /// verir. Teklifler içerikten otomatik keşfedilir; belirli bir modele özel kod yoktur. Dosya yoksa <see cref="Empty"/>.
    /// </summary>
    public sealed class WholesaleCatalog
    {
        public static readonly WholesaleCatalog Empty = new WholesaleCatalog(new WholesaleOffer[0]);

        /// <summary>Aksesuar teklifleri (eski davranış: yalnızca aksesuarlar).</summary>
        public IReadOnlyList<WholesaleOffer> Offers { get; }

        /// <summary>Ürün (telefon modeli) teklifleri.</summary>
        public IReadOnlyList<WholesaleOffer> ProductOffers { get; }

        /// <summary>Tüm teklifler (aksesuar + ürün).</summary>
        public IReadOnlyList<WholesaleOffer> AllOffers { get; }

        public bool IsEmpty
        {
            get { return AllOffers.Count == 0; }
        }

        public WholesaleCatalog(IEnumerable<WholesaleOffer> offers)
        {
            if (offers == null)
            {
                throw new ArgumentNullException(nameof(offers));
            }

            var all = new List<WholesaleOffer>(offers);
            var accessories = new List<WholesaleOffer>();
            var products = new List<WholesaleOffer>();
            foreach (WholesaleOffer offer in all)
            {
                (offer.Kind == WholesaleItemKind.Accessory ? accessories : products).Add(offer);
            }

            AllOffers = new ReadOnlyCollection<WholesaleOffer>(all);
            Offers = new ReadOnlyCollection<WholesaleOffer>(accessories);
            ProductOffers = new ReadOnlyCollection<WholesaleOffer>(products);
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

        /// <summary>Toptancının bu ürün (telefon modeli) için teklifini bulur.</summary>
        public bool TryGetProduct(string supplierId, string productId, out WholesaleOffer offer)
        {
            foreach (WholesaleOffer candidate in ProductOffers)
            {
                if (candidate.SupplierId == supplierId && candidate.ProductId == productId)
                {
                    offer = candidate;
                    return true;
                }
            }

            offer = null;
            return false;
        }

        /// <summary>Verilen günde geçerli AKSESUAR teklifleri.</summary>
        public IReadOnlyList<WholesaleOffer> AvailableOn(int day)
        {
            return Filter(Offers, day);
        }

        /// <summary>Verilen günde geçerli ürün (telefon) teklifleri.</summary>
        public IReadOnlyList<WholesaleOffer> ProductOffersAvailableOn(int day)
        {
            return Filter(ProductOffers, day);
        }

        private static IReadOnlyList<WholesaleOffer> Filter(IReadOnlyList<WholesaleOffer> source, int day)
        {
            var list = new List<WholesaleOffer>();
            foreach (WholesaleOffer offer in source)
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
