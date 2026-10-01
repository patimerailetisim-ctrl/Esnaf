using UnityEngine;
using UnityEngine.UI;

namespace Esnaf.App.Ui
{
    /// <summary>
    /// Yuvarlatılmış köşeli kart/hap/daire üretimi ve yumuşak gölge. Köşe sprite'ı çalışma anında üretilir (dış asset yok),
    /// 9-dilimli çizilir; hap ve daire aynı sprite'tır (kenarlar küçülünce yarıçap otomatik düşer). Yalnızca görünüm.
    /// </summary>
    internal static class UiKit
    {
        private const int Size = 96;
        private const int Radius = 40;
        private static Sprite _rounded;

        public static Sprite RoundedSprite
        {
            get
            {
                if (_rounded == null)
                {
                    _rounded = BuildRounded();
                }

                return _rounded;
            }
        }

        private static Sprite BuildRounded()
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, Radius, Size - Radius);
                    float cy = Mathf.Clamp(y + 0.5f, Radius, Size - Radius);
                    float distance = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    float alpha = Mathf.Clamp01(Radius - distance + 0.5f);
                    pixels[y * Size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(Radius, Radius, Radius, Radius));
        }

        /// <summary>Yuvarlatılmış dikdörtgen (köşe 40 br; küçük kutuda otomatik küçülür).</summary>
        public static RectTransform Rounded(Transform parent, string name, Color color)
        {
            RectTransform rect = UiBuilder.CreatePanel(parent, name, color);
            Image image = rect.GetComponent<Image>();
            image.sprite = RoundedSprite;
            image.type = Image.Type.Sliced;
            return rect;
        }

        /// <summary>Beyaz kart + hafif, yumuşak gölge.</summary>
        public static RectTransform Card(Transform parent, string name, Color color)
        {
            RectTransform rect = Rounded(parent, name, color);
            AddShadow(rect.gameObject, 0.07f, 12f);
            AddShadow(rect.gameObject, 0.05f, 4f);
            return rect;
        }

        public static void AddShadow(GameObject target, float alpha, float distance)
        {
            var shadow = target.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.05f, 0.1f, 0.25f, alpha);
            shadow.effectDistance = new Vector2(0f, -distance);
        }

        /// <summary>Yuvarlatılmış düğme: renkli zemin + ortalı yazı.</summary>
        public static Button RoundedButton(Transform parent, string name, string label, int fontSize, Color fill, Color textColor, UnityEngine.Events.UnityAction onClick)
        {
            RectTransform rect = Rounded(parent, name, fill);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            button.onClick.AddListener(onClick);
            Text text = UiBuilder.CreateText(rect, "Label", fontSize, TextAnchor.MiddleCenter, textColor);
            text.text = label;
            UiBuilder.Stretch(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 0f), new Vector2(-12f, 0f));
            return button;
        }

        /// <summary>Yazılı küçük hap rozet (seviye rozeti, ton etiketi).</summary>
        public static Text Badge(Transform parent, string name, Color fill, Color textColor, int fontSize)
        {
            RectTransform rect = Rounded(parent, name, fill);
            Text text = UiBuilder.CreateText(rect, "Label", fontSize, TextAnchor.MiddleCenter, textColor);
            UiBuilder.Stretch(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(20f, 0f), new Vector2(-20f, 0f));
            return text;
        }

        public static void Size2(RectTransform rect, float width, float height)
        {
            LayoutElement layout = EnsureLayout(rect);
            if (width > 0f)
            {
                layout.preferredWidth = width;
                layout.minWidth = width;
            }

            if (height > 0f)
            {
                layout.preferredHeight = height;
                layout.minHeight = height;
            }
        }

        /// <summary>Alt çocuklar arasında bırakılan boşluk için esnek yer tutucu.</summary>
        public static void Flex(RectTransform rect, float weight)
        {
            LayoutElement layout = EnsureLayout(rect);
            layout.flexibleWidth = weight;
            layout.flexibleHeight = weight;
        }

        /// <summary>
        /// Çocukları kaldırır. Çocuk hemen ebeveynden ayrılır (Destroy çerçeve sonuna ertelenir; aynı çerçevede yeniden çizimde eski çocuk
        /// görünmesin); Play Mode'da Destroy, Edit Mode'da (testler) DestroyImmediate kullanılır.
        /// </summary>
        public static void Clear(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                child.transform.SetParent(null, false);
                if (Application.isPlaying)
                {
                    Object.Destroy(child);
                }
                else
                {
                    Object.DestroyImmediate(child);
                }
            }
        }

        /// <summary>Çocuklara yatay yerleşim (Row) ekler.</summary>
        public static HorizontalLayoutGroup Row(RectTransform rect, float spacing, int padding, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return layout;
        }

        /// <summary>Çocuklara dikey yerleşim (Column) ekler.</summary>
        public static VerticalLayoutGroup Column(RectTransform rect, float spacing, int padding, TextAnchor alignment = TextAnchor.UpperLeft)
        {
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        /// <summary>Satır kaydıran etiket (isteğe bağlı kalın).</summary>
        public static Text Label(Transform parent, string name, string text, int size, Color color, bool bold = false, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            Text label = UiBuilder.CreateWrappedText(parent, name, size, anchor, color);
            label.text = text ?? string.Empty;
            if (bold)
            {
                label.fontStyle = FontStyle.Bold;
            }

            return label;
        }

        /// <summary>Çocuklarını dikey dizen, içeriğine göre yüksekliği büyüyen kart.</summary>
        public static RectTransform AutoCard(Transform parent, string name, Color fill, float spacing = 12f, int padding = 32)
        {
            RectTransform card = Card(parent, name, fill);
            Column(card, spacing, padding);
            return card;
        }

        private static LayoutElement EnsureLayout(RectTransform rect)
        {
            LayoutElement layout = rect.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = rect.gameObject.AddComponent<LayoutElement>();
            }

            return layout;
        }
    }
}
