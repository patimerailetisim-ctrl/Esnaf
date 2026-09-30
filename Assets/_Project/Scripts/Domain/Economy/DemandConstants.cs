namespace Esnaf.Domain.Economy
{
    /// <summary>Talep modeli sabitleri (economy_constants.json "demand"; GDD v0.2 10.2).</summary>
    public sealed class DemandConstants
    {
        /// <summary>Talebin canlı olduğu ilk gün; öncesinde tüm modeller 1,00.</summary>
        public int LiveFromDay { get; }

        /// <summary>Günlük rastgele değişim: ± bu kadar.</summary>
        public double DailyNoise { get; }

        /// <summary>Ortalamaya çekim: talep += oran × (1 − talep).</summary>
        public double MeanReversion { get; }

        public double Min { get; }
        public double Max { get; }

        /// <summary>Son penceredeki her satış talebi bu çarpanla çarpar (0,985).</summary>
        public double PressurePerSale { get; }

        public int PressureWindowDays { get; }

        /// <summary>Satış baskısının en düşük çarpanı (en fazla −%10).</summary>
        public double PressureFloor { get; }

        public DemandConstants(
            int liveFromDay,
            double dailyNoise,
            double meanReversion,
            double min,
            double max,
            double pressurePerSale,
            int pressureWindowDays,
            double pressureFloor)
        {
            LiveFromDay = liveFromDay;
            DailyNoise = dailyNoise;
            MeanReversion = meanReversion;
            Min = min;
            Max = max;
            PressurePerSale = pressurePerSale;
            PressureWindowDays = pressureWindowDays;
            PressureFloor = pressureFloor;
        }
    }
}
