using System.Collections.Generic;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Business;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Game;
using Esnaf.Domain.Time;
using Esnaf.Presentation;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Presentation
{
    /// <summary>
    /// Gün 12.7 — Raf ekranından fiyat belirleme (UiFlow): satın alınan telefon fiyatsızdır ve satılabilir stok sayılmaz; oyuncu ürüne dokunup fiyat seçer (maliyet, fiyat, tahmini kâr, marj görünür),
    /// geçerli fiyat IGameApi.SetPrice ile kaydedilir ve ürün satılabilir olur; sıfır/geçersiz fiyat kaydedilmez. Daha önce atlanan müşteriler geri gelmez.
    /// </summary>
    public class UiFlowShelfPricingTests
    {
        private static GameSession New(ulong seed = 1UL)
        {
            return GameSession.NewGame(MarketHarness.RealContent(), seed);
        }

        // Rafta fiyatsız (otomatik fiyat almamış) bir telefon.
        private static GameSession WithUnpricedPhone(ulong seed = 1UL, int bought = 1)
        {
            GameSession s = New(seed);
            foreach (var guided in s.Market.Listings.Take(bought).ToList())
            {
                Assert.IsTrue(s.Api.BuyListing(guided.ListingId).IsSuccess);
            }

            return s;
        }

        private static UiFlow Flow(GameSession s)
        {
            return new UiFlow(s.Api, new ContentPresentation(s.Content), s.Bus);
        }

        private static StockLine Line(GameSession s, int index = 0)
        {
            return s.Api.GetInventory()[index];
        }

        private static UiFlow OpenShelf(GameSession s)
        {
            UiFlow flow = Flow(s);
            Assert.IsTrue(flow.OpenShelf());
            return flow;
        }

        // ---------- fiyatsız stok ----------

        [Test]
        public void ABoughtPhone_GetsNoAutomaticPrice_AndIsNotSellableStock()
        {
            GameSession s = WithUnpricedPhone();

            Assert.IsFalse(Line(s).ListPrice.IsPositive, "otomatik satış fiyatı yok");
            Assert.IsFalse(s.Customers.HasSellableStock(), "fiyatsız telefon satılabilir stok değil");
            using (UiFlow flow = OpenShelf(s))
            {
                ShelfItemRowViewModel row = flow.ShelfScreen.Items.Single();
                Assert.IsFalse(row.IsSellable);
                Assert.AreEqual(TurkishTexts.ShelfNoPriceLine, row.PriceLine);
                Assert.AreEqual(Line(s).InstanceId, row.InstanceId);
                Assert.IsNull(flow.ShelfScreen.Editor, "dokunulmadan panel yok");
            }
        }

        [Test]
        public void TheShelfStillShowsCapacityOutOfFifteen()
        {
            GameSession s = WithUnpricedPhone();
            using (UiFlow flow = OpenShelf(s))
            {
                Assert.AreEqual("Doluluk: 1/15", flow.ShelfScreen.CapacityLine);
            }
        }

        // ---------- fiyat paneli ----------

        [Test]
        public void TappingAnItem_OpensThePanel_WithCost_ASuggestedPrice_ProfitAndMargin_ButSavesNothing()
        {
            GameSession s = WithUnpricedPhone();
            StockLine line = Line(s);
            using (UiFlow flow = OpenShelf(s))
            {
                Assert.IsTrue(flow.SelectShelfItem(line.InstanceId));

                ShelfPriceEditorViewModel editor = flow.ShelfScreen.Editor;
                long old = (long)System.Math.Ceiling(line.CostBasis.Tl * 1.2 / 100.0) * 100;
                long suggested = line.DemandCeiling.IsPositive ? System.Math.Min(old, line.DemandCeiling.Tl / 100 * 100) : old; // Gün 13.2: öneri müşteri tavanını aşmaz
                Assert.IsNotNull(editor);
                Assert.AreEqual(TurkishTexts.ShelfAcquisitionCost(line.CostBasis), editor.CostLine, "alış maliyeti");
                Assert.AreEqual(suggested, editor.Price.Tl, "başlangıç önerisi");
                Assert.AreEqual(TurkishTexts.ShelfEditPrice(Money.FromTl(suggested)), editor.PriceLine);
                Assert.AreEqual(TurkishTexts.ShelfProfit(Money.FromTl(suggested) - line.CostBasis), editor.ProfitLine, "tahmini kâr");
                Assert.AreEqual(TurkishTexts.ShelfMargin((suggested - line.CostBasis.Tl) * 100 / suggested), editor.MarginLine, "marj");
                Assert.AreEqual(TurkishTexts.ShelfNoSavedPrice, editor.SavedLine);
                Assert.IsTrue(editor.CanSave);
                Assert.IsFalse(Line(s).ListPrice.IsPositive, "öneri kaydedilmez: ürün hâlâ fiyatsız");
                Assert.IsFalse(s.Customers.HasSellableStock());
            }
        }

        [Test]
        public void TheStepper_MovesThePrice_AndRecomputesProfitAndMargin()
        {
            GameSession s = WithUnpricedPhone();
            StockLine line = Line(s);
            using (UiFlow flow = OpenShelf(s))
            {
                flow.SelectShelfItem(line.InstanceId);
                long start = flow.ShelfScreen.Editor.Price.Tl;

                Assert.IsTrue(flow.AdjustShelfPrice(1000));
                Assert.AreEqual(start + 1000, flow.ShelfScreen.Editor.Price.Tl);
                Assert.IsTrue(flow.AdjustShelfPrice(-100));
                Assert.AreEqual(start + 900, flow.ShelfScreen.Editor.Price.Tl);
                Assert.IsTrue(flow.SetShelfPrice(line.CostBasis.Tl + 1200));
                ShelfPriceEditorViewModel e = flow.ShelfScreen.Editor;
                Assert.AreEqual(TurkishTexts.ShelfProfit(Money.FromTl(1200)), e.ProfitLine);
                Assert.AreEqual(TurkishTexts.ShelfMargin(1200L * 100 / (line.CostBasis.Tl + 1200)), e.MarginLine);

                flow.AdjustShelfPrice(-100000000);
                Assert.AreEqual(10L, flow.ShelfScreen.Editor.Price.Tl, "en az 10 ₺");
            }
        }

        [Test]
        public void APriceBelowCost_ShowsALoss_WithANegativeMargin()
        {
            GameSession s = WithUnpricedPhone();
            StockLine line = Line(s);
            using (UiFlow flow = OpenShelf(s))
            {
                flow.SelectShelfItem(line.InstanceId);

                flow.SetShelfPrice(line.CostBasis.Tl - 800);

                ShelfPriceEditorViewModel e = flow.ShelfScreen.Editor;
                Assert.AreEqual(TurkishTexts.ShelfProfit(Money.FromTl(-800)), e.ProfitLine);
                StringAssert.StartsWith("Tahmini zarar", e.ProfitLine);
                Assert.Less(long.Parse(e.MarginLine.Replace("Marj: %", string.Empty)), 0L);
            }
        }

        [Test]
        public void TappingTheSameItemAgain_ClosesThePanel_AndBackClosesItBeforeLeavingTheShelf()
        {
            GameSession s = WithUnpricedPhone();
            long id = Line(s).InstanceId;
            using (UiFlow flow = OpenShelf(s))
            {
                flow.SelectShelfItem(id);
                flow.SelectShelfItem(id);
                Assert.IsNull(flow.ShelfScreen.Editor);

                flow.SelectShelfItem(id);
                flow.Back();
                Assert.AreEqual(UiScreen.Shelf, flow.CurrentScreen, "önce panel kapanır");
                Assert.IsNull(flow.ShelfScreen.Editor);
                flow.Back();
                Assert.AreEqual(UiScreen.Listings, flow.CurrentScreen);
            }
        }

        // ---------- kaydetme ----------

        [Test]
        public void SavingAValidPrice_UsesSetPrice_AndTheItemBecomesSellable()
        {
            GameSession s = WithUnpricedPhone();
            StockLine line = Line(s);
            using (UiFlow flow = OpenShelf(s))
            {
                flow.SelectShelfItem(line.InstanceId);
                flow.SetShelfPrice(9500);

                Result saved = flow.SaveShelfPrice();

                Assert.IsTrue(saved.IsSuccess, saved.ErrorCode);
                Assert.AreEqual(Money.FromTl(9500), Line(s).ListPrice);
                Assert.IsTrue(s.Customers.HasSellableStock(), "artık satılabilir stok");
                Assert.IsTrue(flow.ShelfScreen.Items.Single().IsSellable);
                Assert.AreEqual(TurkishTexts.ShelfSellableLine(Money.FromTl(9500)), flow.ShelfScreen.Items.Single().PriceLine);
                Assert.IsNull(flow.ShelfScreen.Editor, "kayıttan sonra panel kapanır");
                Assert.AreEqual(TurkishTexts.ShelfPriceSaved(flow.Content.ModelName(line.DefinitionId), Money.FromTl(9500)), flow.StatusMessage);
            }
        }

        [Test]
        public void ARepricedItem_StartsThePanelAtItsSavedPrice()
        {
            GameSession s = WithUnpricedPhone();
            long id = Line(s).InstanceId;
            Assert.IsTrue(s.Api.SetPrice(id, Money.FromTl(8800)).IsSuccess);
            using (UiFlow flow = OpenShelf(s))
            {
                flow.SelectShelfItem(id);

                Assert.AreEqual(8800L, flow.ShelfScreen.Editor.Price.Tl);
                Assert.AreEqual(TurkishTexts.ShelfSavedPrice(Money.FromTl(8800)), flow.ShelfScreen.Editor.SavedLine);
            }
        }

        [TestCase(0L)]
        [TestCase(15L)]
        [TestCase(1000000010L)]
        public void AZeroOrInvalidPrice_IsNotSaved(long price)
        {
            GameSession s = WithUnpricedPhone();
            long id = Line(s).InstanceId;
            string digest = s.Api.GetStateDigest();
            using (UiFlow flow = OpenShelf(s))
            {
                flow.SelectShelfItem(id);
                flow.SetShelfPrice(price);
                Assert.IsFalse(flow.ShelfScreen.Editor.CanSave);
                Assert.AreEqual(string.Empty, flow.ShelfScreen.Editor.ProfitLine);

                Result saved = flow.SaveShelfPrice();

                Assert.AreEqual("price.invalid", saved.ErrorCode);
                Assert.AreEqual(TurkishTexts.PriceError("price.invalid"), flow.StatusMessage);
                Assert.IsFalse(Line(s).ListPrice.IsPositive);
                Assert.IsFalse(s.Customers.HasSellableStock());
                Assert.AreEqual(digest, s.Api.GetStateDigest(), "durum değişmedi");
                Assert.IsNotNull(flow.ShelfScreen.Editor, "panel açık kalır");
            }
        }

        [Test]
        public void TheShelfActions_AreRefusedOffTheShelfScreen_ForUnknownItems_AndWithoutASelection()
        {
            GameSession s = WithUnpricedPhone();
            using (UiFlow flow = Flow(s))
            {
                Assert.IsFalse(flow.SelectShelfItem(Line(s).InstanceId), "raf ekranında değil");
                Assert.IsTrue(flow.OpenShelf());
                Assert.IsFalse(flow.SelectShelfItem(987654L), "bilinmeyen ürün");
                Assert.IsFalse(flow.AdjustShelfPrice(100));
                Assert.IsFalse(flow.SetShelfPrice(500));
                Assert.AreEqual("ui.no_shelf_item_selected", flow.SaveShelfPrice().ErrorCode);
            }
        }

        // ---------- müşteri talebine etkisi ----------

        [Test]
        public void PricingMidDay_MakesTheItemRequestable_ButSkippedCustomersNeverComeBack()
        {
            GameSession s = WithUnpricedPhone();
            var arrived = new List<long>();
            using (s.Bus.Subscribe<CustomerArrived>(e => arrived.Add(e.CustomerId)))
            using (UiFlow flow = OpenShelf(s))
            {
                IReadOnlyList<QueuedCustomer> plan = s.CustomerQueue.PlanFor(s.Time.Day);
                int pricedAt = plan[2].ArrivalMinute + 1;
                s.Api.AdvanceTime(pricedAt - s.Api.GetClock().MinuteOfDay);
                Assert.AreEqual(0, arrived.Count, "fiyatsızken kimse gelmez");

                flow.SelectShelfItem(Line(s).InstanceId);
                flow.SetShelfPrice(Line(s).CostBasis.Tl);
                Assert.IsTrue(flow.SaveShelfPrice().IsSuccess);
                s.Api.AdvanceTime(StoreHours.CloseMinute - s.Api.GetClock().MinuteOfDay);

                Assert.Greater(arrived.Count, 0, "fiyat girilince sonraki müşteriler gelir");
                foreach (long id in arrived)
                {
                    Assert.IsTrue(plan.Skip(3).Any(p => p.CustomerId == id), "yalnızca fiyattan sonra gelenler");
                }

                Assert.IsFalse(arrived.Contains(plan[0].CustomerId) || arrived.Contains(plan[1].CustomerId) || arrived.Contains(plan[2].CustomerId), "atlananlar geri gelmez");
            }
        }

        [Test]
        public void TheFifteenSlotShelf_TakesFifteenPhones_AndEachIsPricedOnItsOwn()
        {
            GameSession s = New(3UL);
            int guard = 0;
            while (s.Api.GetInventory().Count < 15 && guard++ < 20)
            {
                foreach (var l in s.Api.GetListings().ToList())
                {
                    if (s.Api.GetInventory().Count < 15)
                    {
                        Assert.IsTrue(s.Api.BuyListing(l.ListingId).IsSuccess);
                    }
                }

                if (s.Api.GetInventory().Count < 15)
                {
                    Assert.IsTrue(s.Api.EndDay().IsSuccess);
                }
            }

            Assert.AreEqual(15, s.Api.GetInventory().Count);
            using (UiFlow flow = OpenShelf(s))
            {
                long id = Line(s, 7).InstanceId;
                flow.SelectShelfItem(id);
                flow.SetShelfPrice(Line(s, 7).CostBasis.Tl + 700);
                Assert.IsTrue(flow.SaveShelfPrice().IsSuccess);

                Assert.AreEqual(1, s.Api.GetInventory().Count(x => x.ListPrice.IsPositive), "yalnızca fiyatlanan ürün satılabilir");
                Assert.IsTrue(flow.ShelfScreen.Items.Single(r => r.InstanceId == id).IsSellable);
                Assert.AreEqual("Doluluk: 15/15", flow.ShelfScreen.CapacityLine);
            }
        }
        // ---------- öneri müşteri tavanını aşmaz (E13 Pro) ----------

        // Sahne tohumuyla ilk ilan (Elma E13 Pro, alış 31.650 ₺): eski öneri 38.000 ₺ tavanı (≈34.304 ₺) aşıyordu ve hiç müşteri gelmiyordu.
        private static GameSession WithE13()
        {
            GameSession s = GameSession.NewGame(MarketHarness.RealContent(), 20260101UL);
            var first = s.Api.GetListings()[0];
            Assert.AreEqual("phone.elma_e13_pro", first.DefinitionId);
            Assert.IsTrue(s.Api.BuyListing(first.ListingId).IsSuccess);
            return s;
        }

        [Test]
        public void E13Pro_TheSuggestionStaysWithinTheDemandCeiling_AndDoesNotDisableTheCustomers()
        {
            GameSession s = WithE13();
            StockLine line = Line(s);
            Assert.AreEqual(Money.FromTl(31650), line.CostBasis);
            Assert.IsTrue(line.DemandCeiling.IsPositive, "müşteri tavanı bilinir");
            Assert.AreEqual(34300L, line.DemandCeiling.Tl, "1,15 × Max (29.830) ≈ 34.304 → 10 ₺'ye aşağı");
            using (UiFlow flow = OpenShelf(s))
            {
                flow.SelectShelfItem(line.InstanceId);

                long suggested = flow.ShelfScreen.Editor.Price.Tl;

                Assert.AreEqual(34300L, suggested, "eski öneri 38.000 ₺ idi");
                Assert.LessOrEqual(suggested, line.DemandCeiling.Tl);
                Assert.AreEqual(0L, suggested % 10, "geçerli fiyat adımı");
                Assert.AreEqual(0L, suggested % UiFlow.ShelfPriceStep, "100 ₺'lik adıma uyar");
                Assert.IsNull(flow.ShelfScreen.Editor.Warning, "önerilen fiyatta uyarı yok");
                Assert.IsFalse(Line(s).ListPrice.IsPositive, "öneri kaydedilmiş fiyat değil");

                Assert.IsTrue(flow.SaveShelfPrice().IsSuccess);

                Assert.IsTrue(s.Customers.HasSellableStock());
                foreach (QueuedCustomer c in s.CustomerQueue.PlanFor(1))
                {
                    QueuedCustomer qc;
                    CustomerSlot slot;
                    Assert.IsTrue(s.CustomerQueue.TryResolve(c.CustomerId, out qc, out slot));
                    Assert.AreNotEqual(0L, s.Customers.FindInterest(slot), "öneriyi kaydetmek müşteriyi devre dışı bırakmaz: " + c.ArrivalText);
                }
            }
        }

        [Test]
        public void ThePanel_ShowsTheCeiling_AndWarnsOnlyAboveIt()
        {
            GameSession s = WithE13();
            StockLine line = Line(s);
            using (UiFlow flow = OpenShelf(s))
            {
                flow.SelectShelfItem(line.InstanceId);
                Assert.AreEqual(TurkishTexts.ShelfCeilingLine(line.DemandCeiling), flow.ShelfScreen.Editor.CeilingLine);

                flow.SetShelfPrice(line.DemandCeiling.Tl);
                Assert.IsNull(flow.ShelfScreen.Editor.Warning, "tavanın kendisinde uyarı yok");

                flow.AdjustShelfPrice(ShelfStepUp());

                Assert.AreEqual("Bu fiyatın üzerinde müşteriler bu telefonu pahalı bulabilir.", flow.ShelfScreen.Editor.Warning);
                Assert.AreEqual(TurkishTexts.ShelfExpensiveWarning, flow.ShelfScreen.Editor.Warning);
                Assert.IsTrue(flow.ShelfScreen.Editor.CanSave, "uyarı kaydı engellemez; karar oyuncunundur");
            }
        }

        private static long ShelfStepUp()
        {
            return UiFlow.ShelfPriceStep;
        }

        [Test]
        public void TheCeilingIsOnlyInformation_ItDoesNotChangeTheGameState_OrTheEligibilityRules()
        {
            GameSession s = WithE13();
            long id = Line(s).InstanceId;
            Assert.IsTrue(s.Api.SetPrice(id, Money.FromTl(38000)).IsSuccess, "tavanın üstünde de kaydedilebilir (kural değişmedi)");
            string digest = s.Api.GetStateDigest();

            for (int i = 0; i < 3; i++)
            {
                s.Api.GetInventory();
            }

            Assert.AreEqual(digest, s.Api.GetStateDigest(), "tavan hesabı durumu değiştirmez");
            foreach (QueuedCustomer c in s.CustomerQueue.PlanFor(1))
            {
                QueuedCustomer qc;
                CustomerSlot slot;
                s.CustomerQueue.TryResolve(c.CustomerId, out qc, out slot);
                Assert.AreEqual(0L, s.Customers.FindInterest(slot), "38.000 ₺ hâlâ çok pahalı: IsEligible değişmedi");
            }
        }

        [Test]
        public void WhenNoCustomerTypeFitsTheItem_TheOldSuggestionRemains_AndThereIsNoCeiling()
        {
            GameSession s = WithE13();
            long id = Line(s).InstanceId;
            // bilinen NPC'lerin hepsi ürünü oyuncuya satmış sayılsın: uygun müşteri tipi kalmaz
            foreach (string npc in new[] { "npc.kemal", "npc.selin" })
            {
                s.Npcs.RecordSoldToPlayer(npc, id);
            }

            StockLine line = Line(s);
            using (UiFlow flow = OpenShelf(s))
            {
                flow.SelectShelfItem(id);

                Assert.IsFalse(line.DemandCeiling.IsPositive, "uygun müşteri tipi yok: tavan bilinmiyor");
                Assert.IsNull(flow.ShelfScreen.Editor.CeilingLine);
                Assert.IsNull(flow.ShelfScreen.Editor.Warning);
                Assert.AreEqual((long)System.Math.Ceiling(line.CostBasis.Tl * 1.2 / 100.0) * 100, flow.ShelfScreen.Editor.Price.Tl, "eski öneri");
            }
        }
    }
}
