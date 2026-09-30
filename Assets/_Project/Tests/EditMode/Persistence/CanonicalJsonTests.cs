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
        private static readonly object[][] Golden =
        {
            new object[] { 0.5120871669493438, "0.5120871669493438" },
            new object[] { 0.9802138046024543, "0.9802138046024543" },
            new object[] { 0.7666180147282152, "0.7666180147282152" },
            new object[] { 0.7091521128839813, "0.7091521128839813" },
            new object[] { 0.9817245566526737, "0.9817245566526737" },
            new object[] { 0.5424123071976978, "0.5424123071976978" },
            new object[] { 0.3669868400741799, "0.3669868400741799" },
            new object[] { 1.0121075477673915, "1.0121075477673915" },
            new object[] { 0.1, "0.1" },
            new object[] { 0.30000000000000004, "0.30000000000000004" },
            new object[] { 1.0, "1.0" },
            new object[] { 3081.0, "3081.0" },
            new object[] { 0.0, "0.0" },
            new object[] { -1.5, "-1.5" },
            new object[] { 100000000000000.0, "100000000000000.0" },
            new object[] { 123456789012345.0, "123456789012345.0" },
            new object[] { 1e15, "1000000000000000.0" },
            new object[] { 1e16, "10000000000000000.0" },
            new object[] { 12345678901234568.0, "12345678901234568.0" },
            new object[] { 1e17, "1E+17" },
            new object[] { 1e22, "1E+22" },
            new object[] { 1e23, "1E+23" },
            new object[] { 9.999999999999997e22, "9.999999999999997E+22" },
            new object[] { 1e-10, "1E-10" },
            new object[] { 1.5e-9, "1.5E-09" },
            new object[] { 1e-99, "1E-99" },
            new object[] { 1.5e18, "1.5E+18" },
            new object[] { 906851161693619.25, "906851161693619.2" },
            new object[] { 906851161693619.375, "906851161693619.4" },
            new object[] { 9007199254740992.0, "9007199254740992.0" },
            new object[] { 123456789012345680.0, "1.2345678901234568E+17" },
            new object[] { 0.0001, "0.0001" },
            new object[] { 0.00012, "0.00012" },
            new object[] { 0.00001234, "1.234E-05" },
            new object[] { 0.00001, "1E-05" },
            new object[] { 1.5e-7, "1.5E-07" },
            new object[] { 1e-100, "1E-100" },
            new object[] { -2.5e-300, "-2.5E-300" },
            new object[] { double.MaxValue, "1.7976931348623157E+308" },
            new object[] { double.MinValue, "-1.7976931348623157E+308" },
            new object[] { double.Epsilon, "5E-324" },
            new object[] { 2.2250738585072014E-308, "2.2250738585072014E-308" },
            new object[] { 4.35, "4.35" },
            new object[] { 0.85, "0.85" },
            new object[] { 1.05, "1.05" }
        };

        [TestCaseSource(nameof(Golden))]
        public void FormatDouble_GivesTheFixedCanonicalText(double value, string expected)
        {
            Assert.AreEqual(expected, CanonicalJson.FormatDouble(value));
        }

        [Test]
        public void FormatDouble_NegativeZeroKeepsItsSign()
        {
            Assert.AreEqual("-0.0", CanonicalJson.FormatDouble(-0.0));
            Assert.AreEqual("0.0", CanonicalJson.FormatDouble(0.0));
        }

        [Test]
        public void FormatDouble_NonFiniteValuesUseTheJsonNetLiterals()
        {
            Assert.AreEqual("NaN", CanonicalJson.FormatDouble(double.NaN));
            Assert.AreEqual("Infinity", CanonicalJson.FormatDouble(double.PositiveInfinity));
            Assert.AreEqual("-Infinity", CanonicalJson.FormatDouble(double.NegativeInfinity));
        }

        [Test]
        public void FormatDouble_TheLegacySeventeenDigitSpellingsOfTheSameNumbersAreNotProduced()
        {
            Assert.AreEqual("0.5120871669493438", CanonicalJson.FormatDouble(double.Parse("0.51208716694934375", CultureInfo.InvariantCulture)));
            Assert.AreEqual("0.9802138046024543", CanonicalJson.FormatDouble(double.Parse("0.98021380460245433", CultureInfo.InvariantCulture)));
            Assert.AreEqual("0.7666180147282152", CanonicalJson.FormatDouble(double.Parse("0.76661801472821522", CultureInfo.InvariantCulture)));
        }

        [Test]
        public void FormatDouble_IsIndependentOfTheCurrentCulture()
        {
            CultureInfo saved = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("tr-TR");
                Assert.AreEqual("0.5120871669493438", CanonicalJson.FormatDouble(0.5120871669493438));
                Assert.AreEqual("1.5E-07", CanonicalJson.FormatDouble(1.5e-7));
                Assert.AreEqual("-2.0", CanonicalJson.FormatDouble(-2.0));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = saved;
            }
        }

        [Test]
        public void FormatDouble_RoundTripsEveryFiniteValueExactly()
        {
            var rng = new Random(20260930);
            var bytes = new byte[8];
            for (int i = 0; i < 20000; i++)
            {
                rng.NextBytes(bytes);
                double v = BitConverter.ToDouble(bytes, 0);
                if (double.IsNaN(v) || double.IsInfinity(v))
                {
                    continue;
                }

                double back = double.Parse(CanonicalJson.FormatDouble(v), NumberStyles.Float, CultureInfo.InvariantCulture);
                Assert.AreEqual(BitConverter.DoubleToInt64Bits(v), BitConverter.DoubleToInt64Bits(back), CanonicalJson.FormatDouble(v));
            }
        }

        [Test]
        public void FormatDouble_RoundTripsTypicalGameValuesAndUsesAtMostSeventeenDigits()
        {
            var rng = new Random(7);
            for (int i = 0; i < 5000; i++)
            {
                double v = 0.5 + rng.NextDouble();
                string text = CanonicalJson.FormatDouble(v);

                Assert.AreEqual(v, double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture), text);
                Assert.LessOrEqual(text.Length, 19, text);
            }
        }

        [Test]
        public void FormatDouble_PicksTheShortestDigitCountThatRoundTrips()
        {
            for (int digits = 1; digits <= 15; digits++)
            {
                double v = double.Parse("0." + new string('1', digits), CultureInfo.InvariantCulture);
                Assert.AreEqual("0." + new string('1', digits), CanonicalJson.FormatDouble(v));
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

            Assert.AreEqual("{\"z\":0.5120871669493438,\"a\":[3081.0,1,-0.0]}", CanonicalJson.Write(o, false));
            Assert.AreEqual("{\n  \"z\": 0.5120871669493438,\n  \"a\": [\n    3081.0,\n    1,\n    -0.0\n  ]\n}", CanonicalJson.Write(o, true));
        }

        [Test]
        public void Write_FloatParsedTokensAreWrittenAsDoubles_IntegersStayIntegers()
        {
            JObject o = JObject.Parse("{\"x\": 1.0, \"y\": 1, \"z\": 1e2, \"w\": 0.50000000000000000001}");

            Assert.AreEqual("{\"x\":1.0,\"y\":1,\"z\":100.0,\"w\":0.5}", CanonicalJson.Write(o, false));
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
