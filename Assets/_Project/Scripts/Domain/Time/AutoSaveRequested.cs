namespace Esnaf.Domain.Time
{
    /// <summary>
    /// Bildirim: gün sonu boru hattının son adımı otomatik kayıt istedi (GDD v0.3 3.4 adım 8). Domain kaydı kendisi yapmaz;
    /// kayıt Persistence'ın işidir (UA2). Gün tamamen bitmiş ve yeni gün açılmış olarak yayınlanır.
    /// </summary>
    public sealed class AutoSaveRequested
    {
        /// <summary>Yeni günün numarası.</summary>
        public int Day { get; }

        public AutoSaveRequested(int day)
        {
            Day = day;
        }
    }
}
