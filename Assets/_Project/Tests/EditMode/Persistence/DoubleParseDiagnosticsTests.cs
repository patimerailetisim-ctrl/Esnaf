using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Esnaf.Persistence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Esnaf.Tests.Persistence
{
    /// <summary>
    /// TEŞHİS ÖLÇÜMÜ (Gün 9 düzeltmesi, geçici): CanonicalJson.FormatDouble'ın metninin çalışma zamanında hangi double bit desenine
    /// parse edildiğini ölçer. Orijinal değer literalden değil BİT DESENİNDEN kurulur (derleyicinin literal ayrıştırmasına güvenilmez).
    /// Alternatif gösterimler sabit metinlerdir (Python Decimal ile tam değerden üretildi), çalışma zamanının ToString'i kullanılmaz.
    /// Çıktı: Console, TestContext ve %TEMP%/esnaf_double_diag.txt. Test yalnızca FORMATTER metnini altın değerle doğrular;
    /// parse sonuçlarını doğrulamaz, yalnızca raporlar (ölçüm). Hiçbir üretim koduna dokunmaz.
    /// </summary>
    public class DoubleParseDiagnosticsTests
    {
        private sealed class Case
        {
            public readonly string Name;
            public readonly long Bits;
            public readonly string[] Alternatives;

            public Case(string name, long bits, string[] alternatives)
            {
                Name = name;
                Bits = bits;
                Alternatives = alternatives;
            }
        }

        private static readonly int[] Digits = { 15, 16, 17, 20, 25 };

        private static readonly Case[] Cases =
        {
            new Case("CASE 1  -7.756553093017971E-171", -7161293256289857021L, new[] { "-7.75655309301797e-171", "-7.756553093017971e-171", "-7.7565530930179706e-171", "-7.7565530930179705526e-171", "-7.756553093017970552608691e-171" }),
            new Case("CASE 2  0.90904188119529", 4606363140900106981L, new[] { "9.09041881195290e-1", "9.090418811952899e-1", "9.0904188119528995e-1", "9.0904188119528994694e-1", "9.090418811952899469375211e-1" }),
            new Case("CASE 2b bits reported as Expected in the Unity failure", 4606363140900106976L, new[] { "9.09041881195289e-1", "9.090418811952894e-1", "9.0904188119528939e-1", "9.0904188119528939183e-1", "9.090418811952893918260088e-1" }),
            new Case("CASE 2c bits reported as Actual in the Unity failure", 4606363140900106987L, new[] { "9.09041881195291e-1", "9.090418811952906e-1", "9.0904188119529061e-1", "9.0904188119529061307e-1", "9.090418811952906130713359e-1" }),
            new Case("CASE 3  0.5120871669493438", 4602787690693784971L, new[] { "5.12087166949344e-1", "5.120871669493438e-1", "5.1208716694934375e-1", "5.1208716694934375280e-1", "5.120871669493437527975743e-1" }),
        };

        // .NET 8'de doğrulanmış kanonik metinler (bit deseni → en kısa gidiş-dönüş).
        private static readonly string[] ExpectedCanonical =
        {
            "-7.756553093017971E-171",
            "0.90904188119529",
            "0.9090418811952894",
            "0.9090418811952906",
            "0.5120871669493438"
        };

        private static string Hex(long bits)
        {
            return "0x" + bits.ToString("X16", CultureInfo.InvariantCulture);
        }

        private static string Fields(long bits)
        {
            return "sign=" + ((bits >> 63) & 1) + " expField=" + ((bits >> 52) & 0x7FF) + " fraction=0x" + (bits & 0xFFFFFFFFFFFFFL).ToString("X13", CultureInfo.InvariantCulture);
        }

        private static string CodeUnits(string s)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                sb.Append(((int)s[i]).ToString("X2", CultureInfo.InvariantCulture)).Append(' ');
            }

            return sb.ToString().TrimEnd();
        }

        private static string ParseNet(string text)
        {
            try
            {
                long bits = BitConverter.DoubleToInt64Bits(double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture));
                return bits.ToString(CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                return "EXCEPTION " + ex.GetType().Name;
            }
        }

        private static long ParseBits(string text)
        {
            return BitConverter.DoubleToInt64Bits(double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture));
        }

        private static long NewtonsoftBits(string text)
        {
            using (var reader = new JsonTextReader(new StringReader("[" + text + "]")) { DateParseHandling = DateParseHandling.None, FloatParseHandling = FloatParseHandling.Double })
            {
                JArray a = JArray.Load(reader);
                return BitConverter.DoubleToInt64Bits((double)((JValue)a[0]).Value);
            }
        }

        [Test]
        public void Diagnostic_MeasuresWhatTheCanonicalTextParsesToInThisRuntime()
        {
            var log = new StringBuilder();
            log.AppendLine("=== ESNAF double parse diagnostic ===");
            log.AppendLine("Runtime: " + System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription + " | Environment.Version=" + Environment.Version);
            log.AppendLine("Object assembly: " + typeof(object).Assembly.FullName);
            log.AppendLine("IntPtr.Size=" + IntPtr.Size + " BitConverter.IsLittleEndian=" + BitConverter.IsLittleEndian);
            var mismatches = new List<string>();

            for (int c = 0; c < Cases.Length; c++)
            {
                Case kase = Cases[c];
                double original = BitConverter.Int64BitsToDouble(kase.Bits);
                string canonical = CanonicalJson.FormatDouble(original);

                log.AppendLine();
                log.AppendLine(kase.Name);
                log.AppendLine("original bits = " + kase.Bits + " (" + Hex(kase.Bits) + ") " + Fields(kase.Bits));
                log.AppendLine("original bits re-read from the double = " + BitConverter.DoubleToInt64Bits(original));
                log.AppendLine("original value (runtime R, informational only) = " + original.ToString("R", CultureInfo.InvariantCulture));
                log.AppendLine("original value (runtime G17, informational only) = " + original.ToString("G17", CultureInfo.InvariantCulture));
                log.AppendLine("canonical text = " + canonical);
                log.AppendLine("canonical text UTF-16 code units = " + CodeUnits(canonical));
                log.AppendLine("canonical length = " + canonical.Length);

                string parsed = ParseNet(canonical);
                log.AppendLine("canonical parsed bits (double.Parse, Invariant) = " + parsed);
                long pb;
                if (long.TryParse(parsed, NumberStyles.Integer, CultureInfo.InvariantCulture, out pb))
                {
                    double pv = BitConverter.Int64BitsToDouble(pb);
                    log.AppendLine("canonical parsed value (runtime R, informational only) = " + pv.ToString("R", CultureInfo.InvariantCulture));
                    log.AppendLine("original == parsed (double ==) = " + (original == pv));
                    log.AppendLine("exact round trip (bits equal) = " + (pb == kase.Bits) + " (parsed - original = " + (pb - kase.Bits) + " ULP)");
                }

                long nb = NewtonsoftBits(canonical);
                log.AppendLine("canonical parsed bits (Newtonsoft JsonTextReader) = " + nb + " exact=" + (nb == kase.Bits));

                for (int i = 0; i < kase.Alternatives.Length; i++)
                {
                    string text = kase.Alternatives[i];
                    long alt = ParseBits(text);
                    long altNs = NewtonsoftBits(text);
                    log.AppendLine(Digits[i] + " digits = " + text + " -> double.Parse bits " + alt + " (equal=" + (alt == kase.Bits) + ", delta=" + (alt - kase.Bits) + ") | Newtonsoft bits " + altNs + " (equal=" + (altNs == kase.Bits) + ")");
                }

                if (!string.Equals(canonical, ExpectedCanonical[c], StringComparison.Ordinal))
                {
                    mismatches.Add(kase.Name + ": formatter gave '" + canonical + "', expected '" + ExpectedCanonical[c] + "'");
                }
            }

            string text2 = log.ToString();
            Console.WriteLine(text2);
            TestContext.WriteLine(text2);
            try
            {
                File.WriteAllText(Path.Combine(Path.GetTempPath(), "esnaf_double_diag.txt"), text2);
            }
            catch (Exception)
            {
                // dosyaya yazılamazsa Console/TestContext çıktısı yeterli.
            }

            Assert.IsEmpty(mismatches, "FormatDouble bu çalışma zamanında altın metni üretmedi:\n" + string.Join("\n", mismatches));
        }
    }
}
