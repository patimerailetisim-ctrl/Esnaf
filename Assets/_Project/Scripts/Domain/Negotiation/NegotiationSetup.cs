using Esnaf.Core;

namespace Esnaf.Domain.Negotiation
{
    /// <summary>
    /// Pazarlığın başlangıç durumu (değişmez). Ret fiyatı ve taban gizli değerlerdir; yalnızca Domain içinde kalır.
    /// </summary>
    public sealed class NegotiationSetup
    {
        public Money Ask { get; }

        /// <summary>Satıcının o günkü ret fiyatı R (ruh hali dahil).</summary>
        public double Reject { get; }

        /// <summary>Koz kartlarının R'yi indirebileceği en düşük değer (gerçek değer × taban oranı); 0 = taban yok.</summary>
        public double Floor { get; }

        public int Patience { get; }
        public int Trust { get; }
        public double Urgency { get; }
        public double Persuasion { get; }
        public double WrongCardMultiplier { get; }
        public int Day { get; }

        public NegotiationSetup(
            Money ask,
            double reject,
            double floor,
            int patience,
            int trust,
            double urgency,
            double persuasion,
            double wrongCardMultiplier,
            int day)
        {
            Ask = ask;
            Reject = reject;
            Floor = floor;
            Patience = patience;
            Trust = trust;
            Urgency = urgency;
            Persuasion = persuasion;
            WrongCardMultiplier = wrongCardMultiplier;
            Day = day;
        }
    }
}
