using Esnaf.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Esnaf.App.Ui
{
    /// <summary>
    /// Dükkan ana ekranı (Gün 13.4): telefon rafı (satışta / satış dışı, telefon görselleriyle), aksesuar rafı, aktif müşteri ve "Günü Bitir". Satış ekranı DEĞİLDİR:
    /// telefona dokunmak mevcut Raf fiyat panelini (<see cref="UiFlow.OpenShelfItemFromShop"/>), "Müşteriye Git" mevcut satış akışını (<see cref="UiFlow.GoToActiveCustomer"/>) açar.
    /// Yalnızca <see cref="UiFlow.ShopScreen"/>'i çizer; oyun kuralı yoktur. Her şey kaydırmalı alandadır (taşmaz); üst HUD ve alt navigasyon sabittir.
    /// </summary>
    internal sealed class ShopView
    {
        private const int TilesPerRow = 3;
        private const float TileHeight = 400f;

        private readonly UiFlow _flow;
        private readonly GameObject _root;
        private readonly RectTransform _content;

        public ShopView(Transform canvas, UiFlow flow)
        {
            _flow = flow;
            RectTransform root = UiBuilder.CreatePanel(canvas, "ShopScreen", UiTheme.Background);
            _root = root.gameObject;
            UiBuilder.Stretch(root, Vector2.zero, Vector2.one, new Vector2(0f, NavBarView.Height), new Vector2(0f, -TopBarView.Height));

            _content = UiBuilder.CreateVerticalList(root, "List", UiTheme.Background, 18f, 24f);
            RectTransform list = _content.parent as RectTransform;
            UiBuilder.Stretch(list, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        public void Show()
        {
            ShopScreenViewModel shop = _flow.ShopScreen;
            bool visible = _flow.CurrentScreen == UiScreen.Shop && shop != null;
            _root.SetActive(visible);
            if (!visible)
            {
                return;
            }

            UiKit.Clear(_content); // Play Mode'da Destroy, Edit Mode'da (testler) DestroyImmediate

            AddHeader("ShelfHeader", shop.ShelfHeader);
            if (shop.ShelfEmptyNote != null)
            {
                AddNote("ShelfEmpty", shop.ShelfEmptyNote);
            }

            AddTiles(shop.SellablePhones);
            if (shop.OffSalePhones.Count > 0)
            {
                AddHeader("OffSaleHeader", shop.OffSaleHeader);
                AddTiles(shop.OffSalePhones);
            }

            AddHeader("AccessoryHeader", shop.AccessoryHeader);
            if (shop.AccessoriesEmptyNote != null)
            {
                AddNote("AccessoriesEmpty", shop.AccessoriesEmptyNote);
            }
            else
            {
                RectTransform card = UiKit.AutoCard(_content, "AccessoryCard", UiTheme.Card, 8f, 24);
                foreach (ShopAccessoryViewModel a in shop.Accessories)
                {
                    RectTransform row = new GameObject("Accessory_" + a.AccessoryId, typeof(RectTransform)).GetComponent<RectTransform>();
                    row.SetParent(card, false);
                    UiKit.Row(row, 8f, 0);
                    UiKit.Size2(row, 0f, 54f);
                    Text name = UiKit.Label(row, "Name", a.Name, 34, UiTheme.Ink);
                    UiKit.Flex(name.rectTransform, 1f);
                    Text qty = UiKit.Label(row, "Quantity", a.QuantityText, 36, UiTheme.Ink, true, TextAnchor.UpperRight);
                    UiKit.Size2(qty.rectTransform, 140f, 0f);
                }
            }

            AddHeader("CustomerHeader", shop.CustomerHeader);
            if (shop.Customer != null)
            {
                AddCustomer(shop.Customer);
            }
            else
            {
                AddNote("CustomerNote", shop.CustomerNote);
            }

            Button endDay = UiKit.RoundedButton(_content, "EndDayButton", shop.EndDayButtonText, 40, UiTheme.CardSoft, UiTheme.Ink, () => _flow.EndDay());
            UiKit.Size2(endDay.GetComponent<RectTransform>(), 0f, 110f);
        }

        private void AddHeader(string name, string text)
        {
            Text header = UiKit.Label(_content, name, text, 36, UiTheme.Muted, true);
            UiKit.Size2(header.rectTransform, 0f, 56f);
        }

        private void AddNote(string name, string text)
        {
            RectTransform card = UiKit.AutoCard(_content, name + "Card", UiTheme.Card, 8f, 28);
            UiKit.Label(card, name, text, 36, UiTheme.Muted, false, TextAnchor.MiddleCenter);
        }

        // Telefon karoları: satır başına 3; son satırın boşlukları görünmez yer tutucudur (karolar eşit genişlikte kalır).
        private void AddTiles(System.Collections.Generic.IReadOnlyList<ShopPhoneViewModel> phones)
        {
            for (int start = 0; start < phones.Count; start += TilesPerRow)
            {
                RectTransform row = new GameObject("PhoneRow", typeof(RectTransform)).GetComponent<RectTransform>();
                row.SetParent(_content, false);
                UiKit.Row(row, 14f, 0, TextAnchor.UpperCenter);
                UiKit.Size2(row, 0f, TileHeight);
                for (int i = 0; i < TilesPerRow; i++)
                {
                    if (start + i < phones.Count)
                    {
                        AddTile(row, phones[start + i]);
                    }
                    else
                    {
                        RectTransform spacer = new GameObject("Spacer", typeof(RectTransform)).GetComponent<RectTransform>();
                        spacer.SetParent(row, false);
                        UiKit.Flex(spacer, 1f);
                    }
                }
            }
        }

        private void AddTile(RectTransform row, ShopPhoneViewModel phone)
        {
            long id = phone.InstanceId;
            Button tile = UiKit.RoundedButton(row, "Phone_" + id, string.Empty, 30, phone.IsSellable ? UiTheme.Card : UiTheme.CardSoft, UiTheme.Ink, () => _flow.OpenShelfItemFromShop(id));
            UiKit.AddShadow(tile.gameObject, 0.07f, 10f);
            UiKit.Flex(tile.GetComponent<RectTransform>(), 1f);
            UiKit.Size2(tile.GetComponent<RectTransform>(), 0f, TileHeight);
            tile.GetComponentInChildren<Text>().gameObject.SetActive(false);
            RectTransform tileRect = tile.GetComponent<RectTransform>();

            // Telefon görseli (maskeli, taşmaz).
            RectTransform image = UiKit.Rounded(tileRect, "PhoneImage", UiTheme.CardSoft);
            image.GetComponent<Image>().raycastTarget = false;
            UiBuilder.Stretch(image, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-100f, -230f), new Vector2(100f, -20f));
            image.gameObject.AddComponent<RectMask2D>();
            var phoneRect = new GameObject("Phone", typeof(RectTransform)).GetComponent<RectTransform>();
            phoneRect.SetParent(image, false);
            phoneRect.anchorMin = new Vector2(0.5f, 0.5f);
            phoneRect.anchorMax = new Vector2(0.5f, 0.5f);
            phoneRect.sizeDelta = new Vector2(700f, 700f);
            phoneRect.localScale = new Vector3(0.3f, 0.3f, 1f);
            PhoneMockView.Draw(phoneRect, phone.DefinitionId, PhoneAngle.Front);

            Text model = UiKit.Label(tileRect, "Model", phone.Model, 30, UiTheme.Ink, true, TextAnchor.MiddleCenter);
            model.raycastTarget = false;
            UiBuilder.Stretch(model.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(8f, 96f), new Vector2(-8f, 168f));
            Text price = UiKit.Label(tileRect, "Price", phone.PriceText, phone.IsSellable ? 34 : 28, phone.IsSellable ? UiTheme.Good : UiTheme.Warn, true, TextAnchor.MiddleCenter);
            price.raycastTarget = false;
            UiBuilder.Stretch(price.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(8f, 20f), new Vector2(-8f, 90f));
        }

        private void AddCustomer(ShopCustomerViewModel customer)
        {
            RectTransform card = UiKit.Card(_content, "CustomerCard", UiTheme.GoldSoft);
            UiKit.Size2(card, 0f, 250f);

            RectTransform portrait = UiKit.Rounded(card, "Portrait", UiTheme.CardSoft);
            portrait.GetComponent<Image>().raycastTarget = false;
            UiBuilder.Stretch(portrait, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, -90f), new Vector2(200f, 90f));
            portrait.gameObject.AddComponent<RectMask2D>();
            CustomerPortraitView.Draw(portrait, customer.Name, customer.NpcId);

            Text title = UiKit.Label(card, "CustomerTitle", customer.Title, 38, UiTheme.Ink, true);
            UiBuilder.Stretch(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(224f, -80f), new Vector2(-20f, -16f));
            Text interest = UiKit.Label(card, "CustomerInterest", customer.InterestLine, 30, UiTheme.Muted);
            UiBuilder.Stretch(interest.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(224f, -140f), new Vector2(-20f, -80f));
            if (customer.WaitingLine != null)
            {
                Text waiting = UiKit.Label(card, "CustomerWaiting", customer.WaitingLine, 28, UiTheme.Muted);
                UiBuilder.Stretch(waiting.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(224f, -184f), new Vector2(-20f, -140f));
            }

            if (customer.CanGo)
            {
                Button go = UiKit.RoundedButton(card, "GoToCustomerButton", customer.GoButtonText, 36, UiTheme.Gold, UiTheme.Ink, () => _flow.GoToActiveCustomer());
                UiBuilder.Stretch(go.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-330f, 14f), new Vector2(-20f, 84f));
                go.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
            }
        }
    }
}
