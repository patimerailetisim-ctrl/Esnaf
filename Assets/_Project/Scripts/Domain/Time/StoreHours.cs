using System.Globalization;

namespace Esnaf.Domain.Time
{
    /// <summary>
    /// Mağazanın günlük çalışma saatleri (Gün 12.1): açılış 09:00, kapanış 21:00. Saat GERÇEK zamanlı değildir; günün dakikası olarak (gece yarısından beri) tutulur ve
    /// yalnızca oyun aksiyonlarıyla ilerler. Sabitler kod sabitidir (içerik/şema değişmez).
    /// </summary>
    public static class StoreHours
    {
        public const int OpenMinute = 9 * 60;
        public const int CloseMinute = 21 * 60;

        /// <summary>Açılıştan kapanışa kadar açık kalınan dakika (720).</summary>
        public const int OpenMinutes = CloseMinute - OpenMinute;

        /// <summary>"09:00" biçimi (24 saat, iki haneli).</summary>
        public static string Format(int minuteOfDay)
        {
            int hour = minuteOfDay / 60;
            int minute = minuteOfDay % 60;
            return hour.ToString("00", CultureInfo.InvariantCulture) + ":" + minute.ToString("00", CultureInfo.InvariantCulture);
        }

        public static bool IsValidMinute(int minuteOfDay)
        {
            return minuteOfDay >= OpenMinute && minuteOfDay <= CloseMinute;
        }
    }
}
