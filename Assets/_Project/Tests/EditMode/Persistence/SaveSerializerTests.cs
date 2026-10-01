using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Game;
using Esnaf.Persistence;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Persistence
{
    public class SaveSerializerTests
    {
        private static readonly SaveMeta Meta = new SaveMeta
        {
            AppVersion = "0.1.0",
            ContentSchemaVersion = 1,
            CreatedAtUtc = "2026-01-02T03:04:05Z",
            SavedAtUtc = "2026-01-02T04:05:06Z",
            PlayTimeSeconds = 5230
        };

        private static readonly SavePreview Preview = new SavePreview { Day = 4, Cash = 238300, Wealth = 252100 };

        private static GameSession Rich(ulong seed = 5UL, int steps = 200)
        {
            GameSession s = GameSession.NewGame(MarketHarness.RealContent(), seed);
            SessionDriver.Run(s, new Random((int)seed * 13), steps);
            return s;
        }

        private static string Ser(GameSession s, SaveSerializer serializer = null)
        {
            return (serializer ?? new SaveSerializer()).Serialize(s.Capture(), Meta, Preview);
        }

        // ---------- sağlama (bağımsız referans) ----------

        [Test]
        public void Checksum_IsSha256_WithAPrefix_MatchingAnIndependentReference()
        {
            // Bağımsız kaynak: python3 hashlib.sha256(b"abc") ve sha256("{\"a\":1}")
            Assert.AreEqual("sha256:ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", SaveSerializer.ComputeChecksum("abc"));
            Assert.AreEqual("sha256:015abd7f5cc57a2dd94b7590f04ad8084273905ee33ec5cebeae62276a97f862", SaveSerializer.ComputeChecksum("{\"a\":1}"));
            Assert.AreEqual("sha256:e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", SaveSerializer.ComputeChecksum(string.Empty));
        }

        [Test]
        public void Checksum_UsesUtf8()
        {
            // python3: hashlib.sha256("Ayşe Hanım".encode("utf-8")).hexdigest()
            Assert.AreEqual("sha256:71f092dfe6d7f129618d48f50606e2b2f912c7665a011004dbfe9c549d4f88ae", SaveSerializer.ComputeChecksum("Ayşe Hanım"));
        }

        // ---------- biçim ----------

        [Test]
        public void Serialize_WritesTheGddHeaderAndPayloadSections()
        {
            GameSession s = Rich();
            string text = Ser(s);

            var root = (Dictionary<string, object>)MiniJson.Parse(text);
            var header = (Dictionary<string, object>)root["header"];
            var payload = (Dictionary<string, object>)root["payload"];

            Assert.AreEqual("esnaf-save", header["format"]);
            Assert.AreEqual(1.0, header["saveVersion"]);
            Assert.AreEqual("0.1.0", header["appVersion"]);
            Assert.AreEqual(1.0, header["contentSchemaVersion"]);
            Assert.AreEqual("2026-01-02T03:04:05Z", header["createdAtUtc"]);
            Assert.AreEqual("2026-01-02T04:05:06Z", header["savedAtUtc"]);
            Assert.AreEqual(5230.0, header["playTimeSeconds"]);
            StringAssert.StartsWith("sha256:", (string)header["checksum"]);
            var preview = (Dictionary<string, object>)header["preview"];
            Assert.AreEqual(4.0, preview["day"]);
            Assert.AreEqual(238300.0, preview["cash"]);
            Assert.AreEqual(252100.0, preview["wealth"]);
            CollectionAssert.AreEqual(
                new[] { "time", "rng", "ids", "economy", "inventory", "business", "market", "instances", "npcStates", "knowledge", "customers", "activeNegotiation", "activeSale" },
                payload.Keys.ToArray());
        }

        [Test]
        public void Serialize_IsDeterministic_AndTheTextRoundTripsToTheSameText()
        {
            GameSession s = Rich();
            var serializer = new SaveSerializer();

            string a = Ser(s, serializer);
            string b = Ser(s, serializer);
            Result<ParsedSave> parsed = serializer.Parse(a);
            string c = serializer.Serialize(parsed.Value.Snapshot, Meta, Preview);

            Assert.AreEqual(a, b);
            Assert.IsTrue(parsed.IsSuccess, parsed.ErrorCode + parsed.Message);
            Assert.AreEqual(a, c, "yükle → yeniden yaz aynı metni verir");
        }

        [Test]
        public void TheFile_ContainsOnlyIdsAndNumbers_NoDefinitionFieldsAndNoComputedValue()
        {
            string text = Ser(Rich());

            StringAssert.DoesNotContain("basePrice", text);
            StringAssert.DoesNotContain("Yıldız", text);
            StringAssert.DoesNotContain("trueValue", text);
            StringAssert.DoesNotContain("TrueValue", text);
            StringAssert.Contains("phone.", text);
        }

        [Test]
        public void TheSizeStaysWellBelowThreeHundredKilobytes()
        {
            GameSession s = Rich(9UL, 600);

            Assert.Less(Ser(s).Length, 300 * 1024);
        }

        // ---------- gidiş-dönüş ----------

        [TestCase(1UL)]
        [TestCase(2UL)]
        [TestCase(3UL)]
        [TestCase(4UL)]
        [TestCase(5UL)]
        [TestCase(6UL)]
        public void Parse_ThenRestore_KeepsTheExactState(ulong seed)
        {
            GameSession original = Rich(seed);
            var serializer = new SaveSerializer();

            Result<ParsedSave> parsed = serializer.Parse(Ser(original, serializer));

            Assert.IsTrue(parsed.IsSuccess, parsed.ErrorCode + ": " + parsed.Message);
            Assert.IsNull(DeepCompare.FirstDifference(original.Capture(), parsed.Value.Snapshot), "JSON gidiş-dönüşü tam olmalı");
            Result<GameSession> restored = GameSession.Restore(MarketHarness.RealContent(), parsed.Value.Snapshot);
            Assert.IsTrue(restored.IsSuccess, restored.Message);
            Assert.AreEqual(original.Api.GetStateDigest(), restored.Value.Api.GetStateDigest(), "I7");
        }

        [Test]
        public void Doubles_SurviveBitForBit()
        {
            GameSession original = Rich(2UL, 150);
            var serializer = new SaveSerializer();

            GameSnapshot back = serializer.Parse(Ser(original, serializer)).Value.Snapshot;

            Assert.IsNull(DeepCompare.FirstDifference(original.Capture(), back));
        }

        [TestCase(1UL)]
        [TestCase(2UL)]
        [TestCase(3UL)]
        [TestCase(4UL)]
        [TestCase(5UL)]
        [TestCase(6UL)]
        public void TheSnapshot_NeverHoldsANegativeZero_SoTheCanonicalZeroNormalizationLosesNothing(ulong seed)
        {
            // Canonical JSON -0.0'ı +0.0 yazar (Unity Mono işaretli sıfırı okuyamaz). Bu normalizasyon yalnızca oyun durumu -0.0 taşımıyorsa kayıpsızdır.
            GameSession s = Rich(seed);

            var tokens = Newtonsoft.Json.Linq.JObject.FromObject(s.Capture()).DescendantsAndSelf();
            foreach (Newtonsoft.Json.Linq.JToken token in tokens)
            {
                var value = token as Newtonsoft.Json.Linq.JValue;
                if (value != null && value.Value is double)
                {
                    Assert.AreNotEqual(long.MinValue, BitConverter.DoubleToInt64Bits((double)value.Value), "negatif sıfır: " + token.Path);
                }
            }
        }

        [Test]
        public void UlongValues_SurviveAtTheTopOfTheRange()
        {
            GameSession s = GameSession.NewGame(MarketHarness.RealContent(), ulong.MaxValue);
            var serializer = new SaveSerializer();

            Result<ParsedSave> parsed = serializer.Parse(Ser(s, serializer));

            Assert.IsTrue(parsed.IsSuccess, parsed.Message);
            Assert.AreEqual("18446744073709551615", parsed.Value.Snapshot.Time.Seed);
            Result<GameSession> restored = GameSession.Restore(MarketHarness.RealContent(), parsed.Value.Snapshot);
            Assert.IsTrue(restored.IsSuccess, restored.Message);
            Assert.AreEqual(s.Api.GetStateDigest(), restored.Value.Api.GetStateDigest());
        }

        [Test]
        public void Parse_ReturnsTheHeader()
        {
            var serializer = new SaveSerializer();

            SaveHeader header = serializer.Parse(Ser(Rich(), serializer)).Value.Header;

            Assert.AreEqual("esnaf-save", header.Format);
            Assert.AreEqual(1, header.SaveVersion);
            Assert.AreEqual("0.1.0", header.AppVersion);
            Assert.AreEqual(1, header.ContentSchemaVersion);
            Assert.AreEqual("2026-01-02T03:04:05Z", header.CreatedAtUtc);
            Assert.AreEqual("2026-01-02T04:05:06Z", header.SavedAtUtc);
            Assert.AreEqual(5230L, header.PlayTimeSeconds);
            Assert.AreEqual(4, header.Preview.Day);
            Assert.AreEqual(238300L, header.Preview.Cash);
            Assert.AreEqual(252100L, header.Preview.Wealth);
        }

        // ---------- bozuk dosyalar ----------

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("{")]
        [TestCase("not json at all")]
        [TestCase("[1,2,3]")]
        [TestCase("{\"header\": ")]
        public void Parse_GarbageIsAParseError(string text)
        {
            Result<ParsedSave> r = new SaveSerializer().Parse(text);

            Assert.IsTrue(r.IsFailure);
            Assert.AreEqual("save.parse", r.ErrorCode);
        }

        [Test]
        public void Parse_ATruncatedFile_IsAParseError()
        {
            string text = Ser(Rich());

            Result<ParsedSave> r = new SaveSerializer().Parse(text.Substring(0, text.Length / 2));

            Assert.AreEqual("save.parse", r.ErrorCode);
        }

        [Test]
        public void Parse_TextAfterTheDocument_IsAParseError()
        {
            Result<ParsedSave> r = new SaveSerializer().Parse(Ser(Rich()) + "\n{\"extra\": true}");

            Assert.AreEqual("save.parse", r.ErrorCode);
        }

        [Test]
        public void Parse_ATamperedPayload_FailsTheChecksum()
        {
            string text = Ser(Rich());
            string tampered = text.Replace("\"businessAssets\": 0,", "\"businessAssets\": 10,");

            Assert.AreNotEqual(text, tampered, "test verisi: değiştirilecek yer bulunamadı");
            Result<ParsedSave> r = new SaveSerializer().Parse(tampered);

            Assert.AreEqual("save.checksum", r.ErrorCode);
        }

        [Test]
        public void Parse_AWrongChecksum_Fails()
        {
            string text = Ser(Rich());
            int at = text.IndexOf("sha256:", StringComparison.Ordinal);
            string bad = text.Substring(0, at + 7) + (text[at + 7] == '0' ? "1" : "0") + text.Substring(at + 8);

            Assert.AreEqual("save.checksum", new SaveSerializer().Parse(bad).ErrorCode);
        }

        [Test]
        public void Parse_AMissingChecksum_IsAFormatError()
        {
            string text = Ser(Rich());
            int at = text.IndexOf("\"checksum\"", StringComparison.Ordinal);
            int end = text.IndexOf('\n', at);
            string without = text.Substring(0, at) + text.Substring(end + 1);

            Assert.AreEqual("save.format", new SaveSerializer().Parse(without).ErrorCode);
        }

        [Test]
        public void Parse_TheFileIsEditableInWhitespaceOnly_WithoutBreakingTheChecksum()
        {
            var serializer = new SaveSerializer();
            string text = Ser(Rich(), serializer);
            string compact = Newtonsoft.Json.Linq.JObject.Parse(text).ToString(Newtonsoft.Json.Formatting.None);

            Assert.IsTrue(serializer.Parse(compact).IsSuccess, "girinti/boşluk farkı sağlamayı bozmaz");
        }

        [Test]
        public void Parse_WrongFormatName_IsAFormatError()
        {
            string text = Ser(Rich()).Replace("esnaf-save", "other-save");

            Assert.AreEqual("save.format", new SaveSerializer().Parse(text).ErrorCode);
        }

        [TestCase("\"header\"", "\"headr\"")]
        [TestCase("\"payload\"", "\"payloat\"")]
        public void Parse_MissingSections_AreFormatErrors(string find, string replacement)
        {
            string text = Ser(Rich()).Replace(find, replacement);

            Assert.AreEqual("save.format", new SaveSerializer().Parse(text).ErrorCode);
        }

        [Test]
        public void Parse_SaveVersionMustBeAWholeNumber()
        {
            string text = Ser(Rich()).Replace("\"saveVersion\": 1", "\"saveVersion\": \"one\"");

            Assert.AreEqual("save.format", new SaveSerializer().Parse(text).ErrorCode);
        }

        [Test]
        public void Parse_AStructurallyBrokenPayload_IsAPayloadError()
        {
            // Sağlama yeniden hesaplanır: dosya "bozulmamış" ama yük tipli nesneye çevrilemiyor.
            var serializer = new SaveSerializer();
            string text = Ser(Rich(), serializer);
            var root = Newtonsoft.Json.Linq.JObject.Parse(text);
            root["payload"]["time"] = "not an object";
            root["header"]["checksum"] = SaveSerializer.ComputeChecksum(CanonicalJson.Write(root["payload"], false));

            Result<ParsedSave> r = serializer.Parse(root.ToString());

            Assert.AreEqual("save.payload", r.ErrorCode);
        }

        [Test]
        public void Parse_AMissingSaveVersion_IsAFormatError_NotACrash()
        {
            string text = Ser(Rich());
            int at = text.IndexOf("\"saveVersion\"", StringComparison.Ordinal);
            int end = text.IndexOf('\n', at);
            string without = text.Substring(0, at) + text.Substring(end + 1);

            Assert.AreEqual("save.format", new SaveSerializer().Parse(without).ErrorCode);
        }

        [Test]
        public void Parse_ANewerVersion_IsReportedBeforeTheChecksumIsLookedAt()
        {
            string text = Ser(Rich()).Replace("\"saveVersion\": 1", "\"saveVersion\": 99").Replace("\"businessAssets\": 0,", "\"businessAssets\": 10,");
            Assert.AreNotEqual(Ser(Rich()), text);

            Assert.AreEqual("save.version_newer", new SaveSerializer().Parse(text).ErrorCode, "gelecek sürümün sağlama kuralı farklı olabilir");
        }

        [Test]
        public void Parse_ANumberThatDoesNotFit_IsAPayloadError()
        {
            var serializer = new SaveSerializer();
            var root = Newtonsoft.Json.Linq.JObject.Parse(Ser(Rich(), serializer));
            root["payload"]["time"]["day"] = 99999999999L;
            root["header"]["checksum"] = SaveSerializer.ComputeChecksum(CanonicalJson.Write(root["payload"], false));

            Assert.AreEqual("save.payload", serializer.Parse(root.ToString()).ErrorCode);
        }

        [Test]
        public void Checksum_HasSixtyFourHexDigits_Lowercase()
        {
            string c = SaveSerializer.ComputeChecksum("x");

            Assert.AreEqual(7 + 64, c.Length);
            StringAssert.IsMatch("^sha256:[0-9a-f]{64}$", c);
        }

        // ---------- önizleme ----------

        [Test]
        public void ReadHeader_ReturnsThePreview_WithoutValidatingThePayload()
        {
            var serializer = new SaveSerializer();
            string text = Ser(Rich(), serializer);
            string brokenPayload = text.Replace("\"businessAssets\": 0,", "\"businessAssets\": 10,");
            Assert.AreNotEqual(text, brokenPayload);

            Result<SaveHeader> header = serializer.ReadHeader(brokenPayload);

            Assert.IsTrue(header.IsSuccess);
            Assert.AreEqual(4, header.Value.Preview.Day);
        }

        [Test]
        public void ReadHeader_RejectsGarbageAndForeignFormats()
        {
            var serializer = new SaveSerializer();

            Assert.AreEqual("save.parse", serializer.ReadHeader("nope").ErrorCode);
            Assert.AreEqual("save.format", serializer.ReadHeader("{}").ErrorCode);
            Assert.AreEqual("save.format", serializer.ReadHeader(Ser(Rich()).Replace("esnaf-save", "x")).ErrorCode);
        }

        // ---------- sürümler ve migrasyon ----------

        private sealed class RenameMigration : IMigration
        {
            public int From { get { return 0; } }
            public int To { get { return 1; } }
            public int Calls;

            public void Apply(Newtonsoft.Json.Linq.JObject payload)
            {
                Calls++;
                payload["business"] = payload["shop"];
                payload.Remove("shop");
            }
        }

        private static string AsVersion0(string v1Text)
        {
            var root = Newtonsoft.Json.Linq.JObject.Parse(v1Text);
            var payload = (Newtonsoft.Json.Linq.JObject)root["payload"];
            payload["shop"] = payload["business"];
            payload.Remove("business");
            root["header"]["saveVersion"] = 0;
            root["header"]["checksum"] = SaveSerializer.ComputeChecksum(CanonicalJson.Write(payload, false));
            return root.ToString();
        }

        [Test]
        public void Migration_AFakeVersionZeroFile_IsUpgradedAndLoads()
        {
            GameSession original = Rich();
            var migration = new RenameMigration();
            var serializer = new SaveSerializer(new MigrationRunner(1, new IMigration[] { migration }));
            string v0 = AsVersion0(Ser(original, serializer));

            Result<ParsedSave> parsed = serializer.Parse(v0);

            Assert.IsTrue(parsed.IsSuccess, parsed.ErrorCode + ": " + parsed.Message);
            Assert.AreEqual(1, migration.Calls);
            Result<GameSession> restored = GameSession.Restore(MarketHarness.RealContent(), parsed.Value.Snapshot);
            Assert.IsTrue(restored.IsSuccess, restored.Message);
            Assert.AreEqual(original.Api.GetStateDigest(), restored.Value.Api.GetStateDigest());
        }

        [Test]
        public void Migration_AFileAlreadyAtTheCurrentVersion_DoesNotRunMigrations()
        {
            var migration = new RenameMigration();
            var serializer = new SaveSerializer(new MigrationRunner(1, new IMigration[] { migration }));

            Assert.IsTrue(serializer.Parse(Ser(Rich(), serializer)).IsSuccess);
            Assert.AreEqual(0, migration.Calls);
        }

        [Test]
        public void Migration_WithoutAPathForAnOldVersion_IsUnsupported()
        {
            var serializer = new SaveSerializer();
            string v0 = AsVersion0(Ser(Rich(), serializer));

            Assert.AreEqual("save.version_unsupported", serializer.Parse(v0).ErrorCode);
        }

        [TestCase(2)]
        [TestCase(99)]
        public void Version_NewerThanTheAppSupports_IsRejectedBeforeAnythingElse(int version)
        {
            var serializer = new SaveSerializer();
            string text = Ser(Rich(), serializer).Replace("\"saveVersion\": 1", "\"saveVersion\": " + version);

            Result<ParsedSave> r = serializer.Parse(text);

            Assert.AreEqual("save.version_newer", r.ErrorCode, "sağlama tutmasa bile ileri sürüm önce bildirilir");
        }

        [Test]
        public void Version_AnEarlierFileIsUpgradedThroughTheWholeChain()
        {
            GameSession original = Rich();
            var log = new List<string>();
            var chain = new IMigration[] { new LoggingMigration(0, 1, log), new LoggingMigration(1, 2, log), new LoggingMigration(2, 3, log) };
            var v3 = new SaveSerializer(new MigrationRunner(3, chain));
            string v1Text = Ser(original, new SaveSerializer()).Replace("\"saveVersion\": 1", "\"saveVersion\": 1");

            Result<ParsedSave> r = v3.Parse(v1Text);

            Assert.IsTrue(r.IsSuccess, r.ErrorCode + r.Message);
            CollectionAssert.AreEqual(new[] { "1>2", "2>3" }, log);
        }

        private sealed class LoggingMigration : IMigration
        {
            private readonly List<string> _log;

            public int From { get; }
            public int To { get; }

            public LoggingMigration(int from, int to, List<string> log)
            {
                From = from;
                To = to;
                _log = log;
            }

            public void Apply(Newtonsoft.Json.Linq.JObject payload)
            {
                _log.Add(From + ">" + To);
            }
        }

        [Test]
        public void Serialize_WritesTheRunnersCurrentVersion()
        {
            var v2 = new SaveSerializer(new MigrationRunner(2, new IMigration[] { new LoggingMigration(1, 2, new List<string>()) }));

            SaveHeader header = v2.Parse(Ser(Rich(), v2)).Value.Header;

            Assert.AreEqual(2, header.SaveVersion);
            Assert.AreEqual(2, v2.CurrentVersion);
            Assert.AreEqual(1, new SaveSerializer().CurrentVersion);
        }

        [Test]
        public void Null_Arguments_AreProgrammerErrors()
        {
            var serializer = new SaveSerializer();
            GameSnapshot snap = Rich().Capture();

            Assert.Throws<ArgumentNullException>(() => serializer.Serialize(null, Meta, Preview));
            Assert.Throws<ArgumentNullException>(() => serializer.Serialize(snap, null, Preview));
            Assert.Throws<ArgumentNullException>(() => serializer.Serialize(snap, Meta, null));
            Assert.Throws<ArgumentNullException>(() => serializer.Parse(null));
            Assert.Throws<ArgumentNullException>(() => serializer.ReadHeader(null));
            Assert.Throws<ArgumentNullException>(() => SaveSerializer.ComputeChecksum(null));
        }
    }
}
