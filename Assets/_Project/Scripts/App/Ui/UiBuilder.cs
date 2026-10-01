using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Esnaf.App.Ui
{
    /// <summary>
    /// Arayüz nesnelerini kodla kuran Unity yardımcıları (sahnede elle YAML yok). Yalnızca görünüm: oyun kuralı yoktur.
    /// Metin: UnityEngine.UI.Text (TMP'ye geçiş sonra). Düzen: dikey 9:16, referans 1080x1920, Scale With Screen Size.
    /// </summary>
    internal static class UiBuilder
    {
        public const float ReferenceWidth = 1080f;
        public const float ReferenceHeight = 1920f;

        private static Font _font;

        public static Font DefaultFont
        {
            get
            {
                if (_font == null)
                {
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (_font == null)
                    {
                        _font = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Helvetica", "Liberation Sans" }, 32);
                    }
                }

                return _font;
            }
        }

        public static Canvas CreateCanvas(string name)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>Yeni Input System ile çalışan EventSystem (proje yalnızca yeni Input System kullanır). Zaten varsa dokunmaz.</summary>
        public static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        public static RectTransform CreatePanel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go.GetComponent<RectTransform>();
        }

        public static Text CreateText(Transform parent, string name, int fontSize, TextAnchor alignment, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = DefaultFont;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>Satır kaydıran metin (uzun satırlar için). Yüksekliği içeriğine göre liste yerleşimi belirler.</summary>
        public static Text CreateWrappedText(Transform parent, string name, int fontSize, TextAnchor alignment, Color color)
        {
            Text text = CreateText(parent, name, fontSize, alignment, color);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        /// <summary>Etiketli düğme (Image + Button + Text). Tıklama <paramref name="onClick"/>'i çağırır.</summary>
        public static Button CreateButton(Transform parent, string name, string label, int fontSize, Color color, UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            Text text = CreateText(go.transform, "Label", fontSize, TextAnchor.MiddleCenter, Color.white);
            text.text = label;
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return button;
        }

        /// <summary>Tamsayı girişli tek satırlık metin kutusu (eski InputField + Text). Değişince <paramref name="onChanged"/> çağrılır.</summary>
        public static InputField CreateIntegerField(Transform parent, string name, string placeholder, int fontSize, UnityAction<string> onChanged)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.95f, 0.96f, 0.98f, 1f);

            Text text = CreateText(go.transform, "Text", fontSize, TextAnchor.MiddleCenter, new Color(0.08f, 0.09f, 0.12f, 1f));
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 6f), new Vector2(-10f, -6f));
            Text hint = CreateText(go.transform, "Placeholder", fontSize - 8, TextAnchor.MiddleCenter, new Color(0.45f, 0.47f, 0.52f, 1f));
            hint.text = placeholder;
            Stretch(hint.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 6f), new Vector2(-10f, -6f));

            var field = go.GetComponent<InputField>();
            field.targetGraphic = go.GetComponent<Image>();
            field.textComponent = text;
            field.placeholder = hint;
            field.contentType = InputField.ContentType.IntegerNumber;
            field.characterLimit = 10;
            field.onValueChanged.AddListener(onChanged);
            return field;
        }

        /// <summary>Dikey kaydırmalı liste: ScrollRect + içerik (VerticalLayoutGroup + ContentSizeFitter). İçerik nesnesini döndürür.</summary>
        public static RectTransform CreateVerticalList(Transform parent, string name, Color background, float spacing, float padding)
        {
            var scrollGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
            scrollGo.transform.SetParent(parent, false);
            scrollGo.GetComponent<Image>().color = background;

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(scrollGo.transform, false);
            var content = contentGo.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            var layout = contentGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            contentGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.viewport = scrollGo.GetComponent<RectTransform>();
            return content;
        }

        public static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
