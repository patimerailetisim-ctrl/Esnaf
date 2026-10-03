using Esnaf.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Esnaf.App.Ui
{
    /// <summary>
    /// İlanlar ekranı: başlık, kaydırmalı ilan listesi, durum mesajı ve "Günü Bitir" düğmesi. Yalnızca <see cref="UiFlow"/> ile konuşur
    /// (satırlar ListingRowViewModel, tıklama OpenListing: seçer ve detaya geçer, düğme EndDay, Raf düğmesi OpenShelf); oyun kuralı yoktur. Liste her değişimde yeniden kurulur.
    /// </summary>
    internal sealed class ListingsView
    {
        private const float StatusHeight = 90f;
        private const float BottomBarHeight = 300f; // iki sıra düğme
        private const float TitleHeight = 110f;
        private const float CardHeight = 400f;

        private static readonly Color RowColor = new Color(0.18f, 0.2f, 0.26f, 1f);
        private static readonly Color SelectedRowColor = new Color(0.2f, 0.4f, 0.65f, 1f);

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

            RectTransform root = UiBuilder.CreatePanel(canvas, "ListingsScreen", new Color(0.09f, 0.1f, 0.13f, 1f));
            _root = root.gameObject;
            UiBuilder.Stretch(root, Vector2.zero, Vector2.one, new Vector2(0f, NavBarView.Height), new Vector2(0f, -TopBarView.Height));

            Text title = UiBuilder.CreateText(root, "Title", 56, TextAnchor.MiddleLeft, Color.white);
            title.text = TurkishTexts.ListingsTitle;
            UiBuilder.Stretch(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -TitleHeight), Vector2.zero);

            _content = UiBuilder.CreateVerticalList(root, "List", new Color(0.09f, 0.1f, 0.13f, 1f), 16f, 24f);
            RectTransform list = _content.parent as RectTransform;
            UiBuilder.Stretch(list, Vector2.zero, Vector2.one, new Vector2(0f, BottomBarHeight + StatusHeight), new Vector2(0f, -TitleHeight));

            _empty = UiBuilder.CreateWrappedText(root, "Empty", 44, TextAnchor.MiddleCenter, new Color(0.7f, 0.72f, 0.78f, 1f));
            _empty.text = TurkishTexts.NoListings;
            UiBuilder.Stretch(_empty.rectTransform, new Vector2(0f, 0.4f), new Vector2(1f, 0.6f), Vector2.zero, Vector2.zero);

            _status = UiBuilder.CreateText(root, "Status", 40, TextAnchor.MiddleCenter, new Color(1f, 0.75f, 0.45f, 1f));
            UiBuilder.Stretch(_status.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(40f, BottomBarHeight), new Vector2(-40f, BottomBarHeight + StatusHeight));

            Button endDay = UiBuilder.CreateButton(root, "EndDayButton", TurkishTexts.EndDayButton, 52, new Color(0.7f, 0.3f, 0.25f, 1f), OnEndDayClicked);
            UiBuilder.Stretch(endDay.GetComponent<RectTransform>(), new Vector2(0.68f, 0f), new Vector2(1f, 0f), new Vector2(10f, 25f), new Vector2(-40f, 145f));

            Button shelf = UiBuilder.CreateButton(root, "ShelfButton", TurkishTexts.ShelfButton(0, 0), 46, new Color(0.3f, 0.33f, 0.42f, 1f), OnShelfClicked);
            UiBuilder.Stretch(shelf.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(40f, 160f), new Vector2(-10f, BottomBarHeight - 20f));
            _shelfLabel = shelf.GetComponentInChildren<Text>();
            _shelfLabel.fontSize = 38;

            Button customers = UiBuilder.CreateButton(root, "CustomersButton", TurkishTexts.CustomersTitle, 38, new Color(0.25f, 0.5f, 0.45f, 1f), OnCustomersClicked);
            UiBuilder.Stretch(customers.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(10f, 160f), new Vector2(-40f, BottomBarHeight - 20f));
            _customersLabel = customers.GetComponentInChildren<Text>();

            Button wholesale = UiBuilder.CreateButton(root, "WholesaleButton", TurkishTexts.WholesaleButton, 38, new Color(0.7f, 0.45f, 0.2f, 1f), OnWholesaleClicked);
            UiBuilder.Stretch(wholesale.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0.34f, 0f), new Vector2(40f, 25f), new Vector2(-10f, 145f));

            Button accessories = UiBuilder.CreateButton(root, "AccessoryStockButton", TurkishTexts.AccessoryStockButton(0, 0), 36, new Color(0.3f, 0.33f, 0.42f, 1f), OnAccessoryStockClicked);
            UiBuilder.Stretch(accessories.GetComponent<RectTransform>(), new Vector2(0.34f, 0f), new Vector2(0.68f, 0f), new Vector2(10f, 25f), new Vector2(-10f, 145f));
            _accessoriesLabel = accessories.GetComponentInChildren<Text>();
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

        // İlan kartı (Gün 13.3): solda telefon görseli, sağda model, kondisyon, satıcı, istenen fiyat, tahmini değer ve "İncele". Metinler sarılır; görsel maskelenir (taşmaz).
        private void AddRow(ListingRowViewModel row)
        {
            long listingId = row.ListingId;
            Button button = UiBuilder.CreateButton(_content, "Listing_" + listingId, string.Empty, 40, row.IsSelected ? SelectedRowColor : RowColor, () => _flow.OpenListing(listingId));
            var layout = button.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = CardHeight;
            layout.preferredHeight = CardHeight;
            button.GetComponentInChildren<Text>().gameObject.SetActive(false);
            RectTransform card = button.GetComponent<RectTransform>();

            RectTransform image = UiBuilder.CreatePanel(card, "PhoneImage", new Color(0.09f, 0.1f, 0.13f, 1f));
            image.GetComponent<Image>().raycastTarget = false;
            UiBuilder.Stretch(image, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, -115f), new Vector2(250f, 115f));
            image.gameObject.AddComponent<RectMask2D>();
            var phone = new GameObject("Phone", typeof(RectTransform)).GetComponent<RectTransform>();
            phone.SetParent(image, false);
            phone.anchorMin = new Vector2(0.5f, 0.5f);
            phone.anchorMax = new Vector2(0.5f, 0.5f);
            phone.sizeDelta = new Vector2(700f, 700f);
            phone.localScale = new Vector3(0.33f, 0.33f, 1f);
            PhoneMockView.Draw(phone, row.DefinitionId, PhoneAngle.Front);

            AddCardLine(card, "Title", row.Title, 44, 0f, 56f, Color.white, true);
            AddCardLine(card, "Condition", row.ConditionText, 30, 58f, 84f, new Color(0.78f, 0.8f, 0.86f, 1f), false);
            AddCardLine(card, "Seller", row.SellerText, 32, 144f, 46f, new Color(0.78f, 0.8f, 0.86f, 1f), false);
            AddCardLine(card, "Asking", row.AskingText, 36, 192f, 50f, new Color(1f, 0.85f, 0.4f, 1f), true);
            AddCardLine(card, "Estimated", row.EstimatedText, 32, 244f, 46f, new Color(0.55f, 0.9f, 0.6f, 1f), false);

            Button inspect = UiBuilder.CreateButton(card, "InspectButton", row.InspectButtonText, 36, new Color(0.2f, 0.4f, 0.65f, 1f), () => _flow.OpenListing(listingId));
            UiBuilder.Stretch(inspect.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-260f, 16f), new Vector2(-20f, 90f));
        }

        // Kartın sağ sütununda yukarıdan 'top' px aşağıda, 'height' px yüksekliğinde sarılan metin satırı.
        private static void AddCardLine(RectTransform card, string name, string text, int size, float top, float height, Color color, bool bold)
        {
            Text label = UiBuilder.CreateWrappedText(card, name, size, TextAnchor.UpperLeft, color);
            label.text = text;
            label.raycastTarget = false;
            if (bold)
            {
                label.fontStyle = FontStyle.Bold;
            }

            UiBuilder.Stretch(label.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(280f, -12f - top - height), new Vector2(-20f, -12f - top));
        }

        private void OnShelfClicked()
        {
            _flow.OpenShelf();
        }

        private void OnWholesaleClicked()
        {
            _flow.OpenWholesale();
        }

        private void OnAccessoryStockClicked()
        {
            _flow.OpenAccessoryStock();
        }

        private void OnCustomersClicked()
        {
            _flow.OpenCustomers();
        }

        private void OnEndDayClicked()
        {
            _flow.EndDay();
        }
    }
}
