using Esnaf.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Esnaf.App.Ui
{
    /// <summary>
    /// Toptancı ekranı (Kârbaz görsel dili): üstte toptancı adı, nakit ve aksesuar stok durumu; her teklif bir kart (ad, paket, birim maliyet, paket fiyatı, Satın Al).
    /// Kilitli teklifte düğme devre dışıdır ve "Gün 3'te açılır" der. Yalnızca <see cref="UiFlow"/> ile konuşur (WholesaleScreen, BuyWholesalePack,
    /// OpenAccessoryStock, Back); satın alma kuralı yoktur. İçerik her değişimde yeniden kurulur.
    /// </summary>
    internal sealed class WholesalePanelView
    {
        private const float HeaderHeight = 200f;
        private const float Gutter = 32f;

        private readonly UiFlow _flow;
        private readonly GameObject _root;
        private readonly Text _kicker;
        private readonly Text _title;
        private readonly Text _info;
        private readonly RectTransform _content;

        public WholesalePanelView(Transform canvas, UiFlow flow)
        {
            _flow = flow;

            RectTransform root = UiBuilder.CreatePanel(canvas, "WholesaleScreen", UiTheme.Background);
            _root = root.gameObject;
            UiBuilder.Stretch(root, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -TopBarView.Height));

            _content = UiBuilder.CreateVerticalList(root, "Body", UiTheme.Background, 24f, Gutter);
            RectTransform body = _content.parent as RectTransform;
            UiBuilder.Stretch(body, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -HeaderHeight));

            RectTransform header = UiBuilder.CreatePanel(root, "Header", UiTheme.Background);
            UiBuilder.Stretch(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -HeaderHeight), Vector2.zero);

            Button back = UiKit.RoundedButton(header, "BackButton", TurkishTexts.BackButton, 40, UiTheme.Card, UiTheme.Ink, () => _flow.Back());
            UiKit.AddShadow(back.gameObject, 0.08f, 6f);
            UiBuilder.Stretch(back.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Gutter, -50f), new Vector2(Gutter + 190f, 50f));

            _kicker = UiBuilder.CreateText(header, "Kicker", 36, TextAnchor.MiddleLeft, UiTheme.Muted);
            _kicker.fontStyle = FontStyle.Bold;
            UiBuilder.Stretch(_kicker.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(Gutter + 230f, 40f), new Vector2(-Gutter, 84f));

            _title = UiBuilder.CreateText(header, "Title", 62, TextAnchor.MiddleLeft, UiTheme.Ink);
            _title.fontStyle = FontStyle.Bold;
            UiBuilder.Stretch(_title.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(Gutter + 230f, -28f), new Vector2(-Gutter, 44f));

            _info = UiBuilder.CreateText(header, "Info", 38, TextAnchor.MiddleLeft, UiTheme.Muted);
            UiBuilder.Stretch(_info.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(Gutter + 230f, -80f), new Vector2(-Gutter, -26f));
        }

        public void Show()
        {
            WholesaleScreenViewModel screen = _flow.WholesaleScreen;
            bool visible = _flow.CurrentScreen == UiScreen.Wholesale && screen != null;
            _root.SetActive(visible);
            if (!visible)
            {
                return;
            }

            UiKit.Clear(_content);
            _kicker.text = screen.Title;
            _title.text = screen.SupplierName;
            _info.text = screen.CashLine + "  •  " + screen.StockLine;

            AddStatus();
            AddStockLink(screen);
            if (screen.EmptyNote != null)
            {
                RectTransform note = UiKit.AutoCard(_content, "EmptyCard", UiTheme.Card, 8f, 40);
                UiKit.Label(note, "Note", screen.EmptyNote, 40, UiTheme.Muted, false, TextAnchor.MiddleCenter);
            }

            foreach (WholesaleOfferRowViewModel offer in screen.Offers)
            {
                AddOffer(offer);
            }
        }

        private void AddStatus()
        {
            string message = _flow.StatusMessage;
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            RectTransform card = UiKit.AutoCard(_content, "Status", UiTheme.WarnSoft, 4f, 24);
            UiKit.Label(card, "Message", message, 36, UiTheme.Ink, false, TextAnchor.MiddleCenter);
        }

        private void AddStockLink(WholesaleScreenViewModel screen)
        {
            Button link = UiKit.RoundedButton(_content, "StockLink", TurkishTexts.ToAccessoryStock + "  (" + screen.StockLine + ")", 34, UiTheme.GoldSoft, UiTheme.Ink, () => _flow.OpenAccessoryStock());
            UiKit.Size2(link.GetComponent<RectTransform>(), 0f, 110f);
        }

        private void AddOffer(WholesaleOfferRowViewModel offer)
        {
            RectTransform card = UiKit.Card(_content, "Offer_" + offer.AccessoryId, UiTheme.Card);
            UiKit.Size2(card, 0f, 270f);

            Text name = UiKit.Label(card, "Name", offer.Name, 48, UiTheme.Ink, true);
            UiBuilder.Stretch(name.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(32f, -84f), new Vector2(-32f, -20f));

            Text lines = UiKit.Label(card, "Lines", offer.PackLine + "\n" + offer.UnitCostLine + "\n" + offer.PackPriceLine, 32, UiTheme.Muted);
            lines.lineSpacing = 1.15f;
            UiBuilder.Stretch(lines.rectTransform, new Vector2(0f, 0f), new Vector2(0.55f, 1f), new Vector2(32f, 22f), new Vector2(-8f, -92f));

            string supplierId = offer.SupplierId;
            string accessoryId = offer.AccessoryId;
            bool enabled = offer.IsButtonEnabled;
            Button button = UiKit.RoundedButton(
                card, "Buy_" + accessoryId, offer.ButtonText, 36, enabled ? UiTheme.Gold : UiTheme.Disabled, enabled ? UiTheme.Ink : UiTheme.Muted,
                () => _flow.BuyWholesalePack(supplierId, accessoryId));
            button.interactable = enabled;
            button.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
            if (enabled)
            {
                UiKit.AddShadow(button.gameObject, 0.18f, 6f);
            }

            UiBuilder.Stretch(button.GetComponent<RectTransform>(), new Vector2(0.56f, 0f), new Vector2(1f, 0f), new Vector2(8f, 28f), new Vector2(-28f, 130f));

            if (offer.LockNote != null)
            {
                Text note = UiKit.Label(card, "LockNote", offer.LockNote, 28, UiTheme.Warn, false, TextAnchor.MiddleRight);
                UiBuilder.Stretch(note.rectTransform, new Vector2(0.45f, 0f), new Vector2(1f, 0f), new Vector2(0f, 134f), new Vector2(-28f, 190f));
            }
        }
    }
}
