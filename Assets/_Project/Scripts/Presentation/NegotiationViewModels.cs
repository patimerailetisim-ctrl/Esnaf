using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Esnaf.Presentation
{
    /// <summary>Koz kartı satırı (NegotiationView kartlarından). IsUsed API'den gelir; seçim yalnızca ekran durumudur.</summary>
    public sealed class NegotiationCardRowViewModel
    {
        public int Index { get; }
        public string Text { get; }
        public bool IsUsed { get; }
        public bool IsSelected { get; }

        public NegotiationCardRowViewModel(int index, string text, bool isUsed, bool isSelected)
        {
            Index = index;
            Text = text;
            IsUsed = isUsed;
            IsSelected = isSelected;
        }
    }

    /// <summary>
    /// Pazarlık ekranının hazır Türkçe satırları. Hepsi IGameApi görünümünden (NegotiationView, nakit, ilan) türer; satıcının cevabı
    /// yalnızca dönen fiyatın önceki fiyatla karşılaştırmasından anlatılır (karar ve matematik oyundadır).
    /// </summary>
    public sealed class NegotiationScreenViewModel
    {
        public string Title { get; }
        public string SellerLine { get; }
        public string AskingLine { get; }
        public string ShownLine { get; }
        public string CashLine { get; }
        public string RoundLine { get; }
        public string PhaseText { get; }
        public string MoodLine { get; }
        public string PatienceLine { get; }
        public string InsultLine { get; }
        public string ReplyLine { get; }
        public string YourOfferLine { get; }
        public string OfferText { get; }
        public IReadOnlyList<NegotiationCardRowViewModel> Cards { get; }
        public string CardsNote { get; }
        public int SelectedCardIndex { get; }
        public bool CanAcceptFinal { get; }

        public NegotiationScreenViewModel(
            string title,
            string sellerLine,
            string askingLine,
            string shownLine,
            string cashLine,
            string roundLine,
            string phaseText,
            string moodLine,
            string patienceLine,
            string insultLine,
            string replyLine,
            string yourOfferLine,
            string offerText,
            IEnumerable<NegotiationCardRowViewModel> cards,
            string cardsNote,
            int selectedCardIndex,
            bool canAcceptFinal)
        {
            Title = title;
            SellerLine = sellerLine;
            AskingLine = askingLine;
            ShownLine = shownLine;
            CashLine = cashLine;
            RoundLine = roundLine;
            PhaseText = phaseText;
            MoodLine = moodLine;
            PatienceLine = patienceLine;
            InsultLine = insultLine;
            ReplyLine = replyLine;
            YourOfferLine = yourOfferLine;
            OfferText = offerText;
            Cards = new ReadOnlyCollection<NegotiationCardRowViewModel>(new List<NegotiationCardRowViewModel>(cards));
            CardsNote = cardsNote;
            SelectedCardIndex = selectedCardIndex;
            CanAcceptFinal = canAcceptFinal;
        }
    }
}
