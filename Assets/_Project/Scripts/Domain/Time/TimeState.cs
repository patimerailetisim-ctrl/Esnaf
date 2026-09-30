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

        public TimeState(int day, ulong masterSeed)
        {
            if (day < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(day), "Day must be at least 1.");
            }

            Day = day;
            MasterSeed = masterSeed;
        }

        /// <summary>Kayıttan yükleme: günü doğrudan yazar (en az 1).</summary>
        internal void Restore(int day)
        {
            if (day < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(day), "Day must be at least 1.");
            }

            Day = day;
        }

        /// <summary>Bir sonraki güne geçer ve yeni günü döndürür. Taşarsa OverflowException; gün değişmez.</summary>
        public int Advance()
        {
            int next = checked(Day + 1);
            Day = next;
            return next;
        }
    }
}
