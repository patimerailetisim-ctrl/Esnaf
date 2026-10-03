using Esnaf.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Esnaf.App.Ui
{
    /// <summary>
    /// Alt navigasyon ikonları (Gün 13.4): dış asset ve emoji olmadan, yuvarlatılmış UI parçalarından çizilen düz vektör tarzı ikonlar (dükkan, kamyon, telefon, kullanıcı).
    /// Hepsi 64×64 birimlik aynı tasarım alanında, aynı çizgi kalınlığında ve ölçüde; <paramref name="cutout"/> içi boş görünen parçaların (kapı, ekran) arka plan rengidir.
    /// Yalnızca görünüm.
    /// </summary>
    internal static class NavIcons
    {
        public const float Size = 64f;

        public static void Draw(RectTransform host, NavTab tab, Color color, Color cutout)
        {
            switch (tab)
            {
                case NavTab.Shop:
                    DrawShop(host, color, cutout);
                    break;
                case NavTab.Wholesale:
                    DrawTruck(host, color, cutout);
                    break;
                case NavTab.Listings:
                    DrawPhone(host, color, cutout);
                    break;
                default:
                    DrawUser(host, color);
                    break;
            }
        }

        // Dükkan: tente + üç tente yuvarlağı + gövde + kapı.
        private static void DrawShop(RectTransform host, Color c, Color cut)
        {
            Piece(host, "Awning", c, 0f, 20f, 58f, 16f);
            Piece(host, "Body", c, 0f, -8f, 46f, 30f);
            Piece(host, "Door", cut, -1f, -12f, 14f, 22f);
            Piece(host, "Window", cut, 14f, -6f, 10f, 10f);
        }

        // Teslimat kamyonu: yük kasası + kabin + iki tekerlek.
        private static void DrawTruck(RectTransform host, Color c, Color cut)
        {
            Piece(host, "Cargo", c, -8f, 5f, 38f, 28f);
            Piece(host, "Cab", c, 20f, 0f, 20f, 20f);
            Piece(host, "WheelBack", c, -14f, -14f, 15f, 15f);
            Piece(host, "WheelBackHub", cut, -14f, -14f, 6f, 6f);
            Piece(host, "WheelFront", c, 16f, -14f, 15f, 15f);
            Piece(host, "WheelFrontHub", cut, 16f, -14f, 6f, 6f);
        }

        // Telefon: dış gövde + ekran + hoparlör + ana düğme.
        private static void DrawPhone(RectTransform host, Color c, Color cut)
        {
            Piece(host, "Frame", c, 0f, 0f, 34f, 58f);
            Piece(host, "Screen", cut, 0f, 1f, 26f, 42f);
            Piece(host, "Speaker", c, 0f, 22f, 12f, 3f);
            Piece(host, "Home", c, 0f, -22f, 5f, 5f);
        }

        // Kullanıcı: baş (daire) + omuzlar.
        private static void DrawUser(RectTransform host, Color c)
        {
            Piece(host, "Head", c, 0f, 13f, 24f, 24f);
            Piece(host, "Shoulders", c, 0f, -14f, 46f, 26f);
        }

        // Tasarım alanında (merkez orijin, y yukarı) yuvarlatılmış dikdörtgen/daire parçası.
        private static void Piece(RectTransform host, string name, Color color, float cx, float cy, float w, float h)
        {
            RectTransform rect = UiKit.Rounded(host, name, color);
            rect.GetComponent<Image>().raycastTarget = false;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(w, h);
            rect.anchoredPosition = new Vector2(cx, cy);
        }
    }
}
