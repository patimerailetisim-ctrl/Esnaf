using Esnaf.Core;

namespace Esnaf.Domain.Negotiation
{
    /// <summary>Bir satış turunun görünür sonucu.</summary>
    public sealed class SaleRound
    {
        public int Round { get; }
        public Money Ask { get; }

        /// <summary>Müşterinin bu turdan sonraki teklifi (anlaşmada = anlaşma fiyatı).</summary>
        public Money ShownPrice { get; }

        public NegotiationPhase Phase { get; }

        /// <summary>İstenen fiyat "çok pahalı" eşiğinin üstündeydi (hakaret).</summary>
        public bool TooExpensive { get; }

        public Money DealPrice { get; }

        public SaleRound(int round, Money ask, Money shownPrice, NegotiationPhase phase, bool tooExpensive, Money dealPrice)
        {
            Round = round;
            Ask = ask;
            ShownPrice = shownPrice;
            Phase = phase;
            TooExpensive = tooExpensive;
            DealPrice = dealPrice;
        }
    }
}
