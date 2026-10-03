using Esnaf.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Esnaf.App.Ui
{
    /// <summary>
    /// Alt navigasyon ikonları (Gün 13.4 düzeltme): <see cref="NavIconRaster"/>'ın çizdiği kalın çizgili, kenarları yumuşak 128×128 ikonlar çalışma anında beyaz bir Sprite'a çevrilir
    /// (dış asset yok, emoji yok) ve <see cref="Image.color"/> ile renklendirilir: aktif = Kârbaz altını, pasif = açık gri. Dört ikon aynı aileden, aynı çizgi kalınlığında ve kenar boşluğundadır.
    /// Yalnızca görünüm.
    /// </summary>
    internal static class NavIcons
    {
        /// <summary>Ekranda gösterilen ikon alanı (piksel).</summary>
        public const float Size = 56f;

        private static readonly Sprite[] Cache = new Sprite[4];

        public static Sprite SpriteOf(NavTab tab)
        {
            int index = (int)tab;
            if (Cache[index] == null)
            {
                Cache[index] = Build(tab);
            }

            return Cache[index];
        }

        /// <summary>İkonu verilen alana tek bir Image olarak koyar (host'un kendisi Image olur) ve rengini ayarlar.</summary>
        public static void Draw(RectTransform host, NavTab tab, Color color)
        {
            Image image = host.GetComponent<Image>();
            if (image == null)
            {
                image = host.gameObject.AddComponent<Image>();
            }

            image.sprite = SpriteOf(tab);
            image.color = color;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        private static Sprite Build(NavTab tab)
        {
            byte[] alpha = NavIconRaster.Render(tab);
            var texture = new Texture2D(NavIconRaster.Size, NavIconRaster.Size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[alpha.Length];
            for (int i = 0; i < alpha.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, alpha[i]);
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, NavIconRaster.Size, NavIconRaster.Size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
