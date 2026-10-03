using Esnaf.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Esnaf.App.Ui
{
    /// <summary>
    /// İlan Detayı ekranı (Gün 13.3): büyük telefon görseli, model, kondisyon, satıcı, istenen fiyat, tahmini değer, ekspertiz durumu; "Ekspertiz Yap", "Pazarlık Yap", "Satın Al" (mevcut BuyNow) ve "Geri".
    /// Yalnızca <see cref="UiFlow"/> ile konuşur (Detail, Back, OpenAppraisal, OpenNegotiation, BuyNow); oyun kuralı yoktur ve "kârlı/kötü ilan" kararı vermez.
    /// Görsel ve bilgiler kaydırmalı alandadır (küçük ekranda taşmaz); düğmeler altta sabittir.
    /// </summary>
    internal sealed class DetailView
    {
        private const float BackHeight = 120f;
        private const float StatusHeight = 90f;
        private const float ButtonsHeight = 290f;
        private const float ImageHeight = 560f;

        private static readonly Color Bg = new Color(0.09f, 0.1f, 0.13f, 1f);

        private readonly UiFlow _flow;
        private readonly GameObject _root;
        private readonly Text _title;
        private readonly RectTransform _content;
        private readonly Text _status;
        private readonly Text _buyLabel;
        private readonly Text _appraisalLabel;
        private readonly Text _negotiationLabel;
        private readonly Text _backLabel;

        public DetailView(Transform canvas, UiFlow flow)
        {
            _flow = flow;

            RectTransform root = UiBuilder.CreatePanel(canvas, "DetailScreen", Bg);
            _root = root.gameObject;
            UiBuilder.Stretch(root, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -TopBarView.Height));

            Button back = UiBuilder.CreateButton(root, "BackButton", TurkishTexts.BackButton, 44, new Color(0.3f, 0.33f, 0.42f, 1f), OnBackClicked);
            UiBuilder.Stretch(back.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0.4f, 1f), new Vector2(40f, -BackHeight - 20f), new Vector2(0f, -20f));
            _backLabel = back.GetComponentInChildren<Text>();

            _title = UiBuilder.CreateWrappedText(root, "Title", 60, TextAnchor.MiddleLeft, Color.white);
            _title.fontStyle = FontStyle.Bold;
            UiBuilder.Stretch(_title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -BackHeight - 150f), new Vector2(-40f, -BackHeight - 40f));

            // Kaydırmalı bilgi alanı (görsel + satırlar).
            _content = UiBuilder.CreateVerticalList(root, "Info", Bg, 14f, 24f);
            RectTransform list = _content.parent as RectTransform;
            UiBuilder.Stretch(list, Vector2.zero, Vector2.one, new Vector2(0f, ButtonsHeight + StatusHeight + 10f), new Vector2(0f, -BackHeight - 160f));

            _status = UiBuilder.CreateWrappedText(root, "Status", 38, TextAnchor.MiddleCenter, new Color(1f, 0.75f, 0.45f, 1f));
            UiBuilder.Stretch(_status.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(40f, ButtonsHeight), new Vector2(-40f, ButtonsHeight + StatusHeight));

            Button appraisal = UiBuilder.CreateButton(root, "AppraisalButton", TurkishTexts.DoAppraisalButton, 46, new Color(0.25f, 0.5f, 0.45f, 1f), OnAppraisalClicked);
            UiBuilder.Stretch(appraisal.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(40f, 25f), new Vector2(-15f, 145f));
            _appraisalLabel = appraisal.GetComponentInChildren<Text>();

            Button negotiation = UiBuilder.CreateButton(root, "NegotiationButton", TurkishTexts.DoNegotiationButton, 46, new Color(0.7f, 0.45f, 0.2f, 1f), OnNegotiationClicked);
            UiBuilder.Stretch(negotiation.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(15f, 25f), new Vector2(-40f, 145f));
            _negotiationLabel = negotiation.GetComponentInChildren<Text>();

            Button buy = UiBuilder.CreateButton(root, "BuyButton", TurkishTexts.BuyNowLabel(Esnaf.Core.Money.Zero), 48, new Color(0.2f, 0.55f, 0.3f, 1f), OnBuyClicked);
            UiBuilder.Stretch(buy.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(40f, 160f), new Vector2(-40f, 270f));
            _buyLabel = buy.GetComponentInChildren<Text>();
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
            _status.text = _flow.StatusMessage ?? string.Empty;
            _buyLabel.text = detail.BuyButtonText;
            _appraisalLabel.text = detail.AppraisalButtonText;
            _negotiationLabel.text = detail.NegotiationButtonText;
            _backLabel.text = detail.BackButtonText;

            UiKit.Clear(_content); // Play Mode'da Destroy, Edit Mode'da (testler) DestroyImmediate

            // Büyük telefon görseli (maskeli, taşmaz).
            RectTransform image = UiBuilder.CreatePanel(_content, "PhoneImage", new Color(0.13f, 0.15f, 0.2f, 1f));
            image.GetComponent<Image>().raycastTarget = false;
            var imageLayout = image.gameObject.AddComponent<LayoutElement>();
            imageLayout.minHeight = ImageHeight;
            imageLayout.preferredHeight = ImageHeight;
            image.gameObject.AddComponent<RectMask2D>();
            var phone = new GameObject("Phone", typeof(RectTransform)).GetComponent<RectTransform>();
            phone.SetParent(image, false);
            phone.anchorMin = new Vector2(0.5f, 0.5f);
            phone.anchorMax = new Vector2(0.5f, 0.5f);
            phone.sizeDelta = new Vector2(700f, 700f);
            phone.localScale = new Vector3(0.78f, 0.78f, 1f);
            PhoneMockView.Draw(phone, detail.DefinitionId, PhoneAngle.Front);

            AddLine("Condition", detail.ConditionLine, 38, new Color(0.88f, 0.9f, 0.95f, 1f), false, 110f);
            AddLine("Seller", detail.SellerLine, 40, new Color(0.88f, 0.9f, 0.95f, 1f), false, 60f);
            AddLine("Asking", detail.AskingLine, 48, new Color(1f, 0.85f, 0.4f, 1f), true, 70f);
            AddLine("Estimated", detail.EstimatedLine, 44, new Color(0.55f, 0.9f, 0.6f, 1f), false, 64f);
            AddLine("EstimatedHint", detail.EstimatedHint, 30, new Color(0.65f, 0.68f, 0.75f, 1f), false, 90f);
            AddLine("AppraisalStatus", detail.AppraisalStatusLine, 40, new Color(0.88f, 0.9f, 0.95f, 1f), false, 64f);
            if (!string.IsNullOrEmpty(detail.AppraisalRangeLine))
            {
                AddLine("AppraisalRange", detail.AppraisalRangeLine, 38, new Color(0.55f, 0.9f, 0.6f, 1f), false, 64f);
            }

            AddLine("Remaining", detail.RemainingLine, 36, new Color(0.7f, 0.72f, 0.78f, 1f), false, 56f);
        }

        private void AddLine(string name, string text, int size, Color color, bool bold, float height)
        {
            Text label = UiBuilder.CreateWrappedText(_content, name, size, TextAnchor.MiddleLeft, color);
            label.text = text;
            if (bold)
            {
                label.fontStyle = FontStyle.Bold;
            }

            var layout = label.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
        }

        private void OnBackClicked()
        {
            _flow.Back();
        }

        private void OnAppraisalClicked()
        {
            _flow.OpenAppraisal();
        }

        private void OnBuyClicked()
        {
            _flow.BuyNow();
        }

        private void OnNegotiationClicked()
        {
            _flow.OpenNegotiation();
        }
    }
}
