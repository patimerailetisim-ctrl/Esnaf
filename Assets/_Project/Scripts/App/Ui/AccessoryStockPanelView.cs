using Esnaf.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Esnaf.App.Ui
{
    /// <summary>
    /// Aksesuar Stoğu ekranı (salt okunur): üstte "Stok: X / 60 adet" ve stok maliyeti; her ürün bir kart (ad, adet, ortalama maliyet, toplam maliyet, kapasite payı).
    /// Telefon rafından (Raf ekranı) tamamen ayrıdır. Yalnızca <see cref="UiFlow"/> ile konuşur (AccessoryStockScreen, OpenWholesale, Back).
    /// </summary>
    internal sealed class AccessoryStockPanelView
    {
        private const float HeaderHeight = 200f;
        private const float Gutter = 32f;

        private readonly UiFlow _flow;
        private readonly GameObject _root;
        private readonly Text _kicker;
        private readonly Text _title;
        private readonly Text _info;
        private readonly RectTransform _content;

        public AccessoryStockPanelView(Transform canvas, UiFlow flow)
        {
            _flow = flow;

            RectTransform root = UiBuilder.CreatePanel(canvas, "AccessoryStockScreen", UiTheme.Background);
            _root = root.gameObject;
            UiBuilder.Stretch(root, Vector2.zero, Vector2.one, new Vector2(0f, NavBarView.Height), new Vector2(0f, -TopBarView.Height));

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
            AccessoryStockScreenViewModel screen = _flow.AccessoryStockScreen;
            bool visible = _flow.CurrentScreen == UiScreen.AccessoryStock && screen != null;
            _root.SetActive(visible);
            if (!visible)
            {
                return;
            }

            UiKit.Clear(_content);
            _kicker.text = TurkishTexts.AccessoryStockTitle;
            _title.text = screen.CapacityLine;
            _info.text = screen.TotalCostLine;

            Button link = UiKit.RoundedButton(_content, "WholesaleLink", TurkishTexts.ToWholesale, 34, UiTheme.GoldSoft, UiTheme.Ink, () => _flow.OpenWholesale());
            UiKit.Size2(link.GetComponent<RectTransform>(), 0f, 110f);

            if (screen.EmptyNote != null)
            {
                RectTransform note = UiKit.AutoCard(_content, "EmptyCard", UiTheme.Card, 8f, 40);
                UiKit.Label(note, "Note", screen.EmptyNote, 40, UiTheme.Muted, false, TextAnchor.MiddleCenter);
            }

            foreach (AccessoryStockRowViewModel item in screen.Items)
            {
                RectTransform card = UiKit.AutoCard(_content, "Item_" + item.AccessoryId, UiTheme.Card, 8f, 32);
                UiKit.Label(card, "Name", item.Name, 46, UiTheme.Ink, true);
                UiKit.Label(card, "Quantity", item.QuantityLine + "  •  " + item.ShareLine, 34, UiTheme.Ink);
                UiKit.Label(card, "Average", item.AverageCostLine, 32, UiTheme.Muted);
                UiKit.Label(card, "Total", item.TotalCostLine, 32, UiTheme.Muted);
            }
        }
    }
}
