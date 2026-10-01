using Esnaf.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Esnaf.App.Ui
{
    /// <summary>
    /// Telefon Detayı ekranı: seçili ilanın bilgileri, "Geri", "Ekspertiz" ve "Pazarlık" düğmeleri ve durum mesajı.
    /// Yalnızca <see cref="UiFlow"/> ile konuşur (Detail, Back, OpenAppraisal, RequestNegotiation); oyun kuralı yoktur.
    /// Ekspertiz düğmesi Ekspertiz ekranını açar; pazarlık sonraki adımda UiFlow içinde uygulanır (düğme hazırdır).
    /// </summary>
    internal sealed class DetailView
    {
        private const float BackHeight = 120f;
        private const float StatusHeight = 90f;
        private const float ButtonsHeight = 170f;

        private readonly UiFlow _flow;
        private readonly GameObject _root;
        private readonly Text _title;
        private readonly Text _lines;
        private readonly Text _status;

        public DetailView(Transform canvas, UiFlow flow)
        {
            _flow = flow;

            RectTransform root = UiBuilder.CreatePanel(canvas, "DetailScreen", new Color(0.09f, 0.1f, 0.13f, 1f));
            _root = root.gameObject;
            UiBuilder.Stretch(root, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -TopBarView.Height));

            Button back = UiBuilder.CreateButton(root, "BackButton", TurkishTexts.BackButton, 44, new Color(0.3f, 0.33f, 0.42f, 1f), OnBackClicked);
            UiBuilder.Stretch(back.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0.4f, 1f), new Vector2(40f, -BackHeight - 20f), new Vector2(0f, -20f));

            _title = UiBuilder.CreateText(root, "Title", 64, TextAnchor.MiddleLeft, Color.white);
            UiBuilder.Stretch(_title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -BackHeight - 170f), new Vector2(-40f, -BackHeight - 40f));

            _lines = UiBuilder.CreateText(root, "Lines", 46, TextAnchor.UpperLeft, new Color(0.88f, 0.9f, 0.95f, 1f));
            _lines.lineSpacing = 1.5f;
            UiBuilder.Stretch(_lines.rectTransform, Vector2.zero, Vector2.one, new Vector2(40f, ButtonsHeight + StatusHeight + 20f), new Vector2(-40f, -BackHeight - 200f));

            _status = UiBuilder.CreateText(root, "Status", 40, TextAnchor.MiddleCenter, new Color(1f, 0.75f, 0.45f, 1f));
            UiBuilder.Stretch(_status.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(40f, ButtonsHeight), new Vector2(-40f, ButtonsHeight + StatusHeight));

            Button appraisal = UiBuilder.CreateButton(root, "AppraisalButton", TurkishTexts.AppraisalButton, 50, new Color(0.25f, 0.5f, 0.45f, 1f), OnAppraisalClicked);
            UiBuilder.Stretch(appraisal.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(40f, 25f), new Vector2(-15f, ButtonsHeight - 25f));

            Button negotiation = UiBuilder.CreateButton(root, "NegotiationButton", TurkishTexts.NegotiationButton, 50, new Color(0.7f, 0.45f, 0.2f, 1f), OnNegotiationClicked);
            UiBuilder.Stretch(negotiation.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(15f, 25f), new Vector2(-40f, ButtonsHeight - 25f));
        }

        public void Show()
        {
            bool visible = _flow.CurrentScreen == UiScreen.Detail && _flow.Detail != null;
            _root.SetActive(visible);
            if (!visible)
            {
                return;
            }

            ListingDetailViewModel detail = _flow.Detail;
            _title.text = detail.Title;
            _lines.text = detail.StorageLine + "\n" + detail.AgeLine + "\n" + detail.PriceLine + "\n"
                + detail.BoxLine + "\n" + detail.InvoiceLine + "\n" + detail.SellerLine + "\n" + detail.RemainingLine;
            _status.text = _flow.StatusMessage ?? string.Empty;
        }

        private void OnBackClicked()
        {
            _flow.Back();
        }

        private void OnAppraisalClicked()
        {
            _flow.OpenAppraisal();
        }

        private void OnNegotiationClicked()
        {
            _flow.RequestNegotiation();
        }
    }
}
