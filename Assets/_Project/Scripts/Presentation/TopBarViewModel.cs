using System;

namespace Esnaf.Presentation
{
    /// <summary>Üst barın iki metni: gün ve nakit. Değer eşitliği vardır (aynıysa ekran yeniden çizilmez).</summary>
    public sealed class TopBarViewModel : IEquatable<TopBarViewModel>
    {
        public string DayText { get; }
        public string CashText { get; }

        public TopBarViewModel(string dayText, string cashText)
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
        }

        public bool Equals(TopBarViewModel other)
        {
            return other != null && DayText == other.DayText && CashText == other.CashText;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as TopBarViewModel);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(DayText, CashText);
        }
    }
}
