using System;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Game;
using Esnaf.Persistence;
using Esnaf.Tests.Support;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Esnaf.Tests.Game
{
    /// <summary>
    /// Aksesuar stoğu kaydı (Day 11.2.3): adet ve maliyet tabanı kayıttan sonra aynı kalır; eski kayıtta bölüm yoksa stok boştur;
    /// boş stokta bölüm YAZILMAZ (aksesuarsız kayıtlar eskisiyle aynı metin); özet ve defter gidiş-dönüşte değişmez.
    /// </summary>
    public class AccessorySaveLoadTests
    {
        private const string Supplier = "supplier.ucuz_toptan";
        private const string Adapter = "accessory.charger_adapter";
        private const string Case = "accessory.phone_case";
        private const string Powerbank = "accessory.powerbank";

        private static readonly SaveMeta Meta = new SaveMeta
        {
            AppVersion = "0.1.0",
            ContentSchemaVersion = 1,
            CreatedAtUtc = "2026-01-02T03:04:05Z",
            SavedAtUtc = "2026-01-02T04:05:06Z",
            PlayTimeSeconds = 1
        };

        private static readonly SavePreview Preview = new SavePreview { Day = 1, Cash = 1, Wealth = 1 };

        private static GameSession New(ulong seed = 3UL)
        {
            return GameSession.NewGame(MarketHarness.RealContent(), seed);
        }

        private static GameSession Stocked()
        {
            GameSession s = New();
            Assert.IsTrue(s.WholesaleService.BuyPack(Supplier, Adapter, 1).IsSuccess);
            Assert.IsTrue(s.WholesaleService.BuyPack(Supplier, Case, 1).IsSuccess);
            Assert.IsTrue(s.WholesaleService.BuyPack(Supplier, Case, 1).IsSuccess);
            Assert.IsTrue(s.WholesaleService.BuyPack(Supplier, Powerbank, 3).IsSuccess);
            return s;
        }

        // Gerçek yazma + okuma yolu (metin, sağlama, tipli nesne) üzerinden gidiş-dönüş.
        private static GameSession ThroughText(GameSession s, out string text)
        {
            var serializer = new SaveSerializer();
            text = serializer.Serialize(s.Capture(), Meta, Preview);
            Result<ParsedSave> parsed = serializer.Parse(text);
            Assert.IsTrue(parsed.IsSuccess, parsed.ErrorCode + ": " + parsed.Message);
            Result<GameSession> restored = GameSession.Restore(MarketHarness.RealContent(), parsed.Value.Snapshot);
            Assert.IsTrue(restored.IsSuccess, restored.ErrorCode + ": " + restored.Message);
            return restored.Value;
        }

        [Test]
        public void AnEmptyStock_RoundTrips_AndWritesNoAccessoriesSection()
        {
            GameSession s = New();
            string text;

            GameSession back = ThroughText(s, out text);

            Assert.AreEqual(0, back.AccessoryStock.TotalUnits);
            Assert.IsNull(s.Capture().Accessories, "boş stok yazılmaz");
            Assert.IsFalse(((JObject)JObject.Parse(text)["payload"]).ContainsKey("accessories"), "kayıt metninde bölüm yok");
            Assert.AreEqual(s.Api.GetStateDigest(), back.Api.GetStateDigest());
        }

        [Test]
        public void AFilledStock_KeepsQuantityAndTotalCost_ThroughSaveAndLoad()
        {
            GameSession s = Stocked();
            string text;

            GameSession back = ThroughText(s, out text);

            Assert.AreEqual(10, back.AccessoryStock.Quantity(Adapter));
            Assert.AreEqual(Money.FromTl(1500), back.AccessoryStock.TotalCost(Adapter));
            Assert.AreEqual(40, back.AccessoryStock.Quantity(Case));
            Assert.AreEqual(Money.FromTl(2800), back.AccessoryStock.TotalCost(Case));
            Assert.AreEqual(5, back.AccessoryStock.Quantity(Powerbank));
            Assert.AreEqual(Money.FromTl(1750), back.AccessoryStock.TotalCost(Powerbank));
            Assert.AreEqual(55, back.AccessoryStock.TotalUnits);
            Assert.AreEqual(s.AccessoryStock.StockCost, back.AccessoryStock.StockCost);
            CollectionAssert.AreEqual(s.AccessoryStock.AccessoryIds, back.AccessoryStock.AccessoryIds);
            StringAssert.Contains("accessories", text);
        }

        [Test]
        public void TheDigest_RoundTrips_AndSeesTheStock()
        {
            GameSession s = Stocked();
            string before = New().Api.GetStateDigest();
            string text;

            GameSession back = ThroughText(s, out text);

            Assert.AreEqual(s.Api.GetStateDigest(), back.Api.GetStateDigest());
            Assert.AreNotEqual(before, s.Api.GetStateDigest());
            string d1 = s.Api.GetStateDigest();
            s.AccessoryStock.Remove(Case, 1);
            Assert.AreNotEqual(d1, s.Api.GetStateDigest(), "stok özeti değiştirir");
        }

        [Test]
        public void TheLedgerAndCash_AreUnchangedByTheRoundTrip()
        {
            GameSession s = Stocked();
            string text;

            GameSession back = ThroughText(s, out text);

            Assert.AreEqual(s.EconomyService.Cash, back.EconomyService.Cash);
            Assert.AreEqual(s.EconomyState.Ledger.Count, back.EconomyState.Ledger.Count);
            Assert.AreEqual(s.EconomyState.Ledger.Balance, back.EconomyState.Ledger.Balance);
            Assert.AreEqual(4, back.EconomyState.Ledger.Records.Count(r => r.TypeId == TransactionTypeIds.WholesalePurchase));
            Assert.IsNull(DeepCompare.FirstDifference(s.Capture().Economy, back.Capture().Economy));
            Assert.IsTrue(back.EconomyState.Ledger.Verify().IsSuccess);
            Assert.AreEqual(s.Wealth.Calculate().Total, back.Wealth.Calculate().Total);
            Assert.AreEqual(s.Wealth.Calculate().GetAmount(WealthKeys.AccessoryStock), back.Wealth.Calculate().GetAmount(WealthKeys.AccessoryStock));
        }

        [Test]
        public void ARestoredGame_KeepsBuyingAndSelling_FromTheRestoredStock()
        {
            GameSession s = Stocked();
            string text;
            GameSession back = ThroughText(s, out text);

            Assert.AreEqual("stock.full", back.WholesaleService.BuyPack(Supplier, Case, 3).ErrorCode, "55 birim dolu: 20'lik paket s\u0131\u011Fmaz");
            Assert.AreEqual(Money.FromTl(700), back.AccessoryStock.Remove(Case, 10).Value, "maliyet taban\u0131 kay\u0131ttan sonra da orant\u0131l\u0131 (2.800 / 40 \u00D7 10)");
            Assert.AreEqual(Money.FromTl(2100), back.AccessoryStock.TotalCost(Case));
            Assert.IsTrue(back.AccessoryStock.Remove(Case, 20).IsSuccess);
            var again = back.WholesaleService.BuyPack(Supplier, Adapter, 3); // defterin son g\u00FCn\u00FC 3 (powerbank)
            Assert.IsTrue(again.IsSuccess, again.ErrorCode + " yer a\u00E7\u0131l\u0131nca al\u0131\u015F devam eder");
            Assert.AreEqual(20, back.AccessoryStock.Quantity(Adapter));
        }

        [Test]
        public void ASaveWithoutTheAccessoriesSection_LoadsAnEmptyStock()
        {
            GameSession s = New();
            GameSnapshot snap = s.Capture();
            snap.Accessories = null; // eski kayıt

            Result<GameSession> restored = GameSession.Restore(MarketHarness.RealContent(), snap);

            Assert.IsTrue(restored.IsSuccess, restored.ErrorCode);
            Assert.AreEqual(0, restored.Value.AccessoryStock.TotalUnits);
            Assert.AreEqual(s.Api.GetStateDigest(), restored.Value.Api.GetStateDigest());
        }

        [Test]
        public void TheOldFixtureSave_StillLoads_WithAnEmptyAccessoryStock()
        {
            string text = System.IO.File.ReadAllText(System.IO.Path.Combine(TestPaths.FixturesDirectory(), "save_v1.json"));
            Result<ParsedSave> parsed = new SaveSerializer().Parse(text);
            Assert.IsTrue(parsed.IsSuccess);

            Result<GameSession> restored = GameSession.Restore(MarketHarness.RealContent(), parsed.Value.Snapshot);

            Assert.IsTrue(restored.IsSuccess, restored.ErrorCode + ": " + restored.Message);
            Assert.AreEqual(0, restored.Value.AccessoryStock.TotalUnits);
            Assert.AreEqual("de4eb924fea4491c", restored.Value.Api.GetStateDigest());
        }

        [Test]
        public void ASaveWithoutAccessories_IsByteIdentical_ToWhatTheOldCodeWrote()
        {
            // Alan yazılmadığı için aksesuarsız kaydın metni ve sağlaması değişmez: fikstürü yeniden yazınca aynı metin çıkar.
            string fixture = System.IO.File.ReadAllText(System.IO.Path.Combine(TestPaths.FixturesDirectory(), "save_v1.json"));
            var serializer = new SaveSerializer();
            ParsedSave parsed = serializer.Parse(fixture).Value;

            string again = serializer.Serialize(parsed.Snapshot, new SaveMeta
            {
                AppVersion = parsed.Header.AppVersion,
                ContentSchemaVersion = parsed.Header.ContentSchemaVersion,
                CreatedAtUtc = parsed.Header.CreatedAtUtc,
                SavedAtUtc = parsed.Header.SavedAtUtc,
                PlayTimeSeconds = parsed.Header.PlayTimeSeconds
            }, parsed.Header.Preview);

            Assert.AreEqual(((JObject)JObject.Parse(fixture)["header"])["checksum"].ToString(), ((JObject)JObject.Parse(again)["header"])["checksum"].ToString());
        }

        [Test]
        public void SavingThroughTheSaveService_AndLoading_KeepsTheStock()
        {
            var storage = new InMemorySaveStorage();
            var service = new SaveService(storage, new SaveSerializer(), new FakeSaveClock(new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc)), "0.1.0");
            GameSession s = Stocked();

            Assert.IsTrue(service.Save(s, 10).IsSuccess);
            LoadOutcome outcome = service.Load(MarketHarness.RealContent());

            Assert.AreEqual(LoadStatus.Loaded, outcome.Status);
            Assert.AreEqual(55, outcome.Session.AccessoryStock.TotalUnits);
            Assert.AreEqual(s.Api.GetStateDigest(), outcome.Session.Api.GetStateDigest());
        }

        // ---------- geçersiz kayıt ----------

        private static void AssertInvalid(Action<GameSnapshot> corrupt, string text)
        {
            GameSnapshot snap = Stocked().Capture();
            corrupt(snap);

            Result<GameSession> r = GameSession.Restore(MarketHarness.RealContent(), snap);

            Assert.IsTrue(r.IsFailure, text);
            Assert.AreEqual("save.invalid", r.ErrorCode);
            StringAssert.Contains(text, r.Message);
        }

        [Test]
        public void ABrokenAccessoriesSection_IsRefused_AsAnInvalidSave()
        {
            AssertInvalid(s => s.Accessories.Stock = null, "accessories.stock");
            AssertInvalid(s => s.Accessories.Stock[0].AccessoryId = "accessory.ghost", "not in the content");
            AssertInvalid(s => s.Accessories.Stock[0].AccessoryId = string.Empty, "empty");
            AssertInvalid(s => s.Accessories.Stock[1].AccessoryId = s.Accessories.Stock[0].AccessoryId, "duplicate");
            AssertInvalid(s => s.Accessories.Stock[0].Quantity = 0, "quantity");
            AssertInvalid(s => s.Accessories.Stock[0].Quantity = -3, "quantity");
            AssertInvalid(s => s.Accessories.Stock[0].TotalCost = -10, "totalCost");
            AssertInvalid(s => s.Accessories.Stock[0].Quantity = 61, "stock.full");
        }
    }
}
