using Esnaf.Core;

namespace Esnaf.Domain.Negotiation
{
    /// <summary>Bir teklif turunun görünür sonucu.</summary>
    public sealed class RoundResult
    {
        public int Round { get; }
        public Money Offer { get; }

        /// <summary>Satıcının bu turdan sonraki fiyatı (anlaşmada = anlaşma fiyatı).</summary>
        public Money ShownPrice { get; }

        public NegotiationPhase Phase { get; }

        /// <summary>Teklif "hakaret" eşiğinin altındaydı.</summary>
        public bool Insulted { get; }

        public CardOutcome CardOutcome { get; }

        /// <summary>Anlaşıldıysa anlaşma fiyatı; değilse sıfır.</summary>
        public Money DealPrice { get; }

        public RoundResult(int round, Money offer, Money shownPrice, NegotiationPhase phase, bool insulted, CardOutcome cardOutcome, Money dealPrice)
        {
            Round = round;
            Offer = offer;
            ShownPrice = shownPrice;
            Phase = phase;
            Insulted = insulted;
            CardOutcome = cardOutcome;
            DealPrice = dealPrice;
        }
    }
}
