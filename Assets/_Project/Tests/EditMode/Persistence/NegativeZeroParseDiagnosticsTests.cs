using System;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Esnaf.Tests.Persistence
{
    /// <summary>
    /// TEŞHİS ÖLÇÜMÜ 3 (Gün 9 düzeltmesi, geçici): hangi metin yazımları işaretli sıfırı (-0.0, bit deseni 0x8000000000000000)
    /// çalışma zamanında doğru parse eder? double.Parse (Invariant) ve Newtonsoft JsonTextReader ayrı ayrı ölçülür.
    /// Çıktı: Console, TestContext, %TEMP%/esnaf_double_diag3.txt. Hiçbir üretim koduna dokunmaz; sonucu doğrulamaz, raporlar.
    /// </summary>
    public class NegativeZeroParseDiagnosticsTests
    {
        private const long NegativeZeroBits = long.MinValue; // 0x8000000000000000

        private static readonly string[] Spellings =
        {
            "-0.0",
            "-0",
            "-0e0",
            "-0.0e0",
            "-0.0000000000000000e0",
            "+0.0",
            "0.0",
            "0",
            "0e0",
            "0.0000000000000000e0"
        };

        private static string Hex(long bits)
        {
            return "0x" + bits.ToString("X16", CultureInfo.InvariantCulture);
        }

        private static string DoubleParse(string text, out long bits)
        {
            bits = 0;
            try
            {
                bits = BitConverter.DoubleToInt64Bits(double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture));
                return null;
            }
            catch (Exception ex)
            {
                return ex.GetType().Name;
            }
        }

        private static string NewtonsoftParse(string text, out long bits, out string tokenType)
        {
            bits = 0;
            tokenType = "?";
            try
            {
                using (var reader = new JsonTextReader(new StringReader("[" + text + "]")) { DateParseHandling = DateParseHandling.None, FloatParseHandling = FloatParseHandling.Double })
                {
                    var value = (JValue)JArray.Load(reader)[0];
                    tokenType = value.Type.ToString();
                    object raw = value.Value;
                    bits = raw is double ? BitConverter.DoubleToInt64Bits((double)raw) : BitConverter.DoubleToInt64Bits(Convert.ToDouble(raw, CultureInfo.InvariantCulture));
                    return null;
                }
            }
            catch (Exception ex)
            {
                return ex.GetType().Name;
            }
        }

        [Test]
        public void Diagnostic_MeasuresWhichSpellingsParseToNegativeZero()
        {
            var log = new StringBuilder();
            log.AppendLine("=== ESNAF negative zero diagnostic ===");
            log.AppendLine("Runtime: " + System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription + " | Environment.Version=" + Environment.Version);
            log.AppendLine("target bits for -0.0 = " + NegativeZeroBits + " (" + Hex(NegativeZeroBits) + ")");
            log.AppendLine("sanity: BitConverter.DoubleToInt64Bits(-0.0 built from bits) = " + BitConverter.DoubleToInt64Bits(BitConverter.Int64BitsToDouble(NegativeZeroBits)));

            foreach (string text in Spellings)
            {
                long dp;
                long ns;
                string tokenType;
                string dpError = DoubleParse(text, out dp);
                string nsError = NewtonsoftParse(text, out ns, out tokenType);
                bool wantsNegative = text.StartsWith("-", StringComparison.Ordinal);

                log.AppendLine();
                log.AppendLine("text '" + text + "' (expected " + (wantsNegative ? "-0.0 0x8000000000000000" : "+0.0 0x0000000000000000") + ")");
                log.AppendLine("   double.Parse: " + (dpError ?? ("bits " + dp + " " + Hex(dp) + " isNegativeZeroBits=" + (dp == NegativeZeroBits))));
                log.AppendLine("   Newtonsoft : " + (nsError ?? ("bits " + ns + " " + Hex(ns) + " isNegativeZeroBits=" + (ns == NegativeZeroBits) + " token=" + tokenType)));
                if (dpError == null && nsError == null)
                {
                    log.AppendLine("   double.Parse == Newtonsoft: " + (dp == ns));
                }
            }

            string output = log.ToString();
            Console.WriteLine(output);
            TestContext.WriteLine(output);
            try
            {
                File.WriteAllText(Path.Combine(Path.GetTempPath(), "esnaf_double_diag3.txt"), output);
            }
            catch (Exception)
            {
                // dosyaya yazılamazsa Console/TestContext çıktısı yeterli.
            }

            Assert.AreEqual(NegativeZeroBits, BitConverter.DoubleToInt64Bits(BitConverter.Int64BitsToDouble(NegativeZeroBits)), "ölçümün kendi sağlaması");
        }
    }
}
