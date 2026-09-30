namespace Esnaf.Domain.Negotiation
{
    /// <summary>Süren alış pazarlığı: ilan, satıcı ve motor durumu. Aynı anda en fazla bir pazarlık vardır.</summary>
    public sealed class ActiveNegotiation
    {
        public long ListingId { get; }
        public long InstanceId { get; }
        public string SellerNpcId { get; }
        public NegotiationState State { get; internal set; }

        /// <summary>Son teklif hakaret sayıldı mı (görünüm için).</summary>
        public bool LastOfferInsulted { get; internal set; }

        internal ActiveNegotiation(long listingId, long instanceId, string sellerNpcId, NegotiationState state)
        {
            ListingId = listingId;
            InstanceId = instanceId;
            SellerNpcId = sellerNpcId;
            State = state;
        }
    }

    /// <summary>Alış akışının durumu (kayda girer; Gün 9'da Capture/Restore bunu da kapsar).</summary>
    public sealed class TradeState
    {
        /// <summary>Süren pazarlık; yoksa null.</summary>
        public ActiveNegotiation Current { get; internal set; }
    }
}
