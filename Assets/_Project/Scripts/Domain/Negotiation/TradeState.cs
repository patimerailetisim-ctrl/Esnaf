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

    /// <summary>Süren satış pazarlığı: müşteri, ürün ve motor durumu.</summary>
    public sealed class ActiveSale
    {
        public long CustomerId { get; }
        public string NpcId { get; }
        public long InstanceId { get; }
        public SaleState State { get; internal set; }

        /// <summary>Son istenen fiyat "çok pahalı" sayıldı mı (görünüm için).</summary>
        public bool LastAskTooExpensive { get; internal set; }

        internal ActiveSale(long customerId, string npcId, long instanceId, SaleState state)
        {
            CustomerId = customerId;
            NpcId = npcId;
            InstanceId = instanceId;
            State = state;
        }
    }

    /// <summary>Alış akışının durumu (kayda girer; Gün 9'da Capture/Restore bunu da kapsar).</summary>
    public sealed class TradeState
    {
        /// <summary>Süren pazarlık; yoksa null.</summary>
        public ActiveNegotiation Current { get; internal set; }

        /// <summary>Süren satış pazarlığı; yoksa null. Alış ve satış birlikte en fazla bir pazarlık olabilir.</summary>
        public ActiveSale CurrentSale { get; internal set; }

        /// <summary>Alış ya da satış pazarlığı sürüyor (gün bitirilemez, yenisi başlatılamaz).</summary>
        public bool IsBusy
        {
            get { return Current != null || CurrentSale != null; }
        }
    }
}
