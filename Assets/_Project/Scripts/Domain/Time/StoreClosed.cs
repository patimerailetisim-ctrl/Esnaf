namespace Esnaf.Domain.Time
{
    /// <summary>Bildirim: günün saati mağaza kapanışına (21:00) ulaştı. Gün bitmez; "Günü Bitir" oyuncunun kararıdır.</summary>
    public sealed class StoreClosed
    {
        public int Day { get; }

        public StoreClosed(int day)
        {
            Day = day;
        }
    }
}
