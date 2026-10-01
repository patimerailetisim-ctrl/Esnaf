using System;
using UnityEngine;
using UnityEngine.UI;

namespace Esnaf.App.Ui
{
    /// <summary>
    /// Müşteri portresi. Gerçek portre asset'i yokken temiz bir silüet çizilir (UI parçalarıyla; dış asset yok). İleride
    /// <see cref="CustomerPortraits.Provider"/> bir NPC için Sprite döndürürse silüet yerine o görsel gösterilir (ekran kodu değişmez).
    /// Yalnızca görünüm; müşteri verisi taşımaz.
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

        /// <summary>Verilen kutuyu (anchor tam esnek) portreyle doldurur.</summary>
        public static void Draw(RectTransform host, string npcId)
        {
            UiKit.Clear(host);

            Sprite sprite = CustomerPortraits.Get(npcId);
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

            Color tone = Tones[Math.Abs(HashOf(npcId)) % Tones.Length];

            // Omuzlar (alt, taşan kısım RectMask2D ile kırpılır) + baş + ince altın halka.
            RectTransform shoulders = Part(host, "Shoulders", tone, 0f, -300f, 620f, 520f);
            Part(shoulders, "Collar", new Color(1f, 1f, 1f, 0.08f), 0f, 170f, 150f, 150f);
            Part(host, "HeadRing", UiTheme.Gold, 0f, 70f, 296f, 296f);
            Part(host, "Head", new Color(0.82f, 0.86f, 0.93f, 1f), 0f, 70f, 280f, 280f);
            Part(host, "Hair", tone, 0f, 150f, 280f, 130f);
        }

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

    /// <summary>Gerçek müşteri portrelerinin bağlanacağı nokta: npcKimliği → Sprite. null döndürürse silüet çizilir.</summary>
    internal static class CustomerPortraits
    {
        public static Func<string, Sprite> Provider;

        public static Sprite Get(string npcId)
        {
            return Provider == null || npcId == null ? null : Provider(npcId);
        }
    }
}
