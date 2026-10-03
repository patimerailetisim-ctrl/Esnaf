using Esnaf.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Esnaf.App.Ui
{
    /// <summary>
    /// İlanlar ekranı (Gün 13.4 UI revizyonu): koyu lacivert ikinci el telefon pazarı. Güçlü başlık + "İkinci el telefon ilanları" alt başlığı, yuvarlatılmış ilan kartları
    /// (telefon görseli, model, kutu/fatura etiketleri, satıcı, altın vurgulu istenen fiyat, tahmini değer, "İncele") ve boş durum notu. Yalnızca <see cref="UiFlow"/> ile konuşur
    /// (OpenListing, OpenShelf, OpenCustomers, OpenWholesale, OpenAccessoryStock, EndDay); oyun kuralı yoktur. Eski beş kısayol düğmesi korunur (işlev ve mevcut testler aynı) ama altta
    /// tek sıra küçük hap olarak durur; asıl navigasyon artık kalıcı alt çubuktur. Liste her değişimde yeniden kurulur.
    /// </summary>
    internal sealed class ListingsView
    {
        private const float StatusHeight = 70f;
        private const float StripHeight = 96f;
        private const float HeaderHeight = 200f;
        private const float CardHeight = 330f;

        private readonly UiFlow _flow;
        private readonly GameObject _root;
        private readonly RectTransform _content;
        private readonly Text _empty;
        private readonly Text _status;
        private readonly Text _shelfLabel;
        private readonly Text _customersLabel;
        private readonly Text _accessoriesLabel;

        public ListingsView(Transform canvas, UiFlow flow)
        {
            _flow = flow;

            RectTransform root = UiBuilder.CreatePanel(canvas, "ListingsScreen", KarbazDark.Background);
            _root = root.gameObject;
            UiBuilder.Stretch(root, Vector2.zero, Vector2.one, new Vector2(0f, NavBarView.Height), new Vector2(0f, -TopBarView.Height));

            // Başlık: telefon ikonlu yuvarlak kare + "İlanlar" + alt başlık
            RectTransform badge = UiKit.Rounded(root, "TitleBadge", KarbazDark.Card);
            badge.GetComponent<Image>().raycastTarget = false;
            badge.gameObject.AddComponent<Outline>().effectColor = KarbazDark.Border;
            UiBuilder.Stretch(badge, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -140f), new Vector2(120f, -50f));
            var icon = new GameObject("Icon", typeof(RectTransform)).GetComponent<RectTransform>();
            icon.SetParent(badge, false);
            icon.anchorMin = new Vector2(0.5f, 0.5f);
            icon.anchorMax = new Vector2(0.5f, 0.5f);
            icon.sizeDelta = new Vector2(NavIcons.Size, NavIcons.Size);
            NavIcons.Draw(icon, NavTab.Listings, KarbazDark.TextPrimary);

            Text title = UiBuilder.CreateText(root, "Title", 64, TextAnchor.MiddleLeft, KarbazDark.TextPrimary);
            title.text = TurkishTexts.ListingsTitle;
            title.fontStyle = FontStyle.Bold;
            UiBuilder.Stretch(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(148f, -110f), new Vector2(-30f, -36f));
            Text subtitle = UiBuilder.CreateText(root, "Subtitle", 34, TextAnchor.MiddleLeft, KarbazDark.TextMuted);
            subtitle.text = TurkishTexts.ListingsSubtitle;
            UiBuilder.Stretch(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(148f, -160f), new Vector2(-30f, -108f));

            _content = UiBuilder.CreateVerticalList(root, "List", KarbazDark.Background, 22f, 24f);
            RectTransform list = _content.parent as RectTransform;
            UiBuilder.Stretch(list, Vector2.zero, Vector2.one, new Vector2(0f, StatusHeight + StripHeight), new Vector2(0f, -HeaderHeight));

            _empty = UiBuilder.CreateWrappedText(root, "Empty", 44, TextAnchor.MiddleCenter, KarbazDark.TextMuted);
            _empty.text = TurkishTexts.NoListings;
            UiBuilder.Stretch(_empty.rectTransform, new Vector2(0.05f, 0.4f), new Vector2(0.95f, 0.6f), Vector2.zero, Vector2.zero);

            _status = UiBuilder.CreateWrappedText(root, "Status", 36, TextAnchor.MiddleCenter, KarbazDark.Gold);
            UiBuilder.Stretch(_status.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(40f, StripHeight), new Vector2(-40f, StripHeight + StatusHeight));

            // Kısayol şeridi: Raf | Müşteriler | Toptancı | Aksesuar | Günü Bitir (tek sıra, küçük hap).
            _shelfLabel = AddShortcut(root, "ShelfButton", TurkishTexts.ShelfButton(0, 0), 0, KarbazDark.Card, () => _flow.OpenShelf());
            _customersLabel = AddShortcut(root, "CustomersButton", TurkishTexts.CustomersTitle, 1, KarbazDark.Card, () => _flow.OpenCustomers());
            AddShortcut(root, "WholesaleButton", TurkishTexts.WholesaleButton, 2, KarbazDark.Card, () => _flow.OpenWholesale());
            _accessoriesLabel = AddShortcut(root, "AccessoryStockButton", TurkishTexts.AccessoryStockButton(0, 0), 3, KarbazDark.Card, () => _flow.OpenAccessoryStock());
            AddShortcut(root, "EndDayButton", TurkishTexts.EndDayButton, 4, new Color(0.36f, 0.16f, 0.17f, 1f), () => _flow.EndDay());
        }

        private static Text AddShortcut(RectTransform root, string name, string label, int index, Color fill, UnityEngine.Events.UnityAction onClick)
        {
            Button button = UiKit.RoundedButton(root, name, label, 24, fill, KarbazDark.TextPrimary, onClick);
            Outline border = button.gameObject.AddComponent<Outline>();
            border.effectColor = KarbazDark.Border;
            border.effectDistance = new Vector2(2f, -2f);
            float min = index * 0.2f;
            UiBuilder.Stretch(button.GetComponent<RectTransform>(), new Vector2(min, 0f), new Vector2(min + 0.2f, 0f), new Vector2(6f, 12f), new Vector2(-6f, StripHeight - 8f));
            return button.GetComponentInChildren<Text>();
        }

        public void Show()
        {
            bool visible = _flow.CurrentScreen == UiScreen.Listings;
            _root.SetActive(visible);
            if (!visible)
            {
                return;
            }

            UiKit.Clear(_content); // Play Mode'da Destroy, Edit Mode'da (testler) DestroyImmediate

            foreach (ListingRowViewModel row in _flow.Listings)
            {
                AddRow(row);
            }

            _empty.gameObject.SetActive(_flow.Listings.Count == 0);
            _empty.text = _flow.ListingsEmptyNote ?? TurkishTexts.NoListings;
            _status.text = _flow.StatusMessage ?? string.Empty;
            _shelfLabel.text = _flow.ShelfButtonText;
            _customersLabel.text = _flow.QueueButtonText; // Gün 12.5: günlük müşteri akışının durumu
            _accessoriesLabel.text = _flow.AccessoryButtonText;
        }

        // İlan kartı: solda telefon görseli, ortada model + etiketler + satıcı, sağda istenen fiyat + tahmini değer + "İncele". Metinler sarılır, görsel maskelenir (taşmaz).
        private void AddRow(ListingRowViewModel row)
        {
            long listingId = row.ListingId;
            Button button = UiKit.RoundedButton(_content, "Listing_" + listingId, string.Empty, 30, KarbazDark.Card, KarbazDark.TextPrimary, () => _flow.OpenListing(listingId));
            UiKit.AddShadow(button.gameObject, 0.25f, 10f);
            Outline border = button.gameObject.AddComponent<Outline>();
            border.effectColor = row.IsSelected ? KarbazDark.Gold : KarbazDark.Border;
            border.effectDistance = new Vector2(2f, -2f);
            RectTransform card = button.GetComponent<RectTransform>();
            UiKit.Size2(card, 0f, CardHeight);
            button.GetComponentInChildren<Text>().gameObject.SetActive(false);

            // telefon görseli (portre oran, maskeli)
            RectTransform image = UiKit.Rounded(card, "PhoneImage", KarbazDark.CardInset);
            image.GetComponent<Image>().raycastTarget = false;
            UiBuilder.Stretch(image, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, -140f), new Vector2(222f, 140f));
            image.gameObject.AddComponent<RectMask2D>();
            var phone = new GameObject("Phone", typeof(RectTransform)).GetComponent<RectTransform>();
            phone.SetParent(image, false);
            phone.anchorMin = new Vector2(0.5f, 0.5f);
            phone.anchorMax = new Vector2(0.5f, 0.5f);
            phone.sizeDelta = new Vector2(700f, 700f);
            phone.localScale = new Vector3(0.38f, 0.38f, 1f);
            PhoneMockView.Draw(phone, row.DefinitionId, PhoneAngle.Front);

            // orta sütun
            AddText(card, "Title", row.Title, 42, KarbazDark.TextPrimary, true, 0f, 0.62f, 24f, 60f);
            RectTransform chips = AddChipRow(card, row);
            AddText(card, "Seller", row.SellerText, 30, KarbazDark.TextMuted, false, 0f, 0.62f, 156f, 44f);
            string detail = row.UrgentChip == null ? row.RemainingText : row.RemainingText + "  •  " + row.UrgentChip;
            AddText(card, "Remaining", detail, 28, KarbazDark.TextMuted, false, 0f, 0.62f, 204f, 44f);
            if (chips != null)
            {
                chips.SetAsLastSibling();
            }

            // sağ sütun
            AddText(card, "AskingLabel", row.AskingLabelText, 28, KarbazDark.TextMuted, false, 0.62f, 1f, 24f, 40f, 0f, 20f);
            AddText(card, "Asking", row.PriceValueText, 46, KarbazDark.Gold, true, 0.62f, 1f, 62f, 62f, 0f, 20f);
            AddText(card, "Estimated", row.EstimatedText, 26, KarbazDark.TextMuted, false, 0.62f, 1f, 128f, 70f, 0f, 20f);

            Button inspect = UiKit.RoundedButton(card, "InspectButton", row.InspectButtonText + "  ›", 36, KarbazDark.Action, KarbazDark.TextPrimary, () => _flow.OpenListing(listingId));
            UiBuilder.Stretch(inspect.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-250f, 22f), new Vector2(-22f, 100f));
            inspect.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
        }

        // Kartın üstünden 'top' px aşağıda 'height' yüksekliğinde sarılan metin; yatay konum kartın genişlik oranlarıyla ('minX'..'maxX'), sol boşluk 'minPad' (görsel yanı) sağ boşluk 'rightPad'.
        private static void AddText(RectTransform card, string name, string text, int size, Color color, bool bold, float minX, float maxX, float top, float height, float leftPad = -1f, float rightPad = 10f)
        {
            Text label = UiBuilder.CreateWrappedText(card, name, size, TextAnchor.UpperLeft, color);
            label.text = text;
            label.raycastTarget = false;
            if (bold)
            {
                label.fontStyle = FontStyle.Bold;
            }

            float left = leftPad >= 0f ? leftPad : 246f;
            if (minX > 0f)
            {
                left = 0f;
            }

            UiBuilder.Stretch(label.rectTransform, new Vector2(minX, 1f), new Vector2(maxX, 1f), new Vector2(left, -top - height), new Vector2(-rightPad, -top));
        }

        // Kutu / fatura etiketleri (yuvarlatılmış hap): "Kutu var", "Fatura var" ... Yalnızca herkese açık ilan bilgisi; hap genişliği yazıya göre.
        private RectTransform AddChipRow(RectTransform card, ListingRowViewModel row)
        {
            var go = new GameObject("Chips", typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(card, false);
            UiBuilder.Stretch(rect, new Vector2(0f, 1f), new Vector2(0.62f, 1f), new Vector2(246f, -142f), new Vector2(-10f, -94f));
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            AddChip(rect, "Chip_Age", row.AgeChip, KarbazDark.ChipBlue, KarbazDark.TextPrimary);
            AddChip(rect, "Chip_Box", row.BoxChip, KarbazDark.Chip, row.HasBox ? KarbazDark.TextPrimary : KarbazDark.TextMuted);
            AddChip(rect, "Chip_Invoice", row.InvoiceChip, KarbazDark.Chip, row.HasInvoice ? KarbazDark.TextPrimary : KarbazDark.TextMuted);
            return rect;
        }

        private static void AddChip(RectTransform parent, string name, string text, Color fill, Color textColor)
        {
            RectTransform chip = UiKit.Rounded(parent, name, fill);
            chip.GetComponent<Image>().raycastTarget = false;
            var layout = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 4, 4);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            chip.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            Text label = UiBuilder.CreateText(chip, "Label", 24, TextAnchor.MiddleCenter, textColor);
            label.text = text;
            label.raycastTarget = false;
        }
    }
}
