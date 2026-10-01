using System;
using System.Collections.Generic;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Market;
using Esnaf.Domain.Game;
using Esnaf.Presentation;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Presentation
{
    public class UiFlowTests
    {
        private static GameSession NewSession()
        {
            return GameSession.NewGame(MarketHarness.RealContent(), 1UL);
        }

        private static UiFlow Flow(GameSession session)
        {
            return new UiFlow(session.Api, new ContentPresentation(session.Content), session.Bus);
        }

        [Test]
        public void ANewGame_ShowsDayOneAndTheOpeningCash()
        {
            using (UiFlow flow = Flow(NewSession()))
            {
                Assert.AreEqual("Gün 1", flow.TopBar.DayText);
                Assert.AreEqual("Nakit: 250.000 ₺", flow.TopBar.CashText);
                Assert.AreEqual(UiScreen.Listings, flow.CurrentScreen);
            }
        }

        [Test]
        public void TheTopBar_FollowsTheGame_WithoutAManualRefresh()
        {
            GameSession session = NewSession();
            using (UiFlow flow = Flow(session))
            {
                Assert.IsTrue(session.Api.EndDay().IsSuccess);

                Assert.AreEqual("G\u00FCn 2", flow.TopBar.DayText);
                Assert.AreEqual(TurkishTexts.Cash(session.Api.GetCash()), flow.TopBar.CashText);
            }
        }

        [Test]
        public void TheTopBarCash_FollowsCashChanges_WithoutAManualRefresh()
        {
            GameSession session = NewSession();
            using (UiFlow flow = Flow(session))
            {
                for (int i = 0; i < 12 && session.Api.GetCash() == Money.FromTl(250000); i++)
                {
                    Assert.IsTrue(session.Api.EndDay().IsSuccess);
                }

                Assert.AreNotEqual(Money.FromTl(250000), session.Api.GetCash(), "birka\u00E7 g\u00FCn i\u00E7inde g\u00FCnl\u00FCk gider nakdi de\u011Fi\u015Ftirmeli");
                Assert.AreEqual(TurkishTexts.Cash(session.Api.GetCash()), flow.TopBar.CashText);
                Assert.AreEqual(TurkishTexts.Day(session.Api.GetDay()), flow.TopBar.DayText);
            }
        }

        [Test]
        public void Refresh_ReadsTheCurrentStateAndRaisesChangedOnce()
        {
            GameSession session = NewSession();
            using (UiFlow flow = new UiFlow(session.Api, new ContentPresentation(session.Content)))
            {
                session.Api.EndDay();
                Assert.AreEqual("Gün 1", flow.TopBar.DayText, "olay yolu verilmedi: kendi kendine güncellenmez");
                int raised = 0;
                flow.Changed += () => raised++;

                flow.Refresh();

                Assert.AreEqual(1, raised);
                Assert.AreEqual("Gün 2", flow.TopBar.DayText);
                Assert.AreEqual(TurkishTexts.Cash(session.Api.GetCash()), flow.TopBar.CashText);
            }
        }

        [Test]
        public void AfterDispose_TheFlowStopsFollowingTheGame()
        {
            GameSession session = NewSession();
            UiFlow flow = Flow(session);
            flow.Dispose();
            int raised = 0;
            flow.Changed += () => raised++;

            session.Api.EndDay();

            Assert.AreEqual("Gün 1", flow.TopBar.DayText);
            Assert.AreEqual(0, raised);
            Assert.AreEqual(0, ((EventBus)session.Bus).SubscriberCount<Esnaf.Domain.Economy.CashChanged>());
            Assert.AreEqual(0, ((EventBus)session.Bus).SubscriberCount<Esnaf.Domain.Time.DayStarted>());
            flow.Dispose();
        }

        [Test]
        public void AfterDispose_RefreshDoesNothing()
        {
            GameSession session = NewSession();
            var flow = new UiFlow(session.Api, new ContentPresentation(session.Content));
            int raised = 0;
            flow.Changed += () => raised++;
            flow.Dispose();
            session.Api.EndDay();

            flow.Refresh();

            Assert.AreEqual(0, raised);
            Assert.AreEqual("G\u00FCn 1", flow.TopBar.DayText);
        }

        [Test]
        public void TheFlow_SubscribesToTheEventsThatChangeWhatItShows()
        {
            GameSession session = NewSession();
            var bus = (EventBus)session.Bus;
            using (Flow(session))
            {
                Assert.AreEqual(1, bus.SubscriberCount<Esnaf.Domain.Economy.CashChanged>());
                Assert.AreEqual(1, bus.SubscriberCount<Esnaf.Domain.Time.DayStarted>());
                Assert.AreEqual(1, bus.SubscriberCount<ListingsGenerated>());
                Assert.AreEqual(1, bus.SubscriberCount<ListingExpired>());
                Assert.AreEqual(1, bus.SubscriberCount<Esnaf.Domain.Negotiation.ListingPurchased>());
            }

            Assert.AreEqual(0, bus.SubscriberCount<ListingsGenerated>());
            Assert.AreEqual(0, bus.SubscriberCount<ListingExpired>());
            Assert.AreEqual(0, bus.SubscriberCount<Esnaf.Domain.Negotiation.ListingPurchased>());
        }

        [Test]
        public void Changed_IsRaisedWhenTheGameChanges()
        {
            GameSession session = NewSession();
            using (UiFlow flow = Flow(session))
            {
                int raised = 0;
                flow.Changed += () => raised++;

                session.Api.EndDay();

                Assert.Greater(raised, 0);
            }
        }

        [Test]
        public void TheTopBarViewModel_HasValueEquality()
        {
            var a = new TopBarViewModel("Gün 1", "Nakit: 5 ₺");

            Assert.IsTrue(a.Equals(new TopBarViewModel("Gün 1", "Nakit: 5 ₺")));
            Assert.IsFalse(a.Equals(new TopBarViewModel("Gün 2", "Nakit: 5 ₺")));
            Assert.IsFalse(a.Equals(new TopBarViewModel("Gün 1", "Nakit: 6 ₺")));
            Assert.IsFalse(a.Equals(null));
            Assert.AreEqual(a.GetHashCode(), new TopBarViewModel("Gün 1", "Nakit: 5 ₺").GetHashCode());
        }

        [Test]
        public void TheConstructor_ChecksItsArguments()
        {
            GameSession session = NewSession();
            var content = new ContentPresentation(session.Content);

            Assert.Throws<ArgumentNullException>(() => new UiFlow(null, content));
            Assert.Throws<ArgumentNullException>(() => new UiFlow(session.Api, null));
            Assert.Throws<ArgumentNullException>(() => new TopBarViewModel(null, "x"));
            Assert.Throws<ArgumentNullException>(() => new TopBarViewModel("x", null));
        }

        // ---------- ilanlar ----------

        [Test]
        public void TheListings_AreTheApiListings_InTheSameOrder_WithTurkishTexts()
        {
            GameSession session = NewSession();
            using (UiFlow flow = Flow(session))
            {
                IReadOnlyList<ListingView> api = session.Api.GetListings();

                Assert.Greater(api.Count, 0);
                Assert.AreEqual(api.Count, flow.Listings.Count);
                for (int i = 0; i < api.Count; i++)
                {
                    ListingView l = api[i];
                    ListingRowViewModel row = flow.Listings[i];
                    Assert.AreEqual(l.ListingId, row.ListingId);
                    Assert.AreEqual(session.Content.GetProduct(l.DefinitionId).Name, row.Title);
                    Assert.AreEqual(l.StorageGb + " GB", row.StorageText);
                    Assert.AreEqual(l.AgeMonths + " ay", row.AgeText);
                    Assert.AreEqual("\u0130stenen: " + MoneyFormatter.Format(l.AskingPrice), row.PriceText);
                    Assert.AreEqual(l.HasBox ? "Kutu: var" : "Kutu: yok", row.BoxText);
                    Assert.AreEqual(l.HasInvoice ? "Fatura: var" : "Fatura: yok", row.InvoiceText);
                    Assert.AreEqual("Sat\u0131c\u0131: " + session.Content.GetNpc(l.SellerNpcId).Name, row.SellerText);
                    Assert.AreEqual(l.RemainingDays <= 1 ? "Son g\u00FCn" : l.RemainingDays + " g\u00FCn kald\u0131", row.RemainingText);
                }
            }
        }

        [Test]
        public void Seed1_DayOne_ShowsTheKnownListings_AsGoldenRegression()
        {
            using (UiFlow flow = Flow(NewSession()))
            {
                string[] rows = flow.Listings.Select(r => string.Join(" | ", new[] { r.ListingId.ToString(), r.Title, r.StorageText, r.AgeText, r.PriceText, r.RemainingText, r.BoxText, r.InvoiceText, r.SellerText })).ToArray();

                CollectionAssert.AreEqual(new[]
                {
                    "1 | Nova N3 Pro | 256 GB | 26 ay | \u0130stenen: 12.750 \u20BA | 4 g\u00FCn kald\u0131 | Kutu: var | Fatura: var | Sat\u0131c\u0131: Ay\u015Fe Han\u0131m",
                    "2 | Y\u0131ld\u0131z Y5 | 64 GB | 34 ay | \u0130stenen: 3.850 \u20BA | 4 g\u00FCn kald\u0131 | Kutu: yok | Fatura: yok | Sat\u0131c\u0131: Ay\u015Fe Han\u0131m",
                    "3 | Y\u0131ld\u0131z Y5 | 64 GB | 24 ay | \u0130stenen: 5.800 \u20BA | 4 g\u00FCn kald\u0131 | Kutu: yok | Fatura: yok | Sat\u0131c\u0131: Ay\u015Fe Han\u0131m"
                }, rows);
            }
        }

        [Test]
        public void NothingIsSelectedAtFirst()
        {
            using (UiFlow flow = Flow(NewSession()))
            {
                Assert.IsNull(flow.SelectedListingId);
                Assert.IsNull(flow.SelectedListing);
                Assert.IsTrue(flow.Listings.All(r => !r.IsSelected));
                Assert.IsNull(flow.StatusMessage);
            }
        }

        [Test]
        public void SelectListing_KeepsTheSelection_AndMarksOnlyThatRow()
        {
            using (UiFlow flow = Flow(NewSession()))
            {
                long id = flow.Listings[flow.Listings.Count - 1].ListingId;
                int raised = 0;
                flow.Changed += () => raised++;

                Assert.IsTrue(flow.SelectListing(id));

                Assert.AreEqual(id, flow.SelectedListingId);
                Assert.AreEqual(id, flow.SelectedListing.ListingId);
                Assert.AreEqual(1, flow.Listings.Count(r => r.IsSelected));
                Assert.IsTrue(flow.Listings.Single(r => r.ListingId == id).IsSelected);
                Assert.AreEqual(1, raised);
            }
        }

        [Test]
        public void SelectListing_AnotherListing_MovesTheSelection()
        {
            using (UiFlow flow = Flow(NewSession()))
            {
                flow.SelectListing(flow.Listings[0].ListingId);
                flow.SelectListing(flow.Listings[1].ListingId);

                Assert.AreEqual(flow.Listings[1].ListingId, flow.SelectedListingId);
                Assert.AreEqual(1, flow.Listings.Count(r => r.IsSelected));
                Assert.IsFalse(flow.Listings[0].IsSelected);
            }
        }

        [Test]
        public void SelectListing_AnUnknownListing_IsRefused_KeepsTheOldSelection_AndSaysWhy()
        {
            using (UiFlow flow = Flow(NewSession()))
            {
                long first = flow.Listings[0].ListingId;
                flow.SelectListing(first);
                int raised = 0;
                flow.Changed += () => raised++;

                Assert.IsFalse(flow.SelectListing(-5));

                Assert.AreEqual(first, flow.SelectedListingId);
                Assert.AreEqual("Bu ilan art\u0131k yok.", flow.StatusMessage);
                Assert.AreEqual(1, raised);
            }
        }

        [Test]
        public void ASuccessfulSelection_ClearsTheStatusMessage()
        {
            using (UiFlow flow = Flow(NewSession()))
            {
                flow.SelectListing(-5);
                Assert.IsNotNull(flow.StatusMessage);

                flow.SelectListing(flow.Listings[0].ListingId);

                Assert.IsNull(flow.StatusMessage);
            }
        }

        [Test]
        public void ClearSelection_RemovesIt_AndNotifiesOnlyWhenSomethingWasSelected()
        {
            using (UiFlow flow = Flow(NewSession()))
            {
                int raised = 0;
                flow.Changed += () => raised++;
                flow.ClearSelection();
                Assert.AreEqual(0, raised);

                flow.SelectListing(flow.Listings[0].ListingId);
                raised = 0;
                flow.ClearSelection();

                Assert.IsNull(flow.SelectedListingId);
                Assert.IsTrue(flow.Listings.All(r => !r.IsSelected));
                Assert.AreEqual(1, raised);
            }
        }

        [Test]
        public void TheSelection_SurvivesRefresh_WhileTheListingExists()
        {
            using (UiFlow flow = Flow(NewSession()))
            {
                long id = flow.Listings[0].ListingId;
                flow.SelectListing(id);

                flow.Refresh();

                Assert.AreEqual(id, flow.SelectedListingId);
                Assert.IsTrue(flow.Listings.Single(r => r.ListingId == id).IsSelected);
            }
        }

        [Test]
        public void EndDay_RefreshesTheListings_KeepsASurvivingSelection_AndDropsAnExpiredOne()
        {
            GameSession session = NewSession();
            using (UiFlow flow = Flow(session))
            {
                long id = flow.Listings[0].ListingId;
                flow.SelectListing(id);
                bool expired = false;
                for (int day = 0; day < 6 && !expired; day++)
                {
                    Assert.IsTrue(flow.EndDay().IsSuccess);

                    CollectionAssert.AreEqual(session.Api.GetListings().Select(l => l.ListingId).ToArray(), flow.Listings.Select(r => r.ListingId).ToArray(), "liste taze olmal\u0131");
                    bool stillListed = session.Api.GetListings().Any(l => l.ListingId == id);
                    if (stillListed)
                    {
                        Assert.AreEqual(id, flow.SelectedListingId);
                    }
                    else
                    {
                        Assert.IsNull(flow.SelectedListingId, "kalkan ilan se\u00E7ili kalmamal\u0131");
                        expired = true;
                    }
                }

                Assert.IsTrue(expired, "ilan birka\u00E7 g\u00FCn i\u00E7inde kalkmal\u0131");
            }
        }

        [Test]
        public void EndDay_Succeeds_MovesTheDay_AndLeavesNoMessage()
        {
            GameSession session = NewSession();
            using (UiFlow flow = Flow(session))
            {
                Result<Esnaf.Domain.Game.DayEndReport> result = flow.EndDay();

                Assert.IsTrue(result.IsSuccess);
                Assert.AreEqual("G\u00FCn 2", flow.TopBar.DayText);
                Assert.IsNull(flow.StatusMessage);
            }
        }

        [Test]
        public void EndDay_WhileANegotiationIsOpen_IsRefused_WithATurkishMessage_AndChangesNothing()
        {
            GameSession session = NewSession();
            using (UiFlow flow = Flow(session))
            {
                long listingId = flow.Listings[0].ListingId;
                Assert.IsTrue(session.Api.StartNegotiation(listingId).IsSuccess);
                flow.Refresh();
                string before = session.Api.GetStateDigest();

                Result<Esnaf.Domain.Game.DayEndReport> result = flow.EndDay();

                Assert.IsTrue(result.IsFailure);
                Assert.AreEqual("negotiation.in_progress", result.ErrorCode);
                Assert.AreEqual("\u00D6nce s\u00FCren pazarl\u0131\u011F\u0131 bitirmelisin.", flow.StatusMessage);
                Assert.AreEqual("G\u00FCn 1", flow.TopBar.DayText);
                Assert.AreEqual(before, session.Api.GetStateDigest());

                session.Api.WalkAway();
                Assert.IsTrue(flow.EndDay().IsSuccess);
                Assert.IsNull(flow.StatusMessage, "ba\u015Far\u0131l\u0131 komut eski mesaj\u0131 temizler");
            }
        }

        // ---------- telefon detay\u0131 ----------

        [Test]
        public void TheFlowStartsOnTheListings_WithoutADetail()
        {
            using (UiFlow flow = Flow(NewSession()))
            {
                Assert.AreEqual(UiScreen.Listings, flow.CurrentScreen);
                Assert.IsNull(flow.Detail);
            }
        }

        [Test]
        public void OpenListing_SelectsIt_ShowsTheDetail_AndNotifiesOnce()
        {
            GameSession session = NewSession();
            using (UiFlow flow = Flow(session))
            {
                ListingView expected = session.Api.GetListings()[1];
                int raised = 0;
                flow.Changed += () => raised++;

                Assert.IsTrue(flow.OpenListing(expected.ListingId));

                Assert.AreEqual(UiScreen.Detail, flow.CurrentScreen);
                Assert.AreEqual(expected.ListingId, flow.SelectedListingId);
                Assert.AreEqual(expected.ListingId, flow.Detail.ListingId);
                Assert.AreEqual(session.Content.GetProduct(expected.DefinitionId).Name, flow.Detail.Title);
                Assert.AreEqual("Depolama: " + expected.StorageGb + " GB", flow.Detail.StorageLine);
                Assert.AreEqual("Ya\u015F: " + expected.AgeMonths + " ay", flow.Detail.AgeLine);
                Assert.AreEqual("\u0130stenen fiyat: " + MoneyFormatter.Format(expected.AskingPrice), flow.Detail.PriceLine);
                Assert.AreEqual(expected.HasBox ? "Kutu: var" : "Kutu: yok", flow.Detail.BoxLine);
                Assert.AreEqual(expected.HasInvoice ? "Fatura: var" : "Fatura: yok", flow.Detail.InvoiceLine);
                Assert.AreEqual("Sat\u0131c\u0131: " + session.Content.GetNpc(expected.SellerNpcId).Name, flow.Detail.SellerLine);
                Assert.AreEqual(1, raised);
            }
        }

        [Test]
        public void OpenListing_AnUnknownListing_StaysOnTheListings_AndSaysWhy()
        {
            using (UiFlow flow = Flow(NewSession()))
            {
                Assert.IsFalse(flow.OpenListing(-5));

                Assert.AreEqual(UiScreen.Listings, flow.CurrentScreen);
                Assert.IsNull(flow.Detail);
                Assert.AreEqual("Bu ilan art\u0131k yok.", flow.StatusMessage);
            }
        }

        [Test]
        public void OpenSelectedListing_OpensTheSelection_AndDoesNothingWithoutOne()
        {
            using (UiFlow flow = Flow(NewSession()))
            {
                int raised = 0;
                flow.Changed += () => raised++;
                Assert.IsFalse(flow.OpenSelectedListing());
                Assert.AreEqual(0, raised);
                Assert.AreEqual(UiScreen.Listings, flow.CurrentScreen);

                long id = flow.Listings[2].ListingId;
                flow.SelectListing(id);
                raised = 0;

                Assert.IsTrue(flow.OpenSelectedListing());

                Assert.AreEqual(UiScreen.Detail, flow.CurrentScreen);
                Assert.AreEqual(id, flow.Detail.ListingId);
                Assert.AreEqual(1, raised);
            }
        }

        [Test]
        public void OpenSelectedListing_ClearsAnEarlierMessage()
        {
            using (UiFlow flow = Flow(NewSession()))
            {
                flow.SelectListing(flow.Listings[0].ListingId);
                flow.SelectListing(-5);
                Assert.IsNotNull(flow.StatusMessage);

                Assert.IsTrue(flow.OpenSelectedListing());

                Assert.IsNull(flow.StatusMessage);
            }
        }

        [Test]
        public void Back_ReturnsToTheListings_KeepingTheSelection_AndClearingTheMessage()
        {
            using (UiFlow flow = Flow(NewSession()))
            {
                long id = flow.Listings[0].ListingId;
                flow.OpenListing(id);
                flow.RequestAppraisal();
                Assert.IsNotNull(flow.StatusMessage);
                int raised = 0;
                flow.Changed += () => raised++;

                flow.Back();

                Assert.AreEqual(UiScreen.Listings, flow.CurrentScreen);
                Assert.AreEqual(id, flow.SelectedListingId);
                Assert.IsTrue(flow.Listings.Single(r => r.ListingId == id).IsSelected);
                Assert.IsNull(flow.StatusMessage);
                Assert.AreEqual(1, raised);
            }
        }

        [Test]
        public void Back_OnTheListings_DoesNothing()
        {
            using (UiFlow flow = Flow(NewSession()))
            {
                int raised = 0;
                flow.Changed += () => raised++;

                flow.Back();

                Assert.AreEqual(0, raised);
                Assert.AreEqual(UiScreen.Listings, flow.CurrentScreen);
            }
        }

        [Test]
        public void TheDetailButtons_ArePreparedButNotImplementedYet_AndOnlyWorkOnTheDetailScreen()
        {
            GameSession session = NewSession();
            using (UiFlow flow = Flow(session))
            {
                string digest = session.Api.GetStateDigest();
                flow.RequestAppraisal();
                Assert.IsNull(flow.StatusMessage, "detay ekran\u0131nda de\u011Filken bir \u015Fey yapmaz");
                flow.RequestNegotiation();
                Assert.IsNull(flow.StatusMessage);

                flow.OpenListing(flow.Listings[0].ListingId);
                int raised = 0;
                flow.Changed += () => raised++;
                flow.RequestAppraisal();
                Assert.AreEqual("Ekspertiz bir sonraki ad\u0131mda eklenecek.", flow.StatusMessage);
                flow.RequestNegotiation();
                Assert.AreEqual("Pazarl\u0131k bir sonraki ad\u0131mda eklenecek.", flow.StatusMessage);

                Assert.AreEqual(2, raised);
                Assert.AreEqual(UiScreen.Detail, flow.CurrentScreen);
                Assert.AreEqual(digest, session.Api.GetStateDigest(), "oyun durumu de\u011Fi\u015Fmez (hen\u00FCz uygulanmad\u0131)");
            }
        }

        [Test]
        public void ClearSelection_LeavesTheDetailScreen()
        {
            using (UiFlow flow = Flow(NewSession()))
            {
                flow.OpenListing(flow.Listings[0].ListingId);

                flow.ClearSelection();

                Assert.AreEqual(UiScreen.Listings, flow.CurrentScreen);
                Assert.IsNull(flow.Detail);
                Assert.IsNull(flow.SelectedListingId);
            }
        }

        [Test]
        public void IfTheOpenedListingLeavesTheMarket_TheFlowFallsBackToTheListings()
        {
            GameSession session = NewSession();
            using (UiFlow flow = Flow(session))
            {
                long listingId = flow.Listings[0].ListingId;
                flow.OpenListing(listingId);
                session.Api.StartNegotiation(listingId);
                var view = session.Api.GetNegotiation();

                Assert.IsTrue(session.Api.MakeOffer(view.ShownPrice).IsSuccess);

                Assert.AreEqual(UiScreen.Listings, flow.CurrentScreen);
                Assert.IsNull(flow.Detail);
                Assert.IsNull(flow.SelectedListingId);
            }
        }

        [Test]
        public void TheDetail_FollowsTheListing_WhileItIsOpen()
        {
            GameSession session = NewSession();
            using (UiFlow flow = Flow(session))
            {
                flow.OpenListing(flow.Listings[0].ListingId);
                string before = flow.Detail.RemainingLine;

                flow.Refresh();

                Assert.AreEqual(before, flow.Detail.RemainingLine);
                Assert.AreEqual(UiScreen.Detail, flow.CurrentScreen);
            }
        }

        [Test]
        public void AFailedEndDay_StillNotifiesTheScreen_SoTheMessageIsShown()
        {
            GameSession session = NewSession();
            using (UiFlow flow = Flow(session))
            {
                Assert.IsTrue(session.Api.StartNegotiation(flow.Listings[0].ListingId).IsSuccess);
                int raised = 0;
                string messageSeenByTheScreen = null;
                flow.Changed += () =>
                {
                    raised++;
                    messageSeenByTheScreen = flow.StatusMessage;
                };

                Assert.IsTrue(flow.EndDay().IsFailure);

                Assert.AreEqual(1, raised);
                Assert.AreEqual("\u00D6nce s\u00FCren pazarl\u0131\u011F\u0131 bitirmelisin.", messageSeenByTheScreen);
            }
        }

        [Test]
        public void TheListings_FollowAnExternalChange_ThroughTheEvents()
        {
            GameSession session = NewSession();
            using (UiFlow flow = Flow(session))
            {
                int before = flow.Listings.Count;
                long listingId = flow.Listings[0].ListingId;
                flow.SelectListing(listingId);
                session.Api.StartNegotiation(listingId);
                // sat\u0131c\u0131n\u0131n fiyat\u0131na ula\u015Fan teklif: ilan al\u0131n\u0131r ve pazardan kalkar
                var view = session.Api.GetNegotiation();
                Assert.IsTrue(session.Api.MakeOffer(view.ShownPrice).IsSuccess);

                Assert.AreEqual(before - 1, flow.Listings.Count, "ListingPurchased olay\u0131 listeyi g\u00FCncellemeli");
                Assert.IsNull(flow.SelectedListingId, "al\u0131nan ilan se\u00E7ili kalmamal\u0131");
            }
        }
    }
}
