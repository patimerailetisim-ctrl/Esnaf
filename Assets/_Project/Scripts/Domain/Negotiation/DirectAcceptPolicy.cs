using System;

namespace Esnaf.Domain.Negotiation
{
    /// <summary>
    /// Müşterinin oyuncunun İSTEDİĞİ fiyatı pazarlıksız KABUL EDEBİLECEĞİ tavanı (M'nin oranı) mevcut müşteri kişilik sayılarından türetir. SAF ve DETERMİNİSTİKTİR:
    /// rastgelelik yok, kayıtta durum yok (müşteri NPC'sinin değişmez sayıları + o anki güvenden hesaplanır).
    ///
    /// Müşteri, istenen fiyat <c>≤ oran × M</c> ise (ve müşterinin kendi teklifinden yüksekse) istenen fiyata anlaşır; yoksa mevcut pazarlık sürer. Oran her zaman
    /// <c>[<see cref="MinRatio"/>, 1]</c> aralığındadır, yani müşteri hiçbir zaman M'nin üstünü ödemez (I4) ve hiçbir müşteri kişilikten bağımsız "hep pazarlık" ya da "hep kabul" olmaz.
    ///
    /// Kolaylaştıranlar: ACELE (urgency), BÜTÇE esnekliği (valueRatio), SABIR azlığı (düşük patience pazarlıktan çabuk sıkılır), BİLGİ (düşük valueSigma: değeri bilir,
    /// makul fiyatı tereddütsüz alır) ve GÜVEN. Zorlaştıranlar: PAZARLIK eğilimi (düşük açılış teklifi oranı) ve bilgisizlik.
    /// </summary>
    public static class DirectAcceptPolicy
    {
        public const double MinRatio = 0.75;
        private const double Base = 0.82;

        public static double Ratio(double openingOfferRatio, double valueRatio, int patience, double urgency, double valueSigma, int trust)
        {
            double ratio = Base
                + 0.5 * (openingOfferRatio - 0.85)   // pazarlık eğilimi: düşük açılış = pazarlıkçı
                + 0.10 * urgency                      // acele
                + 0.5 * (valueRatio - 1.0)           // bütçe esnekliği
                + 0.015 * (5 - patience)              // sabırsız: pazarlığa girmek istemez
                + 0.3 * (0.10 - valueSigma)           // bilgi: değeri bilen tereddüt etmez
                + 0.10 * ((trust - 50) / 100.0);      // güven
            return Math.Max(MinRatio, Math.Min(1.0, ratio));
        }
    }
}
