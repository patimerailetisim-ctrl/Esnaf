namespace Esnaf.Domain.Time
{
    /// <summary>
    /// Gün sonu adımlarının sabit sıra numaraları (GDD v0.3 3.4). Bir adımın sahibi sistem henüz yoksa numarası boş kalır;
    /// sistem yazıldığında adımı bu numarayla listeye eklenir.
    /// </summary>
    public static class DayEndOrder
    {
        public const int MissedCustomers = 1;
        public const int DailyExpense = 2;
        public const int StockHoldingLoss = 3;
        public const int ListingExpiry = 4;
        public const int DemandUpdate = 5;
        public const int Progression = 6;
        public const int NewDay = 7;
        public const int AutoSave = 8;
    }
}
