using System;

namespace Esnaf.Presentation
{
    /// <summary>
    /// Alt navigasyon ikonları (Gün 13.4 düzeltme): dış asset ve emoji olmadan, işaretli uzaklık alanlarıyla (SDF) çizilen KALIN ÇİZGİLİ, kenarları yumuşatılmış (anti-aliased) 128×128 ikonlar.
    /// Dükkan (tente + vitrin + kapı), Toptancı (kamyon: kasa, kabin, iki tekerlek), İlanlar (telefon + liste çizgileri), Profil (baş + omuzlar). Hepsi aynı çizgi kalınlığında, aynı
    /// kenar boşluğunda ve benzer görsel ağırlıkta tek bir aileden. Unity'yi bilmez: yalnızca alfa kanalı (0–255; indeks = y × Size + x, y aşağıdan yukarı) döner;
    /// Unity tarafı bunu beyaz bir dokuya çevirip renklendirir. Oyun kuralı yoktur.
    /// </summary>
    public static class NavIconRaster
    {
        public const int Size = 128;

        /// <summary>Çizgi kalınlığı (piksel; 128'lik alanda). Dört ikonda da aynıdır.</summary>
        public const float Stroke = 9f;

        public static byte[] Render(NavTab tab)
        {
            Func<float, float, float> shape = ShapeOf(tab);
            var alpha = new byte[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float d = shape(x + 0.5f, y + 0.5f);
                    float coverage = Math.Min(1f, Math.Max(0f, 0.5f - d)); // 1 piksellik yumuşak kenar
                    alpha[y * Size + x] = (byte)Math.Round(coverage * 255f);
                }
            }

            return alpha;
        }

        private static Func<float, float, float> ShapeOf(NavTab tab)
        {
            switch (tab)
            {
                case NavTab.Shop:
                    return Shop;
                case NavTab.Wholesale:
                    return Truck;
                case NavTab.Listings:
                    return Phone;
                default:
                    return Person;
            }
        }

        // ---------- ikonlar ----------

        // Dükkan: tente (üst çubuk + 4 tente yuvarlağı), mağaza gövdesi (çerçeve) ve ortada kapı.
        private static float Shop(float x, float y)
        {
            float awning = RoundRect(x, y, 64f, 102f, 52f, 7f, 4f);
            float scallops = Min(
                Circle(x, y, 26.5f, 92f, 11f),
                Circle(x, y, 51.5f, 92f, 11f),
                Circle(x, y, 76.5f, 92f, 11f),
                Circle(x, y, 101.5f, 92f, 11f));
            float body = Outline(RoundRect(x, y, 64f, 44f, 42f, 32f, 5f));
            float door = RoundRect(x, y, 64f, 31f, 10f, 18f, 3f);
            return Min(awning, scallops, body, door);
        }

        // Toptancı: sağa bakan teslimat kamyonu — kasa (çerçeve), kabin (dolu, pencereli), iki tekerlek (jantlı).
        private static float Truck(float x, float y)
        {
            float cargo = Outline(RoundRect(x, y, 41f, 64f, 29f, 24f, 5f));
            float cab = Polygon(x, y, 72f, 40f, 72f, 80f, 94f, 80f, 116f, 55f, 116f, 40f);
            float cabWindow = Polygon(x, y, 79f, 74f, 79f, 62f, 100f, 62f, 92f, 74f);
            float cabWithWindow = Math.Max(cab, -cabWindow);
            float wheelBack = Math.Max(Circle(x, y, 33f, 30f, 14f), -Circle(x, y, 33f, 30f, 5f));
            float wheelFront = Math.Max(Circle(x, y, 92f, 30f, 14f), -Circle(x, y, 92f, 30f, 5f));
            return Min(cargo, cabWithWindow, wheelBack, wheelFront);
        }

        // İlanlar: solda telefon (çerçeve, hoparlör, ana düğme), sağda üç liste çizgisi.
        private static float Phone(float x, float y)
        {
            float frame = Outline(RoundRect(x, y, 42f, 64f, 25f, 46f, 9f));
            float speaker = Capsule(x, y, 34f, 98f, 50f, 98f, 2.5f);
            float home = Circle(x, y, 42f, 30f, 4f);
            float lines = Min(
                Capsule(x, y, 86f, 92f, 114f, 92f, 4.5f),
                Capsule(x, y, 86f, 64f, 114f, 64f, 4.5f),
                Capsule(x, y, 86f, 36f, 114f, 36f, 4.5f));
            return Min(frame, speaker, home, lines);
        }

        // Profil: baş (halka) + omuzlar (düz tabanlı kubbe çerçevesi).
        private static float Person(float x, float y)
        {
            float head = Outline(Circle(x, y, 64f, 90f, 18f));
            float dome = Math.Max(RoundRect(x, y, 64f, 6f, 44f, 52f, 44f), 14f - y); // yarım disk: düz taban, yuvarlak omuz çizgisi
            return Min(head, Outline(dome));
        }

        // ---------- işaretli uzaklık yardımcıları ----------

        private static float Outline(float d)
        {
            return Math.Abs(d) - Stroke / 2f;
        }

        private static float Min(params float[] values)
        {
            float m = values[0];
            for (int i = 1; i < values.Length; i++)
            {
                m = Math.Min(m, values[i]);
            }

            return m;
        }

        private static float Circle(float x, float y, float cx, float cy, float r)
        {
            return (float)Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;
        }

        private static float RoundRect(float x, float y, float cx, float cy, float hx, float hy, float r)
        {
            float qx = Math.Abs(x - cx) - hx + r;
            float qy = Math.Abs(y - cy) - hy + r;
            float outside = (float)Math.Sqrt(Math.Max(qx, 0f) * Math.Max(qx, 0f) + Math.Max(qy, 0f) * Math.Max(qy, 0f));
            return outside + Math.Min(Math.Max(qx, qy), 0f) - r;
        }

        private static float Capsule(float x, float y, float ax, float ay, float bx, float by, float r)
        {
            float pax = x - ax;
            float pay = y - ay;
            float bax = bx - ax;
            float bay = by - ay;
            float h = Math.Min(1f, Math.Max(0f, (pax * bax + pay * bay) / (bax * bax + bay * bay)));
            float dx = pax - bax * h;
            float dy = pay - bay * h;
            return (float)Math.Sqrt(dx * dx + dy * dy) - r;
        }

        // Dışbükey/içbükey çokgen için işaretli uzaklık (köşe çiftleri x0,y0,x1,y1,...).
        private static float Polygon(float x, float y, params float[] v)
        {
            int n = v.Length / 2;
            float d = (x - v[0]) * (x - v[0]) + (y - v[1]) * (y - v[1]);
            float s = 1f;
            for (int i = 0, j = n - 1; i < n; j = i, i++)
            {
                float ex = v[2 * j] - v[2 * i];
                float ey = v[2 * j + 1] - v[2 * i + 1];
                float wx = x - v[2 * i];
                float wy = y - v[2 * i + 1];
                float t = Math.Min(1f, Math.Max(0f, (wx * ex + wy * ey) / (ex * ex + ey * ey)));
                float bx = wx - ex * t;
                float by = wy - ey * t;
                d = Math.Min(d, bx * bx + by * by);
                bool c1 = y >= v[2 * i + 1];
                bool c2 = y < v[2 * j + 1];
                bool c3 = ex * wy > ey * wx;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3))
                {
                    s = -s;
                }
            }

            return s * (float)Math.Sqrt(d);
        }
    }
}
