using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Business;
using Esnaf.Domain.Content;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Game;
using Esnaf.Domain.Phone;
using Esnaf.Domain.Products;
using Esnaf.Domain.Wholesale;
using Esnaf.Tests.Support;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Esnaf.Tests.Wholesale
{
    /// <summary>
    /// Telefon toptancısı (Gün 14) — VERİ ODAKLI ve genellenebilir: <c>ProductDefinition → WholesaleOffer → paket → envanter</c>. Testler tek bir modeli değil sistemi sınar:
    /// içerikteki TÜM telefonların teklifi var, farklı paket adetleri aynı servisle çalışır, yalnızca içerik (yeni ProductDefinition + teklif satırı) eklenince kod değişmeden alınabilir,
    /// geçersiz/yinelenen teklif içerikte reddedilir, nakit/kapasite yetersizliğinde paket tamamen reddedilir, alış atomiktir, kayıt/yükleme korunur.
    /// </summary>
    public class PhoneWholesaleTests
    {
        private const string Supplier = "supplier.ucuz_toptan";
        private const string NewModel = "phone.test_xyz_ultra";

        // ---------- içerik kurma ----------

        // Gerçek içerik dosyalarını okur; verilen kanca ile phone_models.json ve wholesale.json üzerinde değişiklik yapar (manifest atlanır: test, yeni kimliği manifeste yazmaz).
        private static ContentLoadResult Load(Action<JObject, JObject> mutate = null)
        {
            var source = new DictionaryContentSource();
            JObject phones = null;
            JObject wholesale = null;
            foreach (string file in Directory.GetFiles(TestPaths.ContentDataDirectory(), "*.json"))
            {
                string name = Path.GetFileName(file);
                string text = File.ReadAllText(file);
                if (name == ContentFileNames.IdManifest)
                {
                    continue;
                }

                if (name == ContentFileNames.PhoneModels)
                {
                    phones = JObject.Parse(text);
                    continue;
                }

                if (name == ContentFileNames.Wholesale)
                {
                    wholesale = JObject.Parse(text);
                    continue;
                }

                source.Add(name, text);
            }

            if (mutate != null)
            {
                mutate(phones, wholesale);
            }

            source.Add(ContentFileNames.PhoneModels, phones.ToString());
            source.Add(ContentFileNames.Wholesale, wholesale.ToString());
            return ContentDatabase.Load(source, new ContentLoadOptions { RequireManifest = false });
        }

        private static ContentDatabase Content(Action<JObject, JObject> mutate = null)
        {
            ContentLoadResult result = Load(mutate);
            Assert.IsNotNull(result.Database, result.FormatIssues());
            return result.Database;
        }

        // Yeni bir telefon modeli: mevcut bir modelin kopyası, yeni kimlik ve ad (yalnızca içerik).
        private static void AddNewModel(JObject phones, JObject wholesale, int packSize, long unitCost, int availableFromDay = 1, bool withOffer = true)
        {
            var models = (JArray)phones["models"];
            var copy = (JObject)models.First(m => (string)m["id"] == "phone.nova_n1_lite").DeepClone();
            copy["id"] = NewModel;
            copy["name"] = "Test XYZ Ultra";
            models.Add(copy);
            if (withOffer)
            {
                ((JArray)wholesale["suppliers"][0]["offers"]).Add(JObject.FromObject(new
                {
                    productId = NewModel,
                    unitCost,
                    packSize,
                    availableFromDay,
                    suggestedRetail = unitCost + 500
                }));
            }
        }

        private static GameSession New(ContentDatabase content = null, ulong seed = 1UL)
        {
            return GameSession.NewGame(content ?? MarketHarness.RealContent(), seed);
        }

        private static string[] PhoneIds(ContentDatabase content)
        {
            return content.Products.Where(p => p.Sector == "phone").Select(p => p.Id).ToArray();
        }

        // ---------- içerikten keşif ----------

        [Test]
        public void EveryPhoneModelInTheContent_HasAWholesaleOffer_DiscoveredFromData()
        {
            ContentDatabase content = MarketHarness.RealContent();

            string[] offered = content.Wholesale.ProductOffers.Select(o => o.ProductId).ToArray();

            CollectionAssert.AreEquivalent(PhoneIds(content), offered, "her telefon modelinin toptan teklifi içerikte var");
            Assert.AreEqual(10, offered.Length);
            foreach (WholesaleOffer o in content.Wholesale.ProductOffers)
            {
                Assert.AreEqual(WholesaleItemKind.Product, o.Kind);
                Assert.IsTrue(o.UnitCost.IsPositive);
                Assert.Greater(o.PackSize, 0);
                Assert.GreaterOrEqual(o.AvailableFromDay, 1);
            }
        }

        [Test]
        public void ThePacksSizesAndCostsDiffer_AndAllComeFromContent()
        {
            ContentDatabase content = MarketHarness.RealContent();

            Assert.Greater(content.Wholesale.ProductOffers.Select(o => o.PackSize).Distinct().Count(), 1, "paket adetleri modele göre content'ten gelir");
            Assert.Greater(content.Wholesale.ProductOffers.Select(o => o.UnitCost.Tl).Distinct().Count(), 5);
        }

        [Test]
        public void TheContentPrices_AreDerivedFromTheExistingEconomy_NotInvented()
        {
            ContentDatabase content = MarketHarness.RealContent();
            var calculator = new ValueCalculator(content.ValueTables);
            foreach (WholesaleOffer o in content.Wholesale.ProductOffers)
            {
                ProductDefinition def = content.GetProduct(o.ProductId);
                var unit = new ProductInstance { DefinitionId = def.Id, StorageGb = def.BaseStorageGb, AgeMonths = 0 };
                unit.Attributes[PhoneAttributes.Battery] = AttributeValue.FromNumber(100);
                unit.Attributes[PhoneAttributes.Body] = AttributeValue.FromNumber(100);
                unit.Attributes[PhoneAttributes.Screen] = AttributeValue.FromText(PhoneAttributes.ScreenOriginal);
                unit.Attributes[PhoneAttributes.Camera] = AttributeValue.FromText(PhoneAttributes.CameraOk);
                unit.Attributes[PhoneAttributes.Box] = AttributeValue.FromFlag(true);
                unit.Attributes[PhoneAttributes.Invoice] = AttributeValue.FromFlag(true);
                double value = calculator.TrueValue(unit, def).Tl;

                Assert.That(o.UnitCost.Tl / value, Is.InRange(0.88, 0.92), o.ProductId + ": toptan maliyet ≈ sıfır birimin değerinin %90'ı");
                Assert.That(Math.Abs(o.SuggestedRetail.Tl - value) / value, Is.LessThan(0.01), o.ProductId + ": önerilen satış ≈ sıfır birimin değeri");
                Assert.AreEqual(0L, o.UnitCost.Tl % 10, "10 ₺ adımı");
                Assert.Less(o.UnitCost.Tl, o.SuggestedRetail.Tl, "perakende ve toptan bağımsız içerik değerleri; toptan daha düşük");
            }
        }

        [Test]
        public void TheApi_ListsEveryPhoneOffer_WithPackPriceComputedFromUnitCost_AndTheAccessoryOffersAreUnchanged()
        {
            GameSession s = New();

            var phones = s.Api.GetPhoneWholesaleOffers();

            Assert.AreEqual(10, phones.Count);
            foreach (PhoneWholesaleOfferView v in phones)
            {
                Assert.AreEqual(v.UnitCost.Tl * v.PackSize, v.PackCost.Tl, "paket fiyatı hesaplanır");
                Assert.IsTrue(v.IsAvailableToday);
                Assert.AreEqual(s.Content.GetProduct(v.ProductId).Name, v.ProductName);
            }

            Assert.AreEqual(6, s.Api.GetWholesaleOffers().Count, "aksesuar toptancısı etkilenmez");
        }

        // ---------- aynı servisle, farklı modeller ve paket adetleri ----------

        [Test]
        public void TwoDifferentModels_AreBoughtThroughTheSameService_WithDifferentPackSizes()
        {
            GameSession s = New();
            WholesaleOffer cheap = s.Content.Wholesale.ProductOffers.First(o => o.ProductId == "phone.nova_n1_lite");
            WholesaleOffer dear = s.Content.Wholesale.ProductOffers.First(o => o.ProductId == "phone.elma_e14_pro_max");
            Assert.AreNotEqual(cheap.PackSize, dear.PackSize);
            long cash = s.Api.GetCash().Tl;

            Result<PhonePackReceipt> a = s.Api.BuyPhonePack(Supplier, cheap.ProductId);
            Result<PhonePackReceipt> b = s.Api.BuyPhonePack(Supplier, dear.ProductId);

            Assert.IsTrue(a.IsSuccess, a.ErrorCode);
            Assert.IsTrue(b.IsSuccess, b.ErrorCode);
            Assert.AreEqual(cheap.PackSize, a.Value.Quantity);
            Assert.AreEqual(dear.PackSize, b.Value.Quantity);
            Assert.AreEqual(cheap.PackSize + dear.PackSize, s.Api.GetInventory().Count);
            Assert.AreEqual(cheap.PackSize, s.Api.GetInventory().Count(l => l.DefinitionId == cheap.ProductId));
            Assert.AreEqual(dear.PackSize, s.Api.GetInventory().Count(l => l.DefinitionId == dear.ProductId));
            Assert.AreEqual(cash - cheap.PackCost.Tl - dear.PackCost.Tl, s.Api.GetCash().Tl);
        }

        [Test]
        public void EveryOfferInTheContent_IsBuyable_ThroughTheSameService()
        {
            foreach (WholesaleOffer offer in MarketHarness.RealContent().Wholesale.ProductOffers)
            {
                GameSession s = New();

                Result<PhonePackReceipt> r = s.Api.BuyPhonePack(offer.SupplierId, offer.ProductId);

                Assert.IsTrue(r.IsSuccess, offer.ProductId + ": " + r.ErrorCode);
                Assert.AreEqual(offer.PackSize, s.Api.GetInventory().Count(l => l.DefinitionId == offer.ProductId), offer.ProductId);
            }
        }

        [Test]
        public void ThePackBecomesSeparateNewProductInstances_InTheExistingInventory()
        {
            GameSession s = New();
            WholesaleOffer offer = s.Content.Wholesale.ProductOffers.First(o => o.ProductId == "phone.nova_n3_pro");

            Result<PhonePackReceipt> r = s.Api.BuyPhonePack(Supplier, offer.ProductId);

            Assert.IsTrue(r.IsSuccess, r.ErrorCode);
            Assert.AreEqual(offer.PackSize, r.Value.InstanceIds.Count);
            Assert.AreEqual(offer.PackSize, r.Value.InstanceIds.Distinct().Count(), "her telefon ayrı ProductInstance");
            foreach (long id in r.Value.InstanceIds)
            {
                ProductInstance unit = s.Store.Get(id);
                Assert.AreEqual(offer.ProductId, unit.DefinitionId, "aynı ProductDefinition");
                Assert.AreEqual(ProductLocation.Inventory, unit.Location);
                Assert.AreEqual(offer.UnitCost, unit.CostBasis, "maliyet tabanı = birim maliyet");
                Assert.AreEqual(offer.UnitCost, unit.PurchasePrice);
                Assert.IsFalse(unit.ListPrice.IsPositive, "otomatik satış fiyatı yok");
                Assert.AreEqual(0, unit.AgeMonths);
                Assert.AreEqual(100L, unit.GetNumber(PhoneAttributes.Battery));
                Assert.AreEqual(100L, unit.GetNumber(PhoneAttributes.Body));
                Assert.AreEqual(PhoneAttributes.ScreenOriginal, unit.GetText(PhoneAttributes.Screen));
                Assert.AreEqual(PhoneAttributes.CameraOk, unit.GetText(PhoneAttributes.Camera));
                Assert.IsTrue(unit.GetFlag(PhoneAttributes.Box));
                Assert.IsTrue(unit.GetFlag(PhoneAttributes.Invoice));
            }

            Assert.AreEqual(1, s.EconomyState.Ledger.Records.Count(x => x.TypeId == "wholesale_purchase"), "paket için TEK defter satırı");
            Assert.AreEqual(s.Wealth.Calculate().Total, Money.FromTl(250000), "alış servet yaratmaz: nakit stoğa dönüşür");
        }

        // ---------- yalnızca içerik: yeni telefon ----------

        [Test]
        public void ANewPhoneAddedOnlyToContent_ShowsUpAndIsBuyable_WithoutAnyCodeChange()
        {
            ContentDatabase content = Content((phones, wholesale) => AddNewModel(phones, wholesale, packSize: 4, unitCost: 3000));
            GameSession s = New(content);

            PhoneWholesaleOfferView view = s.Api.GetPhoneWholesaleOffers().Single(o => o.ProductId == NewModel);
            Assert.AreEqual("Test XYZ Ultra", view.ProductName);
            Assert.AreEqual(4, view.PackSize);
            Assert.AreEqual(3000L, view.UnitCost.Tl);
            Assert.AreEqual(12000L, view.PackCost.Tl);
            Assert.AreEqual(11, s.Api.GetPhoneWholesaleOffers().Count, "10 mevcut + 1 yeni");

            Result<PhonePackReceipt> r = s.Api.BuyPhonePack(Supplier, NewModel);

            Assert.IsTrue(r.IsSuccess, r.ErrorCode);
            Assert.AreEqual(4, s.Api.GetInventory().Count(l => l.DefinitionId == NewModel));
            Assert.AreEqual(250000 - 12000, s.Api.GetCash().Tl);
        }

        [Test]
        public void ANewPhone_BoughtFromTheWholesaler_FlowsThroughShelfPricingAndCustomersLikeAnyOtherPhone()
        {
            ContentDatabase content = Content((phones, wholesale) => AddNewModel(phones, wholesale, packSize: 3, unitCost: 4000));
            GameSession s = New(content);
            Assert.IsTrue(s.Api.BuyPhonePack(Supplier, NewModel).IsSuccess);
            Assert.IsFalse(s.Customers.HasSellableStock(), "fiyatsız: satılabilir stok değil");
            StockLine line = s.Api.GetInventory().First(l => l.DefinitionId == NewModel);

            Assert.IsTrue(s.Api.SetPrice(line.InstanceId, Money.FromTl(4500)).IsSuccess, "mevcut fiyatlandırma kullanılır");

            Assert.IsTrue(s.Customers.HasSellableStock());
            Assert.IsTrue(s.CustomerQueue.PlanFor(1).Any(c =>
            {
                QueuedCustomer q;
                CustomerSlot slot;
                s.CustomerQueue.TryResolve(c.CustomerId, out q, out slot);
                return s.Customers.FindInterest(slot) == line.InstanceId;
            }), "müşteri sistemi toptan telefonu normal satılabilir telefon olarak görür");
        }

        [Test]
        public void ThePriceAtTheContentsSuggestedRetail_BringsCustomersForEveryPhoneModel()
        {
            foreach (WholesaleOffer offer in MarketHarness.RealContent().Wholesale.ProductOffers)
            {
                GameSession s = New();
                Result<PhonePackReceipt> r = s.Api.BuyPhonePack(Supplier, offer.ProductId);
                Assert.IsTrue(r.IsSuccess, r.ErrorCode);
                long id = r.Value.InstanceIds[0];
                Assert.IsTrue(s.Api.SetPrice(id, offer.SuggestedRetail).IsSuccess);

                Assert.IsTrue(s.Customers.HasSellableStock(), offer.ProductId);
                bool interest = s.CustomerQueue.PlanFor(1).Any(c =>
                {
                    QueuedCustomer q;
                    CustomerSlot slot;
                    s.CustomerQueue.TryResolve(c.CustomerId, out q, out slot);
                    return s.Customers.FindInterest(slot) == id;
                });
                Assert.IsTrue(interest, offer.ProductId + ": önerilen satış fiyatında müşteri ilgilenir");
            }
        }

        // ---------- içerik doğrulaması ----------

        [Test]
        public void AnOfferForAnUnknownProductId_IsRejectedByTheContent()
        {
            ContentLoadResult result = Load((phones, wholesale) =>
                ((JArray)wholesale["suppliers"][0]["offers"]).Add(JObject.FromObject(new { productId = "phone.does_not_exist", unitCost = 1000, packSize = 2, availableFromDay = 1 })));

            Assert.IsNull(result.Database);
            Assert.IsTrue(result.Issues.Any(i => i.Code == ContentIssueCodes.WholesaleReferenceMissing), result.FormatIssues());
        }

        [Test]
        public void ADuplicateOffer_ForTheSameSupplierAndModel_IsRejected()
        {
            ContentLoadResult result = Load((phones, wholesale) =>
                ((JArray)wholesale["suppliers"][0]["offers"]).Add(JObject.FromObject(new { productId = "phone.nova_n1_lite", unitCost = 4000, packSize = 2, availableFromDay = 1 })));

            Assert.IsNull(result.Database);
            Assert.IsTrue(result.Issues.Any(i => i.Code == ContentIssueCodes.WholesaleOfferDuplicate), result.FormatIssues());
        }

        [Test]
        public void AnOfferNeedsExactlyOneItem_AccessoryOrProduct()
        {
            ContentLoadResult both = Load((phones, wholesale) =>
                ((JArray)wholesale["suppliers"][0]["offers"]).Add(JObject.FromObject(new { accessoryId = "accessory.powerbank", productId = "phone.yildiz_y5", unitCost = 1, packSize = 1, availableFromDay = 1 })));
            ContentLoadResult none = Load((phones, wholesale) =>
                ((JArray)wholesale["suppliers"][0]["offers"]).Add(JObject.FromObject(new { unitCost = 1, packSize = 1, availableFromDay = 1 })));

            Assert.IsNull(both.Database);
            Assert.IsTrue(both.Issues.Any(i => i.Code == ContentIssueCodes.WholesaleFieldInvalid));
            Assert.IsNull(none.Database);
            Assert.IsTrue(none.Issues.Any(i => i.Code == ContentIssueCodes.WholesaleFieldInvalid));
        }

        [TestCase("unitCost", 0)]
        [TestCase("packSize", 0)]
        [TestCase("availableFromDay", 0)]
        public void InvalidNumbers_InAProductOffer_AreRejected(string field, int value)
        {
            ContentLoadResult result = Load((phones, wholesale) =>
            {
                JObject offer = JObject.FromObject(new { productId = "phone.yildiz_y5", unitCost = 6000, packSize = 2, availableFromDay = 1 });
                offer.Remove("productId");
                offer["productId"] = "phone.zirve_z5";
                ((JArray)wholesale["suppliers"][0]["offers"]).Where(o => (string)o["productId"] == "phone.zirve_z5").ToList().ForEach(o => o.Remove());
                offer[field] = value;
                ((JArray)wholesale["suppliers"][0]["offers"]).Add(offer);
            });

            Assert.IsNull(result.Database);
            Assert.IsTrue(result.Issues.Any(i => i.Code == ContentIssueCodes.WholesaleFieldInvalid), result.FormatIssues());
        }

        // ---------- servis hataları ----------

        [Test]
        public void ServiceErrors_AreRejected_WithoutChangingTheGame()
        {
            ContentDatabase content = Content((phones, wholesale) => AddNewModel(phones, wholesale, packSize: 2, unitCost: 3000, withOffer: false));
            GameSession s = New(content);
            string digest = s.Api.GetStateDigest();

            Assert.AreEqual("supplier.unknown", s.Api.BuyPhonePack("supplier.nope", "phone.nova_n1_lite").ErrorCode);
            Assert.AreEqual("product.unknown", s.Api.BuyPhonePack(Supplier, "phone.does_not_exist").ErrorCode, "geçersiz ProductId reddedilir");
            Assert.AreEqual("product.unknown", s.Api.BuyPhonePack(Supplier, "accessory.powerbank").ErrorCode, "aksesuar kimliği telefon değildir");
            Assert.AreEqual("product.unknown", s.Api.BuyPhonePack(Supplier, null).ErrorCode);
            Assert.AreEqual("offer.unknown", s.Api.BuyPhonePack(Supplier, NewModel).ErrorCode, "ürün var ama teklif yok");

            Assert.AreEqual(digest, s.Api.GetStateDigest(), "hata hiçbir şeyi değiştirmez");
        }

        [Test]
        public void AnOfferBeforeItsDay_IsRefused_ThenOpens()
        {
            ContentDatabase content = Content((phones, wholesale) => AddNewModel(phones, wholesale, packSize: 2, unitCost: 3000, availableFromDay: 3));
            GameSession s = New(content);
            string digest = s.Api.GetStateDigest();

            Assert.IsFalse(s.Api.GetPhoneWholesaleOffers().Single(o => o.ProductId == NewModel).IsAvailableToday);
            Assert.AreEqual("wholesale.not_available_yet", s.Api.BuyPhonePack(Supplier, NewModel).ErrorCode);
            Assert.AreEqual(digest, s.Api.GetStateDigest());

            s.Api.EndDay();
            s.Api.EndDay();

            Assert.IsTrue(s.Api.BuyPhonePack(Supplier, NewModel).IsSuccess);
        }

        [Test]
        public void InsufficientCash_IsRefused_AndNothingChanges()
        {
            ContentDatabase content = Content((phones, wholesale) => AddNewModel(phones, wholesale, packSize: 3, unitCost: 100000));
            GameSession s = New(content);
            string digest = s.Api.GetStateDigest();

            Result<PhonePackReceipt> r = s.Api.BuyPhonePack(Supplier, NewModel);

            Assert.AreEqual("cash.insufficient", r.ErrorCode);
            Assert.AreEqual(digest, s.Api.GetStateDigest());
            Assert.AreEqual(0, s.Api.GetInventory().Count);
        }

        [Test]
        public void WhenTheShelfCannotHoldThePack_TheWholePackIsRefused()
        {
            GameSession s = New();
            Assert.IsTrue(s.Api.BuyPhonePack(Supplier, "phone.nova_n1_lite").IsSuccess); // 10 adet
            Assert.AreEqual(10, s.Api.GetInventory().Count);
            string digest = s.Api.GetStateDigest();
            long cash = s.Api.GetCash().Tl;
            int ledger = s.EconomyState.Ledger.Count;
            long lastId = s.InstanceIds.LastIssued;

            Result<PhonePackReceipt> r = s.Api.BuyPhonePack(Supplier, "phone.yildiz_y5"); // 10 daha: 15 kapasiteye sığmaz

            Assert.AreEqual("inventory.full", r.ErrorCode);
            Assert.AreEqual(10, s.Api.GetInventory().Count, "paketin hiçbir telefonu eklenmedi");
            Assert.AreEqual(cash, s.Api.GetCash().Tl);
            Assert.AreEqual(ledger, s.EconomyState.Ledger.Count);
            Assert.AreEqual(lastId, s.InstanceIds.LastIssued, "kimlik üretilmedi");
            Assert.AreEqual(digest, s.Api.GetStateDigest());
        }

        [Test]
        public void ASuccessfulPurchase_IsAtomic_CashLedgerStockAndIdsMoveTogether()
        {
            GameSession s = New();
            WholesaleOffer offer = s.Content.Wholesale.ProductOffers.First(o => o.ProductId == "phone.zirve_z5");
            long cash = s.Api.GetCash().Tl;
            int ledger = s.EconomyState.Ledger.Count;
            long lastId = s.InstanceIds.LastIssued;

            Result<PhonePackReceipt> r = s.Api.BuyPhonePack(Supplier, offer.ProductId);

            Assert.IsTrue(r.IsSuccess);
            Assert.AreEqual(cash - offer.PackCost.Tl, s.Api.GetCash().Tl);
            Assert.AreEqual(ledger + 1, s.EconomyState.Ledger.Count);
            Assert.AreEqual(lastId + offer.PackSize, s.InstanceIds.LastIssued);
            Assert.AreEqual(offer.PackSize, s.Api.GetInventory().Count);
            Assert.AreEqual(r.Value.LedgerRecordId, s.EconomyState.Ledger.Records.Last().Id);
        }

        // ---------- kayıt / yükleme ----------

        [Test]
        public void SaveAndLoad_KeepTheWholesalePhones_TheirCostsAndTheNextIds()
        {
            GameSession s = New();
            PhonePackReceipt receipt = s.Api.BuyPhonePack(Supplier, "phone.samsun_vega_a3").Value;
            Assert.IsTrue(s.Api.SetPrice(receipt.InstanceIds[0], Money.FromTl(11000)).IsSuccess);

            GameSession r = GameSession.Restore(MarketHarness.RealContent(), s.Capture()).Value;

            Assert.AreEqual(s.Api.GetStateDigest(), r.Api.GetStateDigest());
            Assert.AreEqual(receipt.Quantity, r.Api.GetInventory().Count);
            foreach (long id in receipt.InstanceIds)
            {
                ProductInstance a = s.Store.Get(id);
                ProductInstance b = r.Store.Get(id);
                Assert.AreEqual(a.CostBasis, b.CostBasis);
                Assert.AreEqual(a.ListPrice, b.ListPrice);
                Assert.AreEqual(a.DefinitionId, b.DefinitionId);
                Assert.AreEqual(100L, b.GetNumber(PhoneAttributes.Battery));
            }

            Assert.AreEqual(Money.FromTl(11000), r.Api.GetInventory().First(l => l.InstanceId == receipt.InstanceIds[0]).ListPrice);
            PhonePackReceipt next = r.Api.BuyPhonePack(Supplier, "phone.nova_n1_lite").Value;
            Assert.AreEqual(receipt.InstanceIds.Max() + 1, next.InstanceIds.Min(), "kimlik sayacı kayıttan sonra kaldığı yerden sürer");
        }

        // ---------- sınırlar ----------

        [Test]
        public void ThePhoneWholesaler_DoesNotTouchTheAccessoryWholesaler_OrTheRngStreams()
        {
            GameSession s = New();
            var rng = s.Capture().Rng;
            Assert.IsTrue(s.Api.BuyPhonePack(Supplier, "phone.nova_n1_lite").IsSuccess);

            Assert.IsNull(DeepCompare.FirstDifference(rng, s.Capture().Rng), "yeni RNG yok");
            Assert.AreEqual(0, s.Api.GetAccessoryStock().TotalUnits, "aksesuar stoğuna dokunmaz");
            Assert.IsTrue(s.Api.BuyWholesalePack(Supplier, "accessory.charger_adapter").IsSuccess, "aksesuar toptancısı çalışır");
            Assert.AreEqual(10, s.Api.GetAccessoryStock().TotalUnits);
        }
    }
}
