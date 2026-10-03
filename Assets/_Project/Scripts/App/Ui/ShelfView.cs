using Esnaf.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Esnaf.App.Ui
{
    /// <summary>
    /// Raf ekranı (Gün 12.7): doluluk, raftaki ürünler (model, maliyet, satış fiyatı ya da "fiyat girilmedi") ve dokunulan ürünün fiyat paneli
    /// (maliyet, seçili fiyat, tahmini kâr, marj; −/+ düğmeleri, Kaydet). Yalnızca <see cref="UiFlow"/> ile konuşur
    /// (ShelfScreen, SelectShelfItem, AdjustShelfPrice, SaveShelfPrice, CloseShelfEditor, Back). Liste her değişimde yeniden kurulur.
    /// </summary>
    internal sealed class ShelfView
    {
        private const float BackHeight = 120f;
        private const float HeaderHeight = 150f;

        private static readonly Color RowColor = new Color(0.18f, 0.2f, 0.26f, 1f);
        private static readonly Color SelectedRowColor = new Color(0.25f, 0.34f, 0.45f, 1f);
        private static readonly Color EditorColor = new Color(0.14f, 0.16f, 0.22f, 1f);

        private readonly UiFlow _flow;
        private readonly GameObject _root;
        private readonly Text _title;
        private readonly RectTransform _content;

        public ShelfView(Transform canvas, UiFlow flow)
        {
            _flow = flow;

            RectTransform root = UiBuilder.CreatePanel(canvas, "ShelfScreen", new Color(0.09f, 0.1f, 0.13f, 1f));
            _root = root.gameObject;
            UiBuilder.Stretch(root, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -TopBarView.Height));

            Button back = UiBuilder.CreateButton(root, "BackButton", TurkishTexts.BackButton, 44, new Color(0.3f, 0.33f, 0.42f, 1f), () => _flow.Back());
            UiBuilder.Stretch(back.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0.4f, 1f), new Vector2(40f, -BackHeight - 20f), new Vector2(0f, -20f));

            _title = UiBuilder.CreateText(root, "Title", 50, TextAnchor.MiddleLeft, Color.white);
            UiBuilder.Stretch(_title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -BackHeight - HeaderHeight), new Vector2(-40f, -BackHeight - 30f));

            _content = UiBuilder.CreateVerticalList(root, "List", new Color(0.09f, 0.1f, 0.13f, 1f), 14f, 24f);
            RectTransform list = _content.parent as RectTransform;
            UiBuilder.Stretch(list, Vector2.zero, Vector2.one, new Vector2(0f, 20f), new Vector2(0f, -BackHeight - HeaderHeight - 10f));
        }

        public void Show()
        {
            ShelfScreenViewModel screen = _flow.ShelfScreen;
            bool visible = _flow.CurrentScreen == UiScreen.Shelf && screen != null;
            _root.SetActive(visible);
            if (!visible)
            {
                return;
            }

            _title.text = screen.Title + "  •  " + screen.CapacityLine;

            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(_content.GetChild(i).gameObject);
            }

            if (!string.IsNullOrEmpty(_flow.StatusMessage))
            {
                Text status = UiBuilder.CreateWrappedText(_content, "Status", 38, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.4f, 1f));
                status.text = _flow.StatusMessage;
                status.gameObject.AddComponent<LayoutElement>().minHeight = 110f;
            }

            if (screen.EmptyNote != null)
            {
                Text note = UiBuilder.CreateWrappedText(_content, "Empty", 42, TextAnchor.MiddleCenter, new Color(0.7f, 0.72f, 0.78f, 1f));
                note.text = screen.EmptyNote;
                note.gameObject.AddComponent<LayoutElement>().minHeight = 160f;
            }
            else
            {
                Text hint = UiBuilder.CreateWrappedText(_content, "Hint", 34, TextAnchor.MiddleCenter, new Color(0.7f, 0.72f, 0.78f, 1f));
                hint.text = TurkishTexts.ShelfPriceButtonHint;
                hint.gameObject.AddComponent<LayoutElement>().minHeight = 80f;
            }

            if (screen.Editor != null)
            {
                AddEditor(screen.Editor);
            }

            foreach (ShelfItemRowViewModel item in screen.Items)
            {
                long id = item.InstanceId;
                Color color = item.IsSelected ? SelectedRowColor : RowColor;
                Button row = UiBuilder.CreateButton(_content, "Item_" + id, string.Empty, 40, color, () => _flow.SelectShelfItem(id));
                var layout = row.gameObject.AddComponent<LayoutElement>();
                layout.minHeight = 190f;
                layout.preferredHeight = 190f;
                row.GetComponentInChildren<Text>().gameObject.SetActive(false);

                Text label = UiBuilder.CreateWrappedText(row.transform, "RowLabel", 38, TextAnchor.MiddleLeft, Color.white);
                label.text = item.Title + "\n" + item.CostLine;
                UiBuilder.Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(30f, 0f), new Vector2(-30f, 0f));
                label.raycastTarget = false;

                Text price = UiBuilder.CreateWrappedText(row.transform, "PriceLine", 34, TextAnchor.LowerRight, item.IsSellable ? new Color(0.55f, 0.9f, 0.6f, 1f) : new Color(1f, 0.7f, 0.45f, 1f));
                price.text = item.PriceLine;
                UiBuilder.Stretch(price.rectTransform, Vector2.zero, Vector2.one, new Vector2(30f, 12f), new Vector2(-30f, 0f));
                price.raycastTarget = false;
            }
        }

        // Seçili ürünün fiyat paneli: bilgi satırları, −/+ adımlar, Kaydet / Vazgeç.
        private void AddEditor(ShelfPriceEditorViewModel editor)
        {
            RectTransform box = UiBuilder.CreatePanel(_content, "PriceEditor", EditorColor);
            var boxLayout = box.gameObject.AddComponent<LayoutElement>();
            boxLayout.minHeight = 860f;
            boxLayout.preferredHeight = 860f;

            AddEditorLine(box, "EditorTitle", editor.Title, 44, 0, Color.white);
            AddEditorLine(box, "EditorCost", editor.CostLine, 36, 1, new Color(0.75f, 0.78f, 0.85f, 1f));
            AddEditorLine(box, "EditorSaved", editor.SavedLine, 32, 2, new Color(0.65f, 0.68f, 0.75f, 1f));
            AddEditorLine(box, "EditorPrice", editor.PriceLine, 48, 3, new Color(1f, 0.85f, 0.4f, 1f));
            AddEditorLine(box, "EditorProfit", editor.ProfitLine, 36, 4, Color.white);
            AddEditorLine(box, "EditorMargin", editor.MarginLine, 36, 5, Color.white);

            AddStepButton(box, "PriceMinus1000", "\u22121000", -1000L, 0);
            AddStepButton(box, "PriceMinus100", "\u2212100", -100L, 1);
            AddStepButton(box, "PricePlus100", "+100", 100L, 2);
            AddStepButton(box, "PricePlus1000", "+1000", 1000L, 3);

            Button save = UiBuilder.CreateButton(box, "SavePriceButton", editor.SaveButtonText, 44, editor.CanSave ? new Color(0.25f, 0.55f, 0.35f, 1f) : new Color(0.3f, 0.32f, 0.38f, 1f), () => _flow.SaveShelfPrice());
            UiBuilder.Stretch(save.GetComponent<RectTransform>(), new Vector2(0.04f, 0f), new Vector2(0.62f, 0f), new Vector2(0f, 24f), new Vector2(0f, 150f));
            Button cancel = UiBuilder.CreateButton(box, "CancelPriceButton", TurkishTexts.ShelfCloseEditorButton, 40, new Color(0.3f, 0.33f, 0.42f, 1f), () => _flow.CloseShelfEditor());
            UiBuilder.Stretch(cancel.GetComponent<RectTransform>(), new Vector2(0.66f, 0f), new Vector2(0.96f, 0f), new Vector2(0f, 24f), new Vector2(0f, 150f));
        }

        private static void AddEditorLine(RectTransform box, string name, string text, int size, int row, Color color)
        {
            Text label = UiBuilder.CreateText(box, name, size, TextAnchor.MiddleLeft, color);
            label.text = text;
            float top = -16f - row * 78f;
            UiBuilder.Stretch(label.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(30f, top - 72f), new Vector2(-30f, top));
        }

        private void AddStepButton(RectTransform box, string name, string label, long delta, int column)
        {
            Button button = UiBuilder.CreateButton(box, name, label, 38, new Color(0.3f, 0.33f, 0.42f, 1f), () => _flow.AdjustShelfPrice(delta));
            float min = 0.04f + column * 0.235f;
            UiBuilder.Stretch(button.GetComponent<RectTransform>(), new Vector2(min, 0f), new Vector2(min + 0.215f, 0f), new Vector2(0f, 200f), new Vector2(0f, 330f));
        }
    }
}
