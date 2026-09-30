using Esnaf.Core;

namespace Esnaf.Domain.Economy
{
    /// <summary>
    /// Ekonomi sabitleri (economy_constants.json; Day 4 için gereken alt küme): başlangıç sermayesi, günlük gider kuralı,
    /// başlangıç raf kapasitesi. Denge sayıları kodda değil veri dosyasındadır. Bu sınıf doğrulama yapmaz.
    /// </summary>
    public sealed class EconomyConstants
    {
        public Money OpeningCapital { get; }

        /// <summary>Günlük giderin başladığı ilk oyun günü (öncesinde gider 0).</summary>
        public int DailyExpenseFromDay { get; }

        public Money DailyExpenseAmount { get; }
        public int InitialShelfCapacity { get; }

        public EconomyConstants(Money openingCapital, int dailyExpenseFromDay, Money dailyExpenseAmount, int initialShelfCapacity)
        {
            OpeningCapital = openingCapital;
            DailyExpenseFromDay = dailyExpenseFromDay;
            DailyExpenseAmount = dailyExpenseAmount;
            InitialShelfCapacity = initialShelfCapacity;
        }
    }
}
