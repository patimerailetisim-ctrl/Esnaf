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
    /// Çalışma zamanının double.ToString davranışına güvenilmez (eski Mono "R": 15 sonra 17 hane; modern .NET: en kısa gidiş-dönüş),
    /// ayrıca Unity Mono'nun double.Parse'ı en kısa metinlerde 1+ ULP hata yapabilir (ölçüldü). Bu yüzden:
    /// - Her sonlu, sıfırdan farklı double TAM değerinden (BigInteger) hesaplanan, yarıya-çift yuvarlanmış 17 ANLAMLI HANELİ metinle yazılır:
    ///   "d.dddddddddddddddde&lt;üs&gt;" (üs işaretli tamsayı, "+" ve sıfır dolgusu yok; örn. 0.1 → 1.0000000000000001e-1). 17 hane her double'ı ayırt eder.
    /// - Sıfır (+0 ve -0) her zaman "0.0" yazılır. -0.0 bilinçli olarak +0.0'a normalize edilir: Unity Mono "-0.0", "-0", "-0e0", "-0.0e0"
    ///   metinlerinin hepsini +0 okur (ölçüldü), işaretli sıfır JSON'da korunamaz. Oyun durumunda anlamlı bir fark yoktur (DAY9_SCOPE.md).
    /// Sayılar JSON'da SAYI olarak kalır; tamsayılar (long/ulong) double'a çevrilmez; metinler JSON.NET'in sabit kaçışıyla yazılır;
    /// satır sonu her zaman "\n", girinti iki boşluk. NaN/Infinity JSON.NET sabitleriyle yazılır (oyun durumu sonlu sayı üretir).
    /// </summary>
    public static class CanonicalJson
    {
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

        /// <summary>Bir double'ın kanonik metni (bkz. sınıf açıklaması). Kültürden ve çalışma zamanından bağımsızdır; -0.0 → "0.0".</summary>
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
                return "0.0";
            }

            long mantissa = expField == 0 ? fraction : (fraction | (1L << 52));
            int exponent = expField == 0 ? -1074 : expField - 1075;

            string digits;
            int exp10;
            SeventeenDigits(mantissa, exponent, out digits, out exp10);
            return (negative ? "-" : "") + digits[0] + "." + digits.Substring(1) + "e" + exp10.ToString(CultureInfo.InvariantCulture);
        }

        // v = mantissa * 2^exponent (tam). C = yarıya-çift yuvarlanmış v * 10^(16-k), 10^k <= v < 10^(k+1); C her zaman 17 haneli.
        private static void SeventeenDigits(long mantissa, int exponent, out string digits, out int exp10)
        {
            BigInteger vNum = exponent >= 0 ? new BigInteger(mantissa) << exponent : new BigInteger(mantissa);
            BigInteger vDen = exponent >= 0 ? BigInteger.One : BigInteger.One << -exponent;
            int k = DecimalExponent(vNum, vDen, mantissa, exponent);

            int p = 16 - k;
            BigInteger numr = p >= 0 ? vNum * BigInteger.Pow(10, p) : vNum;
            BigInteger denr = p >= 0 ? vDen : vDen * BigInteger.Pow(10, -p);
            BigInteger remainder;
            BigInteger c = BigInteger.DivRem(numr, denr, out remainder);
            int half = (2 * remainder).CompareTo(denr); // tam yarıda çifte yuvarla
            if (half > 0 || (half == 0 && !c.IsEven))
            {
                c += 1;
            }

            if (c == BigInteger.Pow(10, 17))
            {
                c /= 10;
                k++;
            }

            digits = c.ToString(CultureInfo.InvariantCulture);
            exp10 = k;
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
    }
}
