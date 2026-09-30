using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Esnaf.Core;

namespace Esnaf.Domain.Negotiation
{
    /// <summary>
    /// UI'ya giden, DEĞİŞMEZ pazarlık görünümü (K3). Güven, sabır, ret fiyatı (R), taban ve satıcının ikna/aciliyet sayıları
    /// bu tipe ASLA eklenmez (bir test üyeleri denetler); oyuncu yalnızca ruh hali ve sabır KADEMESİNİ görür.
    /// </summary>
    public sealed class NegotiationView
    {
        public long ListingId { get; }
        public long InstanceId { get; }
        public string SellerNpcId { get; }
        public NegotiationPhase Phase { get; }
        public int Round { get; }

        /// <summary>Satıcının şu anki fiyatı.</summary>
        public Money ShownPrice { get; }

        /// <summary>Anlaşma fiyatı; yalnızca Deal aşamasında sıfırdan farklıdır.</summary>
        public Money DealPrice { get; }

        public NegotiationLevel Mood { get; }
        public NegotiationLevel Patience { get; }

        /// <summary>Son teklif satıcıyı gücendirdi mi ("hakaret").</summary>
        public bool LastOfferInsulted { get; }

        public IReadOnlyList<NegotiationCardView> Cards { get; }

        public NegotiationView(
            long listingId,
            long instanceId,
            string sellerNpcId,
            NegotiationPhase phase,
            int round,
            Money shownPrice,
            Money dealPrice,
            NegotiationLevel mood,
            NegotiationLevel patience,
            bool lastOfferInsulted,
            IEnumerable<NegotiationCardView> cards)
        {
            if (cards == null)
            {
                throw new ArgumentNullException(nameof(cards));
            }

            ListingId = listingId;
            InstanceId = instanceId;
            SellerNpcId = sellerNpcId;
            Phase = phase;
            Round = round;
            ShownPrice = shownPrice;
            DealPrice = dealPrice;
            Mood = mood;
            Patience = patience;
            LastOfferInsulted = lastOfferInsulted;
            Cards = new ReadOnlyCollection<NegotiationCardView>(new List<NegotiationCardView>(cards));
        }
    }
}
