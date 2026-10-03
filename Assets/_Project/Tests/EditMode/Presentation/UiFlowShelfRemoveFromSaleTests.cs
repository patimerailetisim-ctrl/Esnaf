using System.Collections.Generic;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Business;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Game;
using Esnaf.Domain.Products;
using Esnaf.Domain.Time;
using Esnaf.Presentation;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Presentation
{
    /// <summary>
    /// Gün 13.2 — raf yönetimi: seçili telefonun model/görsel/maliyet/fiyat/kâr/marj/stok bilgisi ve "Satıştan Çıkar" (IGameApi.ClearPrice: etiket fiyatı 0). Telefon yok edilmez,
    /// stoktan düşmez, maliyeti/mülkiyeti değişmez; yalnızca satılabilir stok olmaktan çıkar ve müşteri talep havuzuna girmez. Yeniden fiyatlanınca satışa döner.
    /// </summary>
    public class UiFlowShelfRemoveFromSaleTests
    {
        private const ulong Seed = 20260101UL;

        // İki farklı telefon rafta; ikisi de maliyetine fiyatlı (tüm müşteriler ilgilenir).
        private static GameSession WithTwoPricedPhones(ulong seed = Seed, IEventBus bus = null)
        {
            GameSession s = GameSession.NewGame(MarketHarness.RealContent(), seed, bus);
            foreach (var l in s.Api.GetListings().Take(2).ToList())
            {
                Assert.IsTrue(s.Api.BuyListing(l.ListingId).IsSuccess);
            }

            foreach (StockLine line in s.Api.GetInventory())
            {
                Assert.IsTrue(s.Api.SetPrice(line.InstanceId, Money.FromTl((line.CostBasis.Tl + 9) / 10 * 10)).IsSuccess);
            }

            return s;
        }

        private static StockLine Line(GameSession s, int index)
        {
            return s.Api.GetInventory()[index];
        }

        private static UiFlow OpenShelf(GameSession s)
        {
            UiFlow flow = new UiFlow(s.Api, new ContentPresentation(s.Content), s.Bus);
            Assert.IsTrue(flow.OpenShelf());
            return flow;
        }

        // ---------- panel bilgisi ----------

        [Test]
        public void SelectingAPhone_ShowsModel_Image_Cost_Price_ProfitAndMargin_AndStock()
        {
            GameSession s = WithTwoPricedPhones();
            StockLine line = Line(s, 0);
            using (UiFlow flow = OpenShelf(s))
            {
                flow.SelectShelfItem(line.InstanceId);

                ShelfPriceEditorViewModel e = flow.ShelfScreen.Editor;

                Assert.AreEqual(flow.Content.ModelName(line.DefinitionId), e.Title, "model");
                Assert.AreEqual(line.DefinitionId, e.DefinitionId, "görsel için model kimliği");
                Assert.AreEqual(TurkishTexts.ShelfAcquisitionCost(line.CostBasis), e.CostLine, "alış maliyeti");
                Assert.AreEqual(TurkishTexts.ShelfEditPrice(line.ListPrice), e.PriceLine, "satış fiyatı");
                Assert.AreEqual(TurkishTexts.ShelfProfit(line.ListPrice - line.CostBasis), e.ProfitLine, "tahmini kâr/zarar");
                Assert.AreEqual(TurkishTexts.ShelfMargin((line.ListPrice.Tl - line.CostBasis.Tl) * 100 / line.ListPrice.Tl), e.MarginLine, "marj");
                Assert.AreEqual(TurkishTexts.ShelfStock(1), e.StockLine, "stok");
                Assert.AreEqual("Stok: 1 adet", e.StockLine);
                Assert.IsTrue(e.CanRemoveFromSale, "satıştaki ürün için Satıştan Çıkar var");
                Assert.AreEqual("Satıştan Çıkar", e.RemoveButtonText);
            }
        }

        [Test]
        public void TheStockLine_CountsTheUnitsOfTheSameModel()
        {
            GameSession s = GameSession.NewGame(MarketHarness.RealContent(), 3UL);
            var sameModel = new List<long>();
            string model = null;
            int guard = 0;
            while (sameModel.Count < 2 && guard++ < 25)
            {
                foreach (var l in s.Api.GetListings().ToList())
                {
                    if (model == null || l.DefinitionId == model)
                    {
                        if (s.Api.BuyListing(l.ListingId).IsSuccess)
                        {
                            model = l.DefinitionId;
                            sameModel.Add(s.Api.GetInventory().Last().InstanceId);
                        }
                    }
                }

                if (sameModel.Count < 2)
                {
                    s.Api.EndDay();
                }
            }

            if (sameModel.Count < 2)
            {
                Assert.Inconclusive("Aynı modelden iki telefon bulunamadı.");
            }

            using (UiFlow flow = OpenShelf(s))
            {
                flow.SelectShelfItem(sameModel[0]);

                Assert.AreEqual(TurkishTexts.ShelfStock(sameModel.Count), flow.ShelfScreen.Editor.StockLine);
            }
        }

        [Test]
        public void AnUnpricedPhone_HasNoRemoveFromSaleButton()
        {
            GameSession s = GameSession.NewGame(MarketHarness.RealContent(), Seed);
            Assert.IsTrue(s.Api.BuyListing(s.Api.GetListings()[0].ListingId).IsSuccess);
            using (UiFlow flow = OpenShelf(s))
            {
                flow.SelectShelfItem(Line(s, 0).InstanceId);

                Assert.IsFalse(flow.ShelfScreen.Editor.CanRemoveFromSale, "zaten satış dışı");
            }
        }

        // ---------- satıştan çıkarma ----------

        [Test]
        public void RemovingFromSale_KeepsThePhone_TheStock_TheCostAndTheOwnership()
        {
            GameSession s = WithTwoPricedPhones();
            StockLine before = Line(s, 0);
            ProductInstance instance = s.Store.Get(before.InstanceId);
            long cash = s.Api.GetCash().Tl;
            Money wealth = s.Wealth.Calculate().Total;
            int ledger = s.EconomyState.Ledger.Count;
            int stock = s.Api.GetInventory().Count;

            Result r = s.Api.ClearPrice(before.InstanceId);

            Assert.IsTrue(r.IsSuccess, r.ErrorCode);
            Assert.AreEqual(stock, s.Api.GetInventory().Count, "stoktan düşmedi");
            StockLine after = s.Api.GetInventory().Single(l => l.InstanceId == before.InstanceId);
            Assert.AreEqual(before.CostBasis, after.CostBasis, "maliyet değişmedi");
            Assert.AreEqual(before.DefinitionId, after.DefinitionId);
            Assert.IsFalse(after.ListPrice.IsPositive, "satış dışı (etiket fiyatı 0)");
            Assert.AreEqual(ProductLocation.Inventory, instance.Location, "mülkiyet/konum korunur");
            Assert.AreSame(instance, s.Store.Get(before.InstanceId), "telefon yok edilmedi");
            Assert.AreEqual(cash, s.Api.GetCash().Tl, "nakit değişmedi");
            Assert.AreEqual(wealth, s.Wealth.Calculate().Total, "servet değişmedi");
            Assert.AreEqual(ledger, s.EconomyState.Ledger.Count, "defter değişmedi");
        }

        [Test]
        public void ARemovedPhone_IsNoLongerSellableStock_AndNoCustomerRequestsIt()
        {
            GameSession s = GameSession.NewGame(MarketHarness.RealContent(), Seed);
            Assert.IsTrue(s.Api.BuyListing(s.Api.GetListings()[0].ListingId).IsSuccess);
            long id = Line(s, 0).InstanceId;
            Assert.IsTrue(s.Api.SetPrice(id, Line(s, 0).CostBasis).IsSuccess);
            Assert.IsTrue(s.Customers.HasSellableStock());
            QueuedCustomer first = s.CustomerQueue.PlanFor(1)[0];
            QueuedCustomer q;
            CustomerSlot slot;
            Assert.IsTrue(s.CustomerQueue.TryResolve(first.CustomerId, out q, out slot));
            Assert.AreNotEqual(0L, s.Customers.FindInterest(slot));

            Assert.IsTrue(s.Api.ClearPrice(id).IsSuccess);

            Assert.IsFalse(s.Customers.HasSellableStock());
            Assert.AreEqual(0L, s.Customers.FindInterest(slot), "müşteri artık ilgilenmez");
        }

        [Test]
        public void AfterRemoval_NobodyArrives_AndTheyAreSkippedForGood()
        {
            var bus = new EventBus();
            var arrived = new List<CustomerArrived>();
            bus.Subscribe<CustomerArrived>(arrived.Add);
            GameSession s = WithTwoPricedPhones(Seed, bus);
            foreach (StockLine line in s.Api.GetInventory().ToList())
            {
                Assert.IsTrue(s.Api.ClearPrice(line.InstanceId).IsSuccess);
            }

            s.Api.AdvanceTime(StoreHours.CloseMinute - s.Api.GetClock().MinuteOfDay);

            Assert.AreEqual(0, arrived.Count, "satışta ürün yok: müşteri gelmez");
            Assert.AreEqual((1 << s.CustomerQueue.PlanFor(1).Count) - 1, s.Capture().Customers.QueueSkipped, "hepsi kalıcı atlandı");
            Assert.AreEqual(2, s.Api.GetInventory().Count, "ürünler duruyor");
        }

        // ---------- yeniden satışa alma ----------

        [Test]
        public void ARemovedPhone_CanBeSelectedPricedAgainAndPutBackOnSale()
        {
            GameSession s = WithTwoPricedPhones();
            StockLine line = Line(s, 0);
            Assert.IsTrue(s.Api.ClearPrice(Line(s, 1).InstanceId).IsSuccess);
            Assert.IsTrue(s.Api.ClearPrice(line.InstanceId).IsSuccess);
            Assert.IsFalse(s.Customers.HasSellableStock());
            using (UiFlow flow = OpenShelf(s))
            {
                flow.SelectShelfItem(line.InstanceId);
                Assert.IsFalse(flow.ShelfScreen.Editor.CanRemoveFromSale);
                flow.SetShelfPrice(line.CostBasis.Tl);

                Assert.IsTrue(flow.SaveShelfPrice().IsSuccess);

                Assert.AreEqual(Money.FromTl(line.CostBasis.Tl), Line(s, 0).ListPrice, "yeniden fiyatlandı");
                Assert.IsTrue(s.Customers.HasSellableStock(), "tekrar satılabilir stok");
                Assert.IsTrue(flow.ShelfScreen.Items.Single(r => r.InstanceId == line.InstanceId).IsSellable);
            }
        }

        [Test]
        public void ARepricedPhone_BringsLaterCustomersAgain_ButNotTheSkippedOnes()
        {
            var bus = new EventBus();
            var arrived = new List<CustomerArrived>();
            bus.Subscribe<CustomerArrived>(arrived.Add);
            GameSession s = WithTwoPricedPhones(Seed, bus);
            IReadOnlyList<QueuedCustomer> plan = s.CustomerQueue.PlanFor(1);
            StockLine a = Line(s, 0);
            StockLine b = Line(s, 1);
            Assert.IsTrue(s.Api.ClearPrice(a.InstanceId).IsSuccess);
            Assert.IsTrue(s.Api.ClearPrice(b.InstanceId).IsSuccess);
            s.Api.AdvanceTime(plan[1].ArrivalMinute + 1 - s.Api.GetClock().MinuteOfDay);
            Assert.AreEqual(0, arrived.Count);

            Assert.IsTrue(s.Api.SetPrice(a.InstanceId, a.CostBasis).IsSuccess);
            s.Api.AdvanceTime(StoreHours.CloseMinute - s.Api.GetClock().MinuteOfDay);

            Assert.Greater(arrived.Count, 0, "yeniden satışa alınca sonraki müşteriler gelir");
            Assert.IsFalse(arrived.Any(x => x.CustomerId == plan[0].CustomerId || x.CustomerId == plan[1].CustomerId), "atlananlar geri gelmez");
        }

        // ---------- diğer stoklar ----------

        [Test]
        public void RemovingOnePhone_DoesNotAffectTheOtherStock()
        {
            GameSession s = WithTwoPricedPhones();
            StockLine a = Line(s, 0);
            StockLine b = Line(s, 1);

            Assert.IsTrue(s.Api.ClearPrice(a.InstanceId).IsSuccess);

            StockLine bAfter = Line(s, 1);
            Assert.AreEqual(b.InstanceId, bAfter.InstanceId);
            Assert.AreEqual(b.ListPrice, bAfter.ListPrice, "diğer telefonun fiyatı aynı");
            Assert.AreEqual(b.CostBasis, bAfter.CostBasis);
            Assert.IsTrue(s.Customers.HasSellableStock(), "diğer telefon hâlâ satılabilir");
            QueuedCustomer q;
            CustomerSlot slot;
            Assert.IsTrue(s.CustomerQueue.TryResolve(s.CustomerQueue.PlanFor(1)[0].CustomerId, out q, out slot));
            Assert.AreEqual(b.InstanceId, s.Customers.FindInterest(slot), "müşteri yalnızca satıştaki diğer telefonla ilgilenir");
        }

        // ---------- hatalar ----------

        [Test]
        public void ClearPrice_Errors_AndIsIdempotentForAnUnpricedPhone()
        {
            GameSession s = GameSession.NewGame(MarketHarness.RealContent(), Seed);
            Assert.IsTrue(s.Api.BuyListing(s.Api.GetListings()[0].ListingId).IsSuccess);
            long id = Line(s, 0).InstanceId;
            string digest = s.Api.GetStateDigest();

            Assert.AreEqual("instance.unknown", s.Api.ClearPrice(987654L).ErrorCode);
            Assert.AreEqual("instance.not_in_inventory", s.Api.ClearPrice(s.Api.GetListings()[0].InstanceId).ErrorCode, "rafta olmayan ürün");
            Assert.IsTrue(s.Api.ClearPrice(id).IsSuccess, "zaten fiyatsız: etkisiz");
            Assert.AreEqual(digest, s.Api.GetStateDigest(), "hatalar ve etkisiz çağrı durumu değiştirmez");
        }

        [Test]
        public void APhoneInARunningSale_CannotBeRemovedFromSale()
        {
            for (ulong seed = 1; seed <= 400; seed++)
            {
                GameSession s = GameSession.NewGame(MarketHarness.RealContent(), seed);
                Assert.IsTrue(s.Api.BuyListing(s.Api.GetListings()[0].ListingId).IsSuccess);
                StockLine line = Line(s, 0);
                Assert.IsTrue(s.Api.SetPrice(line.InstanceId, line.CostBasis).IsSuccess);
                s.Api.AdvanceTime(s.CustomerQueue.PlanFor(1)[0].ArrivalMinute - s.Api.GetClock().MinuteOfDay);
                CustomerView v = s.Api.GetActiveCustomer();
                if (v == null || v.InstanceId == 0)
                {
                    continue;
                }

                Assert.IsTrue(s.Api.StartSale(v.CustomerId).IsSuccess);

                Assert.AreEqual("price.item_in_sale", s.Api.ClearPrice(line.InstanceId).ErrorCode);
                Assert.IsTrue(Line(s, 0).ListPrice.IsPositive, "satış sürerken fiyat korunur");
                return;
            }

            Assert.Fail("Uygun oturum bulunamadı.");
        }

        // ---------- arayüz akışı ----------

        [Test]
        public void TheRemoveButton_RemovesFromSale_KeepsThePanelOpen_AndShowsAnExplainingMessage()
        {
            GameSession s = WithTwoPricedPhones();
            StockLine line = Line(s, 0);
            using (UiFlow flow = OpenShelf(s))
            {
                flow.SelectShelfItem(line.InstanceId);

                Result r = flow.RemoveSelectedFromSale();

                Assert.IsTrue(r.IsSuccess, r.ErrorCode);
                Assert.AreEqual(TurkishTexts.ShelfRemoved(flow.Content.ModelName(line.DefinitionId)), flow.StatusMessage);
                Assert.IsFalse(Line(s, 0).ListPrice.IsPositive);
                Assert.IsNotNull(flow.ShelfScreen.Editor, "panel açık kalır: yeniden fiyatlanabilir");
                Assert.IsFalse(flow.ShelfScreen.Editor.CanRemoveFromSale);
                ShelfItemRowViewModel row = flow.ShelfScreen.Items.Single(x => x.InstanceId == line.InstanceId);
                Assert.IsFalse(row.IsSellable);
                Assert.AreEqual(TurkishTexts.ShelfNoPriceLine, row.PriceLine);
                Assert.AreEqual(2, flow.ShelfScreen.Items.Count, "raf yerleşimi/stok aynı");
                Assert.IsTrue(flow.ShelfScreen.Items.Single(x => x.InstanceId == Line(s, 1).InstanceId).IsSellable, "diğer ürün etkilenmedi");
            }
        }

        [Test]
        public void TheRemoveAction_IsRefusedWithoutASelection_OrOffTheShelfScreen()
        {
            GameSession s = WithTwoPricedPhones();
            using (UiFlow flow = new UiFlow(s.Api, new ContentPresentation(s.Content), s.Bus))
            {
                Assert.AreEqual("ui.no_shelf_item_selected", flow.RemoveSelectedFromSale().ErrorCode, "raf ekranında değil");
                flow.OpenShelf();
                Assert.AreEqual("ui.no_shelf_item_selected", flow.RemoveSelectedFromSale().ErrorCode, "seçim yok");
                Assert.IsTrue(Line(s, 0).ListPrice.IsPositive);
            }
        }

        // ---------- kayıt ----------

        [Test]
        public void RemovalAndRepricing_SurviveSaveAndLoad()
        {
            GameSession s = WithTwoPricedPhones();
            StockLine a = Line(s, 0);
            Assert.IsTrue(s.Api.ClearPrice(a.InstanceId).IsSuccess);

            GameSession r = GameSession.Restore(MarketHarness.RealContent(), s.Capture()).Value;

            Assert.AreEqual(s.Api.GetStateDigest(), r.Api.GetStateDigest());
            Assert.AreEqual(2, r.Api.GetInventory().Count, "telefon kayıpsız");
            Assert.IsFalse(r.Api.GetInventory().Single(l => l.InstanceId == a.InstanceId).ListPrice.IsPositive, "satış dışı kalır");
            Assert.AreEqual(a.CostBasis, r.Api.GetInventory().Single(l => l.InstanceId == a.InstanceId).CostBasis);
            Assert.IsTrue(r.Api.GetInventory().Single(l => l.InstanceId != a.InstanceId).ListPrice.IsPositive, "diğeri satışta");

            Assert.IsTrue(r.Api.SetPrice(a.InstanceId, a.CostBasis).IsSuccess);
            GameSession again = GameSession.Restore(MarketHarness.RealContent(), r.Capture()).Value;
            Assert.IsTrue(again.Api.GetInventory().Single(l => l.InstanceId == a.InstanceId).ListPrice.IsPositive, "yeniden satışa alma da kayıtlı");
            Assert.AreEqual(r.Api.GetStateDigest(), again.Api.GetStateDigest());
        }

        [Test]
        public void RemovingFromSale_DoesNotTouchTheRngStreams()
        {
            GameSession s = WithTwoPricedPhones();
            var rng = s.Capture().Rng;

            s.Api.ClearPrice(Line(s, 0).InstanceId);
            s.Api.SetPrice(Line(s, 0).InstanceId, Line(s, 0).CostBasis);
            s.Api.ClearPrice(Line(s, 0).InstanceId);

            Assert.IsNull(DeepCompare.FirstDifference(rng, s.Capture().Rng));
        }
    }
}
