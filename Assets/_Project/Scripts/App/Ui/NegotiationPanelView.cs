using Esnaf.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Esnaf.App.Ui
{
    /// <summary>
    /// Pazarlık ekranı: bilgi satırları ve satıcının cevabı (kaydırmalı), koz kartları, teklif kutusu (+/- düğmeleri), "Teklif Ver",
    /// "Koz Kullan", "Son Fiyatı Kabul Et", "Vazgeç" ve "Geri". Yalnızca <see cref="UiFlow"/> ile konuşur; pazarlık kuralı, para hesabı
    /// ve NPC kararı yoktur: hepsi IGameApi'den gelir. Uzun satırlar sarılır (Wrap).
    /// </summary>
    internal sealed class NegotiationPanelView
    {
        private const float BackHeight = 120f;
        private const float StatusHeight = 80f;
        private const float OfferRowHeight = 100f;
        private const float ButtonRowHeight = 110f;
        private const float Gap = 10f;

        private static readonly Color RowColor = new Color(0.18f, 0.2f, 0.26f, 1f);
        private static readonly Color SelectedRowColor = new Color(0.2f, 0.4f, 0.65f, 1f);
        private static readonly Color UsedRowColor = new Color(0.14f, 0.15f, 0.18f, 1f);

        private readonly UiFlow _flow;
        private readonly GameObject _root;
        private readonly Text _title;
        private readonly RectTransform _content;
        private readonly Text _status;
        private readonly InputField _offer;
        private readonly Image _acceptImage;

        public NegotiationPanelView(Transform canvas, UiFlow flow)
        {
            _flow = flow;

            RectTransform root = UiBuilder.CreatePanel(canvas, "NegotiationScreen", new Color(0.09f, 0.1f, 0.13f, 1f));
            _root = root.gameObject;
            UiBuilder.Stretch(root, Vector2.zero, Vector2.one, new Vector2(0f, NavBarView.Height), new Vector2(0f, -TopBarView.Height));

            Button back = UiBuilder.CreateButton(root, "BackButton", TurkishTexts.BackButton, 44, new Color(0.3f, 0.33f, 0.42f, 1f), () => _flow.Back());
            UiBuilder.Stretch(back.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0.3f, 1f), new Vector2(40f, -BackHeight - 20f), new Vector2(0f, -20f));

            _title = UiBuilder.CreateText(root, "Title", 48, TextAnchor.MiddleLeft, Color.white);
            UiBuilder.Stretch(_title.rectTransform, new Vector2(0.3f, 1f), new Vector2(1f, 1f), new Vector2(30f, -BackHeight - 20f), new Vector2(-40f, -20f));

            float y = 20f;
            Button accept = null;
            Button walk = null;
            Button offerButton = null;
            Button cardButton = null;

            accept = UiBuilder.CreateButton(root, "AcceptFinalButton", TurkishTexts.AcceptFinalButton, 40, new Color(0.25f, 0.5f, 0.45f, 1f), () => _flow.AcceptFinalPrice());
            UiBuilder.Stretch(accept.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(40f, y), new Vector2(-Gap, y + ButtonRowHeight));
            _acceptImage = accept.GetComponent<Image>();
            walk = UiBuilder.CreateButton(root, "WalkAwayButton", TurkishTexts.WalkAwayButton, 40, new Color(0.7f, 0.3f, 0.25f, 1f), () => _flow.WalkAway());
            UiBuilder.Stretch(walk.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(Gap, y), new Vector2(-40f, y + ButtonRowHeight));

            y += ButtonRowHeight + Gap;
            offerButton = UiBuilder.CreateButton(root, "MakeOfferButton", TurkishTexts.MakeOfferButton, 44, new Color(0.2f, 0.45f, 0.7f, 1f), () => _flow.SubmitOffer());
            UiBuilder.Stretch(offerButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(40f, y), new Vector2(-Gap, y + ButtonRowHeight));
            cardButton = UiBuilder.CreateButton(root, "UseCardButton", TurkishTexts.UseCardButton, 44, new Color(0.6f, 0.4f, 0.7f, 1f), () => _flow.SubmitCardOffer());
            UiBuilder.Stretch(cardButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(Gap, y), new Vector2(-40f, y + ButtonRowHeight));

            y += ButtonRowHeight + Gap;
            AddStepper(root, "Minus100", "-100", 0.00f, 0.15f, y, -100);
            AddStepper(root, "Minus10", "-10", 0.15f, 0.30f, y, -10);
            _offer = UiBuilder.CreateIntegerField(root, "OfferInput", TurkishTexts.OfferLabel, 46, text => _flow.SetOfferText(text));
            UiBuilder.Stretch(_offer.GetComponent<RectTransform>(), new Vector2(0.30f, 0f), new Vector2(0.70f, 0f), new Vector2(0f, y), new Vector2(0f, y + OfferRowHeight));
            AddStepper(root, "Plus10", "+10", 0.70f, 0.85f, y, 10);
            AddStepper(root, "Plus100", "+100", 0.85f, 1.00f, y, 100);

            y += OfferRowHeight + Gap;
            _status = UiBuilder.CreateWrappedText(root, "Status", 36, TextAnchor.MiddleCenter, new Color(1f, 0.75f, 0.45f, 1f));
            UiBuilder.Stretch(_status.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(40f, y), new Vector2(-40f, y + StatusHeight));

            y += StatusHeight + Gap;
            _content = UiBuilder.CreateVerticalList(root, "List", new Color(0.09f, 0.1f, 0.13f, 1f), 12f, 24f);
            RectTransform list = _content.parent as RectTransform;
            UiBuilder.Stretch(list, Vector2.zero, Vector2.one, new Vector2(0f, y), new Vector2(0f, -BackHeight - 30f));
        }

        private void AddStepper(RectTransform root, string name, string label, float from, float to, float y, int delta)
        {
            Button button = UiBuilder.CreateButton(root, name, label, 38, new Color(0.3f, 0.33f, 0.42f, 1f), () => _flow.AdjustOffer(delta));
            float left = from <= 0f ? 40f : 4f;
            float right = to >= 1f ? -40f : -4f;
            UiBuilder.Stretch(button.GetComponent<RectTransform>(), new Vector2(from, 0f), new Vector2(to, 0f), new Vector2(left, y), new Vector2(right, y + OfferRowHeight));
        }

        public void Show()
        {
            NegotiationScreenViewModel screen = _flow.NegotiationScreen;
            bool visible = _flow.CurrentScreen == UiScreen.Negotiation && screen != null;
            _root.SetActive(visible);
            if (!visible)
            {
                return;
            }

            _title.text = TurkishTexts.NegotiationTitle + " — " + screen.Title;
            _status.text = _flow.StatusMessage ?? string.Empty;
            if (_offer.text != screen.OfferText)
            {
                _offer.SetTextWithoutNotify(screen.OfferText);
            }

            _acceptImage.color = screen.CanAcceptFinal ? new Color(0.25f, 0.6f, 0.45f, 1f) : new Color(0.2f, 0.28f, 0.27f, 1f);

            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(_content.GetChild(i).gameObject);
            }

            AddLine(screen.SellerLine, 40, new Color(0.88f, 0.9f, 0.95f, 1f));
            AddLine(screen.AskingLine, 40, new Color(0.88f, 0.9f, 0.95f, 1f));
            AddLine(screen.ShownLine, 44, new Color(1f, 0.9f, 0.5f, 1f));
            AddLine(screen.CashLine, 40, new Color(0.55f, 0.95f, 0.6f, 1f));
            AddLine(screen.RoundLine + "   •   " + screen.PhaseText, 38, new Color(0.7f, 0.85f, 1f, 1f));
            AddLine(screen.MoodLine + "   •   " + screen.PatienceLine, 36, new Color(0.8f, 0.82f, 0.88f, 1f));
            AddLine(screen.ReplyLine, 42, Color.white);
            if (screen.YourOfferLine != null)
            {
                AddLine(screen.YourOfferLine, 38, new Color(0.8f, 0.82f, 0.88f, 1f));
            }

            if (screen.InsultLine != null)
            {
                AddLine(screen.InsultLine, 38, new Color(1f, 0.55f, 0.5f, 1f));
            }

            AddLine(TurkishTexts.CardsHeader, 42, new Color(0.7f, 0.85f, 1f, 1f));
            if (screen.CardsNote != null)
            {
                AddLine(screen.CardsNote, 36, new Color(0.8f, 0.82f, 0.88f, 1f));
            }

            foreach (NegotiationCardRowViewModel card in screen.Cards)
            {
                AddCard(card);
            }
        }

        private void AddLine(string text, int fontSize, Color color)
        {
            Text line = UiBuilder.CreateWrappedText(_content, "Line", fontSize, TextAnchor.MiddleLeft, color);
            line.text = text;
            line.gameObject.AddComponent<LayoutElement>().minHeight = fontSize + 24f;
        }

        private void AddCard(NegotiationCardRowViewModel card)
        {
            int index = card.Index;
            Color color = card.IsSelected ? SelectedRowColor : (card.IsUsed ? UsedRowColor : RowColor);
            Button button = UiBuilder.CreateButton(_content, "Card_" + index, string.Empty, 36, color, () => _flow.SelectCard(index));
            var layout = button.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 150f;
            layout.preferredHeight = 150f;

            Text label = button.GetComponentInChildren<Text>();
            label.alignment = TextAnchor.MiddleLeft;
            label.fontSize = 34;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.text = card.Text;
            UiBuilder.Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(24f, 0f), new Vector2(-24f, 0f));
        }
    }
}
