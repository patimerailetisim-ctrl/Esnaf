using System;
using Esnaf.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Esnaf.App.Ui
{
    /// <summary>
    /// Gerçek ürün görseli bağlanana kadar kullanılan kaliteli telefon yer tutucusu: UI parçalarıyla çizilir (dış asset yok).
    /// İleride <see cref="PhoneImages.Provider"/> bir model + açı için Sprite döndürürse mock yerine o görsel gösterilir.
    /// Yalnızca görünüm; ekspertiz bilgisi taşımaz.
    /// </summary>
    internal static class PhoneMockView
    {
        private static readonly Color Titanium = new Color(0.33f, 0.43f, 0.56f, 1f);
        private static readonly Color TitaniumLight = new Color(0.45f, 0.56f, 0.7f, 1f);
        private static readonly Color Frame = new Color(0.1f, 0.13f, 0.2f, 1f);
        private static readonly Color Lens = new Color(0.05f, 0.07f, 0.1f, 1f);
        private static readonly Color LensRing = new Color(0.5f, 0.55f, 0.62f, 1f);

        public static void Draw(RectTransform host, string definitionId, PhoneAngle angle)
        {
            UiKit.Clear(host);

            Sprite sprite = PhoneImages.Provider == null ? null : PhoneImages.Provider(definitionId, angle);
            if (sprite != null)
            {
                var go = new GameObject("PhoneImage", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(host, false);
                var image = go.GetComponent<Image>();
                image.sprite = sprite;
                image.preserveAspect = true;
                image.raycastTarget = false;
                UiBuilder.Stretch(image.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                return;
            }

            switch (angle)
            {
                case PhoneAngle.Back:
                    DrawBack(host);
                    break;
                case PhoneAngle.Side:
                    DrawSide(host);
                    break;
                case PhoneAngle.TopBottom:
                    DrawTopBottom(host);
                    break;
                case PhoneAngle.CameraClose:
                    DrawCameraClose(host);
                    break;
                default:
                    DrawFront(host);
                    break;
            }
        }

        // Merkeze göre yerleştirilen parça: (merkez x, merkez y, genişlik, yükseklik) 1000x1000 birimlik kutuda.
        private static RectTransform Part(Transform parent, string name, Color color, float cx, float cy, float w, float h)
        {
            RectTransform rect = UiKit.Rounded(parent, name, color);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(cx, cy);
            rect.sizeDelta = new Vector2(w, h);
            rect.GetComponent<Image>().raycastTarget = false;
            return rect;
        }

        private static void Circle(Transform parent, string name, Color color, float cx, float cy, float diameter)
        {
            Part(parent, name, color, cx, cy, diameter, diameter);
        }

        private static void LensAt(Transform parent, string name, float cx, float cy, float diameter)
        {
            Circle(parent, name + "Ring", LensRing, cx, cy, diameter);
            Circle(parent, name, Lens, cx, cy, diameter * 0.82f);
            Circle(parent, name + "Glint", new Color(0.35f, 0.5f, 0.8f, 1f), cx - diameter * 0.12f, cy + diameter * 0.12f, diameter * 0.18f);
        }

        private static void DrawFront(RectTransform host)
        {
            Part(host, "Body", Frame, 0f, 0f, 330f, 660f);
            RectTransform screen = Part(host, "Screen", new Color(0.12f, 0.3f, 0.78f, 1f), 0f, 0f, 296f, 626f);
            Circle(screen, "WaveA", new Color(0.35f, 0.25f, 0.85f, 0.85f), 40f, -90f, 280f);
            Circle(screen, "WaveB", new Color(0.2f, 0.55f, 0.95f, 0.8f), -60f, 130f, 200f);
            Circle(screen, "WaveC", new Color(0.6f, 0.4f, 0.95f, 0.55f), 70f, 150f, 120f);
            Part(host, "Island", Color.black, 0f, 290f, 90f, 24f);
        }

        private static void DrawBack(RectTransform host)
        {
            Part(host, "Body", Titanium, 0f, 0f, 330f, 660f);
            Part(host, "Sheen", TitaniumLight, -90f, 0f, 90f, 600f).GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);
            RectTransform module = Part(host, "Module", new Color(0.26f, 0.35f, 0.46f, 1f), -62f, 222f, 170f, 170f);
            LensAt(module, "LensA", -34f, 34f, 64f);
            LensAt(module, "LensB", -34f, -34f, 64f);
            LensAt(module, "LensC", 34f, 0f, 64f);
            Circle(host, "Flash", new Color(0.95f, 0.9f, 0.7f, 1f), 62f, 262f, 26f);
            Circle(host, "Logo", new Color(1f, 1f, 1f, 0.35f), 0f, -40f, 54f);
        }

        private static void DrawSide(RectTransform host)
        {
            Part(host, "Frame", Titanium, 0f, 0f, 44f, 660f);
            Part(host, "Button", TitaniumLight, 26f, 150f, 8f, 90f);
            Part(host, "VolumeA", TitaniumLight, -26f, 190f, 8f, 56f);
            Part(host, "VolumeB", TitaniumLight, -26f, 110f, 8f, 56f);
        }

        private static void DrawTopBottom(RectTransform host)
        {
            Part(host, "Top", Titanium, 0f, 120f, 330f, 44f);
            Part(host, "Bottom", Titanium, 0f, -120f, 330f, 44f);
            Part(host, "Port", Frame, 0f, -120f, 60f, 14f);
            Part(host, "SpeakerL", Frame, -92f, -120f, 8f, 8f);
            Part(host, "SpeakerR", Frame, 92f, -120f, 8f, 8f);
        }

        private static void DrawCameraClose(RectTransform host)
        {
            RectTransform module = Part(host, "Module", new Color(0.26f, 0.35f, 0.46f, 1f), 0f, 0f, 520f, 520f);
            LensAt(module, "LensA", -105f, 105f, 190f);
            LensAt(module, "LensB", -105f, -105f, 190f);
            LensAt(module, "LensC", 105f, 0f, 190f);
            Circle(module, "Flash", new Color(0.95f, 0.9f, 0.7f, 1f), 130f, 190f, 40f);
        }
    }

    /// <summary>
    /// Gerçek telefon görsellerinin bağlanacağı nokta: (modelKimliği, açı) → Sprite. null döndürürse mock çizilir.
    /// Örn. oyun açılışında bir Resources/Addressables yükleyicisi atanır; ekran kodu değişmez.
    /// </summary>
    internal static class PhoneImages
    {
        public static Func<string, PhoneAngle, Sprite> Provider;
    }
}
