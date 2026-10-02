namespace Esnaf.Domain.Time
{
    /// <summary>Günün saatinin salt okunur görünümü (IGameApi.GetClock). Saat gerçek zamanlı değildir; oyun aksiyonlarıyla ilerler.</summary>
    public sealed class ClockView
    {
        public int Day { get; }

        /// <summary>Gece yarısından beri dakika (açılış 540, kapanış 1260).</summary>
        public int MinuteOfDay { get; }

        public int Hour
        {
            get { return MinuteOfDay / 60; }
        }

        public int Minute
        {
            get { return MinuteOfDay % 60; }
        }

        /// <summary>"09:00" biçimi.</summary>
        public string Text
        {
            get { return StoreHours.Format(MinuteOfDay); }
        }

        /// <summary>Mağaza açık mı (kapanış saatinden önce)?</summary>
        public bool IsOpen
        {
            get { return MinuteOfDay < StoreHours.CloseMinute; }
        }

        /// <summary>Kapanışa kalan dakika (kapanınca 0).</summary>
        public int MinutesLeft
        {
            get { return StoreHours.CloseMinute - MinuteOfDay; }
        }

        public ClockView(int day, int minuteOfDay)
        {
            Day = day;
            MinuteOfDay = minuteOfDay;
        }
    }
}
