using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Game;
using Esnaf.Domain.Negotiation;
using Esnaf.Persistence;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Persistence
{
    /// <summary>
    /// Kalıcı kayıt fikstürü (GDD v0.3 6.6: "her sürüm için kalıcı test dosyası"): Tests/Fixtures/save_v1.json, seed 20260101 ile
    /// 260 komut oynanıp Gün 17'de alış pazarlığı AÇIKKEN kaydedilmiş gerçek bir dosyadır. Her yeni sürümde bu dosya yeni kodla yüklenmelidir.
    /// </summary>
    public class SaveFixtureTests
    {
        // Fikstür üretilirken canlı oturumun durum özeti (GameStateDigest); dosya ve sabit birlikte değişmemelidir.
        private const string FixtureDigest = "de4eb924fea4491c";

        // Bağımsız Python PCG referansından (/tmp/day3/ref.py) fikstürdeki akış durumlarından hesaplanan SONRAKİ 5 değer.
        private static readonly Dictionary<string, uint[]> ReferenceNext = new Dictionary<string, uint[]>
        {
            { "customers", new uint[] { 1907204275, 352524972, 664897292, 1959526408, 1620818709 } },
            { "demand", new uint[] { 764741944, 3195130307, 3489115049, 723439994, 203675798 } },
            { "market", new uint[] { 2981120266, 2585225348, 2733266713, 3500406903, 2423832597 } },
            { "negotiation", new uint[] { 2581174904, 1717781432, 3271620392, 2526162533, 3082005331 } }
        };

        private static string FixtureText()
        {
            return File.ReadAllText(Path.Combine(TestPaths.FixturesDirectory(), "save_v1.json"));
        }

        [Test]
        public void TheV1Fixture_ParsesWithItsChecksum_AndRestoresToTheRecordedState()
        {
            Result<ParsedSave> parsed = new SaveSerializer().Parse(FixtureText());
            Assert.IsTrue(parsed.IsSuccess, parsed.ErrorCode + ": " + parsed.Message);

            Result<GameSession> restored = GameSession.Restore(MarketHarness.RealContent(), parsed.Value.Snapshot);

            Assert.IsTrue(restored.IsSuccess, restored.ErrorCode + ": " + restored.Message);
            Assert.AreEqual(FixtureDigest, restored.Value.Api.GetStateDigest());
            Assert.AreEqual(17, restored.Value.Api.GetDay());
            Assert.AreEqual(0, restored.Value.LoadWarnings.Count);
            Assert.IsTrue(restored.Value.EconomyState.Ledger.Verify().IsSuccess);
        }

        [Test]
        public void TheFixtureHeader_MatchesTheGddSample()
        {
            SaveHeader h = new SaveSerializer().Parse(FixtureText()).Value.Header;

            Assert.AreEqual("esnaf-save", h.Format);
            Assert.AreEqual(1, h.SaveVersion);
            Assert.AreEqual("0.1.0", h.AppVersion);
            Assert.AreEqual(1, h.ContentSchemaVersion);
            Assert.AreEqual("2026-01-01T10:00:00Z", h.CreatedAtUtc);
            Assert.AreEqual("2026-01-01T11:30:00Z", h.SavedAtUtc);
            Assert.AreEqual(5230L, h.PlayTimeSeconds);
            Assert.AreEqual(17, h.Preview.Day);
            Assert.AreEqual(161660L, h.Preview.Cash);
            Assert.AreEqual(243590L, h.Preview.Wealth);
        }

        [Test]
        public void TheFixturePreview_AgreesWithTheRestoredState()
        {
            SaveHeader h = new SaveSerializer().Parse(FixtureText()).Value.Header;
            GameSession s = GameSession.Restore(MarketHarness.RealContent(), new SaveSerializer().Parse(FixtureText()).Value.Snapshot).Value;

            Assert.AreEqual(s.Api.GetDay(), h.Preview.Day);
            Assert.AreEqual(s.Api.GetCash().Tl, h.Preview.Cash);
            Assert.AreEqual(s.Wealth.Calculate().Total.Tl, h.Preview.Wealth);
        }

        [Test]
        public void TheFixture_ContainsAnOpenNegotiation_ThatResumes()
        {
            GameSession s = GameSession.Restore(MarketHarness.RealContent(), new SaveSerializer().Parse(FixtureText()).Value.Snapshot).Value;

            NegotiationView view = s.Api.GetNegotiation();

            Assert.IsNotNull(view, "T17: pazarlık kaldığı yerden devam eder");
            Assert.AreEqual("negotiation.in_progress", s.Api.EndDay().ErrorCode);
            Assert.IsTrue(s.Api.WalkAway().IsSuccess);
            Assert.IsTrue(s.Api.EndDay().IsSuccess);
        }

        [Test]
        public void TheRandomStreams_ContinueExactlyAsTheIndependentReferenceSays()
        {
            GameSession s = GameSession.Restore(MarketHarness.RealContent(), new SaveSerializer().Parse(FixtureText()).Value.Snapshot).Value;

            foreach (KeyValuePair<string, uint[]> pair in ReferenceNext)
            {
                IRandom rng = s.Rng.Get(pair.Key);
                for (int i = 0; i < pair.Value.Length; i++)
                {
                    Assert.AreEqual(pair.Value[i], rng.NextUInt(), pair.Key + " #" + i);
                }
            }
        }

        [Test]
        public void TheFixture_SavedAgainWithTheSameMeta_IsByteIdentical()
        {
            var serializer = new SaveSerializer();
            ParsedSave parsed = serializer.Parse(FixtureText()).Value;
            GameSession s = GameSession.Restore(MarketHarness.RealContent(), parsed.Snapshot).Value;
            var meta = new SaveMeta
            {
                AppVersion = parsed.Header.AppVersion,
                ContentSchemaVersion = parsed.Header.ContentSchemaVersion,
                CreatedAtUtc = parsed.Header.CreatedAtUtc,
                SavedAtUtc = parsed.Header.SavedAtUtc,
                PlayTimeSeconds = parsed.Header.PlayTimeSeconds
            };

            string again = serializer.Serialize(s.Capture(), meta, parsed.Header.Preview);

            Assert.AreEqual(FixtureText().Replace("\r\n", "\n"), again.Replace("\r\n", "\n"));
        }

        [Test]
        public void TheFixture_LoadsThroughTheWholeServicePath_AndThePlayContinues()
        {
            var storage = new InMemorySaveStorage();
            storage.Put("slot0.json", FixtureText());
            var service = new SaveService(storage, new SaveSerializer(), new FakeSaveClock(DateTime.UtcNow), "0.1.0");

            LoadOutcome o = service.Load(MarketHarness.RealContent());

            Assert.AreEqual(LoadStatus.Loaded, o.Status);
            Assert.AreEqual(FixtureDigest, o.Session.Api.GetStateDigest());
            SessionDriver.Run(o.Session, new Random(3), 120);
            Assert.IsTrue(service.Save(o.Session, 0).IsSuccess);
            Assert.AreEqual(LoadStatus.Loaded, service.Load(MarketHarness.RealContent()).Status);
        }
    }
}
