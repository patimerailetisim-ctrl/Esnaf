using System;
using UnityEngine;
using UnityEngine.UI;

namespace Esnaf.App.Ui
{
    /// <summary>
    /// Müşteri portresi. Müşterinin ADINA göre <see cref="CustomerPortraits.Provider"/> (CustomerPortraitCatalog) gerçek portreyi verirse yalnızca o
    /// gösterilir (en-boy oranı korunur, yüz ezilmez); bulunamazsa temiz bir silüet çizilir (UI parçalarıyla; dış asset yok).
    /// Büyük satış ekranı ile müşteri kartı aynı yolu kullanır. Yalnızca görünüm; müşteri verisi taşımaz.
    /// </summary>
    internal static class CustomerPortraitView
    {
        private static readonly Color[] Tones =
        {
            new Color(0.2f, 0.27f, 0.42f, 1f),
            new Color(0.26f, 0.33f, 0.46f, 1f),
            new Color(0.3f, 0.3f, 0.4f, 1f),
            new Color(0.22f, 0.34f, 0.4f, 1f)
        };

        /// <summary>Verilen kutuyu portreyle doldurur: gerçek portre (müşteri adından) ya da silüet (<paramref name="npcId"/> yalnızca silüet rengini seçer).</summary>
        public static void Draw(RectTransform host, string customerName, string npcId)
        {
            UiKit.Clear(host);

            Sprite sprite = CustomerPortraits.Get(customerName);
            if (sprite != null)
            {
                var go = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(host, false);
                var image = go.GetComponent<Image>();
                image.sprite = sprite;
                image.preserveAspect = true;
                image.raycastTarget = false;
                UiBuilder.Stretch(image.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                return;
            }

            Color tone = Tones[Math.Abs(HashOf(npcId) % Tones.Length)];

            // Silüet: çerçevenin içine sığan KARE bir alanda, kesirli konumlarla çizilir (çerçeve ne olursa olsun daireler yuvarlak kalır).
            // Taşan omuzlar çerçevenin RectMask2D'si ile kırpılır.
            var squareGo = new GameObject("Silhouette", typeof(RectTransform), typeof(AspectRatioFitter));
            squareGo.transform.SetParent(host, false);
            var square = squareGo.GetComponent<RectTransform>();
            UiBuilder.Stretch(square, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var fitter = squareGo.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 1f;

            RectTransform shoulders = Part(square, "Shoulders", tone, 500f, 16f, 1000f, 839f);
            Part(shoulders, "Collar", new Color(1f, 1f, 1f, 0.08f), 500f, 800f, 220f, 260f);
            Part(square, "HeadRing", UiTheme.Gold, 500f, 613f, 477f, 477f);
            Part(square, "Head", new Color(0.82f, 0.86f, 0.93f, 1f), 500f, 613f, 452f, 452f);
            Part(square, "Hair", tone, 500f, 742f, 452f, 210f);
        }

        // Parça: (merkez x, merkez y, genişlik, yükseklik) 1000x1000 birimlik karede; anchor kesirleriyle yerleşir.
        private static RectTransform Part(Transform parent, string name, Color color, float cx, float cy, float w, float h)
        {
            RectTransform rect = UiKit.Rounded(parent, name, color);
            rect.anchorMin = new Vector2((cx - w / 2f) / 1000f, (cy - h / 2f) / 1000f);
            rect.anchorMax = new Vector2((cx + w / 2f) / 1000f, (cy + h / 2f) / 1000f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.GetComponent<Image>().raycastTarget = false;
            return rect;
        }

        private static int HashOf(string text)
        {
            int hash = 17;
            foreach (char c in text ?? string.Empty)
            {
                hash = hash * 31 + c;
            }

            return hash;
        }
    }

    /// <summary>Gerçek müşteri portrelerinin bağlanacağı nokta: müşteri adı → Sprite. null döndürürse silüet çizilir.</summary>
    internal static class CustomerPortraits
    {
        public static Func<string, Sprite> Provider;

        public static Sprite Get(string customerName)
        {
            return Provider == null || customerName == null ? null : Provider(customerName);
        }
    }
}
