using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using Esnaf.Persistence;
using Esnaf.Tests.Support;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Esnaf.Tests.Persistence
{
    /// <summary>
    /// Kanonik JSON (kayıt sağlaması ve dosya metni) çalışma zamanından bağımsız olmalı: Mono / IL2CPP / .NET aynı metni üretir.
    /// Bu yüzden double yazımı çalışma zamanının ToString'ine DEĞİL, kendi BigInteger tabanlı en-kısa-gidiş-dönüş kuralına bağlıdır.
    /// Beklenen metinler sabit altın değerlerdir (bağımsız Python hesabıyla da eşleşir); Unity Test Runner'da da aynı sabitlerle çalışır.
    /// </summary>
    public class CanonicalJsonTests
    {
        // Teşhiste bulunan sorunlu değerler: eski Mono "R" (15 sonra 17 hane) bunları 17 hane yazıyordu, en kısa biçim 16 hanedir.
        // Beklenen metinler çalışma zamanından BAĞIMSIZ üretildi (Python Decimal: tam değer, 17 anlamlı hane, yarıya-çift).
        private static readonly object[][] Golden =
        {
            new object[] { 0.5120871669493438, "5.1208716694934375e-1" },
            new object[] { 0.9802138046024543, "9.8021380460245433e-1" },
            new object[] { 0.7666180147282152, "7.6661801472821522e-1" },
            new object[] { 0.7091521128839813, "7.0915211288398128e-1" },
            new object[] { 0.9817245566526737, "9.8172455665267366e-1" },
            new object[] { 0.5424123071976978, "5.4241230719769784e-1" },
            new object[] { 0.3669868400741799, "3.6698684007417992e-1" },
            new object[] { 1.0121075477673915, "1.0121075477673915e0" },
            new object[] { 0.1, "1.0000000000000001e-1" },
            new object[] { 0.2, "2.0000000000000001e-1" },
            new object[] { 0.3, "2.9999999999999999e-1" },
            new object[] { 0.30000000000000004, "3.0000000000000004e-1" },
            new object[] { 1.0, "1.0000000000000000e0" },
            new object[] { 3081.0, "3.0810000000000000e3" },
            new object[] { 0.0, "0.0" },
            new object[] { -1.5, "-1.5000000000000000e0" },
            new object[] { -1.0, "-1.0000000000000000e0" },
            new object[] { 100000000000000.0, "1.0000000000000000e14" },
            new object[] { 123456789012345.0, "1.2345678901234500e14" },
            new object[] { 1e15, "1.0000000000000000e15" },
            new object[] { 1e16, "1.0000000000000000e16" },
            new object[] { 12345678901234568.0, "1.2345678901234568e16" },
            new object[] { 1e17, "1.0000000000000000e17" },
            new object[] { 1e22, "1.0000000000000000e22" },
            new object[] { 1e23, "9.9999999999999992e22" },
            new object[] { 9.999999999999997e22, "9.9999999999999975e22" },
            new object[] { 9007199254740992.0, "9.0071992547409920e15" },
            new object[] { 123456789012345680.0, "1.2345678901234568e17" },
            new object[] { 123456789.12345679, "1.2345678912345679e8" },
            new object[] { 0.0001, "1.0000000000000000e-4" },
            new object[] { 0.00012, "1.2000000000000000e-4" },
            new object[] { 0.00001, "1.0000000000000001e-5" },
            new object[] { 1.234e-5, "1.2340000000000000e-5" },
            new object[] { 1.5e-7, "1.4999999999999999e-7" },
            new object[] { 1e-10, "1.0000000000000000e-10" },
            new object[] { 1.5e-9, "1.5000000000000000e-9" },
            new object[] { 1e-23, "9.9999999999999996e-24" },
            new object[] { 1e-99, "1.0000000000000000e-99" },
            new object[] { 1e-100, "1.0000000000000000e-100" },
            new object[] { -2.5e-300, "-2.5000000000000000e-300" },
            new object[] { 1.7976931348623157e308, "1.7976931348623157e308" },
            new object[] { -1.7976931348623157e308, "-1.7976931348623157e308" },
            new object[] { 5e-324, "4.9406564584124654e-324" },
            new object[] { 2.2250738585072014e-308, "2.2250738585072014e-308" },
            new object[] { 4.35, "4.3499999999999996e0" },
            new object[] { 0.85, "8.4999999999999998e-1" },
            new object[] { 1.05, "1.0500000000000000e0" },
            new object[] { 906851161693619.25, "9.0685116169361925e14" },
            new object[] { 906851161693619.375, "9.0685116169361938e14" },
            new object[] { 1.5e18, "1.5000000000000000e18" },
            new object[] { -7.756553093017971e-171, "-7.7565530930179706e-171" },
            new object[] { 0.90904188119529, "9.0904188119528995e-1" }
        };

        [TestCaseSource(nameof(Golden))]
        public void FormatDouble_GivesTheFixedCanonicalText(double value, string expected)
        {
            Assert.AreEqual(expected, CanonicalJson.FormatDouble(value));
        }

        [Test]
        public void FormatDouble_ZeroIsAlwaysPositiveZero_NegativeZeroIsNormalized()
        {
            // Karar (DAY9_SCOPE.md): Unity Mono "-0.0"/"-0"/"-0e0"/"-0.0e0" metinlerini +0.0 olarak parse eder; işaretli sıfır JSON'da korunamaz.
            // Bu yüzden canonical çıktı negatif sıfır ÜRETMEZ: -0.0 → "0.0".
            double negativeZero = BitConverter.Int64BitsToDouble(unchecked((long)0x8000000000000000UL));

            Assert.AreEqual("0.0", CanonicalJson.FormatDouble(negativeZero));
            Assert.AreEqual("0.0", CanonicalJson.FormatDouble(0.0));
            Assert.AreEqual("0.0", CanonicalJson.Write(new JArray(negativeZero), false).Trim('[', ']'));
        }

        [Test]
        public void FormatDouble_NegativeZero_ParsesToPositiveZeroBits_InEveryRuntime()
        {
            double negativeZero = BitConverter.Int64BitsToDouble(unchecked((long)0x8000000000000000UL));

            string text = CanonicalJson.FormatDouble(negativeZero);

            Assert.AreEqual(0L, BitConverter.DoubleToInt64Bits(double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture)));
            JValue token = (JValue)JArray.Parse("[" + text + "]")[0];
            Assert.AreEqual(0L, BitConverter.DoubleToInt64Bits((double)token.Value));
        }

        [Test]
        public void FormatDouble_NonFiniteValuesUseTheJsonNetLiterals()
        {
            Assert.AreEqual("NaN", CanonicalJson.FormatDouble(double.NaN));
            Assert.AreEqual("Infinity", CanonicalJson.FormatDouble(double.PositiveInfinity));
            Assert.AreEqual("-Infinity", CanonicalJson.FormatDouble(double.NegativeInfinity));
        }

        [Test]
        public void FormatDouble_AnySpellingOfTheSameDoubleGivesTheSameSeventeenDigitText()
        {
            string expected = "5.1208716694934375e-1";

            Assert.AreEqual(expected, CanonicalJson.FormatDouble(double.Parse("0.51208716694934375", CultureInfo.InvariantCulture)));
            Assert.AreEqual(expected, CanonicalJson.FormatDouble(double.Parse("0.5120871669493438", CultureInfo.InvariantCulture)));
            Assert.AreEqual(expected, CanonicalJson.FormatDouble(BitConverter.Int64BitsToDouble(4602787690693784971L)));
        }

        [Test]
        public void FormatDouble_IsIndependentOfTheCurrentCulture()
        {
            CultureInfo saved = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("tr-TR");
                Assert.AreEqual("5.1208716694934375e-1", CanonicalJson.FormatDouble(BitConverter.Int64BitsToDouble(4602787690693784971L)));
                Assert.AreEqual("1.4999999999999999e-7", CanonicalJson.FormatDouble(1.5e-7));
                Assert.AreEqual("-2.0000000000000000e0", CanonicalJson.FormatDouble(-2.0));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = saved;
            }
        }

        // Çalışma zamanından bağımsız deterministik üreteç (System.Random dizisi Unity/.NET'te farklıdır): xorshift64*.
        private static ulong NextBits(ref ulong state)
        {
            state ^= state >> 12;
            state ^= state << 25;
            state ^= state >> 27;
            return state * 2685821657736338717UL;
        }

        [Test]
        public void FormatDouble_AlwaysWritesSeventeenSignificantDigits_ForEveryFiniteNonZeroValue()
        {
            ulong state = 88172645463325252UL;
            var shape = new System.Text.RegularExpressions.Regex("^-?[1-9]\\.[0-9]{16}e-?[0-9]+$");
            for (int i = 0; i < 20000; i++)
            {
                double v = BitConverter.Int64BitsToDouble(unchecked((long)NextBits(ref state)));
                if (double.IsNaN(v) || double.IsInfinity(v) || v == 0.0)
                {
                    continue;
                }

                string text = CanonicalJson.FormatDouble(v);
                Assert.IsTrue(shape.IsMatch(text), text);
            }
        }

        [Test]
        public void FormatDouble_RoundTripsEveryFiniteValueExactly()
        {
            ulong state = 88172645463325252UL;
            for (int i = 0; i < 20000; i++)
            {
                double v = BitConverter.Int64BitsToDouble(unchecked((long)NextBits(ref state)));
                if (double.IsNaN(v) || double.IsInfinity(v) || v == 0.0)
                {
                    continue;
                }

                string text = CanonicalJson.FormatDouble(v);
                double back = double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
                Assert.AreEqual(BitConverter.DoubleToInt64Bits(v), BitConverter.DoubleToInt64Bits(back), text);
            }
        }

        [Test]
        public void FormatDouble_RoundTripsTypicalGameValues()
        {
            ulong state = 1234567UL;
            for (int i = 0; i < 5000; i++)
            {
                double v = 0.5 + (NextBits(ref state) >> 11) * (1.0 / 9007199254740992.0);
                string text = CanonicalJson.FormatDouble(v);

                Assert.AreEqual(BitConverter.DoubleToInt64Bits(v), BitConverter.DoubleToInt64Bits(double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture)), text);
            }
        }

        // ---------- yazıcı ----------

        private static JObject Sample()
        {
            return new JObject
            {
                ["a"] = 1L,
                ["b"] = new JArray(1, 2, 3),
                ["c"] = new JObject { ["d"] = "x\"y\\z\nü\u0001", ["e"] = true, ["f"] = JValue.CreateNull() },
                ["g"] = new JArray(),
                ["h"] = new JObject(),
                ["i"] = long.MaxValue,
                ["j"] = long.MinValue,
                ["k"] = new JArray(new JObject { ["z"] = 1 }, new JArray(false))
            };
        }

        [Test]
        public void Write_WithoutDoubles_MatchesJsonNetCompactAndIndentedText()
        {
            JObject o = Sample();

            Assert.AreEqual(o.ToString(Formatting.None), CanonicalJson.Write(o, false));
            Assert.AreEqual(o.ToString(Formatting.Indented).Replace("\r\n", "\n"), CanonicalJson.Write(o, true));
        }

        [Test]
        public void Write_KeepsPropertyOrderAndWritesDoublesCanonically()
        {
            var o = new JObject { ["z"] = 0.5120871669493438, ["a"] = new JArray(3081.0, 1, -0.0) };

            Assert.AreEqual("{\"z\":5.1208716694934375e-1,\"a\":[3.0810000000000000e3,1,0.0]}", CanonicalJson.Write(o, false));
            Assert.AreEqual("{\n  \"z\": 5.1208716694934375e-1,\n  \"a\": [\n    3.0810000000000000e3,\n    1,\n    0.0\n  ]\n}", CanonicalJson.Write(o, true));
        }

        [Test]
        public void Write_FloatParsedTokensAreWrittenAsDoubles_IntegersStayIntegers()
        {
            JObject o = JObject.Parse("{\"x\": 1.0, \"y\": 1, \"z\": 1e2, \"w\": 0.50000000000000000001}");

            Assert.AreEqual("{\"x\":1.0000000000000000e0,\"y\":1,\"z\":1.0000000000000000e2,\"w\":5.0000000000000000e-1}", CanonicalJson.Write(o, false));
        }

        [Test]
        public void Write_RejectsTokenTypesThatCannotOccurInASave()
        {
            Assert.Throws<ArgumentException>(() => CanonicalJson.Write(new JObject { ["d"] = new JValue(DateTime.UtcNow) }, false));
            Assert.Throws<ArgumentNullException>(() => CanonicalJson.Write(null, false));
        }

        [Test]
        public void Write_IsTheSameAfterAParseWithLegacySeventeenDigitNumbers()
        {
            JObject legacy = JObject.Parse("{\"i\": 0.51208716694934375}");
            JObject modern = JObject.Parse("{\"i\": 0.5120871669493438}");

            Assert.AreEqual(CanonicalJson.Write(modern, false), CanonicalJson.Write(legacy, false));
        }

        // ---------- sağlama ----------

        [Test]
        public void TheFixtureChecksum_IsComputedFromTheCanonicalPayload()
        {
            string text = File.ReadAllText(Path.Combine(TestPaths.FixturesDirectory(), "save_v1.json"));
            JObject root = JObject.Parse(text);

            string checksum = SaveSerializer.ComputeChecksum(CanonicalJson.Write((JObject)root["payload"], false));

            Assert.AreEqual((string)root["header"]["checksum"], checksum);
        }

        [Test]
        public void TheFixture_StillLoadsWhenItsDoublesAreSpelledWithSeventeenDigits_ButNotWhenAValueChanges()
        {
            string text = File.ReadAllText(Path.Combine(TestPaths.FixturesDirectory(), "save_v1.json"));
            Assert.IsTrue(text.Contains("0.5120871669493438"), "fikstür bu sorunlu değeri içermeli");

            string respelled = text.Replace("0.5120871669493438", "0.51208716694934375");
            string changed = text.Replace("0.5120871669493438", "0.5120871669493439");

            Assert.IsTrue(new SaveSerializer().Parse(respelled).IsSuccess, "aynı sayının başka yazımı aynı kanonik metni verir");
            Assert.AreEqual("save.checksum", new SaveSerializer().Parse(changed).ErrorCode);
        }

        // Eski Mono "R" davranışının taklidi: 15 hane dener, geri dönmüyorsa 17 hane yazar.
        private static string LegacyRoundTripText(double v)
        {
            string s = v.ToString("G15", CultureInfo.InvariantCulture);
            return double.Parse(s, CultureInfo.InvariantCulture) == v ? s : v.ToString("G17", CultureInfo.InvariantCulture);
        }

        [Test]
        public void WhyTheOldFixtureDiffered_AnOldMonoStyleSpellingOfTheSamePayloadHasAnotherChecksum()
        {
            string text = File.ReadAllText(Path.Combine(TestPaths.FixturesDirectory(), "save_v1.json"));
            JObject root;
            using (var reader = new JsonTextReader(new StringReader(text)) { DateParseHandling = DateParseHandling.None })
            {
                root = JObject.Load(reader);
            }

            string canonical = CanonicalJson.Write((JObject)root["payload"], false);
            string legacy = Regex.Replace(canonical, "(?<![\\w.\"-])-?[0-9]+\\.[0-9]+(?![\\w.\"])", m =>
            {
                double v = double.Parse(m.Value, CultureInfo.InvariantCulture);
                string t = LegacyRoundTripText(v);
                return t.Contains(".") || t.Contains("E") ? t : t + ".0";
            });

            Assert.AreNotEqual(canonical, legacy, "eski biçim en az bir sayıyı 17 hane yazar");
            Assert.AreNotEqual((string)root["header"]["checksum"], SaveSerializer.ComputeChecksum(legacy), "eski metin → farklı SHA-256 (Unity hatasının nedeni)");
            Assert.AreEqual((string)root["header"]["checksum"], SaveSerializer.ComputeChecksum(canonical), "kanonik metin → dosyadaki sağlama");
        }

        [Test]
        public void TheFixturePayload_IsWrittenByTheCanonicalWriterAsTheFileHoldsIt()
        {
            string text = File.ReadAllText(Path.Combine(TestPaths.FixturesDirectory(), "save_v1.json"));
            JObject root;
            using (var reader = new JsonTextReader(new StringReader(text)) { DateParseHandling = DateParseHandling.None })
            {
                root = JObject.Load(reader);
            }

            Assert.AreEqual(text.Replace("\r\n", "\n"), CanonicalJson.Write(root, true));
        }
    }
}
