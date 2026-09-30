using System.Collections.Generic;
using Esnaf.Core;

namespace Esnaf.Domain.Negotiation
{
    /// <summary>
    /// Süren pazarlığın durumu (kayda girer). Güven, sabır ve ret fiyatı (R) GİZLİDİR: UI'ya giden görünümler bunları taşımaz.
    /// Değişiklik yalnızca <see cref="NegotiationEngine"/> içinden yapılır.
    /// </summary>
    public sealed class NegotiationState
    {
        private readonly List<string> _usedCards = new List<string>();

        public NegotiationSetup Setup { get; }
        public NegotiationPhase Phase { get; internal set; }

        /// <summary>Verilen teklif sayısı.</summary>
        public int Round { get; internal set; }

        public int Patience { get; internal set; }
        public int Trust { get; internal set; }

        /// <summary>Gizli: satıcının şu anki ret fiyatı (kartlar düşürür, asla artmaz).</summary>
        public double Reject { get; internal set; }

        /// <summary>Satıcının yuvarlanmamış fiyatı (kesin değer; gösterilen fiyat 10 TL'ye yuvarlanır).</summary>
        public double Price { get; internal set; }

        /// <summary>Satıcının şu anki, oyuncuya gösterilen fiyatı.</summary>
        public Money ShownPrice { get; internal set; }

        /// <summary>Anlaşma fiyatı; yalnızca <see cref="NegotiationPhase.Deal"/> aşamasında sıfırdan farklıdır.</summary>
        public Money DealPrice { get; internal set; }

        /// <summary>Bu pazarlıkta oynanmış kartların anahtarları (oynanış sırasıyla).</summary>
        public IReadOnlyList<string> UsedCards
        {
            get { return _usedCards; }
        }

        public bool IsOpen
        {
            get { return Phase == NegotiationPhase.Active || Phase == NegotiationPhase.FinalOffer; }
        }

        internal NegotiationState(NegotiationSetup setup)
        {
            Setup = setup;
            Phase = NegotiationPhase.Active;
            Patience = setup.Patience;
            Trust = setup.Trust;
            Reject = setup.Reject;
            Price = (double)setup.Ask.Tl;
            ShownPrice = setup.Ask;
            DealPrice = Money.Zero;
        }

        internal NegotiationState Clone()
        {
            var copy = new NegotiationState(Setup)
            {
                Phase = Phase,
                Round = Round,
                Patience = Patience,
                Trust = Trust,
                Reject = Reject,
                Price = Price,
                ShownPrice = ShownPrice,
                DealPrice = DealPrice
            };
            copy._usedCards.AddRange(_usedCards);
            return copy;
        }

        internal void MarkCardUsed(string key)
        {
            _usedCards.Add(key);
        }

        internal bool HasUsedCard(string key)
        {
            return _usedCards.Contains(key);
        }
    }
}
