using Esnaf.Core;

namespace Esnaf.Domain.Negotiation
{
    /// <summary>
    /// Satış pazarlığının başlangıç durumu (değişmez). Müşterinin Max'ı (M) gizli değerdir; yalnızca Domain içinde kalır.
    /// </summary>
    public sealed class SaleSetup
    {
        /// <summary>Müşterinin açılış teklifi (M'nin açılış oranı, 10 TL'ye yuvarlı).</summary>
        public Money Opening { get; }

        /// <summary>Gizli: müşterinin en çok ödeyeceği tutar (M).</summary>
        public double Max { get; }

        public int Patience { get; }
        public int Trust { get; }
        public double Urgency { get; }
        public int Day { get; }

        public SaleSetup(Money opening, double max, int patience, int trust, double urgency, int day)
        {
            Opening = opening;
            Max = max;
            Patience = patience;
            Trust = trust;
            Urgency = urgency;
            Day = day;
        }
    }
}
