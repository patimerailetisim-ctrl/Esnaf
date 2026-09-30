using System;
using System.Globalization;
using System.Numerics;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Esnaf.Persistence
{
    /// <summary>
    /// Kayıt metninin KANONİK yazıcısı (sağlama ve dosya). Amaç: Windows/.NET, Mono, IL2CPP'de AYNI metin → AYNI sağlama.
    /// Çalışma zamanının double.ToString davranışına güvenilmez (eski Mono "R": 15 sonra 17 hane; modern .NET: en kısa gidiş-dönüş).
    /// Kural: double, tam değerinden (BigInteger) hesaplanan EN KISA (1..17 hane) ondalık metinle yazılır ve o metin aynı double'a geri döner
    /// (aralık testi de tam sayıyla yapılır; ayrıştırıcıya bağlı değildir). Biçim modern .NET'in "R" çıktısıyla bire bir aynıdır
    /// (ondalık üs ≥ 17 veya ≤ -5 → "d.dddE+XX", aksi halde ondalık; tam sayı değerli double'a ".0" eklenir), bu sayede .NET ile yazılmış v1 kayıtlar değişmeden okunur.
    /// Tamsayılar (long/ulong) double'a çevrilmez; metinler JSON.NET'in sabit kaçışıyla yazılır; satır sonu her zaman "\n", girinti iki boşluk.
    /// NaN/Infinity JSON.NET sabitleriyle yazılır (oyun durumu sonlu sayı üretir; yalnızca tanımlı kalsın diye).
    /// </summary>
    public static class CanonicalJson
    {
        private const int SciUpper = 17;
        private const int SciLower = -5;
        private const string Indent = "  ";

        public static string Write(JToken token, bool indented)
        {
            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            var sb = new StringBuilder();
            WriteToken(sb, token, indented, 0);
            return sb.ToString();
        }

        private static void WriteToken(StringBuilder sb, JToken token, bool indented, int depth)
        {
            JObject obj = token as JObject;
            if (obj != null)
            {
                WriteObject(sb, obj, indented, depth);
                return;
            }

            JArray array = token as JArray;
            if (array != null)
            {
                WriteArray(sb, array, indented, depth);
                return;
            }

            WriteValue(sb, token);
        }

        private static void WriteObject(StringBuilder sb, JObject obj, bool indented, int depth)
        {
            if (obj.Count == 0)
            {
                sb.Append("{}");
                return;
            }

            sb.Append('{');
            bool first = true;
            foreach (JProperty property in obj.Properties())
            {
                if (!first)
                {
                    sb.Append(',');
                }

                first = false;
                NewLine(sb, indented, depth + 1);
                sb.Append(JsonConvert.ToString(property.Name));
                sb.Append(indented ? ": " : ":");
                WriteToken(sb, property.Value, indented, depth + 1);
            }

            NewLine(sb, indented, depth);
            sb.Append('}');
        }

        private static void WriteArray(StringBuilder sb, JArray array, bool indented, int depth)
        {
            if (array.Count == 0)
            {
                sb.Append("[]");
                return;
            }

            sb.Append('[');
            bool first = true;
            foreach (JToken item in array)
            {
                if (!first)
                {
                    sb.Append(',');
                }

                first = false;
                NewLine(sb, indented, depth + 1);
                WriteToken(sb, item, indented, depth + 1);
            }

            NewLine(sb, indented, depth);
            sb.Append(']');
        }

        private static void NewLine(StringBuilder sb, bool indented, int depth)
        {
            if (!indented)
            {
                return;
            }

            sb.Append('\n');
            for (int i = 0; i < depth; i++)
            {
                sb.Append(Indent);
            }
        }

        private static void WriteValue(StringBuilder sb, JToken token)
        {
            JValue value = token as JValue;
            if (value == null)
            {
                throw new ArgumentException("Unsupported JSON token: " + token.Type);
            }

            switch (value.Type)
            {
                case JTokenType.Null:
                    sb.Append("null");
                    return;
                case JTokenType.Boolean:
                    sb.Append((bool)value.Value ? "true" : "false");
                    return;
                case JTokenType.String:
                    sb.Append(JsonConvert.ToString((string)value.Value));
                    return;
                case JTokenType.Integer:
                    sb.Append(((IFormattable)value.Value).ToString(null, CultureInfo.InvariantCulture));
                    return;
                case JTokenType.Float:
                    if (value.Value is double)
                    {
                        sb.Append(FormatDouble((double)value.Value));
                        return;
                    }

                    if (value.Value is float)
                    {
                        sb.Append(FormatDouble((double)(float)value.Value));
                        return;
                    }

                    break;
            }

            throw new ArgumentException("Unsupported JSON value: " + value.Type);
        }

        /// <summary>Bir double'ın kanonik metni (bkz. sınıf açıklaması). Kültürden ve çalışma zamanından bağımsızdır.</summary>
        public static string FormatDouble(double value)
        {
            if (double.IsNaN(value))
            {
                return "NaN";
            }

            if (double.IsPositiveInfinity(value))
            {
                return "Infinity";
            }

            if (double.IsNegativeInfinity(value))
            {
                return "-Infinity";
            }

            long bits = BitConverter.DoubleToInt64Bits(value);
            bool negative = bits < 0;
            int expField = (int)((bits >> 52) & 0x7FF);
            long fraction = bits & 0xFFFFFFFFFFFFFL;
            if (expField == 0 && fraction == 0)
            {
                return negative ? "-0.0" : "0.0";
            }

            long mantissa = expField == 0 ? fraction : (fraction | (1L << 52));
            int exponent = expField == 0 ? -1074 : expField - 1075;

            string digits;
            int exp10;
            ShortestDigits(mantissa, exponent, expField > 1 && fraction == 0, out digits, out exp10);
            string body = Layout(digits, exp10);
            return negative ? "-" + body : body;
        }

        // v = mantissa * 2^exponent (tam). Yarı aralıklar: üst 2^(exponent-1); alt aynı, ama 2'nin kuvvetinde alt komşu yarı uzaklıktadır.
        private static void ShortestDigits(long mantissa, int exponent, bool lowerGapIsHalf, out string digits, out int exp10)
        {
            // Hepsi 2^(exponent-2) birimiyle tamsayı: v = 4m, üst yarı = 2, alt yarı = 2 veya 1.
            int shift = exponent - 2;
            BigInteger vNum;
            BigInteger vDen;
            BigInteger halfNumUp;
            BigInteger halfNumLow;
            BigInteger halfDen;
            if (shift >= 0)
            {
                BigInteger unit = BigInteger.One << shift;
                vNum = new BigInteger(mantissa) * 4 * unit;
                vDen = BigInteger.One;
                halfNumUp = 2 * unit;
                halfNumLow = (lowerGapIsHalf ? 1 : 2) * unit;
                halfDen = BigInteger.One;
            }
            else
            {
                vNum = new BigInteger(mantissa) * 4;
                vDen = BigInteger.One << -shift;
                halfNumUp = 2;
                halfNumLow = lowerGapIsHalf ? 1 : 2;
                halfDen = vDen;
            }

            bool inclusive = (mantissa & 1) == 0; // yuvarlama yarıya-çift: sınır, çift mantiste aralığa dahildir.
            int k = DecimalExponent(vNum, vDen, mantissa, exponent);

            for (int n = 1; n <= 17; n++)
            {
                int p = n - 1 - k; // aday = C / 10^p
                BigInteger numr = vNum;
                BigInteger denr = vDen;
                if (p >= 0)
                {
                    numr *= BigInteger.Pow(10, p);
                }
                else
                {
                    denr *= BigInteger.Pow(10, -p);
                }

                BigInteger remainder;
                BigInteger c = BigInteger.DivRem(numr, denr, out remainder);
                int half2 = (2 * remainder).CompareTo(denr); // tam yarıda çifte yuvarla (modern .NET ile aynı seçim)
                if (half2 > 0 || (half2 == 0 && !c.IsEven))
                {
                    c += 1;
                }

                BigInteger candNum = p < 0 ? c * BigInteger.Pow(10, -p) : c;
                BigInteger candDen = p > 0 ? BigInteger.Pow(10, p) : BigInteger.One;

                // fark = aday - v = (candNum*vDen - vNum*candDen) / (candDen*vDen)
                BigInteger diff = candNum * vDen - vNum * candDen;
                BigInteger half = diff.Sign >= 0 ? halfNumUp : halfNumLow;
                BigInteger absDiff = BigInteger.Abs(diff);
                BigInteger lhs = absDiff * halfDen;
                BigInteger rhs = half * candDen * vDen;
                int cmp = lhs.CompareTo(rhs);
                if (cmp < 0 || (cmp == 0 && inclusive))
                {
                    string text = c.ToString(CultureInfo.InvariantCulture);
                    exp10 = k;
                    if (text.Length > n)
                    {
                        exp10 = k + 1;
                    }

                    digits = text.TrimEnd('0');
                    return;
                }
            }

            throw new InvalidOperationException("A finite double always round-trips within 17 digits.");
        }

        // 10^k <= v < 10^(k+1), tam karşılaştırmayla (Math.Log10 yalnızca tahmindir).
        private static int DecimalExponent(BigInteger vNum, BigInteger vDen, long mantissa, int exponent)
        {
            double estimate = Math.Log10((double)mantissa) + exponent * 0.30102999566398120;
            int k = (int)Math.Floor(estimate);
            while (CompareToPowerOfTen(vNum, vDen, k) < 0)
            {
                k--;
            }

            while (CompareToPowerOfTen(vNum, vDen, k + 1) >= 0)
            {
                k++;
            }

            return k;
        }

        private static int CompareToPowerOfTen(BigInteger vNum, BigInteger vDen, int k)
        {
            BigInteger left = k < 0 ? vNum * BigInteger.Pow(10, -k) : vNum;
            BigInteger right = k > 0 ? vDen * BigInteger.Pow(10, k) : vDen;
            return left.CompareTo(right);
        }

        private static string Layout(string digits, int exp10)
        {
            var sb = new StringBuilder();
            if (exp10 >= SciUpper || exp10 <= SciLower)
            {
                sb.Append(digits[0]);
                if (digits.Length > 1)
                {
                    sb.Append('.').Append(digits, 1, digits.Length - 1);
                }

                sb.Append('E').Append(exp10 < 0 ? '-' : '+');
                int magnitude = Math.Abs(exp10);
                if (magnitude < 10)
                {
                    sb.Append('0');
                }

                sb.Append(magnitude.ToString(CultureInfo.InvariantCulture));
                return sb.ToString();
            }

            if (exp10 >= 0)
            {
                int intLength = exp10 + 1;
                if (digits.Length <= intLength)
                {
                    sb.Append(digits).Append('0', intLength - digits.Length).Append(".0");
                }
                else
                {
                    sb.Append(digits, 0, intLength).Append('.').Append(digits, intLength, digits.Length - intLength);
                }

                return sb.ToString();
            }

            sb.Append("0.").Append('0', -exp10 - 1).Append(digits);
            return sb.ToString();
        }
    }
}
