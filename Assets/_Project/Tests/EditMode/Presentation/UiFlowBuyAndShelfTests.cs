using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Game;
using Esnaf.Domain.Market;
using Esnaf.Presentation;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Presentation
{
    /// <summary>
    /// Doğrudan satın alma ("Satın Al": IGameApi.BuyListing) ve salt okunur Raf ekranı (Gün 10 Adım 6). Para ve raf durumu hep API'dendir;
    /// testler UiFlow'un komutu doğru gönderdiğini ve dönen durumu doğru gösterdiğini doğrular.
    /// </summary>
    public class UiFlowBuyAndShelfTests
    {
        private static GameSession NewSession(ulong seed = 1UL)
        {
            return GameSession.NewGame(MarketHarness.RealContent(), seed);
        }

        private static UiFlow Flow(GameSession session)
        {
            return new UiFlow(session.Api, new ContentPresentation(session.Content), session.Bus);
        }

        private static UiFlow OpenDetail(GameSession session, int index = 0)
        {
            UiFlow flow = Flow(session);
            Assert.IsTrue(flow.OpenListing(flow.Listings[index].ListingId));
            return flow;
        }

        // ---------- satın al ----------

        [Test]
        public void TheDetail_ShowsTheBuyButtonWithThePrice()
        {
            GameSession session = NewSession();
            using (UiFlow flow = OpenDetail(session))
            {
                Money price = session.Api.GetListings()[0].AskingPrice;

                Assert.AreEqual("Satın Al — " + MoneyFormatter.Format(price), flow.Detail.BuyButtonText);
            }
        }

        [Test]
        public void BuyNow_BuysThroughTheApi_AndShowsThePurchase()
        {
            GameSession session = NewSession();
            using (UiFlow flow = OpenDetail(session))
            {
                ListingView listing = session.Api.GetListings()[0];
                Money cash = session.Api.GetCash();

                Result<Money> result = flow.BuyNow();

                Assert.IsTrue(result.IsSuccess, result.ErrorCode);
                Assert.AreEqual(listing.AskingPrice, result.Value);
                Assert.AreEqual(cash - listing.AskingPrice, session.Api.GetCash(), "para API'den düştü");
                Assert.AreEqual(TurkishTexts.Cash(session.Api.GetCash()), flow.TopBar.CashText);
                Assert.AreEqual(1, session.Api.GetInventory().Count);
                Assert.AreEqual(listing.InstanceId, session.Api.GetInventory()[0].InstanceId);
                Assert.IsFalse(flow.Listings.Any(r => r.ListingId == listing.ListingId));
                Assert.AreEqual(UiScreen.Listings, flow.CurrentScreen);
                Assert.IsNull(flow.SelectedListingId);
                Assert.AreEqual(
                    "Satın alındı: " + session.Content.GetProduct(listing.DefinitionId).Name + " — " + MoneyFormatter.Format(listing.AskingPrice) + ". Rafa eklendi (1/6).",
                    flow.StatusMessage);
            }
        }

        [Test]
        public void TheScreenIsToldTheFinalMessage_AfterABuy_AndAfterAFailedBuy()
        {
            GameSession session = NewSession();
            using (UiFlow flow = OpenDetail(session))
            {
                string seen = null;
                flow.Changed += () => seen = flow.StatusMessage;

                flow.BuyNow();

                StringAssert.StartsWith("Satın alındı: ", seen);
            }

            GameSession poor = GameSession.NewGame(ContentVariants.WithOpeningCapital(1000), 1UL);
            using (UiFlow flow = OpenDetail(poor))
            {
                string seen = null;
                flow.Changed += () => seen = flow.StatusMessage;

                flow.BuyNow();

                Assert.AreEqual("Yeterli nakit yok.", seen);
            }
        }

        [Test]
        public void ABoughtListing_CannotBeOpenedOrBoughtAgain()
        {
            GameSession session = NewSession();
            using (UiFlow flow = OpenDetail(session))
            {
                long id = flow.SelectedListingId.Value;
                flow.BuyNow();
                Money cash = session.Api.GetCash();

                Assert.IsFalse(flow.OpenListing(id));
                Assert.AreEqual("Bu ilan artık yok.", flow.StatusMessage);
                Assert.AreEqual("listing.unknown", session.Api.BuyListing(id).ErrorCode);
                Assert.AreEqual(cash, session.Api.GetCash());
                Assert.AreEqual(1, session.Api.GetInventory().Count);
            }
        }

        [Test]
        public void NotEnoughCash_IsExplained_AndNothingChanges()
        {
            GameSession session = GameSession.NewGame(ContentVariants.WithOpeningCapital(1000), 1UL);
            using (UiFlow flow = OpenDetail(session))
            {
                string digest = session.Api.GetStateDigest();

                Result<Money> result = flow.BuyNow();

                Assert.IsTrue(result.IsFailure);
                Assert.AreEqual("cash.insufficient", result.ErrorCode);
                Assert.AreEqual("Yeterli nakit yok.", flow.StatusMessage);
                Assert.AreEqual(UiScreen.Detail, flow.CurrentScreen);
                Assert.AreEqual(digest, session.Api.GetStateDigest());
            }
        }

        [Test]
        public void WhileANegotiationIsOpen_BuyNowIsRefusedByTheApi_WithTheTurkishMessage()
        {
            GameSession session = NewSession();
            using (UiFlow flow = OpenDetail(session, 0))
            {
                Assert.IsTrue(flow.OpenNegotiation());
                flow.Back();
                string digest = session.Api.GetStateDigest();

                Result<Money> result = flow.BuyNow();

                Assert.IsTrue(result.IsFailure);
                Assert.AreEqual("negotiation.in_progress", result.ErrorCode);
                Assert.AreEqual("Önce süren pazarlığı bitirmelisin.", flow.StatusMessage);
                Assert.AreEqual(UiScreen.Detail, flow.CurrentScreen);
                Assert.AreEqual(digest, session.Api.GetStateDigest());
                Assert.IsNotNull(session.Api.GetNegotiation());
            }
        }

        [Test]
        public void AFullShelf_IsExplained()
        {
            GameSession session = NewSession();
            using (UiFlow flow = Flow(session))
            {
                for (int day = 0; day < 10 && session.Api.GetInventory().Count < 6; day++)
                {
                    foreach (ListingView listing in session.Api.GetListings().ToList())
                    {
                        if (session.Api.GetInventory().Count < 6)
                        {
                            Assert.IsTrue(session.Api.BuyListing(listing.ListingId).IsSuccess);
                        }
                    }

                    if (session.Api.GetInventory().Count < 6)
                    {
                        Assert.IsTrue(flow.EndDay().IsSuccess);
                    }
                }

                Assert.AreEqual(6, session.Api.GetInventory().Count);
                Assert.IsTrue(flow.EndDay().IsSuccess);
                flow.OpenListing(flow.Listings[0].ListingId);
                string digest = session.Api.GetStateDigest();

                Result<Money> result = flow.BuyNow();

                Assert.AreEqual("inventory.full", result.ErrorCode);
                Assert.AreEqual("Raf dolu.", flow.StatusMessage);
                Assert.AreEqual(digest, session.Api.GetStateDigest());
            }
        }

        [Test]
        public void BuyNow_OutsideTheDetailScreen_IsRefused_AndChangesNothing()
        {
            GameSession session = NewSession();
            using (UiFlow flow = Flow(session))
            {
                string digest = session.Api.GetStateDigest();
                int raised = 0;
                flow.Changed += () => raised++;

                Result<Money> onListings = flow.BuyNow();
                Assert.AreEqual("ui.not_on_detail_screen", onListings.ErrorCode);

                flow.OpenListing(flow.Listings[0].ListingId);
                flow.OpenAppraisal();
                raised = 0;
                Result<Money> onAppraisal = flow.BuyNow();
                Assert.AreEqual("ui.not_on_detail_screen", onAppraisal.ErrorCode);
                Assert.AreEqual(0, raised);
                Assert.AreEqual(digest, session.Api.GetStateDigest());
            }
        }

        [Test]
        public void ThePaidAppraisalFee_IsInTheShelfCost_AsTheApiSays()
        {
            GameSession session = NewSession();
            for (int i = 0; i < 2; i++)
            {
                session.Api.EndDay();
            }

            using (UiFlow flow = OpenDetail(session))
            {
                long id = flow.SelectedListingId.Value;
                Money fee = session.Api.StartAppraisal(id, "s1").Value.Fee;
                Money price = session.Api.GetListings().Single(l => l.ListingId == id).AskingPrice;

                Assert.IsTrue(flow.BuyNow().IsSuccess);

                Assert.IsTrue(flow.OpenShelf());
                Assert.AreEqual("Maliyet: " + MoneyFormatter.Format(price + fee), flow.ShelfScreen.Items[0].CostLine);
            }
        }

        // ---------- raf ----------

        [Test]
        public void TheShelfButton_ShowsTheFillFromTheApi()
        {
            GameSession session = NewSession();
            using (UiFlow flow = Flow(session))
            {
                Assert.AreEqual("Raf (0/6)", flow.ShelfButtonText);

                session.Api.BuyListing(flow.Listings[0].ListingId);
                Assert.AreEqual("Raf (1/6)", flow.ShelfButtonText);

                session.Api.BuyListing(flow.Listings[0].ListingId);
                Assert.AreEqual("Raf (2/6)", flow.ShelfButtonText);
            }
        }

        [Test]
        public void OpenShelf_ShowsAnEmptyShelf_Politely()
        {
            GameSession session = NewSession();
            using (UiFlow flow = Flow(session))
            {
                int raised = 0;
                flow.Changed += () => raised++;

                Assert.IsTrue(flow.OpenShelf());

                Assert.AreEqual(UiScreen.Shelf, flow.CurrentScreen);
                Assert.AreEqual(1, raised);
                ShelfScreenViewModel shelf = flow.ShelfScreen;
                Assert.AreEqual("Raf", shelf.Title);
                Assert.AreEqual("Doluluk: 0/6", shelf.CapacityLine);
                Assert.AreEqual(0, shelf.Items.Count);
                Assert.AreEqual("Rafta ürün yok.", shelf.EmptyNote);
            }
        }

        [Test]
        public void TheShelf_ListsTheApiStock_WithModelNamesAndCostBasis()
        {
            GameSession session = NewSession();
            using (UiFlow flow = Flow(session))
            {
                session.Api.BuyListing(flow.Listings[0].ListingId);
                session.Api.BuyListing(flow.Listings[1].ListingId);

                Assert.IsTrue(flow.OpenShelf());

                var stock = session.Api.GetInventory();
                ShelfScreenViewModel shelf = flow.ShelfScreen;
                Assert.AreEqual("Doluluk: 2/6", shelf.CapacityLine);
                Assert.AreEqual(stock.Count, shelf.Items.Count);
                for (int i = 0; i < stock.Count; i++)
                {
                    Assert.AreEqual(session.Content.GetProduct(stock[i].DefinitionId).Name, shelf.Items[i].Title);
                    Assert.AreEqual("Maliyet: " + MoneyFormatter.Format(stock[i].CostBasis), shelf.Items[i].CostLine);
                }

                Assert.IsNull(shelf.EmptyNote);
            }
        }

        [Test]
        public void TheShelf_FollowsAnExternalPurchase_WhileItIsOpen()
        {
            GameSession session = NewSession();
            using (UiFlow flow = Flow(session))
            {
                flow.OpenShelf();
                Assert.AreEqual(0, flow.ShelfScreen.Items.Count);

                session.Api.BuyListing(session.Api.GetListings()[0].ListingId);

                Assert.AreEqual(1, flow.ShelfScreen.Items.Count);
                Assert.AreEqual("Doluluk: 1/6", flow.ShelfScreen.CapacityLine);
            }
        }

        [Test]
        public void Back_FromTheShelf_ReturnsToTheListings_AndTheShelfScreenGoesAway()
        {
            using (UiFlow flow = Flow(NewSession()))
            {
                flow.OpenShelf();

                flow.Back();

                Assert.AreEqual(UiScreen.Listings, flow.CurrentScreen);
                Assert.IsNull(flow.ShelfScreen);
            }
        }

        [Test]
        public void OpeningTheShelf_ClearsAnEarlierMessage()
        {
            using (UiFlow flow = Flow(NewSession()))
            {
                flow.SelectListing(-5);
                Assert.IsNotNull(flow.StatusMessage);

                flow.OpenShelf();

                Assert.IsNull(flow.StatusMessage);
            }
        }

        [Test]
        public void OpenShelf_OnlyFromTheListings()
        {
            GameSession session = NewSession();
            using (UiFlow flow = Flow(session))
            {
                flow.OpenListing(flow.Listings[0].ListingId);
                int raised = 0;
                flow.Changed += () => raised++;

                Assert.IsFalse(flow.OpenShelf());

                Assert.AreEqual(UiScreen.Detail, flow.CurrentScreen);
                Assert.IsNull(flow.ShelfScreen);
                Assert.AreEqual(0, raised);
            }
        }

        [Test]
        public void TheShelfScreen_KeepsTheListingSelection_ForWhenTheUserGoesBack()
        {
            using (UiFlow flow = Flow(NewSession()))
            {
                long id = flow.Listings[1].ListingId;
                flow.SelectListing(id);

                flow.OpenShelf();
                flow.Back();

                Assert.AreEqual(id, flow.SelectedListingId);
                Assert.IsTrue(flow.Listings.Single(r => r.ListingId == id).IsSelected);
            }
        }

        [Test]
        public void TheShelfLinesAreShortEnoughToWrap()
        {
            GameSession session = NewSession();
            using (UiFlow flow = Flow(session))
            {
                foreach (ListingView l in session.Api.GetListings().ToList())
                {
                    session.Api.BuyListing(l.ListingId);
                }

                flow.OpenShelf();

                foreach (ShelfItemRowViewModel item in flow.ShelfScreen.Items)
                {
                    Assert.LessOrEqual(item.Title.Length, 60);
                    Assert.LessOrEqual(item.CostLine.Length, 60);
                }
            }
        }
    }
}
