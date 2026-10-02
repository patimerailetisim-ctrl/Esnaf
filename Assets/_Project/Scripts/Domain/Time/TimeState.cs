using System;

namespace Esnaf.Domain.Time
{
    /// <summary>
    /// Zaman durumu (kayda girer; GDD v0.3 4.3): gün numarası ve oyunun ana tohumu. Zaman = oyun günüdür;
    /// gerçek saat kural kodunda yoktur (K8). Rastgele akış durumları <c>RngStreams</c>'tedir.
    /// </summary>
    public sealed class TimeState
    {
        public int Day { get; private set; }
        public ulong MasterSeed { get; }

        /// <summary>
        /// Günün saati: gece yarısından beri dakika (Gün 12.1). Gün açılışta <see cref="StoreHours.OpenMinute"/> (09:00) ile başlar, yalnızca <see cref="AdvanceClock"/> ile ilerler,
        /// <see cref="StoreHours.CloseMinute"/> (21:00) üstüne çıkmaz. Yeni gün saati açılışa sıfırlar. Gerçek saat kullanılmaz.
        /// </summary>
        public int MinuteOfDay { get; private set; } = StoreHours.OpenMinute;

        public TimeState(int day, ulong masterSeed)
        {
            if (day < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(day), "Day must be at least 1.");
            }

            Day = day;
            MasterSeed = masterSeed;
        }

        /// <summary>Kayıttan yükleme: günü doğrudan yazar (en az 1); saat açılışa (09:00) döner.</summary>
        internal void Restore(int day)
        {
            Restore(day, StoreHours.OpenMinute);
        }

        /// <summary>Kayıttan yükleme: günü ve günün saatini doğrudan yazar (gün en az 1; saat açılış–kapanış aralığında).</summary>
        internal void Restore(int day, int minuteOfDay)
        {
            if (day < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(day), "Day must be at least 1.");
            }

            if (!StoreHours.IsValidMinute(minuteOfDay))
            {
                throw new ArgumentOutOfRangeException(nameof(minuteOfDay), "The minute of day must be between opening and closing time.");
            }

            Day = day;
            MinuteOfDay = minuteOfDay;
        }

        /// <summary>Saati ilerletir; kapanışta durur. Gerçekten ilerleyen dakikayı döndürür (≥ 0).</summary>
        internal int AdvanceClock(int minutes)
        {
            if (minutes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minutes), "Minutes must not be negative.");
            }

            int target = (int)Math.Min((long)MinuteOfDay + minutes, StoreHours.CloseMinute);
            int advanced = target - MinuteOfDay;
            MinuteOfDay = target;
            return advanced;
        }

        /// <summary>Bir sonraki güne geçer ve yeni günü döndürür. Taşarsa OverflowException; gün değişmez.</summary>
        public int Advance()
        {
            int next = checked(Day + 1);
            Day = next;
            MinuteOfDay = StoreHours.OpenMinute; // yeni gün mağaza açılışında başlar
            return next;
        }
    }
}
