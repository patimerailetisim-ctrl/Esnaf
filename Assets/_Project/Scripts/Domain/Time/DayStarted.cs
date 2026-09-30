namespace Esnaf.Domain.Time
{
    /// <summary>Bildirim: yeni gün başladı (ilanlar üretilmiş, gün numarası güncel).</summary>
    public sealed class DayStarted
    {
        public int Day { get; }

        public DayStarted(int day)
        {
            Day = day;
        }
    }
}
