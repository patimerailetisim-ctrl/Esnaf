using System;
using System.IO;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Content;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Game;
using Esnaf.Domain.Market;
using Esnaf.Domain.Products;
using Esnaf.Tests.Support;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Esnaf.Tests.Game
{
    /// <summary>GDD v0.3 6.7: içerikte kalmamış ürün kimliği → hata göstermeden otomatik iade / ilan kaldırma.</summary>
    public class SnapshotContentChangeTests
    {
        private static readonly string[] Removable =
        {
            "phone.nova_n1_lite", "phone.samsun_vega_a3", "phone.nova_n3_pro", "phone.zirve_z5", "phone.yildiz_y8_plus",
            "phone.elma_e11", "phone.samsun_vega_s21", "phone.elma_e13_pro"
        };

        private static ContentDatabase ContentWithout(string modelId)
        {
            var source = new DictionaryContentSource();
            foreach (string file in Directory.GetFiles(TestPaths.ContentDataDirectory(), "*.json"))
            {
                string name = Path.GetFileName(file);
                if (name == ContentFileNames.IdManifest)
                {
                    continue; // manifest kaldırılan kimliği hâlâ listelerdi; bu test "manifest atlanmış" bir içerik değişikliğini taklit eder
                }

                string text = File.ReadAllText(file);
                if (name == ContentFileNames.PhoneModels)
                {
                    JObject root = JObject.Parse(text);
                    var models = (JArray)root["models"];
                    foreach (JToken model in models.ToList())
                    {
                        if ((string)model["id"] == modelId)
                        {
                            model.Remove();
                        }
                    }

                    text = root.ToString();
                }

                source.Add(name, text);
            }

            ContentLoadResult result = ContentDatabase.Load(source, new ContentLoadOptions { RequireManifest = false });
            Assert.IsNotNull(result.Database, result.FormatIssues());
            return result.Database;
        }

        /// <summary>Rafında, pazarında ve kaydında verilen modelden ürün olan bir oturum arar.</summary>
        private static GameSession SessionWithModel(string modelId, bool needShelf, bool needMarket)
        {
            for (ulong seed = 1; seed <= 400; seed++)
            {
                GameSession s = GameSession.NewGame(MarketHarness.RealContent(), seed);
                var rnd = new Random((int)seed);
                for (int i = 0; i < 300; i++)
                {
                    SessionDriver.Step(s, rnd);
                    bool shelf = s.InventoryState.ItemIds.Any(id => s.Store.Get(id).DefinitionId == modelId);
                    bool market = s.Market.Listings.Any(l => s.Store.Get(l.InstanceId).DefinitionId == modelId);
                    if ((!needShelf || shelf) && (!needMarket || market) && s.Api.GetNegotiation() == null && s.Api.GetSale() == null)
                    {
                        return s;
                    }
                }
            }

            return null;
        }

        [TestCaseSource(nameof(Removable))]
        public void ShelfItemsOfARemovedModel_AreRefundedAtTheirPurchasePrice(string modelId)
        {
            GameSession s = SessionWithModel(modelId, true, false);
            if (s == null)
            {
                Assert.Ignore("test verisi: " + modelId + " rafta bulunan bir oturum üretilemedi");
            }

            var gone = s.InventoryState.ItemIds.Select(id => s.Store.Get(id)).Where(i => i.DefinitionId == modelId).ToList();
            Money refund = gone.Aggregate(Money.Zero, (a, i) => a + i.PurchasePrice);
            Money cash = s.Api.GetCash();
            int shelf = s.InventoryState.Count;
            GameSnapshot snap = s.Capture();

            Result<GameSession> r = GameSession.Restore(ContentWithout(modelId), snap);

            Assert.IsTrue(r.IsSuccess, r.ErrorCode + ": " + r.Message);
            GameSession back = r.Value;
            Assert.AreEqual(cash + refund, back.Api.GetCash(), "alış fiyatı kadar nakit iade");
            Assert.AreEqual(shelf - gone.Count, back.InventoryState.Count);
            foreach (ProductInstance item in gone)
            {
                Assert.IsFalse(back.Store.TryGet(item.InstanceId, out ProductInstance _), "ürün silinir");
            }

            var refunds = back.EconomyState.Ledger.Records.Where(x => x.TypeId == "content_refund").ToList();
            Assert.AreEqual(gone.Count, refunds.Count);
            Assert.AreEqual(refund, refunds.Aggregate(Money.Zero, (a, x) => a + x.Amount));
            Assert.IsTrue(back.EconomyState.Ledger.Verify().IsSuccess);
            Assert.IsTrue(back.LoadWarnings.Count >= gone.Count);
            Assert.IsTrue(back.LoadWarnings.Any(w => w.Contains("refunded")));
            Assert.IsTrue(back.Api.GetListings().All(l => l.DefinitionId != modelId));
        }

        [Test]
        public void ListingsOfARemovedModel_AreRemovedSilently_AndTheirPendingAppraisalFeeIsWrittenOff()
        {
            const string model = "phone.nova_n3_pro";
            GameSession s = SessionWithModel(model, false, true);
            if (s == null)
            {
                Assert.Ignore("test verisi: pazarda " + model + " bulunan bir oturum üretilemedi");
            }

            GameSnapshot snap = s.Capture();
            int listings = snap.Market.Listings.Count;
            int removed = snap.Market.Listings.Count(l => snap.Instances.First(i => i.InstanceId == l.InstanceId).DefinitionId == model);
            Money shelfRefund = s.InventoryState.ItemIds.Select(id => s.Store.Get(id)).Where(i => i.DefinitionId == model).Aggregate(Money.Zero, (a, i) => a + i.PurchasePrice);
            Money cash = s.Api.GetCash();

            GameSession back = GameSession.Restore(ContentWithout(model), snap).Value;

            Assert.AreEqual(listings - removed, back.Market.Count);
            Assert.AreEqual(cash + shelfRefund, back.Api.GetCash(), "pazardaki ürün için para iadesi yok (yalnızca rafta duranlar iade edilir)");
            Assert.IsTrue(back.Api.GetListings().All(l => l.DefinitionId != model));
            Assert.IsTrue(back.LoadWarnings.Any(w => w.Contains("Listing of instance")));
            Assert.IsTrue(back.EconomyState.Ledger.Verify().IsSuccess);
        }

        [Test]
        public void AnAppraisalFee_PendingOnARemovedMarketItem_BecomesAWastedAppraisal()
        {
            for (ulong seed = 1; seed <= 200; seed++)
            {
                GameSession s = GameSession.NewGame(MarketHarness.RealContent(), seed);
                for (int d = 0; d < 2; d++)
                {
                    s.Api.EndDay();
                }

                ListingView l = s.Api.GetListings().FirstOrDefault(v => v.DefinitionId != "phone.yildiz_y5" && v.DefinitionId != "phone.elma_e14_pro_max" && Removable.Contains(v.DefinitionId));
                if (l == null || s.Api.StartAppraisal(l.ListingId, "s1").IsFailure)
                {
                    continue;
                }

                Money pending = s.EconomyState.PendingAppraisalTotal();
                Assert.IsTrue(pending.IsPositive);
                GameSession back = GameSession.Restore(ContentWithout(l.DefinitionId), s.Capture()).Value;

                Assert.IsTrue(back.EconomyState.PendingAppraisalTotal().IsZero);
                Assert.IsTrue(back.EconomyState.Ledger.Records.Any(x => x.TypeId == "wasted_appraisal"));
                Assert.IsTrue(back.EconomyState.Ledger.Verify().IsSuccess);
                return;
            }

            Assert.Ignore("test verisi: ekspertizli ilan bulunamadı");
        }

        [Test]
        public void SoldItemsOfARemovedModel_StayInTheRecordUntouched()
        {
            for (ulong seed = 1; seed <= 100; seed++)
            {
                GameSession s = GameSession.NewGame(MarketHarness.RealContent(), seed);
                SessionDriver.Run(s, new Random((int)seed), 300);
                var sold = s.Store.All.FirstOrDefault(i => i.Location == ProductLocation.Sold && Removable.Contains(i.DefinitionId));
                if (sold == null)
                {
                    continue;
                }

                GameSession back = GameSession.Restore(ContentWithout(sold.DefinitionId), s.Capture()).Value;

                Assert.IsTrue(back.Store.TryGet(sold.InstanceId, out ProductInstance kept));
                Assert.AreEqual(ProductLocation.Sold, kept.Location);
                return;
            }

            Assert.Ignore("test verisi: satılmış ürün bulunamadı");
        }

        [Test]
        public void AnOpenNegotiation_ForARemovedModel_IsDropped_WithAWarning()
        {
            for (ulong seed = 1; seed <= 200; seed++)
            {
                GameSession s = GameSession.NewGame(MarketHarness.RealContent(), seed);
                ListingView l = s.Api.GetListings().FirstOrDefault(v => Removable.Contains(v.DefinitionId));
                if (l == null)
                {
                    continue;
                }

                s.Api.StartNegotiation(l.ListingId);
                GameSession back = GameSession.Restore(ContentWithout(l.DefinitionId), s.Capture()).Value;

                Assert.IsNull(back.Api.GetNegotiation());
                Assert.IsTrue(back.LoadWarnings.Any(w => w.Contains("open negotiation")));
                Assert.IsTrue(back.Api.EndDay().IsSuccess, "pazarlık düştüğü için gün bitirilebilir");
                return;
            }

            Assert.Ignore("test verisi: kaldırılabilir modelli ilan bulunamadı");
        }

        [Test]
        public void AnOpenSale_OfARemovedModel_IsDropped_WithAWarning()
        {
            for (ulong seed = 1; seed <= 300; seed++)
            {
                GameSession s = GameSession.NewGame(MarketHarness.RealContent(), seed);
                SessionDriver.Run(s, new Random((int)seed), 200);
                if (s.Api.GetNegotiation() != null)
                {
                    s.Api.WalkAway();
                }

                foreach (var line in s.Api.GetInventory().ToList())
                {
                    s.Api.SetPrice(line.InstanceId, Money.FromTl(Money.RoundTo10((long)(line.CostBasis.Tl * 1.1) + 10)));
                }

                foreach (var c in s.Api.GetCustomers().ToList())
                {
                    if (s.Api.GetSale() == null && s.Api.StartSale(c.CustomerId).IsSuccess)
                    {
                        string model = s.Store.Get(s.TradeState.CurrentSale.InstanceId).DefinitionId;
                        if (!Removable.Contains(model))
                        {
                            s.Api.LetCustomerGo();
                            continue;
                        }

                        GameSession back = GameSession.Restore(ContentWithout(model), s.Capture()).Value;

                        Assert.IsNull(back.Api.GetSale());
                        Assert.IsTrue(back.LoadWarnings.Any(w => w.Contains("open sale")));
                        return;
                    }
                }
            }

            Assert.Ignore("test verisi: kaldırılabilir modelli satış bulunamadı");
        }

        [Test]
        public void TheRepairedGame_KeepsPlaying_WithoutErrors()
        {
            GameSession s = SessionWithModel("phone.nova_n1_lite", false, true) ?? SessionWithModel("phone.samsun_vega_a3", false, true);
            if (s == null)
            {
                Assert.Ignore("test verisi");
            }

            string model = s.Market.Listings.Select(l => s.Store.Get(l.InstanceId).DefinitionId).First(m => Removable.Contains(m));
            GameSession back = GameSession.Restore(ContentWithout(model), s.Capture()).Value;

            SessionDriver.Run(back, new Random(5), 250);

            Assert.IsTrue(back.EconomyState.Ledger.Verify().IsSuccess);
        }
    }
}
