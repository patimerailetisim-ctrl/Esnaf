using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Content;
using Esnaf.Domain.Game;
using Esnaf.Persistence;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Persistence
{
    public class SaveServiceTests
    {
        private sealed class Rig
        {
            public readonly InMemorySaveStorage Storage = new InMemorySaveStorage();
            public readonly FakeSaveClock Clock = new FakeSaveClock(new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc));
            public readonly SaveSerializer Serializer = new SaveSerializer();
            public readonly SaveService Service;

            public Rig()
            {
                Service = new SaveService(Storage, Serializer, Clock, "0.1.0");
            }
        }

        private static ContentDatabase Content
        {
            get { return MarketHarness.RealContent(); }
        }

        private static GameSession Played(ulong seed, int steps)
        {
            GameSession s = GameSession.NewGame(Content, seed);
            SessionDriver.Run(s, new Random((int)seed * 7), steps);
            return s;
        }

        private static string Day(Rig rig, string file)
        {
            SaveHeader h = rig.Serializer.Parse(rig.Storage.Files[file]).Value.Header;
            return h.Preview.Day.ToString();
        }

        // ---------- yazma ----------

        [Test]
        public void FirstSave_CreatesOnlySlotZero_AndLeavesNoTempFile()
        {
            var rig = new Rig();

            Result r = rig.Service.Save(GameSession.NewGame(Content, 1UL), 100);

            Assert.IsTrue(r.IsSuccess, r.ErrorCode);
            CollectionAssert.AreEqual(new[] { "slot0.json" }, rig.Storage.List().ToArray());
            Assert.IsNull(rig.Service.LastError);
        }

        [Test]
        public void EachSave_RotatesTheBackups()
        {
            var rig = new Rig();
            GameSession s = GameSession.NewGame(Content, 1UL);
            Assert.IsTrue(rig.Service.Save(s, 0).IsSuccess);
            string first = rig.Storage.Files["slot0.json"];
            s.Api.EndDay();
            Assert.IsTrue(rig.Service.Save(s, 0).IsSuccess);
            string second = rig.Storage.Files["slot0.json"];
            s.Api.EndDay();

            Assert.IsTrue(rig.Service.Save(s, 0).IsSuccess);

            Assert.AreEqual(second, rig.Storage.Files["slot0.bak1.json"]);
            Assert.AreEqual(first, rig.Storage.Files["slot0.bak2.json"]);
            Assert.AreNotEqual(second, rig.Storage.Files["slot0.json"]);
            CollectionAssert.AreEqual(new[] { "slot0.bak1.json", "slot0.bak2.json", "slot0.json" }, rig.Storage.List().ToArray());
            Assert.AreEqual("3", Day(rig, "slot0.json"));
            Assert.AreEqual("2", Day(rig, "slot0.bak1.json"));
            Assert.AreEqual("1", Day(rig, "slot0.bak2.json"));
        }

        [Test]
        public void OnlyTwoBackupsAreKept()
        {
            var rig = new Rig();
            GameSession s = GameSession.NewGame(Content, 1UL);
            for (int i = 0; i < 5; i++)
            {
                Assert.IsTrue(rig.Service.Save(s, 0).IsSuccess);
                s.Api.EndDay();
            }

            CollectionAssert.AreEqual(new[] { "slot0.bak1.json", "slot0.bak2.json", "slot0.json" }, rig.Storage.List().ToArray());
            Assert.AreEqual("5", Day(rig, "slot0.json"));
            Assert.AreEqual("4", Day(rig, "slot0.bak1.json"));
            Assert.AreEqual("3", Day(rig, "slot0.bak2.json"));
        }

        [Test]
        public void TheSequenceOfOperations_FollowsTheGddOrder()
        {
            var rig = new Rig();
            GameSession s = GameSession.NewGame(Content, 1UL);
            rig.Service.Save(s, 0);
            rig.Service.Save(s, 0);
            rig.Storage.Log.Clear();

            rig.Service.Save(s, 0);

            CollectionAssert.AreEqual(
                new[]
                {
                    "delete:slot0.tmp",
                    "write:slot0.tmp",
                    "copy:slot0.bak1.json>slot0.bak2.json",
                    "copy:slot0.json>slot0.bak1.json",
                    "replace:slot0.tmp>slot0.json"
                },
                rig.Storage.Log);
        }

        [Test]
        public void TheWrittenFile_ReflectsTheSessionAndTheMeta()
        {
            var rig = new Rig();
            GameSession s = Played(3UL, 120);

            Assert.IsTrue(rig.Service.Save(s, 5230).IsSuccess);

            ParsedSave parsed = rig.Serializer.Parse(rig.Storage.Files["slot0.json"]).Value;
            Assert.AreEqual("0.1.0", parsed.Header.AppVersion);
            Assert.AreEqual(ContentFileNames.SupportedSchemaVersion, parsed.Header.ContentSchemaVersion);
            Assert.AreEqual("2026-03-04T05:06:07Z", parsed.Header.SavedAtUtc);
            Assert.AreEqual("2026-03-04T05:06:07Z", parsed.Header.CreatedAtUtc);
            Assert.AreEqual(5230L, parsed.Header.PlayTimeSeconds);
            Assert.AreEqual(s.Api.GetDay(), parsed.Header.Preview.Day);
            Assert.AreEqual(s.Api.GetCash().Tl, parsed.Header.Preview.Cash);
            Assert.AreEqual(s.Wealth.Calculate().Total.Tl, parsed.Header.Preview.Wealth);
        }

        [Test]
        public void CreatedAt_IsKeptFromTheFirstSave_WhileSavedAtMoves()
        {
            var rig = new Rig();
            GameSession s = GameSession.NewGame(Content, 1UL);
            rig.Service.Save(s, 0);
            rig.Clock.UtcNow = rig.Clock.UtcNow.AddHours(2);

            rig.Service.Save(s, 0);

            SaveHeader h = rig.Serializer.Parse(rig.Storage.Files["slot0.json"]).Value.Header;
            Assert.AreEqual("2026-03-04T05:06:07Z", h.CreatedAtUtc);
            Assert.AreEqual("2026-03-04T07:06:07Z", h.SavedAtUtc);
        }

        [Test]
        public void Save_PublishesGameSaved_AfterAllFilesAreInPlace()
        {
            var rig = new Rig();
            GameSession s = GameSession.NewGame(Content, 1UL);
            var seen = new List<string>();
            s.Bus.Subscribe<GameSaved>(e => seen.Add(e.Day + ":" + rig.Storage.Exists("slot0.json") + ":" + rig.Storage.Exists("slot0.tmp")));

            rig.Service.Save(s, 0);

            CollectionAssert.AreEqual(new[] { "1:True:False" }, seen);
        }

        // ---------- atomiklik ve hatalar ----------

        [Test]
        public void AnInterruptedTempWrite_LeavesSlotZeroUntouched_AndReportsTheError()
        {
            var rig = new Rig();
            GameSession s = GameSession.NewGame(Content, 1UL);
            rig.Service.Save(s, 0);
            string before = rig.Storage.Files["slot0.json"];
            s.Api.EndDay();
            rig.Storage.InterruptWriteOf("slot0.tmp");

            Result r = rig.Service.Save(s, 0);

            Assert.IsTrue(r.IsFailure);
            Assert.AreEqual("save.io", r.ErrorCode);
            Assert.AreEqual(before, rig.Storage.Files["slot0.json"], "slot0 değişmez");
            Assert.IsFalse(rig.Storage.Exists("slot0.tmp"), "yarım tmp temizlenir");
            Assert.IsFalse(rig.Storage.Exists("slot0.bak1.json"), "yedek dönüşümü başlamadı");
            StringAssert.Contains("Simulated interruption", rig.Service.LastError);
        }

        [TestCase("write", "slot0.tmp")]
        [TestCase("read", "slot0.tmp")]
        [TestCase("copy", "slot0.bak1.json>slot0.bak2.json")]
        [TestCase("copy", "slot0.json>slot0.bak1.json")]
        [TestCase("replace", "slot0.tmp>slot0.json")]
        public void AFailureAtAnyStep_LeavesSlotZeroIntact(string op, string name)
        {
            var rig = new Rig();
            GameSession s = GameSession.NewGame(Content, 1UL);
            rig.Service.Save(s, 0);
            s.Api.EndDay();
            rig.Service.Save(s, 0);
            string current = rig.Storage.Files["slot0.json"];
            s.Api.EndDay();
            rig.Storage.FailOn(op, name);

            Result r = rig.Service.Save(s, 0);

            Assert.IsTrue(r.IsFailure, op + " " + name);
            Assert.AreEqual("save.io", r.ErrorCode);
            Assert.AreEqual(current, rig.Storage.Files["slot0.json"], "slot0 sağlam kalır");
            Assert.IsFalse(rig.Storage.Exists("slot0.tmp"));
            Assert.IsTrue(rig.Serializer.Parse(rig.Storage.Files["slot0.json"]).IsSuccess);
            Assert.IsNotNull(rig.Service.LastError);
        }

        [Test]
        public void ACorruptReadBackOfTheTempFile_AbortsBeforeTouchingSlotZero()
        {
            var rig = new Rig();
            GameSession s = GameSession.NewGame(Content, 1UL);
            rig.Service.Save(s, 0);
            string before = rig.Storage.Files["slot0.json"];
            s.Api.EndDay();
            rig.Storage.CorruptReadOf("slot0.tmp", t => t.Substring(0, t.Length / 2));

            Result r = rig.Service.Save(s, 0);

            Assert.AreEqual("save.verify_failed", r.ErrorCode);
            Assert.AreEqual(before, rig.Storage.Files["slot0.json"]);
            Assert.IsFalse(rig.Storage.Exists("slot0.tmp"));
            Assert.IsFalse(rig.Storage.Exists("slot0.bak1.json"));
        }

        [Test]
        public void AFailedSave_DoesNotPublishGameSaved()
        {
            var rig = new Rig();
            GameSession s = GameSession.NewGame(Content, 1UL);
            int saved = 0;
            s.Bus.Subscribe<GameSaved>(e => saved++);
            rig.Storage.FailOn("write", "slot0.tmp");

            rig.Service.Save(s, 0);

            Assert.AreEqual(0, saved);
        }

        [Test]
        public void ASuccessfulSave_ClearsTheLastError()
        {
            var rig = new Rig();
            GameSession s = GameSession.NewGame(Content, 1UL);
            rig.Storage.FailOn("write", "slot0.tmp");
            rig.Service.Save(s, 0);
            Assert.IsNotNull(rig.Service.LastError);
            rig.Storage.ClearFaults();

            Assert.IsTrue(rig.Service.Save(s, 0).IsSuccess);

            Assert.IsNull(rig.Service.LastError);
        }

        [Test]
        public void AStaleTempFile_IsIgnoredByLoad_AndReplacedBySave()
        {
            var rig = new Rig();
            GameSession s = GameSession.NewGame(Content, 1UL);
            rig.Service.Save(s, 0);
            rig.Storage.Put("slot0.tmp", "half a fi");

            LoadOutcome load = rig.Service.Load(Content);
            Result save = rig.Service.Save(s, 0);

            Assert.AreEqual(LoadStatus.Loaded, load.Status, "tmp yok sayılır");
            Assert.IsTrue(save.IsSuccess);
            Assert.IsFalse(rig.Storage.Exists("slot0.tmp"));
        }

        [Test]
        public void AFailingTempCleanup_DoesNotHideTheOriginalSaveError()
        {
            var rig = new Rig();
            GameSession s = GameSession.NewGame(Content, 1UL);
            rig.Storage.FailOn("write", "slot0.tmp");
            rig.Storage.FailOn("delete", "slot0.tmp");

            Result r = rig.Service.Save(s, 0);

            Assert.AreEqual("save.io", r.ErrorCode, "temizlik hatası yutulur, asıl hata döner");
            Assert.IsNotNull(rig.Service.LastError);
        }

        [Test]
        public void AnUnreadableSlotZero_DoesNotStopTheNextSave_AndTheCreationTimeRestarts()
        {
            var rig = new Rig();
            GameSession s = GameSession.NewGame(Content, 1UL);
            rig.Service.Save(s, 0);
            rig.Clock.UtcNow = rig.Clock.UtcNow.AddDays(1);
            rig.Storage.FailOn("read", "slot0.json");

            Result r = rig.Service.Save(s, 0);

            Assert.IsTrue(r.IsSuccess, "createdAt okunamadı diye kayıt düşmez");
            rig.Storage.ClearFaults();
            Assert.AreEqual("2026-03-05T05:06:07Z", rig.Serializer.Parse(rig.Storage.Files["slot0.json"]).Value.Header.CreatedAtUtc);
        }

        [Test]
        public void IfACorruptFileCannotBeRenamed_ItStaysInPlace_AndTheBackupIsStillLoaded()
        {
            var rig = new Rig();
            GameSession s = GameSession.NewGame(Content, 1UL);
            rig.Service.Save(s, 0);
            rig.Service.Save(s, 0);
            rig.Storage.Put("slot0.json", "garbage");
            rig.Storage.FailOn("replace", "slot0.json>slot0.corrupt");

            LoadOutcome o = rig.Service.Load(Content);

            Assert.AreEqual(LoadStatus.LoadedFromBackup, o.Status);
            CollectionAssert.AreEqual(new[] { "slot0.json" }, o.CorruptFiles.ToArray(), "yeniden adlandırılamadı: dosya yerinde");
            Assert.AreEqual("garbage", rig.Storage.Files["slot0.json"]);
        }

        // ---------- yükleme ----------

        [Test]
        public void Load_WithoutAnyFile_IsNoSave()
        {
            var rig = new Rig();

            LoadOutcome o = rig.Service.Load(Content);

            Assert.AreEqual(LoadStatus.NoSave, o.Status);
            Assert.IsNull(o.Session);
            Assert.AreEqual("save.none", o.MessageKey);
        }

        [Test]
        public void Load_RestoresTheExactState_AndPublishesGameLoaded()
        {
            var rig = new Rig();
            GameSession original = Played(4UL, 200);
            rig.Service.Save(original, 10);
            var bus = new EventBus();
            var loaded = new List<string>();
            bus.Subscribe<GameLoaded>(e => loaded.Add(e.Day + ":" + e.FromBackup));

            LoadOutcome o = rig.Service.Load(Content, bus);

            Assert.AreEqual(LoadStatus.Loaded, o.Status);
            Assert.AreEqual("save.loaded", o.MessageKey);
            Assert.AreEqual(original.Api.GetStateDigest(), o.Session.Api.GetStateDigest());
            Assert.AreEqual(original.Api.GetDay(), o.Day);
            Assert.IsNull(o.BackupName);
            Assert.AreEqual(0, o.CorruptFiles.Count);
            CollectionAssert.AreEqual(new[] { original.Api.GetDay() + ":False" }, loaded);
            Assert.AreSame(bus, o.Session.Bus);
        }

        [Test]
        public void Load_ACorruptSlotZero_IsRenamed_AndTheFirstBackupIsLoaded()
        {
            var rig = new Rig();
            GameSession s = GameSession.NewGame(Content, 1UL);
            rig.Service.Save(s, 0);
            s.Api.EndDay();
            rig.Service.Save(s, 0);
            string good = rig.Storage.Files["slot0.bak1.json"];
            string broken = rig.Storage.Files["slot0.json"].Substring(0, 200);
            rig.Storage.Put("slot0.json", broken);

            LoadOutcome o = rig.Service.Load(Content);

            Assert.AreEqual(LoadStatus.LoadedFromBackup, o.Status);
            Assert.AreEqual("save.recovered_from_backup", o.MessageKey);
            Assert.AreEqual("slot0.bak1.json", o.BackupName);
            Assert.AreEqual(1, o.Day, "Son kayıt bozuktu, önceki kayıt yüklendi (Gün 1)");
            Assert.AreEqual(1, o.Session.Api.GetDay());
            Assert.IsFalse(rig.Storage.Exists("slot0.json"));
            Assert.AreEqual("slot0.corrupt-20260304-050607.json", o.CorruptFiles.Single());
            Assert.AreEqual(broken, rig.Storage.Files["slot0.corrupt-20260304-050607.json"], "bozuk dosya silinmez, saklanır");
            Assert.AreEqual(good, rig.Storage.Files["slot0.bak1.json"]);
        }

        [Test]
        public void Load_AWrongChecksum_FallsBackToo()
        {
            var rig = new Rig();
            GameSession s = GameSession.NewGame(Content, 1UL);
            rig.Service.Save(s, 0);
            s.Api.EndDay();
            rig.Service.Save(s, 0);
            rig.Storage.Put("slot0.json", rig.Storage.Files["slot0.json"].Replace("\"businessAssets\": 0,", "\"businessAssets\": 10,"));

            LoadOutcome o = rig.Service.Load(Content);

            Assert.AreEqual(LoadStatus.LoadedFromBackup, o.Status);
            Assert.AreEqual(1, o.Day);
        }

        [Test]
        public void Load_ASlotThatParsesButDoesNotRestore_FallsBackToo()
        {
            var rig = new Rig();
            GameSession s = GameSession.NewGame(Content, 1UL);
            rig.Service.Save(s, 0);
            rig.Service.Save(s, 0);
            s.Api.EndDay();
            // Yük geçerli JSON ve sağlaması doğru; ama durum tutarsız (defter bakiyesi bozuk).
            GameSnapshot bad = s.Capture();
            bad.Economy.Ledger[0].BalanceAfter += 10;
            rig.Storage.Put("slot0.json", rig.Serializer.Serialize(bad, new SaveMeta { AppVersion = "x", CreatedAtUtc = "a", SavedAtUtc = "b" }, new SavePreview()));

            LoadOutcome o = rig.Service.Load(Content);

            Assert.AreEqual(LoadStatus.LoadedFromBackup, o.Status);
            Assert.AreEqual(1, o.Day);
            Assert.AreEqual(1, o.CorruptFiles.Count);
        }

        [Test]
        public void Load_WhenTheFirstBackupIsCorruptToo_TheSecondIsUsed()
        {
            var rig = new Rig();
            GameSession s = GameSession.NewGame(Content, 1UL);
            for (int i = 0; i < 3; i++)
            {
                rig.Service.Save(s, 0);
                s.Api.EndDay();
            }

            rig.Storage.Put("slot0.json", "garbage");
            rig.Storage.Put("slot0.bak1.json", "{ also garbage");

            LoadOutcome o = rig.Service.Load(Content);

            Assert.AreEqual(LoadStatus.LoadedFromBackup, o.Status);
            Assert.AreEqual("slot0.bak2.json", o.BackupName);
            Assert.AreEqual(1, o.Day);
            Assert.AreEqual(2, o.CorruptFiles.Count);
            CollectionAssert.AreEquivalent(
                new[] { "slot0.bak2.json", "slot0.corrupt-20260304-050607.json", "slot0.corrupt-20260304-050607-2.json" },
                rig.Storage.List().ToArray());
        }

        [Test]
        public void Load_WhenEverythingIsCorrupt_ReportsCorrupt_AndKeepsTheFiles()
        {
            var rig = new Rig();
            rig.Storage.Put("slot0.json", "garbage 1");
            rig.Storage.Put("slot0.bak1.json", "garbage 2");
            rig.Storage.Put("slot0.bak2.json", "garbage 3");

            LoadOutcome o = rig.Service.Load(Content);

            Assert.AreEqual(LoadStatus.Corrupt, o.Status);
            Assert.AreEqual("save.corrupt", o.MessageKey);
            Assert.IsNull(o.Session);
            Assert.AreEqual(3, o.CorruptFiles.Count);
            Assert.AreEqual(3, rig.Storage.List().Count(n => n.Contains(".corrupt-")), "Bozuk dosyayı sakla");
            Assert.IsNotNull(o.Detail);
        }

        [Test]
        public void Load_WithoutSlotZeroButWithABackup_LoadsTheBackup()
        {
            var rig = new Rig();
            GameSession s = GameSession.NewGame(Content, 1UL);
            rig.Service.Save(s, 0);
            s.Api.EndDay();
            rig.Service.Save(s, 0);
            rig.Storage.Delete("slot0.json");

            LoadOutcome o = rig.Service.Load(Content);

            Assert.AreEqual(LoadStatus.LoadedFromBackup, o.Status);
            Assert.AreEqual("slot0.bak1.json", o.BackupName);
            Assert.AreEqual(0, o.CorruptFiles.Count);
        }

        [Test]
        public void Load_ANewerSaveVersion_IsRefused_WithoutTouchingAnyFile()
        {
            var rig = new Rig();
            GameSession s = GameSession.NewGame(Content, 1UL);
            rig.Service.Save(s, 0);
            rig.Service.Save(s, 0);
            string newer = rig.Storage.Files["slot0.json"].Replace("\"saveVersion\": 1", "\"saveVersion\": 99");
            rig.Storage.Put("slot0.json", newer);
            var snapshot = rig.Storage.Files.ToDictionary(p => p.Key, p => p.Value);

            LoadOutcome o = rig.Service.Load(Content);

            Assert.AreEqual(LoadStatus.NewerVersion, o.Status);
            Assert.AreEqual("save.newer_version", o.MessageKey);
            Assert.IsNull(o.Session);
            CollectionAssert.AreEquivalent(snapshot, rig.Storage.Files.ToDictionary(p => p.Key, p => p.Value));
        }

        [Test]
        public void Load_AnUnsupportedOldVersion_IsRefused_WithoutTouchingAnyFile()
        {
            var rig = new Rig();
            GameSession s = GameSession.NewGame(Content, 1UL);
            rig.Service.Save(s, 0);
            rig.Storage.Put("slot0.json", rig.Storage.Files["slot0.json"].Replace("\"saveVersion\": 1", "\"saveVersion\": 0"));
            var snapshot = rig.Storage.Files.ToDictionary(p => p.Key, p => p.Value);

            LoadOutcome o = rig.Service.Load(Content);

            Assert.AreEqual(LoadStatus.UnsupportedVersion, o.Status);
            Assert.AreEqual("save.unsupported_version", o.MessageKey);
            CollectionAssert.AreEquivalent(snapshot, rig.Storage.Files.ToDictionary(p => p.Key, p => p.Value));
        }

        [Test]
        public void Load_AnIoErrorWhileReading_CountsAsCorrupt_ButTheFileIsKept()
        {
            var rig = new Rig();
            GameSession s = GameSession.NewGame(Content, 1UL);
            rig.Service.Save(s, 0);
            rig.Service.Save(s, 0);
            rig.Storage.FailOn("read", "slot0.json");

            LoadOutcome o = rig.Service.Load(Content);

            Assert.AreEqual(LoadStatus.LoadedFromBackup, o.Status, "okunamayan slot0 için yedeğe düşülür");
        }

        // ---------- yeniden başlayınca aynı oyun devam eder ----------

        [TestCase(21UL)]
        [TestCase(22UL)]
        [TestCase(23UL)]
        [TestCase(24UL)]
        public void SaveAndLoad_ThenPlayOn_GivesTheSameGameAsNeverRestarting(ulong seed)
        {
            var rig = new Rig();
            GameSession original = Played(seed, 160);
            Assert.IsTrue(rig.Service.Save(original, 0).IsSuccess);
            GameSession reloaded = rig.Service.Load(Content).Session;
            var a = new Random(77 + (int)seed);
            var b = new Random(77 + (int)seed);

            for (int i = 0; i < 250; i++)
            {
                SessionDriver.Step(original, a);
                SessionDriver.Step(reloaded, b);
                Assert.AreEqual(original.Api.GetStateDigest(), reloaded.Api.GetStateDigest(), "komut #" + i);
            }
        }

        [Test]
        public void SaveAndLoad_ManyTimesInARow_NeverDrifts()
        {
            var rig = new Rig();
            GameSession s = Played(31UL, 120);
            string digest = s.Api.GetStateDigest();
            for (int i = 0; i < 6; i++)
            {
                Assert.IsTrue(rig.Service.Save(s, 0).IsSuccess);
                s = rig.Service.Load(Content).Session;
                Assert.AreEqual(digest, s.Api.GetStateDigest(), "tur " + i);
            }
        }

        // ---------- önizleme ve silme ----------

        [Test]
        public void ReadPreview_ReturnsTheHeaderSummary_WithoutLoading()
        {
            var rig = new Rig();
            GameSession s = Played(5UL, 100);
            rig.Service.Save(s, 0);

            SavePreview p = rig.Service.ReadPreview();

            Assert.AreEqual(s.Api.GetDay(), p.Day);
            Assert.AreEqual(s.Api.GetCash().Tl, p.Cash);
            Assert.AreEqual(s.Wealth.Calculate().Total.Tl, p.Wealth);
        }

        [Test]
        public void ReadPreview_IsNullWithoutASaveOrWithAnUnreadableHeader()
        {
            var rig = new Rig();
            Assert.IsNull(rig.Service.ReadPreview());

            rig.Storage.Put("slot0.json", "garbage");
            Assert.IsNull(rig.Service.ReadPreview());

            rig.Storage.Put("slot0.json", "{}");
            Assert.IsNull(rig.Service.ReadPreview());

            rig.Storage.ClearFaults();
            rig.Service.Save(GameSession.NewGame(Content, 1UL), 0);
            rig.Storage.FailOn("read", "slot0.json");
            Assert.IsNull(rig.Service.ReadPreview(), "G/Ç hatası önizlemeyi düşürmez");
        }

        [Test]
        public void Delete_RemovesTheSlotAndItsBackups_ButKeepsCorruptFiles()
        {
            var rig = new Rig();
            GameSession s = GameSession.NewGame(Content, 1UL);
            for (int i = 0; i < 3; i++)
            {
                rig.Service.Save(s, 0);
            }

            rig.Storage.Put("slot0.tmp", "x");
            rig.Storage.Put("slot0.corrupt-20260101-000000.json", "old corrupt");

            Result r = rig.Service.Delete();

            Assert.IsTrue(r.IsSuccess);
            CollectionAssert.AreEqual(new[] { "slot0.corrupt-20260101-000000.json" }, rig.Storage.List().ToArray());
            Assert.AreEqual(LoadStatus.NoSave, rig.Service.Load(Content).Status);
        }

        [Test]
        public void Delete_ReportsIoErrors()
        {
            var rig = new Rig();
            rig.Service.Save(GameSession.NewGame(Content, 1UL), 0);
            rig.Storage.FailOn("delete", "slot0.bak1.json");

            Assert.AreEqual("save.io", rig.Service.Delete().ErrorCode);
        }

        [Test]
        public void Constructor_AndArguments_AreChecked()
        {
            var rig = new Rig();
            Assert.Throws<ArgumentNullException>(() => new SaveService(null, rig.Serializer, rig.Clock, "v"));
            Assert.Throws<ArgumentNullException>(() => new SaveService(rig.Storage, null, rig.Clock, "v"));
            Assert.Throws<ArgumentNullException>(() => new SaveService(rig.Storage, rig.Serializer, null, "v"));
            Assert.Throws<ArgumentNullException>(() => rig.Service.Save(null, 0));
            Assert.Throws<ArgumentNullException>(() => rig.Service.Load(null));
            Assert.IsTrue(new SaveService(rig.Storage, rig.Serializer, rig.Clock, null).Save(GameSession.NewGame(Content, 1UL), 0).IsSuccess);
        }
    }
}
