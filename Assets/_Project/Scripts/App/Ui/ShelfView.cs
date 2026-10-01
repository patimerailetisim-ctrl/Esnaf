using Esnaf.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Esnaf.App.Ui
{
    /// <summary>
    /// Raf ekranı (salt okunur): doluluk ve raftaki ürünler (model adı, maliyet tabanı). Yalnızca <see cref="UiFlow"/> ile konuşur
    /// (ShelfScreen, Back); fiyat etiketi ve satış bu ekranda yoktur. Liste her değişimde yeniden kurulur.
    /// </summary>
    internal sealed class ShelfView
    {
        private const float BackHeight = 120f;
        private const float HeaderHeight = 150f;

        private static readonly Color RowColor = new Color(0.18f, 0.2f, 0.26f, 1f);

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

            if (screen.EmptyNote != null)
            {
                Text note = UiBuilder.CreateWrappedText(_content, "Empty", 42, TextAnchor.MiddleCenter, new Color(0.7f, 0.72f, 0.78f, 1f));
                note.text = screen.EmptyNote;
                note.gameObject.AddComponent<LayoutElement>().minHeight = 160f;
            }

            foreach (ShelfItemRowViewModel item in screen.Items)
            {
                RectTransform row = UiBuilder.CreatePanel(_content, "Item", RowColor);
                var layout = row.gameObject.AddComponent<LayoutElement>();
                layout.minHeight = 150f;
                layout.preferredHeight = 150f;

                Text label = UiBuilder.CreateWrappedText(row, "Label", 40, TextAnchor.MiddleLeft, Color.white);
                label.text = item.Title + "\n" + item.CostLine;
                UiBuilder.Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(30f, 0f), new Vector2(-30f, 0f));
            }
        }
    }
}
