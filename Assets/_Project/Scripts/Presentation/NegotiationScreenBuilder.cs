using System.Collections.Generic;
using Esnaf.Core;
using Esnaf.Domain.Market;
using Esnaf.Domain.Negotiation;

namespace Esnaf.Presentation
{
    /// <summary>Pazarlık ekranının görünüm modelini IGameApi görünümlerinden kurar. Kural yoktur: yalnızca metne çevirir.</summary>
    internal static class NegotiationScreenBuilder
    {
        public static NegotiationScreenViewModel Build(
            NegotiationView view,
            ListingView listing,
            Money cash,
            string reply,
            Money? lastOffer,
            string offerText,
            int selectedCardIndex,
            ContentPresentation content)
        {
            var cards = new List<NegotiationCardRowViewModel>();
            for (int i = 0; i < view.Cards.Count; i++)
            {
                NegotiationCardView card = view.Cards[i];
                cards.Add(new NegotiationCardRowViewModel(
                    i,
                    TurkishTexts.Card(card.WordingKey, card.Attribute, card.Confidence, card.ProblemValue, card.IsUsed),
                    card.IsUsed,
                    i == selectedCardIndex));
            }

            return new NegotiationScreenViewModel(
                content.ModelName(listing.DefinitionId),
                TurkishTexts.Seller(content.NpcName(view.SellerNpcId)),
                TurkishTexts.AskingLine(listing.AskingPrice),
                TurkishTexts.ShownPriceLine(view.ShownPrice),
                TurkishTexts.CashLine(cash),
                TurkishTexts.Round(view.Round),
                TurkishTexts.Phase(view.Phase),
                TurkishTexts.MoodLine(view.Mood),
                TurkishTexts.PatienceLine(view.Patience),
                view.LastOfferInsulted ? TurkishTexts.Insulted : null,
                reply,
                lastOffer.HasValue ? TurkishTexts.YourOffer(lastOffer.Value) : null,
                offerText,
                cards,
                cards.Count == 0 ? TurkishTexts.NoCards : null,
                selectedCardIndex,
                view.Phase == NegotiationPhase.FinalOffer);
        }
    }
}
