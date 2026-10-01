using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using Esnaf.Core;
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
            new object[] { 1234567890123456.25, "1.2345678901234562e15" },
            new object[] { 1234567890123456.75, "1.2345678901234568e15" },
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
        public void TheFixture_StillLoadsWhenADoubleIsSpelledDifferently_ButNotWhenAValueChanges()
        {
            string text = File.ReadAllText(Path.Combine(TestPaths.FixturesDirectory(), "save_v1.json"));
            Assert.IsTrue(text.Contains("5.1208716694934375e-1"), "fikstür bu sorunlu değeri 17 haneli yazmalı");

            string respelled = text.Replace("5.1208716694934375e-1", "0.5120871669493438");
            string changed = text.Replace("5.1208716694934375e-1", "5.1208716694934385e-1");

            Assert.IsTrue(new SaveSerializer().Parse(respelled).IsSuccess, "aynı sayının başka yazımı aynı kanonik metni verir");
            Assert.AreEqual("save.checksum", new SaveSerializer().Parse(changed).ErrorCode);
        }

        private const string LegacyFixtureName = "save_v1_legacy_shortest.json";

        private static JObject LoadRoot(string name)
        {
            string text = File.ReadAllText(Path.Combine(TestPaths.FixturesDirectory(), name));
            using (var reader = new JsonTextReader(new StringReader(text)) { DateParseHandling = DateParseHandling.None })
            {
                return JObject.Load(reader);
            }
        }

        [Test]
        public void TheLegacyShortestSpellingFixture_IsNowRejectedByItsChecksum_ByDesign()
        {
            // Düzeltme 2 (DAY9_SCOPE.md): kanonik sayı yazımı değişti → en kısa metinle hesaplanmış sağlama artık tutmaz. v1 yayınlanmadı; bilinçli kırılma.
            string legacy = File.ReadAllText(Path.Combine(TestPaths.FixturesDirectory(), LegacyFixtureName));

            Result<ParsedSave> parsed = new SaveSerializer().Parse(legacy);

            Assert.IsTrue(parsed.IsFailure);
            Assert.AreEqual("save.checksum", parsed.ErrorCode);
        }

        [Test]
        public void TheLegacyAndTheCurrentFixtures_HoldTheSameState_BitForBit_AndDifferOnlyInTheChecksum()
        {
            JObject legacy = LoadRoot(LegacyFixtureName);
            JObject current = LoadRoot("save_v1.json");

            int floats = CompareBitExact(legacy, current, "");

            Assert.AreEqual(49, floats, "fikstürdeki double sayısı");
            Assert.AreNotEqual((string)legacy["header"]["checksum"], (string)current["header"]["checksum"]);
            current["header"]["checksum"] = legacy["header"]["checksum"];
            Assert.IsTrue(JToken.DeepEquals(legacy["header"], current["header"]), "başlıkta yalnızca sağlama değişir");
        }

        // Yapı, tamsayılar, metinler ve alan sırası aynı; her double IEEE-754 bit deseni olarak aynı. Karşılaştırılan double sayısını döndürür.
        private static int CompareBitExact(JToken a, JToken b, string path)
        {
            Assert.AreEqual(a.Type, b.Type, path);
            int floats = 0;
            JObject oa = a as JObject;
            if (oa != null)
            {
                var ob = (JObject)b;
                CollectionAssert.AreEqual(System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(oa.Properties(), x => x.Name)), System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(ob.Properties(), x => x.Name)), path);
                foreach (JProperty property in oa.Properties())
                {
                    if (path == "" && property.Name == "header")
                    {
                        // başlık ayrıca doğrulanır (yalnızca sağlama farklı olabilir)
                        floats += CompareBitExact(property.Value, ob[property.Name], path + "/header");
                        continue;
                    }

                    floats += CompareBitExact(property.Value, ob[property.Name], path + "/" + property.Name);
                }

                return floats;
            }

            JArray aa = a as JArray;
            if (aa != null)
            {
                var ab = (JArray)b;
                Assert.AreEqual(aa.Count, ab.Count, path);
                for (int i = 0; i < aa.Count; i++)
                {
                    floats += CompareBitExact(aa[i], ab[i], path + "[" + i + "]");
                }

                return floats;
            }

            var va = (JValue)a;
            var vb = (JValue)b;
            if (va.Type == JTokenType.Float)
            {
                Assert.AreEqual(BitConverter.DoubleToInt64Bits((double)va.Value), BitConverter.DoubleToInt64Bits((double)vb.Value), path);
                return 1;
            }

            if (path != "/header/checksum")
            {
                Assert.AreEqual(va.Value, vb.Value, path);
            }

            return 0;
        }

        [Test]
        public void TheLegacyPayload_CanonicalizedByTheCurrentWriter_GivesTheCurrentFixtureChecksum()
        {
            JObject legacy = LoadRoot(LegacyFixtureName);
            JObject current = LoadRoot("save_v1.json");

            string checksum = SaveSerializer.ComputeChecksum(CanonicalJson.Write((JObject)legacy["payload"], false));

            Assert.AreEqual((string)current["header"]["checksum"], checksum);
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
