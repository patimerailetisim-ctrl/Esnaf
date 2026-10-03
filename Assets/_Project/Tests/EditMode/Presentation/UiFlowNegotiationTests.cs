using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Content;
using Esnaf.Domain.Game;
using Esnaf.Domain.Market;
using Esnaf.Domain.Negotiation;
using Esnaf.Presentation;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Presentation
{
    /// <summary>
    /// Pazarlık ekranı (Gün 10 Adım 5). Pazarlık kuralları Domain'dedir ve burada TEKRAR uygulanmaz: testler UiFlow'un IGameApi'ye doğru
    /// komutu gönderdiğini ve API'nin döndürdüğü durumu doğru gösterdiğini doğrular; beklenen değerler gerçek oturumdan okunur.
    /// </summary>
    public class UiFlowNegotiationTests
    {
        private sealed class DictionarySource : IContentSource
        {
            private readonly Dictionary<string, string> _files = new Dictionary<string, string>();

            public DictionarySource(string directory, string from, string to)
            {
                foreach (string path in Directory.GetFiles(directory, "*.json"))
                {
                    string text = File.ReadAllText(path);
                    _files[Path.GetFileName(path)] = text.Replace(from, to);
                }
            }

            public bool TryGetText(string fileName, out string text)
            {
                return _files.TryGetValue(fileName, out text);
            }
        }

        private static GameSession NewSession(ulong seed = 1UL)
        {
            return GameSession.NewGame(MarketHarness.RealContent(), seed);
        }

        private static UiFlow Flow(GameSession session)
        {
            return new UiFlow(session.Api, new ContentPresentation(session.Content), session.Bus);
        }

        private static UiFlow OpenNegotiation(GameSession session, int listingIndex = 0)
        {
            UiFlow flow = Flow(session);
            Assert.IsTrue(flow.OpenListing(flow.Listings[listingIndex].ListingId));
            Assert.IsTrue(flow.OpenNegotiation(), flow.StatusMessage);
            return flow;
        }

        // ---------- açılış ----------

        [Test]
        public void OpenNegotiation_StartsTheNegotiationForTheSelectedListing_AndShowsItsState()
        {
            GameSession session = NewSession();
            using (UiFlow flow = Flow(session))
            {
                ListingView listing = session.Api.GetListings()[1];
                flow.OpenListing(listing.ListingId);
                int raised = 0;
                flow.Changed += () => raised++;

                Assert.IsTrue(flow.OpenNegotiation());

                Assert.AreEqual(UiScreen.Negotiation, flow.CurrentScreen);
                NegotiationView api = session.Api.GetNegotiation();
                Assert.AreEqual(listing.ListingId, api.ListingId, "StartNegotiation doğru ilanla çağrıldı");
                NegotiationScreenViewModel screen = flow.NegotiationScreen;
                Assert.AreEqual(session.Content.GetProduct(listing.DefinitionId).Name, screen.Title);
                Assert.AreEqual("Satıcı: " + session.Content.GetNpc(listing.SellerNpcId).Name, screen.SellerLine);
                Assert.AreEqual("İlan fiyatı: " + MoneyFormatter.Format(listing.AskingPrice), screen.AskingLine);
                Assert.AreEqual("Satıcının güncel fiyatı: " + MoneyFormatter.Format(api.ShownPrice), screen.ShownLine);
                Assert.AreEqual("Nakdin: " + MoneyFormatter.Format(session.Api.GetCash()), screen.CashLine);
                Assert.AreEqual("Tur: " + api.Round, screen.RoundLine);
                Assert.AreEqual("Pazarlık sürüyor", screen.PhaseText);
                Assert.AreEqual("Satıcı: İstediğim fiyat " + MoneyFormatter.Format(api.ShownPrice) + ".", screen.ReplyLine);
                Assert.AreEqual(api.ShownPrice.Tl.ToString(), screen.OfferText);
                Assert.IsNull(screen.YourOfferLine);
                Assert.IsNull(screen.InsultLine);
                Assert.AreEqual(1, raised);
            }
        }

        [Test]
        public void OpenNegotiation_NotFromTheDetail_DoesNothing()
        {
            GameSession session = NewSession();
            using (UiFlow flow = Flow(session))
            {
                string digest = session.Api.GetStateDigest();

                Assert.IsFalse(flow.OpenNegotiation());

                Assert.AreEqual(UiScreen.Listings, flow.CurrentScreen);
                Assert.IsNull(flow.NegotiationScreen);
                Assert.IsNull(session.Api.GetNegotiation());
                Assert.AreEqual(digest, session.Api.GetStateDigest());
            }
        }

        [Test]
        public void MoodAndPatience_AreShownFromTheApiView()
        {
            GameSession session = NewSession();
            using (UiFlow flow = OpenNegotiation(session))
            {
                NegotiationView api = session.Api.GetNegotiation();

                Assert.AreEqual("Satıcının ruh hali: " + TurkishTexts.Level(api.Mood), flow.NegotiationScreen.MoodLine);
                Assert.AreEqual("Sabır: " + TurkishTexts.Level(api.Patience), flow.NegotiationScreen.PatienceLine);
            }
        }

        [Test]
        public void LeavingTheNegotiationScreen_KeepsTheNegotiationOpen_AndItResumesWithoutANewStart()
        {
            GameSession session = NewSession();
            using (UiFlow flow = OpenNegotiation(session))
            {
                flow.SetOfferText("5000");
                Assert.IsTrue(flow.SubmitOffer().IsSuccess);
                NegotiationView before = session.Api.GetNegotiation();
                flow.Back();
                Assert.AreEqual(UiScreen.Detail, flow.CurrentScreen);
                Assert.IsNotNull(session.Api.GetNegotiation(), "pazarlık açık kalır");
                string digest = session.Api.GetStateDigest();

                Assert.IsTrue(flow.OpenNegotiation());

                Assert.AreEqual(UiScreen.Negotiation, flow.CurrentScreen);
                Assert.AreEqual(digest, session.Api.GetStateDigest(), "devam etmek yeni StartNegotiation çağırmaz");
                Assert.AreEqual("Tur: " + before.Round, flow.NegotiationScreen.RoundLine);
                Assert.AreEqual("Satıcının güncel fiyatı: " + MoneyFormatter.Format(before.ShownPrice), flow.NegotiationScreen.ShownLine);
            }
        }

        [Test]
        public void WhileANegotiationIsOpen_AnotherListingsNegotiationIsRefused_AndTheFirstOneIsNotShown()
        {
            GameSession session = NewSession();
            using (UiFlow flow = OpenNegotiation(session, 0))
            {
                long first = session.Api.GetNegotiation().ListingId;
                flow.Back();
                flow.Back();
                long other = flow.Listings.First(r => r.ListingId != first).ListingId;
                flow.OpenListing(other);

                Assert.IsFalse(flow.OpenNegotiation());

                Assert.AreEqual(UiScreen.Detail, flow.CurrentScreen);
                Assert.AreEqual("Önce süren pazarlığı bitirmelisin.", flow.StatusMessage);
                Assert.IsNull(flow.NegotiationScreen);
                Assert.AreEqual(first, session.Api.GetNegotiation().ListingId);
                Assert.AreEqual(other, flow.Detail.ListingId);
            }
        }

        [Test]
        public void SelectingAnotherListingWhileTheNegotiationScreenIsOpen_ClosesTheScreenInsteadOfShowingTheOldNegotiation()
        {
            GameSession session = NewSession();
            using (UiFlow flow = OpenNegotiation(session, 0))
            {
                long first = session.Api.GetNegotiation().ListingId;
                long other = flow.Listings.First(r => r.ListingId != first).ListingId;

                flow.OpenListing(other);

                Assert.AreEqual(UiScreen.Detail, flow.CurrentScreen);
                Assert.IsNull(flow.NegotiationScreen);
                Assert.AreEqual(other, flow.SelectedListingId);
            }
        }

        [Test]
        public void WhileANegotiationIsOpen_TheDayCannotEnd_AndTheListingsScreenSaysWhy()
        {
            GameSession session = NewSession();
            using (UiFlow flow = OpenNegotiation(session))
            {
                flow.Back();
                flow.Back();

                Assert.IsTrue(flow.EndDay().IsFailure);

                Assert.AreEqual("Önce süren pazarlığı bitirmelisin.", flow.StatusMessage);
                Assert.AreEqual("Gün 1", flow.TopBar.DayText);
                Assert.IsNotNull(session.Api.GetNegotiation());
            }
        }

        // ---------- teklif ----------

        [Test]
        public void SubmitOffer_SendsTheTypedOfferToTheApi_AndShowsTheReturnedState()
        {
            GameSession session = NewSession();
            using (UiFlow flow = OpenNegotiation(session))
            {
                Money sent = Money.Zero;
                int offers = 0;
                session.Bus.Subscribe<OfferMade>(e =>
                {
                    sent = e.Offer;
                    offers++;
                });
                NegotiationView start = session.Api.GetNegotiation();
                flow.SetOfferText("5000");

                Result<NegotiationView> result = flow.SubmitOffer();

                Assert.IsTrue(result.IsSuccess, result.ErrorCode);
                Assert.AreEqual(1, offers);
                Assert.AreEqual(Money.FromTl(5000), sent, "UI'nin yazdığı tutar API'ye gitti");
                NegotiationView api = session.Api.GetNegotiation();
                Assert.AreEqual(start.Round + 1, api.Round);
                NegotiationScreenViewModel screen = flow.NegotiationScreen;
                Assert.AreEqual("Tur: " + api.Round, screen.RoundLine);
                Assert.AreEqual("Satıcının güncel fiyatı: " + MoneyFormatter.Format(api.ShownPrice), screen.ShownLine);
                Assert.AreEqual("Son teklifin: " + MoneyFormatter.Format(Money.FromTl(5000)), screen.YourOfferLine);
                Assert.AreEqual(api.Phase == NegotiationPhase.FinalOffer ? "Satıcı son fiyatını söyledi" : "Pazarlık sürüyor", screen.PhaseText);
            }
        }

        [Test]
        public void TheSellersReply_IsDescribedFromTheReturnedPrice_ACounterOrAHold()
        {
            GameSession session = NewSession();
            using (UiFlow flow = OpenNegotiation(session))
            {
                Money previous = session.Api.GetNegotiation().ShownPrice;
                bool sawCounter = false;
                for (int i = 0; i < 6; i++)
                {
                    flow.SetOfferText("1000");
                    NegotiationView view = flow.SubmitOffer().Value;
                    if (view.Phase != NegotiationPhase.Active)
                    {
                        break;
                    }

                    string expected = view.ShownPrice < previous
                        ? "Satıcı karşı teklif verdi: " + MoneyFormatter.Format(view.ShownPrice) + "."
                        : "Satıcı fiyatında direniyor: " + MoneyFormatter.Format(view.ShownPrice) + ".";
                    Assert.AreEqual(expected, flow.NegotiationScreen.ReplyLine);
                    sawCounter |= view.ShownPrice < previous;
                    previous = view.ShownPrice;
                }

                Assert.IsTrue(sawCounter, "birkaç turda satıcı bir karşı teklif vermeli");
            }
        }

        [Test]
        public void AnInsultingLowOffer_IsFlaggedFromTheApiView()
        {
            GameSession session = NewSession();
            using (UiFlow flow = OpenNegotiation(session))
            {
                flow.SetOfferText("10");

                NegotiationView view = flow.SubmitOffer().Value;

                Assert.AreEqual(view.LastOfferInsulted, flow.NegotiationScreen.InsultLine != null);
                if (view.LastOfferInsulted)
                {
                    Assert.AreEqual("Teklifin satıcıyı gücendirdi.", flow.NegotiationScreen.InsultLine);
                }
            }
        }

        [Test]
        public void ARepeatedTooLowOffer_EventuallyBringsTheFinalOffer_AndOffersAreThenRefusedByTheApi()
        {
            GameSession session = NewSession();
            using (UiFlow flow = OpenNegotiation(session))
            {
                NegotiationView view = session.Api.GetNegotiation();
                for (int i = 0; i < 12 && view.Phase == NegotiationPhase.Active; i++)
                {
                    flow.SetOfferText("1000");
                    view = flow.SubmitOffer().Value;
                }

                Assert.AreEqual(NegotiationPhase.FinalOffer, view.Phase, "sabır bitince son fiyat gelir");
                Assert.AreEqual("Satıcı son fiyatını söyledi", flow.NegotiationScreen.PhaseText);
                Assert.AreEqual("Satıcı: Son fiyatım " + MoneyFormatter.Format(view.ShownPrice) + ". Kabul et ya da kalk.", flow.NegotiationScreen.ReplyLine);
                Assert.IsTrue(flow.NegotiationScreen.CanAcceptFinal);
                string digest = session.Api.GetStateDigest();

                Result<NegotiationView> again = flow.SubmitOffer();

                Assert.IsTrue(again.IsFailure);
                Assert.AreEqual("negotiation.final_offer_only", again.ErrorCode);
                Assert.AreEqual("Satıcı son fiyatını söyledi; kabul et ya da kalk.", flow.StatusMessage);
                Assert.AreEqual(digest, session.Api.GetStateDigest());
            }
        }

        [Test]
        public void ANormalNegotiation_CannotAcceptAFinalPriceThatWasNeverMade()
        {
            GameSession session = NewSession();
            using (UiFlow flow = OpenNegotiation(session))
            {
                string digest = session.Api.GetStateDigest();
                Assert.IsFalse(flow.NegotiationScreen.CanAcceptFinal);

                Result<NegotiationView> result = flow.AcceptFinalPrice();

                Assert.IsTrue(result.IsFailure);
                Assert.AreEqual("negotiation.no_final_offer", result.ErrorCode);
                Assert.AreEqual("Satıcı henüz son fiyat vermedi.", flow.StatusMessage);
                Assert.AreEqual(digest, session.Api.GetStateDigest());
                Assert.AreEqual(UiScreen.Negotiation, flow.CurrentScreen);
            }
        }

        // ---------- satın alma ----------

        [Test]
        public void AnOfferThatReachesTheSellersPrice_BuysThePhone_ThroughTheApi()
        {
            GameSession session = NewSession();
            using (UiFlow flow = OpenNegotiation(session))
            {
                ListingView listing = session.Api.GetListings()[0];
                NegotiationView start = session.Api.GetNegotiation();
                Money cashBefore = session.Api.GetCash();
                flow.SetOfferText(start.ShownPrice.Tl.ToString());

                Result<NegotiationView> result = flow.SubmitOffer();

                Assert.IsTrue(result.IsSuccess, result.ErrorCode);
                Assert.AreEqual(NegotiationPhase.Deal, result.Value.Phase);
                Money price = result.Value.DealPrice;
                Assert.IsTrue(price.IsPositive);
                Assert.AreEqual(cashBefore - price, session.Api.GetCash(), "para gerçek alıştan düştü (API durumundan)");
                Assert.AreEqual(TurkishTexts.Cash(session.Api.GetCash()), flow.TopBar.CashText);
                Assert.AreEqual(1, session.Api.GetInventory().Count);
                Assert.AreEqual(listing.DefinitionId, session.Api.GetInventory()[0].DefinitionId);
                Assert.AreEqual(price, session.Api.GetInventory()[0].CostBasis);
                Assert.IsFalse(flow.Listings.Any(r => r.ListingId == listing.ListingId), "ilan listeden düştü");
                Assert.AreEqual(UiScreen.Listings, flow.CurrentScreen);
                Assert.IsNull(flow.NegotiationScreen);
                Assert.IsNull(session.Api.GetNegotiation());
                Assert.AreEqual(
                    "Satın alındı: " + session.Content.GetProduct(listing.DefinitionId).Name + " — " + MoneyFormatter.Format(price) + ". Rafa eklendi (1/15).",
                    flow.StatusMessage);
            }
        }

        [Test]
        public void ABoughtListing_CannotBeBoughtAgain()
        {
            GameSession session = NewSession();
            using (UiFlow flow = OpenNegotiation(session))
            {
                long id = session.Api.GetNegotiation().ListingId;
                flow.SetOfferText(session.Api.GetNegotiation().ShownPrice.Tl.ToString());
                Assert.IsTrue(flow.SubmitOffer().IsSuccess);
                Money cash = session.Api.GetCash();

                Assert.IsFalse(flow.OpenListing(id));
                Assert.AreEqual("Bu ilan artık yok.", flow.StatusMessage);
                Assert.IsTrue(session.Api.StartNegotiation(id).IsFailure);
                Assert.AreEqual("listing.unknown", session.Api.StartNegotiation(id).ErrorCode);

                Assert.AreEqual(cash, session.Api.GetCash());
                Assert.AreEqual(1, session.Api.GetInventory().Count);
            }
        }

        [Test]
        public void TheFinalPriceAccepted_BuysThePhoneAtTheFinalPrice()
        {
            GameSession session = NewSession();
            using (UiFlow flow = OpenNegotiation(session))
            {
                NegotiationView view = session.Api.GetNegotiation();
                for (int i = 0; i < 12 && view.Phase == NegotiationPhase.Active; i++)
                {
                    flow.SetOfferText("1000");
                    view = flow.SubmitOffer().Value;
                }

                Assert.AreEqual(NegotiationPhase.FinalOffer, view.Phase);
                Money cashBefore = session.Api.GetCash();

                Result<NegotiationView> result = flow.AcceptFinalPrice();

                Assert.IsTrue(result.IsSuccess, result.ErrorCode);
                Assert.AreEqual(NegotiationPhase.Deal, result.Value.Phase);
                Assert.AreEqual(view.ShownPrice, result.Value.DealPrice);
                Assert.AreEqual(cashBefore - result.Value.DealPrice, session.Api.GetCash());
                Assert.AreEqual(1, session.Api.GetInventory().Count);
                Assert.AreEqual(UiScreen.Listings, flow.CurrentScreen);
                StringAssert.StartsWith("Satın alındı: ", flow.StatusMessage);
                StringAssert.EndsWith("Rafa eklendi (1/15).", flow.StatusMessage);
            }
        }

        [Test]
        public void NotEnoughCash_IsRefusedByTheApi_WithTheTurkishMessage_AndTheNegotiationStaysOpen()
        {
            string directory = TestPaths.ContentDataDirectory();
            ContentLoadResult loaded = ContentDatabase.Load(new DictionarySource(directory, "\"openingCapital\": 250000", "\"openingCapital\": 1000"));
            Assert.IsTrue(loaded.IsSuccess, loaded.FormatIssues());
            GameSession session = GameSession.NewGame(loaded.Database, 1UL);
            using (UiFlow flow = OpenNegotiation(session))
            {
                Money cash = session.Api.GetCash();
                Assert.AreEqual(Money.FromTl(1000), cash);
                flow.SetOfferText(session.Api.GetNegotiation().ShownPrice.Tl.ToString());
                string digest = session.Api.GetStateDigest();

                Result<NegotiationView> result = flow.SubmitOffer();

                Assert.IsTrue(result.IsFailure);
                Assert.AreEqual("cash.insufficient", result.ErrorCode);
                Assert.AreEqual("Yeterli nakit yok.", flow.StatusMessage);
                Assert.AreEqual(cash, session.Api.GetCash());
                Assert.AreEqual(digest, session.Api.GetStateDigest());
                Assert.IsNotNull(session.Api.GetNegotiation());
                Assert.AreEqual(UiScreen.Negotiation, flow.CurrentScreen);
                Assert.AreEqual(0, session.Api.GetInventory().Count);
            }
        }

        [Test]
        public void TheOfferText_IsStoredAtOnce_WithoutAnEvent_SoTypingDoesNotRebuildTheScreen()
        {
            GameSession session = NewSession();
            using (UiFlow flow = OpenNegotiation(session))
            {
                int raised = 0;
                flow.Changed += () => raised++;

                flow.SetOfferText("1234");

                Assert.AreEqual("1234", flow.NegotiationScreen.OfferText);
                Assert.AreEqual(0, raised);
            }
        }

        [Test]
        public void TheNegotiationCommands_OutsideTheNegotiationScreen_AreRefused_AndChangeNothing()
        {
            GameSession session = NewSession();
            using (UiFlow flow = Flow(session))
            {
                string digest = session.Api.GetStateDigest();
                int raised = 0;
                flow.Changed += () => raised++;
                flow.SetOfferText("5000");

                foreach (Result<NegotiationView> result in new[] { flow.SubmitOffer(), flow.SubmitCardOffer(), flow.AcceptFinalPrice(), flow.WalkAway() })
                {
                    Assert.IsTrue(result.IsFailure);
                    Assert.AreEqual("ui.not_on_negotiation_screen", result.ErrorCode);
                }

                Assert.IsFalse(flow.SelectCard(0));
                Assert.IsFalse(flow.SelectCard(-1));
                flow.AdjustOffer(100);
                Assert.AreEqual(digest, session.Api.GetStateDigest());
                Assert.AreEqual(0, raised);
                Assert.IsNull(flow.StatusMessage);
            }
        }

        [Test]
        public void SelectCard_RefusesAnIndexOutsideTheCards()
        {
            string level;
            GameSession session = SessionWithACardOnTheFirstListing(out level);
            using (UiFlow flow = OpenNegotiation(session))
            {
                int count = session.Api.GetNegotiation().Cards.Count;

                Assert.IsFalse(flow.SelectCard(-1));
                Assert.IsFalse(flow.SelectCard(count));
                Assert.IsTrue(flow.SelectCard(count - 1));
            }
        }

        // En az iki koz kartı \u00E7\u0131kan bir oturum (s0 ekspertizi); kartlar ekspertiz sonucundan gelir.
        private static GameSession SessionWithTwoCardsOnTheFirstListing()
        {
            for (ulong seed = 1; seed < 3000; seed++)
            {
                GameSession session = NewSession(seed);
                long id = session.Api.GetListings()[0].ListingId;
                var appraisal = session.Api.StartAppraisal(id, "s0");
                if (appraisal.IsSuccess && appraisal.Value.Cards.Count >= 2)
                {
                    return session;
                }
            }

            Assert.Fail("iki koz kart\u0131 \u00E7\u0131karan bir tohum bulunamad\u0131");
            return null;
        }

        [Test]
        public void TheSelectedCard_IsTheOneSentToTheApi()
        {
            GameSession session = SessionWithTwoCardsOnTheFirstListing();
            using (UiFlow flow = OpenNegotiation(session))
            {
                flow.SetOfferText("5000");
                flow.SelectCard(1);

                Assert.IsTrue(flow.SubmitCardOffer().IsSuccess);

                NegotiationView api = session.Api.GetNegotiation();
                Assert.IsFalse(api.Cards[0].IsUsed);
                Assert.IsTrue(api.Cards[1].IsUsed, "se\u00E7ilen kart (1) oynand\u0131");
            }
        }

        [Test]
        public void SelectingAnotherListingWithoutOpeningIt_ClosesTheNegotiationScreen()
        {
            GameSession session = NewSession();
            using (UiFlow flow = OpenNegotiation(session))
            {
                long first = session.Api.GetNegotiation().ListingId;
                long other = flow.Listings.First(r => r.ListingId != first).ListingId;

                Assert.IsTrue(flow.SelectListing(other));

                Assert.AreEqual(UiScreen.Detail, flow.CurrentScreen);
                Assert.IsNull(flow.NegotiationScreen);
                Assert.AreEqual(first, session.Api.GetNegotiation().ListingId, "pazarl\u0131k oyunda a\u00E7\u0131k kal\u0131r");
            }
        }

        [Test]
        public void ResumingANegotiation_StartsTheScreenFresh_NoOldOfferTextCardSelectionOrLastOffer()
        {
            string level;
            GameSession session = SessionWithACardOnTheFirstListing(out level);
            using (UiFlow flow = OpenNegotiation(session))
            {
                flow.SetOfferText("5000");
                flow.SelectCard(0);
                Assert.IsTrue(flow.SubmitOffer().IsSuccess);
                flow.SelectCard(0);
                flow.SetOfferText("6000");
                flow.Back();

                Assert.IsTrue(flow.OpenNegotiation());

                NegotiationScreenViewModel screen = flow.NegotiationScreen;
                Assert.IsNull(screen.YourOfferLine);
                Assert.AreEqual(-1, screen.SelectedCardIndex);
                Assert.AreEqual(session.Api.GetNegotiation().ShownPrice.Tl.ToString(), screen.OfferText);
            }
        }

        [Test]
        public void TheScreenIsToldAboutTheFinalMessage_AfterAWalkAway_AnAcceptedDeal_AndAFailedCommand()
        {
            GameSession session = NewSession();
            using (UiFlow flow = OpenNegotiation(session))
            {
                string seen = null;
                flow.Changed += () => seen = flow.StatusMessage;

                flow.AcceptFinalPrice();
                Assert.AreEqual("Sat\u0131c\u0131 hen\u00FCz son fiyat vermedi.", seen, "hata mesaj\u0131 ekrana bildirilir");

                flow.WalkAway();
                Assert.AreEqual("Pazarl\u0131ktan vazge\u00E7tin; ilan pazardan kalkt\u0131.", seen, "son mesaj ekrana bildirilir");
            }

            GameSession buyer = NewSession();
            using (UiFlow flow = OpenNegotiation(buyer))
            {
                string seen = null;
                flow.Changed += () => seen = flow.StatusMessage;
                flow.SetOfferText(buyer.Api.GetNegotiation().ShownPrice.Tl.ToString());

                flow.SubmitOffer();

                StringAssert.StartsWith("Sat\u0131n al\u0131nd\u0131: ", seen);
            }
        }

        // ---------- vazgeç ----------

        [Test]
        public void WalkAway_EndsTheNegotiation_RemovesTheListing_AndTouchesNoMoney()
        {
            GameSession session = NewSession();
            using (UiFlow flow = OpenNegotiation(session))
            {
                long id = session.Api.GetNegotiation().ListingId;
                Money cash = session.Api.GetCash();

                Result<NegotiationView> result = flow.WalkAway();

                Assert.IsTrue(result.IsSuccess);
                Assert.AreEqual(NegotiationPhase.Failed, result.Value.Phase);
                Assert.IsNull(session.Api.GetNegotiation());
                Assert.IsFalse(flow.Listings.Any(r => r.ListingId == id), "ilan kalıkır (mevcut sistem davranışı)");
                Assert.AreEqual(cash, session.Api.GetCash());
                Assert.AreEqual(0, session.Api.GetInventory().Count);
                Assert.AreEqual(UiScreen.Listings, flow.CurrentScreen);
                Assert.AreEqual("Pazarlıktan vazgeçtin; ilan pazardan kalktı.", flow.StatusMessage);
                Assert.IsTrue(flow.EndDay().IsSuccess, "pazarlık bitti, gün bitirilebilir");
            }
        }

        [Test]
        public void IfTheNegotiationEndsOutsideTheScreen_TheFlowFallsBackToTheListings()
        {
            GameSession session = NewSession();
            using (UiFlow flow = OpenNegotiation(session))
            {
                Assert.IsTrue(session.Api.WalkAway().IsSuccess);

                Assert.AreEqual(UiScreen.Listings, flow.CurrentScreen);
                Assert.IsNull(flow.NegotiationScreen);
            }
        }

        // ---------- teklif kutusu ----------

        [TestCase("", "Bir teklif tutarı gir.")]
        [TestCase("   ", "Bir teklif tutarı gir.")]
        [TestCase("abc", "Teklif yalnızca rakamlardan oluşmalı.")]
        [TestCase("12a", "Teklif yalnızca rakamlardan oluşmalı.")]
        [TestCase("1.000", "Teklif yalnızca rakamlardan oluşmalı.")]
        [TestCase("12,5", "Teklif yalnızca rakamlardan oluşmalı.")]
        [TestCase("-50", "Teklif negatif olamaz.")]
        [TestCase("0", "Teklif sıfırdan büyük olmalı.")]
        [TestCase("000", "Teklif sıfırdan büyük olmalı.")]
        [TestCase("1000000001", "Teklif çok büyük.")]
        [TestCase("99999999999999999999", "Teklif çok büyük.")]
        public void AnUnusableOfferText_IsExplainedLocally_AndNeverReachesTheApi(string text, string message)
        {
            GameSession session = NewSession();
            using (UiFlow flow = OpenNegotiation(session))
            {
                string digest = session.Api.GetStateDigest();
                flow.SetOfferText(text);

                Result<NegotiationView> result = flow.SubmitOffer();

                Assert.IsTrue(result.IsFailure);
                Assert.AreEqual("ui.offer_invalid", result.ErrorCode);
                Assert.AreEqual(message, flow.StatusMessage);
                Assert.AreEqual(digest, session.Api.GetStateDigest(), "API'ye gitmedi");
            }
        }

        [Test]
        public void ANumberThatIsNotAMultipleOfTen_GoesToTheApi_WhichDecides()
        {
            GameSession session = NewSession();
            using (UiFlow flow = OpenNegotiation(session))
            {
                string digest = session.Api.GetStateDigest();
                flow.SetOfferText("5005");

                Result<NegotiationView> result = flow.SubmitOffer();

                Assert.IsTrue(result.IsFailure);
                Assert.AreEqual("offer.invalid", result.ErrorCode, "doğrulama API'de kalır");
                Assert.AreEqual("Geçersiz teklif: pozitif ve 10 ₺'nin katı olmalı.", flow.StatusMessage);
                Assert.AreEqual(digest, session.Api.GetStateDigest());
            }
        }

        [Test]
        public void TheLargestAllowedOffer_IsSentToTheApi()
        {
            GameSession session = NewSession();
            using (UiFlow flow = OpenNegotiation(session))
            {
                flow.SetOfferText("1000000000");

                Result<NegotiationView> result = flow.SubmitOffer();

                Assert.AreNotEqual("ui.offer_invalid", result.ErrorCode);
            }
        }

        [Test]
        public void TheOfferBox_StartsAtTheSellersPrice_AndTheSteppersEditOnlyTheText()
        {
            GameSession session = NewSession();
            using (UiFlow flow = OpenNegotiation(session))
            {
                string digest = session.Api.GetStateDigest();
                long shown = session.Api.GetNegotiation().ShownPrice.Tl;
                Assert.AreEqual(shown.ToString(), flow.NegotiationScreen.OfferText);

                flow.AdjustOffer(-100);
                Assert.AreEqual((shown - 100).ToString(), flow.NegotiationScreen.OfferText);
                flow.AdjustOffer(10);
                Assert.AreEqual((shown - 90).ToString(), flow.NegotiationScreen.OfferText);
                flow.SetOfferText("40");
                flow.AdjustOffer(-100);
                Assert.AreEqual("10", flow.NegotiationScreen.OfferText, "negatif teklife inmez");
                flow.SetOfferText("abc");
                flow.AdjustOffer(100);
                Assert.AreEqual("100", flow.NegotiationScreen.OfferText);
                flow.SetOfferText("999999990");
                flow.AdjustOffer(100);
                Assert.AreEqual("1000000000", flow.NegotiationScreen.OfferText, "üst sınırı aşmaz");

                Assert.AreEqual(digest, session.Api.GetStateDigest(), "oyun durumu değişmedi");
            }
        }

        // ---------- koz ----------

        // Bir ilan için ekspertizden koz kartı çıkan bir tohum bulur (kartlar ekspertiz sonucundan gelir; kural burada uygulanmaz).
        private static GameSession SessionWithACardOnTheFirstListing(out string level)
        {
            for (ulong seed = 1; seed < 400; seed++)
            {
                GameSession session = NewSession(seed);
                long id = session.Api.GetListings()[0].ListingId;
                var appraisal = session.Api.StartAppraisal(id, "s0");
                if (appraisal.IsSuccess && appraisal.Value.Cards.Count > 0)
                {
                    level = "s0";
                    return session;
                }
            }

            Assert.Fail("koz kartı çıkaran bir tohum bulunamadı");
            level = null;
            return null;
        }

        [Test]
        public void TheCards_AreListedFromTheApi_AndAUsedOneIsMarked()
        {
            string level;
            GameSession session = SessionWithACardOnTheFirstListing(out level);
            using (UiFlow flow = OpenNegotiation(session))
            {
                NegotiationView api = session.Api.GetNegotiation();
                Assert.Greater(api.Cards.Count, 0);
                var cards = flow.NegotiationScreen.Cards;
                Assert.AreEqual(api.Cards.Count, cards.Count);
                for (int i = 0; i < cards.Count; i++)
                {
                    NegotiationCardView c = api.Cards[i];
                    Assert.AreEqual(TurkishTexts.Card(c.WordingKey, c.Attribute, c.Confidence, c.ProblemValue, c.IsUsed), cards[i].Text);
                    Assert.IsFalse(cards[i].IsUsed);
                    Assert.IsFalse(cards[i].IsSelected);
                }

                Assert.IsNull(flow.NegotiationScreen.CardsNote);
            }
        }

        [Test]
        public void ACardOffer_GoesThroughTheApi_AndTheCardShowsAsUsed()
        {
            string level;
            GameSession session = SessionWithACardOnTheFirstListing(out level);
            using (UiFlow flow = OpenNegotiation(session))
            {
                NegotiationCardView card = session.Api.GetNegotiation().Cards[0];
                int startRound = session.Api.GetNegotiation().Round;
                flow.SetOfferText("5000");
                Assert.IsTrue(flow.SelectCard(0));
                Assert.IsTrue(flow.NegotiationScreen.Cards[0].IsSelected);
                Assert.AreEqual(0, flow.NegotiationScreen.SelectedCardIndex);

                Result<NegotiationView> result = flow.SubmitCardOffer();

                Assert.IsTrue(result.IsSuccess, result.ErrorCode);
                NegotiationView api = session.Api.GetNegotiation();
                Assert.IsTrue(api.Cards[0].IsUsed, "kart API'de kullanıldı");
                Assert.IsTrue(flow.NegotiationScreen.Cards[0].IsUsed);
                Assert.IsFalse(flow.NegotiationScreen.Cards[0].IsSelected, "kullanılan kart seçimi bırakılır");
                Assert.AreEqual(startRound + 1, api.Round);
                Assert.AreEqual("Son teklifin: " + MoneyFormatter.Format(Money.FromTl(5000)), flow.NegotiationScreen.YourOfferLine);
                Assert.AreEqual(card.AppraisalId, api.Cards[0].AppraisalId);
            }
        }

        [Test]
        public void PlayingTheSameCardTwice_IsRefusedByTheApi_WithTheTurkishMessage()
        {
            string level;
            GameSession session = SessionWithACardOnTheFirstListing(out level);
            using (UiFlow flow = OpenNegotiation(session))
            {
                flow.SetOfferText("5000");
                flow.SelectCard(0);
                Assert.IsTrue(flow.SubmitCardOffer().IsSuccess);
                string digest = session.Api.GetStateDigest();
                flow.SetOfferText("5000");
                flow.SelectCard(0);

                Result<NegotiationView> again = flow.SubmitCardOffer();

                Assert.IsTrue(again.IsFailure);
                Assert.AreEqual("card.already_used", again.ErrorCode);
                Assert.AreEqual("Bu koz kartı zaten kullanıldı.", flow.StatusMessage);
                Assert.AreEqual(digest, session.Api.GetStateDigest());
            }
        }

        [Test]
        public void UsingACardWithoutSelectingOne_ExplainsAndChangesNothing()
        {
            string level;
            GameSession session = SessionWithACardOnTheFirstListing(out level);
            using (UiFlow flow = OpenNegotiation(session))
            {
                string digest = session.Api.GetStateDigest();

                Result<NegotiationView> result = flow.SubmitCardOffer();

                Assert.IsTrue(result.IsFailure);
                Assert.AreEqual("Önce bir koz kartı seç.", flow.StatusMessage);
                Assert.AreEqual(digest, session.Api.GetStateDigest());
            }
        }

        [Test]
        public void WithoutAnyCard_TheScreenSaysSo_AndTheCardActionIsRefusedWithoutTouchingTheGame()
        {
            GameSession session = NewSession(1UL);
            using (UiFlow flow = OpenNegotiation(session))
            {
                Assert.AreEqual(0, session.Api.GetNegotiation().Cards.Count);
                Assert.AreEqual(0, flow.NegotiationScreen.Cards.Count);
                Assert.AreEqual("Elinde koz kartı yok (ekspertiz yaptırınca kartlar çıkabilir).", flow.NegotiationScreen.CardsNote);
                string digest = session.Api.GetStateDigest();

                Assert.IsFalse(flow.SelectCard(0));
                Assert.IsTrue(flow.SubmitCardOffer().IsFailure);

                Assert.AreEqual(digest, session.Api.GetStateDigest());
            }
        }

        [Test]
        public void SelectingTheSameCardAgain_ClearsTheSelection()
        {
            string level;
            GameSession session = SessionWithACardOnTheFirstListing(out level);
            using (UiFlow flow = OpenNegotiation(session))
            {
                flow.SelectCard(0);
                flow.SelectCard(0);

                Assert.AreEqual(-1, flow.NegotiationScreen.SelectedCardIndex);
                Assert.IsFalse(flow.NegotiationScreen.Cards[0].IsSelected);
            }
        }

        // ---------- taşma ----------

        [Test]
        public void EveryLineOfTheNegotiationScreen_IsShortEnoughToWrap_AndHasNoLineBreaks()
        {
            string level;
            GameSession session = SessionWithACardOnTheFirstListing(out level);
            using (UiFlow flow = OpenNegotiation(session))
            {
                var lines = new List<string>();
                for (int i = 0; i < 9; i++)
                {
                    flow.SetOfferText("1000");
                    flow.SubmitOffer();
                    if (flow.NegotiationScreen == null)
                    {
                        break;
                    }

                    NegotiationScreenViewModel s = flow.NegotiationScreen;
                    lines.AddRange(new[] { s.Title, s.SellerLine, s.AskingLine, s.ShownLine, s.CashLine, s.RoundLine, s.PhaseText, s.MoodLine, s.PatienceLine, s.ReplyLine, s.YourOfferLine, s.InsultLine });
                    lines.AddRange(s.Cards.Select(c => c.Text));
                    lines.Add(flow.StatusMessage);
                }

                foreach (string line in lines.Where(l => l != null))
                {
                    Assert.LessOrEqual(line.Length, 140, line);
                    Assert.IsFalse(line.Contains("\n"), line);
                }
            }
        }
    }
}
