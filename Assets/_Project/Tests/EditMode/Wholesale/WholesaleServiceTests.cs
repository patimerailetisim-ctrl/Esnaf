using System;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Accessories;
using Esnaf.Domain.Content;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Game;
using Esnaf.Domain.Wholesale;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Wholesale
{
    /// <summary>
    /// Toptancı satın alma (WholesaleService) gerçek içerik ve gerçek oturum üstünde: gün kuralı, tutar, nakit, stok, defter, servet ve ATOMİKLİK.
    /// Gerçek içerik: Ucuz Toptan; adaptör 150 ₺ × 10, kablo 60 × 20, kulaklık 200 × 10, cam 40 × 20, kılıf 70 × 20, powerbank 350 × 5 (3. günden).
    /// </summary>
    public class WholesaleServiceTests
    {
        private const string Supplier = "supplier.ucuz_toptan";
        private const string Adapter = "accessory.charger_adapter";   // 150 × 10 = 1.500
        private const string Case = "accessory.phone_case";            // 70 × 20 = 1.400
        private const string Powerbank = "accessory.powerbank";        // 350 × 5 = 1.750, 3. gün

        private static GameSession NewSession()
        {
            return GameSession.NewGame(MarketHarness.RealContent(), 1UL);
        }

        private sealed class Snapshot
        {
            public Money Cash;
            public int LedgerCount;
            public Money LedgerBalance;
            public int Units;
            public Money StockCost;
            public Money Wealth;
            public string Digest;

            public static Snapshot Of(GameSession s)
            {
                return new Snapshot
                {
                    Cash = s.EconomyService.Cash,
                    LedgerCount = s.EconomyState.Ledger.Count,
                    LedgerBalance = s.EconomyState.Ledger.Balance,
                    Units = s.AccessoryStock.TotalUnits,
                    StockCost = s.AccessoryStock.StockCost,
                    Wealth = s.Wealth.Calculate().Total,
                    Digest = s.Api.GetStateDigest()
                };
            }
        }

        private static void AssertUnchanged(Snapshot before, GameSession s)
        {
            Snapshot after = Snapshot.Of(s);
            Assert.AreEqual(before.Cash, after.Cash, "nakit");
            Assert.AreEqual(before.LedgerCount, after.LedgerCount, "defter satır sayısı");
            Assert.AreEqual(before.LedgerBalance, after.LedgerBalance, "defter bakiyesi");
            Assert.AreEqual(before.Units, after.Units, "stok birimi");
            Assert.AreEqual(before.StockCost, after.StockCost, "stok maliyeti");
            Assert.AreEqual(before.Wealth, after.Wealth, "servet");
            Assert.AreEqual(before.Digest, after.Digest, "durum özeti");
        }

        // ---------- gün kuralı ----------

        [Test]
        public void ABuy_BeforeTheOfferOpens_IsRefused_AndNothingChanges()
        {
            GameSession s = NewSession();
            Snapshot before = Snapshot.Of(s);

            Result<WholesalePurchaseReceipt> r = s.WholesaleService.BuyPack(Supplier, Powerbank, 2);

            Assert.IsTrue(r.IsFailure);
            Assert.AreEqual("wholesale.not_available_yet", r.ErrorCode);
            AssertUnchanged(before, s);
        }

        [Test]
        public void ABuy_OnTheOpeningDay_Succeeds_AndAfterIt()
        {
            GameSession s = NewSession();

            Assert.IsTrue(s.WholesaleService.BuyPack(Supplier, Adapter, 1).IsSuccess, "1. günden açık teklif");
            Assert.IsTrue(s.WholesaleService.BuyPack(Supplier, Powerbank, 3).IsSuccess);
            Assert.IsTrue(s.WholesaleService.BuyPack(Supplier, Powerbank, 9).IsSuccess);
        }

        [Test]
        public void OffersOn_ListsOnlyTheOpenOffers()
        {
            GameSession s = NewSession();

            Assert.AreEqual(5, s.WholesaleService.OffersOn(1).Count);
            Assert.AreEqual(6, s.WholesaleService.OffersOn(3).Count);
        }

        // ---------- tutar, para, stok ----------

        [Test]
        public void ASuccessfulBuy_BooksUnitCostTimesPackSize_Everywhere()
        {
            GameSession s = NewSession();
            Money cashBefore = s.EconomyService.Cash;

            Result<WholesalePurchaseReceipt> r = s.WholesaleService.BuyPack(Supplier, Adapter, 1);

            Assert.IsTrue(r.IsSuccess, r.ErrorCode);
            Assert.AreEqual(Supplier, r.Value.SupplierId);
            Assert.AreEqual(Adapter, r.Value.AccessoryId);
            Assert.AreEqual(10, r.Value.Quantity, "miktar = packSize");
            Assert.AreEqual(Money.FromTl(150), r.Value.UnitCost);
            Assert.AreEqual(Money.FromTl(1500), r.Value.TotalCost, "150 × 10");
            Assert.AreEqual(cashBefore - Money.FromTl(1500), s.EconomyService.Cash, "para toplam maliyet kadar azalır");
            Assert.AreEqual(10, s.AccessoryStock.Quantity(Adapter), "stok packSize kadar artar");
            Assert.AreEqual(Money.FromTl(1500), s.AccessoryStock.TotalCost(Adapter), "stok maliyeti");
            Assert.AreEqual(10, s.AccessoryStock.TotalUnits);
        }

        [TestCase("accessory.charger_adapter", 1500L, 10)]
        [TestCase("accessory.charge_cable", 1200L, 20)]
        [TestCase("accessory.earphones", 2000L, 10)]
        [TestCase("accessory.screen_protector", 800L, 20)]
        [TestCase("accessory.phone_case", 1400L, 20)]
        public void EveryOffer_CostsUnitCostTimesPack(string accessoryId, long totalTl, int pack)
        {
            GameSession s = NewSession();

            Result<WholesalePurchaseReceipt> r = s.WholesaleService.BuyPack(Supplier, accessoryId, 1);

            Assert.IsTrue(r.IsSuccess, r.ErrorCode);
            Assert.AreEqual(Money.FromTl(totalTl), r.Value.TotalCost);
            Assert.AreEqual(pack, s.AccessoryStock.Quantity(accessoryId));
            Assert.AreEqual(Money.FromTl(totalTl), s.AccessoryStock.TotalCost(accessoryId));
        }

        [Test]
        public void TheSameOffer_CanBeBoughtAgain_WhileCashAndCapacityAllow()
        {
            GameSession s = NewSession();
            Money cashBefore = s.EconomyService.Cash;

            Assert.IsTrue(s.WholesaleService.BuyPack(Supplier, Case, 1).IsSuccess);
            Assert.IsTrue(s.WholesaleService.BuyPack(Supplier, Case, 1).IsSuccess);

            Assert.AreEqual(40, s.AccessoryStock.Quantity(Case));
            Assert.AreEqual(Money.FromTl(2800), s.AccessoryStock.TotalCost(Case));
            Assert.AreEqual(cashBefore - Money.FromTl(2800), s.EconomyService.Cash);
            Assert.AreEqual(2, s.EconomyState.Ledger.Records.Count(x => x.TypeId == "wholesale_purchase"));
        }

        // ---------- defter ----------

        [Test]
        public void ASuccessfulBuy_WritesExactlyOneLedgerRow_OfTheWholesaleType()
        {
            GameSession s = NewSession();
            int rows = s.EconomyState.Ledger.Count;

            Result<WholesalePurchaseReceipt> r = s.WholesaleService.BuyPack(Supplier, Adapter, 1);

            Assert.AreEqual(rows + 1, s.EconomyState.Ledger.Count, "tek defter kaydı");
            TransactionRecord row;
            Assert.IsTrue(s.EconomyState.Ledger.TryGetById(r.Value.LedgerRecordId, out row));
            Assert.AreEqual(TransactionTypeIds.WholesalePurchase, row.TypeId);
            Assert.AreEqual(TransactionCategory.Trade, row.Category);
            Assert.AreEqual(Money.FromTl(-1500), row.Amount, "çıkış: negatif");
            Assert.AreEqual(s.EconomyService.Cash, row.BalanceAfter);
            Assert.AreEqual(Adapter, row.DefinitionId);
            Assert.AreEqual(Supplier, row.NpcId);
            Assert.AreEqual("10", row.MemoArgs.Single());
            Assert.IsNull(row.InstanceId, "telefon ürünü değil");
            Assert.AreEqual(1, row.Day);
        }

        [Test]
        public void TheLedgerRow_HasNoProfitEffect_TheCostGoesToStock()
        {
            GameSession s = NewSession();
            Money wealthBefore = s.Wealth.Calculate().Total;
            TransactionType type = s.Content.TransactionTypes.Get(TransactionTypeIds.WholesalePurchase);

            s.WholesaleService.BuyPack(Supplier, Adapter, 1);

            Assert.AreEqual(ProfitEffect.None, type.ProfitEffect);
            Assert.AreEqual(wealthBefore, s.Wealth.Calculate().Total, "nakitten stoğa geçiş: servet değişmez");
        }

        [Test]
        public void TheLedgerBalance_MatchesCash_AfterSeveralBuys()
        {
            GameSession s = NewSession();

            s.WholesaleService.BuyPack(Supplier, Adapter, 1);
            s.WholesaleService.BuyPack(Supplier, Case, 1);
            s.WholesaleService.BuyPack(Supplier, Powerbank, 3);

            Assert.AreEqual(s.EconomyService.Cash, s.EconomyState.Ledger.Balance);
        }

        // ---------- servet ----------

        [Test]
        public void TheAccessoryStock_EntersWealthAtCost_UnderItsOwnKey()
        {
            GameSession s = NewSession();
            WealthBreakdown before = s.Wealth.Calculate();

            s.WholesaleService.BuyPack(Supplier, Adapter, 1);
            WealthBreakdown after = s.Wealth.Calculate();

            Assert.AreEqual(Money.Zero, before.GetAmount(WealthKeys.AccessoryStock));
            Assert.AreEqual(Money.FromTl(1500), after.GetAmount(WealthKeys.AccessoryStock));
            Assert.AreEqual(before.GetAmount(WealthKeys.Cash) - Money.FromTl(1500), after.GetAmount(WealthKeys.Cash));
            Assert.AreEqual(before.GetAmount(WealthKeys.Stock), after.GetAmount(WealthKeys.Stock), "telefon stoku değişmez");
        }

        // ---------- atomiklik ----------

        [Test]
        public void NotEnoughCash_FailsAndChangesNothing()
        {
            GameSession s = GameSession.NewGame(ContentVariants.WithOpeningCapital(1000), 1UL);
            Snapshot before = Snapshot.Of(s);

            Result<WholesalePurchaseReceipt> r = s.WholesaleService.BuyPack(Supplier, Adapter, 1); // 1.500 > 1.000

            Assert.AreEqual("cash.insufficient", r.ErrorCode);
            AssertUnchanged(before, s);
            Assert.AreEqual(0, s.AccessoryStock.Quantity(Adapter));
        }

        [Test]
        public void NotEnoughCash_ToTheLastTen_StillFails_AndExactCashSucceeds()
        {
            GameSession exact = GameSession.NewGame(ContentVariants.WithOpeningCapital(1500), 1UL);
            GameSession short1 = GameSession.NewGame(ContentVariants.WithOpeningCapital(1490), 1UL);
            Snapshot before = Snapshot.Of(short1);

            Assert.AreEqual("cash.insufficient", short1.WholesaleService.BuyPack(Supplier, Adapter, 1).ErrorCode);
            AssertUnchanged(before, short1);
            Assert.IsTrue(exact.WholesaleService.BuyPack(Supplier, Adapter, 1).IsSuccess);
            Assert.AreEqual(Money.Zero, exact.EconomyService.Cash);
        }

        [Test]
        public void NotEnoughStockCapacity_FailsAndChangesNothing()
        {
            GameSession s = NewSession();
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(s.WholesaleService.BuyPack(Supplier, Case, 1).IsSuccess); // 3 × 20 = 60: raf dolu
            }

            Snapshot before = Snapshot.Of(s);

            Result<WholesalePurchaseReceipt> r = s.WholesaleService.BuyPack(Supplier, Adapter, 1);

            Assert.AreEqual("stock.full", r.ErrorCode);
            AssertUnchanged(before, s);
            Assert.AreEqual(60, s.AccessoryStock.TotalUnits);
        }

        [Test]
        public void ACapacityShortByOneUnit_Fails_AndExactlyFullSucceeds()
        {
            GameSession s = NewSession();
            s.WholesaleService.BuyPack(Supplier, Case, 1);              // 20
            s.WholesaleService.BuyPack(Supplier, "accessory.charge_cable", 1); // 40
            s.WholesaleService.BuyPack(Supplier, Adapter, 1);           // 50 (10 boş)
            Assert.AreEqual(10, s.AccessoryStock.FreeUnits);
            Snapshot before = Snapshot.Of(s);

            Assert.AreEqual("stock.full", s.WholesaleService.BuyPack(Supplier, "accessory.screen_protector", 1).ErrorCode, "20 > 10 boş");
            AssertUnchanged(before, s);
            Assert.IsTrue(s.WholesaleService.BuyPack(Supplier, "accessory.earphones", 1).IsSuccess, "10 = 10 boş: tam dolar");
            Assert.AreEqual(0, s.AccessoryStock.FreeUnits);
        }

        [Test]
        public void WhenBothCashAndCapacityFail_TheCashErrorWins_AndNothingChanges()
        {
            GameSession s = GameSession.NewGame(ContentVariants.WithOpeningCapital(1400), 1UL);
            s.WholesaleService.BuyPack(Supplier, Case, 1); // 1.400: para bitti, stok 20
            Snapshot before = Snapshot.Of(s);

            Assert.AreEqual("cash.insufficient", s.WholesaleService.BuyPack(Supplier, Adapter, 1).ErrorCode);
            AssertUnchanged(before, s);
        }

        [Test]
        public void ARefusedBuy_NeverTouchesThePhoneShelfOrTheLedger_AcrossManyAttempts()
        {
            GameSession s = NewSession();
            int phones = s.Api.GetInventory().Count;
            Snapshot before = Snapshot.Of(s);

            s.WholesaleService.BuyPack("supplier.ghost", Adapter, 1);
            s.WholesaleService.BuyPack(Supplier, "accessory.ghost", 1);
            s.WholesaleService.BuyPack(Supplier, Powerbank, 1);
            s.WholesaleService.BuyPack(Supplier, Adapter, 0);
            s.WholesaleService.BuyPack(null, null, 1);

            AssertUnchanged(before, s);
            Assert.AreEqual(phones, s.Api.GetInventory().Count);
        }

        [Test]
        public void ADayEarlierThanTheLedgersLastDay_IsRefusedByTheLedger_AndChangesNothing()
        {
            GameSession s = NewSession();
            Assert.IsTrue(s.WholesaleService.BuyPack(Supplier, Adapter, 5).IsSuccess);
            Snapshot before = Snapshot.Of(s);

            Result<WholesalePurchaseReceipt> r = s.WholesaleService.BuyPack(Supplier, Case, 4);

            Assert.AreEqual("ledger.day_regression", r.ErrorCode);
            AssertUnchanged(before, s);
            Assert.AreEqual(0, s.AccessoryStock.Quantity(Case), "defter reddedince stok eklenmez");
        }

        // ---------- bulunamayan teklif ----------

        [Test]
        public void AnUnknownSupplier_GivesSupplierUnknown()
        {
            Assert.AreEqual("supplier.unknown", NewSession().WholesaleService.BuyPack("supplier.ghost", Adapter, 1).ErrorCode);
            Assert.AreEqual("supplier.unknown", NewSession().WholesaleService.BuyPack(null, Adapter, 1).ErrorCode);
        }

        [Test]
        public void AnUnknownAccessory_GivesAccessoryUnknown()
        {
            Assert.AreEqual("accessory.unknown", NewSession().WholesaleService.BuyPack(Supplier, "accessory.ghost", 1).ErrorCode);
            Assert.AreEqual("accessory.unknown", NewSession().WholesaleService.BuyPack(Supplier, null, 1).ErrorCode);
        }

        // ---------- çoklu toptancı ve teklif yok ----------

        [Test]
        public void SeveralSuppliers_AreSupported_EachWithItsOwnOffers()
        {
            GameSession s = NewSession();
            var accessories = new AccessoryCatalog(60, new[]
            {
                new AccessoryDefinition("accessory.a", "A", "x", "new", Money.FromTl(200), "a"),
                new AccessoryDefinition("accessory.b", "B", "x", "new", Money.FromTl(300), "b")
            });
            var wholesale = new WholesaleCatalog(new[]
            {
                new WholesaleOffer("supplier.one", "Bir", "accessory.a", Money.FromTl(100), 5, 1),
                new WholesaleOffer("supplier.two", "İki", "accessory.a", Money.FromTl(90), 10, 2),
                new WholesaleOffer("supplier.two", "İki", "accessory.b", Money.FromTl(40), 3, 1)
            });
            var service = new WholesaleService(wholesale, accessories, s.AccessoryStock, s.EconomyService);

            Assert.AreEqual(Money.FromTl(500), service.BuyPack("supplier.one", "accessory.a", 1).Value.TotalCost);
            Assert.AreEqual(Money.FromTl(120), service.BuyPack("supplier.two", "accessory.b", 1).Value.TotalCost);
            Assert.AreEqual("wholesale.not_available_yet", service.BuyPack("supplier.two", "accessory.a", 1).ErrorCode);
            Assert.AreEqual(Money.FromTl(900), service.BuyPack("supplier.two", "accessory.a", 2).Value.TotalCost);
            Assert.AreEqual(15, s.AccessoryStock.Quantity("accessory.a"));
            Assert.AreEqual(Money.FromTl(1400), s.AccessoryStock.TotalCost("accessory.a"));
        }

        [Test]
        public void AnExistingSupplierAndAccessory_WithoutAnOffer_GivesOfferUnknown()
        {
            GameSession s = NewSession();
            var accessories = new AccessoryCatalog(60, new[]
            {
                new AccessoryDefinition("accessory.a", "A", "x", "new", Money.FromTl(200), "a"),
                new AccessoryDefinition("accessory.b", "B", "x", "new", Money.FromTl(300), "b")
            });
            var wholesale = new WholesaleCatalog(new[] { new WholesaleOffer("supplier.one", "Bir", "accessory.a", Money.FromTl(100), 5, 1) });
            var service = new WholesaleService(wholesale, accessories, s.AccessoryStock, s.EconomyService);
            Snapshot before = Snapshot.Of(s);

            Result<WholesalePurchaseReceipt> r = service.BuyPack("supplier.one", "accessory.b", 1);

            Assert.AreEqual("offer.unknown", r.ErrorCode);
            AssertUnchanged(before, s);
        }

        [Test]
        public void ACostTooLargeToBook_IsRefused_AndChangesNothing()
        {
            GameSession s = NewSession();
            var accessories = new AccessoryCatalog(60, new[] { new AccessoryDefinition("accessory.a", "A", "x", "new", Money.FromTl(200), "a") });
            var wholesale = new WholesaleCatalog(new[] { new WholesaleOffer("supplier.one", "Bir", "accessory.a", Money.FromTl(long.MaxValue / 2), 5, 1) });
            var service = new WholesaleService(wholesale, accessories, s.AccessoryStock, s.EconomyService);
            Snapshot before = Snapshot.Of(s);

            Assert.AreEqual("amount.invalid", service.BuyPack("supplier.one", "accessory.a", 1).ErrorCode);
            AssertUnchanged(before, s);
        }

        [Test]
        public void APackTotalThatIsNotAMultipleOfTen_IsRefusedByTheLedgerRule_AndChangesNothing()
        {
            // Defter özgür kuralı: tutarlar 10 ₺'nin katı (GDD). İçerik artık bunu zorlamadığından servis temiz bir hata verir.
            GameSession s = NewSession();
            var accessories = new AccessoryCatalog(60, new[] { new AccessoryDefinition("accessory.a", "A", "x", "new", Money.FromTl(200), "a") });
            var wholesale = new WholesaleCatalog(new[] { new WholesaleOffer("supplier.one", "Bir", "accessory.a", Money.FromTl(47), 5, 1) }); // 235
            var service = new WholesaleService(wholesale, accessories, s.AccessoryStock, s.EconomyService);
            Snapshot before = Snapshot.Of(s);

            Assert.AreEqual("amount.invalid", service.BuyPack("supplier.one", "accessory.a", 1).ErrorCode);
            AssertUnchanged(before, s);
        }

        // ---------- mevcut sistemi koruma ----------

        [Test]
        public void TheService_DoesNotUseRandomness_TheRngStaysUntouched()
        {
            GameSession s = NewSession();
            var rng = s.Capture().Rng;

            s.WholesaleService.BuyPack(Supplier, Adapter, 1);
            s.WholesaleService.BuyPack(Supplier, Adapter, 1);

            Assert.IsNull(DeepCompare.FirstDifference(rng, s.Capture().Rng), "RNG devamı değişmez");
        }

        [Test]
        public void ThePhoneSide_IsUntouched_ByWholesaleBuys()
        {
            GameSession s = NewSession();
            var listings = s.Api.GetListings().Select(l => l.ListingId).ToArray();
            int phones = s.Api.GetInventory().Count;
            int capacity = s.InventoryState.Capacity;

            s.WholesaleService.BuyPack(Supplier, Adapter, 1);

            CollectionAssert.AreEqual(listings, s.Api.GetListings().Select(l => l.ListingId).ToArray());
            Assert.AreEqual(phones, s.Api.GetInventory().Count);
            Assert.AreEqual(capacity, s.InventoryState.Capacity);
        }

        [Test]
        public void ASessionWithoutAccessoryContent_StillBuildsAnEmptyStock_AndRefusesEverything()
        {
            GameSession s = GameSession.NewGame(ContentDatabase.Load(ContentFixtures.ValidSource()).Database, 1UL);

            Assert.AreEqual(0, s.AccessoryStock.TotalUnits);
            Assert.AreEqual("supplier.unknown", s.WholesaleService.BuyPack(Supplier, Adapter, 1).ErrorCode);
        }

        [Test]
        public void TheServiceChecksItsArguments()
        {
            GameSession s = NewSession();

            Assert.Throws<ArgumentNullException>(() => new WholesaleService(null, s.Content.Accessories, s.AccessoryStock, s.EconomyService));
            Assert.Throws<ArgumentNullException>(() => new WholesaleService(s.Content.Wholesale, null, s.AccessoryStock, s.EconomyService));
            Assert.Throws<ArgumentNullException>(() => new WholesaleService(s.Content.Wholesale, s.Content.Accessories, null, s.EconomyService));
            Assert.Throws<ArgumentNullException>(() => new WholesaleService(s.Content.Wholesale, s.Content.Accessories, s.AccessoryStock, null));
        }

        [Test]
        public void TheWealthContributor_ChecksItsArgument_AndReportsTheStockCost()
        {
            Assert.Throws<ArgumentNullException>(() => new AccessoryStockWealthContributor(null));
            var stock = new AccessoryStock(10);
            stock.Add("accessory.a", 2, Money.FromTl(300));
            var contributor = new AccessoryStockWealthContributor(stock);

            Assert.AreEqual(WealthKeys.AccessoryStock, contributor.Key);
            Assert.AreEqual(Money.FromTl(300), contributor.GetValue());
        }
    }
}
