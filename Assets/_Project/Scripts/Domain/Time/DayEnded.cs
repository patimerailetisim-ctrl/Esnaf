namespace Esnaf.Domain.Time
{
    /// <summary>Bildirim: bir gün bitti (yeni gün açılmadan hemen önce). Oyun doğruluğu bu olaya bağlı değildir (K5).</summary>
    public sealed class DayEnded
    {
        public int Day { get; }

        public DayEnded(int day)
        {
            Day = day;
        }
    }
}
