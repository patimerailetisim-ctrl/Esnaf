using System.Globalization;
using System.Text;
using Esnaf.Core;

namespace Esnaf.Presentation
{
    /// <summary>
    /// Para gösterimi: Türkçe binlik ayracı (nokta) ve lira işareti, ör. "250.000 ₺". Kültürden bağımsızdır (kural sabittir).
    /// Yalnızca gösterimdir; para hesabı <see cref="Money"/>'dedir.
    /// </summary>
    public static class MoneyFormatter
    {
        public const char Symbol = '₺';

        public static string Format(Money money)
        {
            long value = money.Tl;
            bool negative = value < 0;
            // long.MinValue'nun mutlak değeri long'a sığmaz: negatif tarafta rakamları çıkarırız.
            string digits = negative
                ? (value == long.MinValue ? "9223372036854775808" : (-value).ToString(CultureInfo.InvariantCulture))
                : value.ToString(CultureInfo.InvariantCulture);

            var sb = new StringBuilder();
            if (negative)
            {
                sb.Append('-');
            }

            for (int i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (digits.Length - i) % 3 == 0)
                {
                    sb.Append('.');
                }

                sb.Append(digits[i]);
            }

            sb.Append(' ').Append(Symbol);
            return sb.ToString();
        }
    }
}
