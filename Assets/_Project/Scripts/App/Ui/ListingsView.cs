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
            UiBuilder.Stretch(root, Vector2.zero, Vector2.one, new Vector2(0f, 0f), new Vector2(0f, -TopBarView.Height));

            Text title = UiBuilder.CreateText(root, "Title", 56, TextAnchor.MiddleLeft, Color.white);
            title.text = TurkishTexts.ListingsTitle;
            UiBuilder.Stretch(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -TitleHeight), Vector2.zero);

            _content = UiBuilder.CreateVerticalList(root, "List", new Color(0.09f, 0.1f, 0.13f, 1f), 16f, 24f);
            RectTransform list = _content.parent as RectTransform;
            UiBuilder.Stretch(list, Vector2.zero, Vector2.one, new Vector2(0f, BottomBarHeight + StatusHeight), new Vector2(0f, -TitleHeight));

            _empty = UiBuilder.CreateText(root, "Empty", 44, TextAnchor.MiddleCenter, new Color(0.7f, 0.72f, 0.78f, 1f));
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
            _status.text = _flow.StatusMessage ?? string.Empty;
            _shelfLabel.text = _flow.ShelfButtonText;
            _customersLabel.text = _flow.CustomersButtonText;
            _accessoriesLabel.text = _flow.AccessoryButtonText;
        }

        private void AddRow(ListingRowViewModel row)
        {
            long listingId = row.ListingId;
            Button button = UiBuilder.CreateButton(_content, "Listing_" + listingId, string.Empty, 40, row.IsSelected ? SelectedRowColor : RowColor, () => _flow.OpenListing(listingId));
            var layout = button.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 230f;
            layout.preferredHeight = 230f;

            Text label = button.GetComponentInChildren<Text>();
            label.alignment = TextAnchor.MiddleLeft;
            label.fontSize = 38;
            label.text = row.Title + "\n"
                + row.StorageText + "  •  " + row.AgeText + "  •  " + row.PriceText + "\n"
                + row.RemainingText + "  •  " + row.BoxText + "  •  " + row.InvoiceText;
            UiBuilder.Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(30f, 0f), new Vector2(-30f, 0f));
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
