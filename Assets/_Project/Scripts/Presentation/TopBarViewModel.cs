using System;

namespace Esnaf.Presentation
{
    /// <summary>Üst barın iki metni: gün ve nakit. Değer eşitliği vardır (aynıysa ekran yeniden çizilmez).</summary>
    public sealed class TopBarViewModel : IEquatable<TopBarViewModel>
    {
        public string DayText { get; }
        public string CashText { get; }

        /// <summary>Günün saati ("09:00", Gün 12.5); boşsa gösterilmez.</summary>
        public string ClockText { get; }

        public TopBarViewModel(string dayText, string cashText, string clockText = "")
        {
            if (dayText == null)
            {
                throw new ArgumentNullException(nameof(dayText));
            }

            if (cashText == null)
            {
                throw new ArgumentNullException(nameof(cashText));
            }

            DayText = dayText;
            CashText = cashText;
            ClockText = clockText ?? string.Empty;
        }

        public bool Equals(TopBarViewModel other)
        {
            return other != null && DayText == other.DayText && CashText == other.CashText && ClockText == other.ClockText;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as TopBarViewModel);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(DayText, CashText, ClockText);
        }
    }
}
