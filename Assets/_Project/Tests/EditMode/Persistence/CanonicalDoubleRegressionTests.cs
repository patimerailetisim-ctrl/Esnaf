using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using Esnaf.Persistence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Esnaf.Tests.Persistence
{
    /// <summary>
    /// KALICI REGRESYON (Gün 9 düzeltmesi): sabit 73 değer (rastgele yok, bit desenlerinden kurulur) için CanonicalJson.FormatDouble
    /// (1) bağımsız BigInteger yardımcısının (bu dosyada, üretim koduna bağlı değil) 17 haneli metniyle AYNI olmalı ve
    /// (2) çalışma zamanında (modern .NET ve Unity Mono) double.Parse ve Newtonsoft ile AYNI bit desenine dönmelidir.
    /// Negatif sıfır bilinçli olarak +0.0'a normalize edilir (Mono "-0.0" metnini +0 okur; DAY9_SCOPE.md). Tolerans yok.
    /// Ölçüm çıktısı: Console, TestContext, %TEMP%/esnaf_double_diag2.txt.
    /// </summary>
    public class CanonicalDoubleRegressionTests
    {
        private sealed class Item
        {
            public readonly string Label;
            public readonly long Bits;

            public Item(string label, long bits)
            {
                Label = label;
                Bits = bits;
            }
        }

        private static readonly Item[] Items =
        {
            new Item("CASE1 -7.756553093017971E-171", -7161293256289857021L),
            new Item("CASE2 0.90904188119529", 4606363140900106981L),
            new Item("CASE2b bits ...976", 4606363140900106976L),
            new Item("CASE2c bits ...987", 4606363140900106987L),
            new Item("CASE3 0.5120871669493438", 4602787690693784971L),
            new Item("0.1", 4591870180066957722L),
            new Item("0.2", 4596373779694328218L),
            new Item("0.3", 4599075939470750515L),
            new Item("1.0", 4607182418800017408L),
            new Item("-1.0", -4616189618054758400L),
            new Item("123456789.12345679", 4728057454355442549L),
            new Item("1e-10", 4457293557087583675L),
            new Item("1e10", 4756540486875873280L),
            new Item("1e20", 4906019910204099648L),
            new Item("1e23", 4950912855330343670L),
            new Item("1e-23", 4262707295203537489L),
            new Item("1.7976931348623157e308", 9218868437227405311L),
            new Item("2.2250738585072014e-308", 4503599627370496L),
            new Item("4.9406564584124654e-324", 1L),
            new Item("double.MinValue", -4503599627370497L),
            new Item("golden 0.30000000000000004", 4599075939470750516L),
            new Item("golden 4.35", 4616583683022153318L),
            new Item("golden 0.85", 4605831338911806259L),
            new Item("golden 1.05", 4607407598781385933L),
            new Item("golden 0.9802138046024543", 4607004200595578475L),
            new Item("golden 0.7666180147282152", 4605080300756207415L),
            new Item("golden 0.7091521128839813", 4604562693927943012L),
            new Item("golden 0.9817245566526737", 4607017808240319309L),
            new Item("golden 0.5424123071976978", 4603060835274429862L),
            new Item("golden 0.3669868400741799", 4600282667102737730L),
            new Item("golden 1.0121075477673915", 4607236946347631003L),
            new Item("golden 9007199254740992.0", 4845873199050653696L),
            new Item("golden 123456789012345680.0", 4862596447618666293L),
            new Item("golden 906851161693619.25", 4830610010207350170L),
            new Item("golden 906851161693619.375", 4830610010207350171L),
            new Item("golden 1e-99", 3125919792542303038L),
            new Item("golden 9.999999999999997e22", 4950912855330343669L),
            new Item("golden 1e15", 4831355200913801216L),
            new Item("golden 1e16", 4846369599423283200L),
            new Item("golden 1e17", 4861130398305394688L),
            new Item("golden 1.5e-7", 4504762867522569078L),
            new Item("golden 1.234e-5", 4533401875807174908L),
            new Item("golden 2.5e-300", 124633661817958447L),
            new Item("golden 12345678901234568.0", 4847542438873900484L),
            new Item("golden 0.0001", 4547007122018943789L),
            new Item("golden 0.00012", 4548482861544840553L),
            new Item("golden 100000000000000.0", 4816244402031689728L),
            new Item("golden 123456789012345.0", 4817745636528479808L),
            new Item("golden 3081.0", 4658993605724078080L),
            new Item("golden 0.0", 0L),
            new Item("golden -0.0", -9223372036854775808L),
            new Item("fixture 0.00771518825351436", 4575545028603249792L),
            new Item("fixture 0.07954817866844799", 4590396471028112512L),
            new Item("fixture 0.25360171208703175", 4598240102222128624L),
            new Item("fixture 0.257752054093336", 4598314868136980832L),
            new Item("fixture 0.3730005651660965", 4600391000743069986L),
            new Item("fixture 0.4", 4600877379321698714L),
            new Item("fixture 0.42260008921047887", 4601284506335086124L),
            new Item("fixture 0.5244176649638914", 4602898753946312190L),
            new Item("fixture 0.5353161043404289", 4602996918361342378L),
            new Item("fixture 0.5713658691464613", 4603321625776036861L),
            new Item("fixture 0.7", 4604480259023595110L),
            new Item("fixture 0.8054793508025518", 4605430332553534419L),
            new Item("fixture 0.9", 4606281698874543309L),
            new Item("fixture 0.9861864342788366", 4607057997261148429L),
            new Item("fixture 1.0022020530629043", 4607192335965370954L),
            new Item("fixture 1.0034320409078015", 4607197875338170903L),
            new Item("fixture 1.0093720395296768", 4607224626713750962L),
            new Item("fixture 1.0141535708177047", 4607246160816277985L),
            new Item("fixture 1.0168599242194878", 4607258349148449788L),
            new Item("fixture 1.0244821929157057", 4607292676794909793L),
            new Item("fixture 4589.854016249886", 4661768612561753987L),
            new Item("fixture 4971.418394857452", 4662188147032778130L),
        };

        // B: v = m * 2^e (tam); C = round-half-even(v * 10^(16-k)), 10^k <= v < 10^(k+1); metin "d.dddddddddddddddde<k>".
        private static string SeventeenDigits(double value, out BigInteger digits17, out int exp10)
        {
            long bits = BitConverter.DoubleToInt64Bits(value);
            bool negative = bits < 0;
            int expField = (int)((bits >> 52) & 0x7FF);
            long fraction = bits & 0xFFFFFFFFFFFFFL;
            digits17 = BigInteger.Zero;
            exp10 = 0;
            if (expField == 0 && fraction == 0)
            {
                return negative ? "-0.0000000000000000e0" : "0.0000000000000000e0";
            }

            long m = expField == 0 ? fraction : (fraction | (1L << 52));
            int e = expField == 0 ? -1074 : expField - 1075;
            BigInteger num = e >= 0 ? new BigInteger(m) << e : new BigInteger(m);
            BigInteger den = e >= 0 ? BigInteger.One : BigInteger.One << -e;

            int k = (int)Math.Floor(Math.Log10((double)m) + e * 0.30102999566398120);
            while (Compare10(num, den, k) < 0)
            {
                k--;
            }

            while (Compare10(num, den, k + 1) >= 0)
            {
                k++;
            }

            int p = 16 - k;
            BigInteger n2 = p >= 0 ? num * BigInteger.Pow(10, p) : num;
            BigInteger d2 = p >= 0 ? den : den * BigInteger.Pow(10, -p);
            BigInteger rem;
            BigInteger c = BigInteger.DivRem(n2, d2, out rem);
            int half = (2 * rem).CompareTo(d2);
            if (half > 0 || (half == 0 && !c.IsEven))
            {
                c += 1;
            }

            if (c == BigInteger.Pow(10, 17))
            {
                c /= 10;
                k++;
            }

            digits17 = c;
            exp10 = k;
            string s = c.ToString(CultureInfo.InvariantCulture);
            return (negative ? "-" : "") + s.Substring(0, 1) + "." + s.Substring(1) + "e" + k.ToString(CultureInfo.InvariantCulture);
        }

        private static int Compare10(BigInteger num, BigInteger den, int k)
        {
            BigInteger left = k < 0 ? num * BigInteger.Pow(10, -k) : num;
            BigInteger right = k > 0 ? den * BigInteger.Pow(10, k) : den;
            return left.CompareTo(right);
        }

        // Metnin (D * 10^(exp10-16)) tam değeri, v'nin kendi yuvarlama aralığında mı? (çalışma zamanından bağımsız, tam sayı aritmetiği)
        private static bool WithinRoundingInterval(double value, BigInteger digits17, int exp10)
        {
            long bits = BitConverter.DoubleToInt64Bits(value);
            int expField = (int)((bits >> 52) & 0x7FF);
            long fraction = bits & 0xFFFFFFFFFFFFFL;
            long m = expField == 0 ? fraction : (fraction | (1L << 52));
            int e = expField == 0 ? -1074 : expField - 1075;
            bool lowerGapIsHalf = expField > 1 && fraction == 0;

            // Birim u = 2^(e-2): v = 4m u, üst yarı aralık = 2u, alt yarı aralık = 2u (2'nin kuvvetinde 1u).
            int shift = e - 2;
            BigInteger vNum = new BigInteger(m) * 4;
            BigInteger vDen = BigInteger.One;
            BigInteger unitNum = BigInteger.One;
            BigInteger unitDen = BigInteger.One;
            if (shift >= 0)
            {
                vNum <<= shift;
                unitNum <<= shift;
            }
            else
            {
                vDen <<= -shift;
                unitDen <<= -shift;
            }

            int scale = exp10 - 16;
            BigInteger tNum = scale >= 0 ? digits17 * BigInteger.Pow(10, scale) : digits17;
            BigInteger tDen = scale >= 0 ? BigInteger.One : BigInteger.Pow(10, -scale);

            BigInteger signedDiff = tNum * vDen - vNum * tDen; // (t - v) * tDen * vDen
            long halfUnits = (signedDiff.Sign < 0 && lowerGapIsHalf) ? 1 : 2;
            // |t - v| <= halfUnits * u  <=>  |signedDiff| * unitDen <= halfUnits * unitNum * tDen * vDen
            return BigInteger.Abs(signedDiff) * unitDen <= halfUnits * unitNum * tDen * vDen;
        }

        private static long ParseBits(string text)
        {
            return BitConverter.DoubleToInt64Bits(double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture));
        }

        private static long NewtonsoftBits(string text)
        {
            using (var reader = new JsonTextReader(new StringReader("[" + text + "]")) { DateParseHandling = DateParseHandling.None, FloatParseHandling = FloatParseHandling.Double })
            {
                return BitConverter.DoubleToInt64Bits((double)((JValue)JArray.Load(reader)[0]).Value);
            }
        }

        [Test]
        public void CanonicalText_MatchesTheIndependentReference_AndRoundTripsBitExactly()
        {
            var log = new StringBuilder();
            log.AppendLine("=== ESNAF 17-digit diagnostic ===");
            log.AppendLine("Runtime: " + System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription + " | Environment.Version=" + Environment.Version);
            int aExact = 0;
            int bExact = 0;
            int aNsExact = 0;
            int bNsExact = 0;
            var aFail = new List<string>();
            var bFail = new List<string>();
            var invalidB = new List<string>();
            var textMismatch = new List<string>();
            var newtonsoftFail = new List<string>();

            foreach (Item item in Items)
            {
                double original = BitConverter.Int64BitsToDouble(item.Bits);
                string a = CanonicalJson.FormatDouble(original);
                BigInteger d17;
                int x17;
                string b = SeventeenDigits(original, out d17, out x17);

                long expectedBits = item.Bits == long.MinValue ? 0L : item.Bits; // -0.0 → +0.0 (bilinçli normalizasyon)
                long aBits = ParseBits(a);
                long bBits = ParseBits(b);
                long aNs = NewtonsoftBits(a);
                long bNs = NewtonsoftBits(b);
                bool aOk = aBits == expectedBits;
                bool bOk = bBits == expectedBits;
                aExact += aOk ? 1 : 0;
                bExact += bOk ? 1 : 0;
                aNsExact += aNs == expectedBits ? 1 : 0;
                bNsExact += bNs == expectedBits ? 1 : 0;

                log.AppendLine(item.Label + " | original bits " + item.Bits);
                log.AppendLine("   A text " + a + " -> parsed " + aBits + " equal=" + aOk + " delta=" + (aBits - expectedBits) + " | Newtonsoft " + aNs + " equal=" + (aNs == item.Bits));
                log.AppendLine("   B text " + b + " -> parsed " + bBits + " equal=" + bOk + " delta=" + (bBits - expectedBits) + " | Newtonsoft " + bNs + " equal=" + (bNs == item.Bits));

                bool isZero = (item.Bits & long.MaxValue) == 0;
                if (!isZero && !string.Equals(a, b, StringComparison.Ordinal))
                {
                    textMismatch.Add(item.Label + ": FormatDouble '" + a + "' != oracle '" + b + "'");
                }

                if (isZero && a != "0.0")
                {
                    textMismatch.Add(item.Label + ": zero must be '0.0' but was '" + a + "'");
                }

                if (aNs != expectedBits)
                {
                    newtonsoftFail.Add(item.Label + " | original bits " + item.Bits + " | text " + a + " | Newtonsoft bits " + aNs);
                }

                if (!aOk)
                {
                    aFail.Add(item.Label + " | original bits " + item.Bits + " | A text " + a + " | parsed bits " + aBits + " | ULP delta " + (aBits - expectedBits));
                }

                if (!bOk)
                {
                    bFail.Add(item.Label + " | original bits " + item.Bits + " | B 17-digit text " + b + " | parsed bits " + bBits + " | ULP delta " + (bBits - expectedBits));
                }

                if (item.Bits != 0 && item.Bits != long.MinValue && !WithinRoundingInterval(original, d17, x17))
                {
                    invalidB.Add(item.Label + ": " + b);
                }
            }

            log.AppendLine();
            log.AppendLine("TOTAL VALUES = " + Items.Length);
            log.AppendLine("A current-shortest:");
            log.AppendLine("  exact = " + aExact + " (double.Parse) / " + aNsExact + " (Newtonsoft)");
            log.AppendLine("  failures = " + (Items.Length - aExact) + " (double.Parse) / " + (Items.Length - aNsExact) + " (Newtonsoft)");
            log.AppendLine("B 17-digit:");
            log.AppendLine("  exact = " + bExact + " (double.Parse) / " + bNsExact + " (Newtonsoft)");
            log.AppendLine("  failures = " + (Items.Length - bExact) + " (double.Parse) / " + (Items.Length - bNsExact) + " (Newtonsoft)");
            log.AppendLine("A failures:");
            foreach (string s in aFail)
            {
                log.AppendLine("  " + s);
            }

            log.AppendLine("B failures:");
            foreach (string s in bFail)
            {
                log.AppendLine("  " + s);
            }

            string text = log.ToString();
            Console.WriteLine(text);
            TestContext.WriteLine(text);
            try
            {
                File.WriteAllText(Path.Combine(Path.GetTempPath(), "esnaf_double_diag2.txt"), text);
            }
            catch (Exception)
            {
                // dosyaya yazılamazsa Console/TestContext çıktısı yeterli.
            }

            Assert.IsEmpty(invalidB, "B metni matematiksel olarak orijinalin yarım aralığında olmalı (ölçüm yardımcısı doğrulaması):\n" + string.Join("\n", invalidB));
            Assert.IsEmpty(textMismatch, "FormatDouble bağımsız 17 haneli referansla aynı olmalı:\n" + string.Join("\n", textMismatch));
            Assert.IsEmpty(aFail, "FormatDouble metni bu çalışma zamanında double.Parse ile bit-exact dönmeli:\n" + string.Join("\n", aFail));
            Assert.IsEmpty(newtonsoftFail, "FormatDouble metni bu çalışma zamanında Newtonsoft ile bit-exact dönmeli:\n" + string.Join("\n", newtonsoftFail));
        }
    }
}
