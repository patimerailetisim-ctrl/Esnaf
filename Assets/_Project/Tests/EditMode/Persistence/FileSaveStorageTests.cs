using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Esnaf.Domain.Game;
using Esnaf.Persistence;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Persistence
{
    /// <summary>Gerçek dosya sistemi karşılığı (geçici klasörde).</summary>
    public class FileSaveStorageTests
    {
        private static int _counter;
        private string _dir;
        private FileSaveStorage _storage;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "esnaf-save-tests-" + Process.GetCurrentProcess().Id + "-" + Interlocked.Increment(ref _counter));
            _storage = new FileSaveStorage(Path.Combine(_dir, "saves"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, true);
            }
        }

        [Test]
        public void TheDirectory_IsCreatedOnTheFirstWrite_NotBefore()
        {
            Assert.IsFalse(Directory.Exists(Path.Combine(_dir, "saves")));
            Assert.IsFalse(_storage.Exists("slot0.json"));
            Assert.AreEqual(0, _storage.List().Count);

            _storage.WriteAllText("slot0.json", "x");

            Assert.IsTrue(Directory.Exists(Path.Combine(_dir, "saves")));
        }

        [Test]
        public void WriteThenRead_RoundTripsUtf8_WithoutABom()
        {
            string text = "Ayşe Hanım — İstanbul ğüşöç ₺";

            _storage.WriteAllText("a.json", text);

            Assert.AreEqual(text, _storage.ReadAllText("a.json"));
            byte[] bytes = File.ReadAllBytes(Path.Combine(_dir, "saves", "a.json"));
            Assert.AreEqual(Encoding.UTF8.GetBytes(text).Length, bytes.Length, "BOM yok");
            Assert.IsTrue(_storage.Exists("a.json"));
        }

        [Test]
        public void Write_ReplacesAnExistingFile()
        {
            _storage.WriteAllText("a.json", "a long first content");
            _storage.WriteAllText("a.json", "short");

            Assert.AreEqual("short", _storage.ReadAllText("a.json"));
        }

        [Test]
        public void Copy_OverwritesTheTarget_AndKeepsTheSource()
        {
            _storage.WriteAllText("a.json", "A");
            _storage.WriteAllText("b.json", "B");

            _storage.Copy("a.json", "b.json");

            Assert.AreEqual("A", _storage.ReadAllText("a.json"));
            Assert.AreEqual("A", _storage.ReadAllText("b.json"));
        }

        [Test]
        public void Replace_MovesOverAnExistingTarget()
        {
            _storage.WriteAllText("tmp", "NEW");
            _storage.WriteAllText("slot", "OLD");

            _storage.Replace("tmp", "slot");

            Assert.AreEqual("NEW", _storage.ReadAllText("slot"));
            Assert.IsFalse(_storage.Exists("tmp"));
        }

        [Test]
        public void Replace_MovesToAMissingTarget()
        {
            _storage.WriteAllText("tmp", "NEW");

            _storage.Replace("tmp", "slot");

            Assert.AreEqual("NEW", _storage.ReadAllText("slot"));
            Assert.IsFalse(_storage.Exists("tmp"));
        }

        [Test]
        public void Delete_IsSilentForAMissingFile_AndRemovesAnExistingOne()
        {
            _storage.Delete("nothing");
            _storage.WriteAllText("a.json", "A");

            _storage.Delete("a.json");

            Assert.IsFalse(_storage.Exists("a.json"));
        }

        [Test]
        public void List_ReturnsSortedFileNamesOnly()
        {
            _storage.WriteAllText("b.json", "B");
            _storage.WriteAllText("a.json", "A");
            Directory.CreateDirectory(Path.Combine(_dir, "saves", "subdir"));

            CollectionAssert.AreEqual(new[] { "a.json", "b.json" }, _storage.List().ToArray());
        }

        [Test]
        public void Reading_AMissingFile_Throws()
        {
            _storage.WriteAllText("a.json", "A");

            Assert.Throws<FileNotFoundException>(() => _storage.ReadAllText("missing.json"));
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("../evil.json")]
        [TestCase("sub/evil.json")]
        [TestCase("sub\\evil.json")]
        [TestCase(".")]
        [TestCase("..")]
        public void UnsafeNames_AreRejected(string name)
        {
            Assert.Throws<ArgumentException>(() => _storage.Exists(name));
            Assert.Throws<ArgumentException>(() => _storage.WriteAllText(name, "x"));
            Assert.Throws<ArgumentException>(() => _storage.Copy(name, "ok.json"));
            Assert.Throws<ArgumentException>(() => _storage.Replace("ok.json", name));
        }

        [Test]
        public void Constructor_NeedsADirectory_AndWriteNeedsText()
        {
            Assert.Throws<ArgumentException>(() => new FileSaveStorage(""));
            Assert.Throws<ArgumentException>(() => new FileSaveStorage("  "));
            Assert.Throws<ArgumentNullException>(() => _storage.WriteAllText("a.json", null));
        }

        [Test]
        public void TheWholeService_WorksOnTheRealFileSystem()
        {
            var clock = new FakeSaveClock(new DateTime(2026, 5, 6, 7, 8, 9, DateTimeKind.Utc));
            var service = new SaveService(_storage, new SaveSerializer(), clock, "0.1.0");
            GameSession original = GameSession.NewGame(MarketHarness.RealContent(), 8UL);
            SessionDriver.Run(original, new Random(5), 150);

            Assert.IsTrue(service.Save(original, 12).IsSuccess);
            Assert.IsTrue(service.Save(original, 13).IsSuccess);
            File.WriteAllText(Path.Combine(_dir, "saves", "slot0.json"), "{ broken");
            LoadOutcome o = service.Load(MarketHarness.RealContent());

            Assert.AreEqual(LoadStatus.LoadedFromBackup, o.Status);
            Assert.AreEqual(original.Api.GetStateDigest(), o.Session.Api.GetStateDigest());
            CollectionAssert.AreEquivalent(
                new[] { "slot0.bak1.json", "slot0.corrupt-20260506-070809.json" },
                _storage.List().Where(n => n != "slot0.json").ToArray());
        }
    }
}
