using System.Linq;
using Esnaf.Presentation;
using NUnit.Framework;

namespace Esnaf.Tests.Presentation
{
    /// <summary>
    /// Gün 13.4 düzeltme — alt navigasyon ikonları (NavIconRaster): dört ikon aynı aileden (aynı çizgi kalınlığı, kenar boşluğu, benzer görsel ağırlık), kenarları yumuşak, birbirinden
    /// belirgin biçimde farklı ve her biri tanınır yapıya sahip: Dükkan (tente + kapı), Toptancı (kamyon), İlanlar (telefon + liste çizgileri, oval DEĞİL), Profil (baş + omuzlar).
    /// </summary>
    public class NavIconRasterTests
    {
        private static readonly NavTab[] Tabs = { NavTab.Shop, NavTab.Wholesale, NavTab.Listings, NavTab.Profile };

        private static int At(byte[] a, int x, int y)
        {
            return a[y * NavIconRaster.Size + x];
        }

        private static double Ink(byte[] a)
        {
            return a.Sum(v => (double)v) / 255.0 / a.Length;
        }

        [Test]
        public void EveryIcon_HasTheRightSize_IsNotEmpty_AndIsDeterministic()
        {
            foreach (NavTab tab in Tabs)
            {
                byte[] a = NavIconRaster.Render(tab);

                Assert.AreEqual(NavIconRaster.Size * NavIconRaster.Size, a.Length);
                Assert.Greater(Ink(a), 0.1, tab + " boş değil");
                CollectionAssert.AreEqual(a, NavIconRaster.Render(tab), tab + " deterministik");
            }
        }

        [Test]
        public void TheFourIcons_HaveSimilarVisualWeight()
        {
            double[] ink = Tabs.Select(t => Ink(NavIconRaster.Render(t))).ToArray();

            foreach (double i in ink)
            {
                Assert.Greater(i, 0.12);
                Assert.Less(i, 0.40);
            }

            Assert.LessOrEqual(ink.Max() / ink.Min(), 2.2, "aynı aile: görsel ağırlıklar birbirine yakın");
        }

        [Test]
        public void EveryIcon_KeepsAMarginToTheEdge()
        {
            const int margin = 4;
            foreach (NavTab tab in Tabs)
            {
                byte[] a = NavIconRaster.Render(tab);
                for (int y = 0; y < NavIconRaster.Size; y++)
                {
                    for (int x = 0; x < NavIconRaster.Size; x++)
                    {
                        bool border = x < margin || y < margin || x >= NavIconRaster.Size - margin || y >= NavIconRaster.Size - margin;
                        if (border)
                        {
                            Assert.AreEqual(0, At(a, x, y), tab + " kenara taşmaz: " + x + "," + y);
                        }
                    }
                }
            }
        }

        [Test]
        public void TheEdgesAreSoft_NotJaggedPlaceholders()
        {
            foreach (NavTab tab in Tabs)
            {
                byte[] a = NavIconRaster.Render(tab);

                Assert.Greater(a.Count(v => v > 0 && v < 255), 200, tab + " anti-aliased kenar");
                Assert.Greater(a.Count(v => v == 255), 500, tab + " dolu çizgiler");
            }
        }

        [Test]
        public void TheIconsAreClearlyDifferentFromEachOther()
        {
            for (int i = 0; i < Tabs.Length; i++)
            {
                for (int j = i + 1; j < Tabs.Length; j++)
                {
                    byte[] a = NavIconRaster.Render(Tabs[i]);
                    byte[] b = NavIconRaster.Render(Tabs[j]);
                    int different = Enumerable.Range(0, a.Length).Count(k => System.Math.Abs(a[k] - b[k]) > 128);

                    Assert.Greater(different / (double)a.Length, 0.12, Tabs[i] + " ve " + Tabs[j] + " ayırt edilebilir");
                }
            }
        }

        [Test]
        public void Shop_HasAnAwning_AHollowBody_AndADoor()
        {
            byte[] a = NavIconRaster.Render(NavTab.Shop);

            Assert.Greater(At(a, 64, 102), 200, "tente üst çubuğu");
            Assert.Greater(At(a, 26, 88), 200, "tente yuvarlakları");
            Assert.Greater(At(a, 64, 31), 200, "kapı");
            Assert.Less(At(a, 40, 40), 30, "gövde içi boş (çerçeve)");
            Assert.Greater(At(a, 22, 44), 200, "gövde çerçevesi (sol kenar)");
        }

        [Test]
        public void Wholesale_IsADeliveryTruck_WithCargoCabAndTwoWheels()
        {
            byte[] a = NavIconRaster.Render(NavTab.Wholesale);

            Assert.Less(At(a, 41, 64), 30, "kasa içi boş (çerçeve)");
            Assert.Greater(At(a, 41, 88), 200, "kasa çerçevesi (üst)");
            Assert.Greater(At(a, 100, 45), 200, "kabin dolu");
            Assert.Less(At(a, 92, 68), 30, "kabin penceresi");
            Assert.Greater(At(a, 33, 19), 200, "arka tekerlek halkası");
            Assert.Less(At(a, 33, 30), 30, "arka jant deliği");
            Assert.Greater(At(a, 92, 19), 200, "ön tekerlek halkası");
            Assert.Less(At(a, 92, 30), 30, "ön jant deliği");
        }

        [Test]
        public void Listings_IsAPhoneWithListLines_NotAnOval()
        {
            byte[] a = NavIconRaster.Render(NavTab.Listings);

            Assert.Greater(At(a, 17, 64), 200, "telefon çerçevesi (sol kenar)");
            Assert.Less(At(a, 42, 64), 30, "telefon ekranı boş");
            Assert.Greater(At(a, 42, 30), 200, "ana düğme");
            Assert.Greater(At(a, 42, 98), 200, "hoparlör");
            Assert.Greater(At(a, 100, 92), 200, "liste çizgisi 1");
            Assert.Greater(At(a, 100, 64), 200, "liste çizgisi 2");
            Assert.Greater(At(a, 100, 36), 200, "liste çizgisi 3");
            Assert.Less(At(a, 100, 78), 30, "çizgiler arası boş");
        }

        [Test]
        public void Profile_IsAHeadAndShoulders()
        {
            byte[] a = NavIconRaster.Render(NavTab.Profile);

            Assert.Greater(At(a, 64, 109), 200, "baş halkası (üst)");
            Assert.Less(At(a, 64, 90), 30, "baş içi boş");
            Assert.Greater(At(a, 64, 12), 200, "omuzların düz tabanı");
            Assert.Less(At(a, 64, 40), 30, "omuz içi boş");
            Assert.Greater(At(a, 22, 20), 200, "omuz yan çizgisi");
        }
    }
}
