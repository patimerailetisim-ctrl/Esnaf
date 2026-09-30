using Esnaf.Core;

namespace Esnaf.Domain.Negotiation
{
    // Yalnızca BİLDİRİM (K5): durum önce değişir, olay sonra yayınlanır. Olaylar gizli pazarlık durumunu taşımaz.

    public sealed class NegotiationStarted
    {
        public long ListingId { get; }
        public long InstanceId { get; }
        public string SellerNpcId { get; }

        public NegotiationStarted(long listingId, long instanceId, string sellerNpcId)
        {
            ListingId = listingId;
            InstanceId = instanceId;
            SellerNpcId = sellerNpcId;
        }
    }

    public sealed class OfferMade
    {
        public long ListingId { get; }
        public int Round { get; }
        public Money Offer { get; }
        public Money ShownPrice { get; }
        public NegotiationPhase Phase { get; }
        public bool Insulted { get; }

        public OfferMade(long listingId, int round, Money offer, Money shownPrice, NegotiationPhase phase, bool insulted)
        {
            ListingId = listingId;
            Round = round;
            Offer = offer;
            ShownPrice = shownPrice;
            Phase = phase;
            Insulted = insulted;
        }
    }

    public sealed class ListingPurchased
    {
        public long ListingId { get; }
        public long InstanceId { get; }
        public string SellerNpcId { get; }
        public Money Price { get; }

        public ListingPurchased(long listingId, long instanceId, string sellerNpcId, Money price)
        {
            ListingId = listingId;
            InstanceId = instanceId;
            SellerNpcId = sellerNpcId;
            Price = price;
        }
    }

    public sealed class NegotiationEnded
    {
        public long ListingId { get; }
        public long InstanceId { get; }

        /// <summary>Deal ya da Failed.</summary>
        public NegotiationPhase Phase { get; }

        public Money DealPrice { get; }

        public NegotiationEnded(long listingId, long instanceId, NegotiationPhase phase, Money dealPrice)
        {
            ListingId = listingId;
            InstanceId = instanceId;
            Phase = phase;
            DealPrice = dealPrice;
        }
    }
}
