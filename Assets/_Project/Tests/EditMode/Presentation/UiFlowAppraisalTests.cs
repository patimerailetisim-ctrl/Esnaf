using System;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Appraisal;
using Esnaf.Domain.Game;
using Esnaf.Domain.Market;
using Esnaf.Presentation;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Presentation
{
    public class UiFlowAppraisalTests
    {
        private static GameSession NewSession()
        {
            return GameSession.NewGame(MarketHarness.RealContent(), 1UL);
        }

        private static UiFlow Flow(GameSession session)
        {
            return new UiFlow(session.Api, new ContentPresentation(session.Content), session.Bus);
        }

        private static UiFlow OpenAppraisal(GameSession session, int listingIndex = 0)
        {
            UiFlow flow = Flow(session);
            Assert.IsTrue(flow.OpenListing(flow.Listings[listingIndex].ListingId));
            Assert.IsTrue(flow.OpenAppraisal());
            return flow;
        }

        private static void EndDays(GameSession session, int days)
        {
            for (int i = 0; i < days; i++)
            {
                Assert.IsTrue(session.Api.EndDay().IsSuccess);
            }
        }

        // ---------- akış ----------

        [Test]
        public void OpenAppraisal_FromTheDetail_ShowsTheAppraisalScreen_WithAllLevels()
        {
            GameSession session = NewSession();
            using (UiFlow flow = Flow(session))
            {
                flow.OpenListing(flow.Listings[0].ListingId);
                int raised = 0;
                flow.Changed += () => raised++;

                Assert.IsTrue(flow.OpenAppraisal());

                Assert.AreEqual(UiScreen.Appraisal, flow.CurrentScreen);
                Assert.AreEqual(1, raised);
                AppraisalScreenViewModel screen = flow.AppraisalScreen;
                Assert.AreEqual(flow.Detail.Title, screen.Title);
                CollectionAssert.AreEqual(new[] { "s0", "s1", "s2", "s3" }, screen.Levels.Select(l => l.LevelId).ToArray());
                CollectionAssert.AreEqual(new[] { "Göz muayenesi", "Temel kontrol", "Ayrıntılı kontrol", "Profesyonel ekspertiz" }, screen.Levels.Select(l => l.Name).ToArray());
                Assert.IsNull(screen.SelectedLevelId);
                Assert.IsNull(screen.Result);
            }
        }

        [Test]
        public void OpenAppraisal_NotFromTheDetail_DoesNothing()
        {
            using (UiFlow flow = Flow(NewSession()))
            {
                int raised = 0;
                flow.Changed += () => raised++;

                Assert.IsFalse(flow.OpenAppraisal());

                Assert.AreEqual(UiScreen.Listings, flow.CurrentScreen);
                Assert.IsNull(flow.AppraisalScreen);
                Assert.AreEqual(0, raised);
            }
        }

        [Test]
        public void TheLevels_ShowTheFeeOfTheListingsSegment_AndTheirLockState()
        {
            GameSession session = NewSession();
            using (UiFlow flow = OpenAppraisal(session))
            {
                ListingView listing = session.Api.GetListings()[0];
                var segment = session.Content.GetProduct(listing.DefinitionId).Segment;
                var levels = flow.AppraisalScreen.Levels;

                for (int i = 0; i < levels.Count; i++)
                {
                    AppraisalLevel level = session.Content.Appraisal.Levels[i];
                    Money fee = level.FeeFor(segment);
                    Assert.AreEqual(fee.IsZero ? "Ücretsiz" : "Ücret: " + MoneyFormatter.Format(fee), levels[i].FeeText, level.Id);
                }

                Assert.AreEqual("Hazır", levels[0].StatusText);
                Assert.AreEqual("Kilitli (Gün 3'te açılır)", levels[1].StatusText);
                Assert.AreEqual("Kilitli (Gün 5'te açılır)", levels[2].StatusText);
                Assert.AreEqual("Kilitli (Gün 6'te açılır)", levels[3].StatusText);
                CollectionAssert.AreEqual(new[] { false, true, true, true }, levels.Select(l => l.IsLocked).ToArray());
                Assert.IsTrue(levels.All(l => !l.IsDone));
            }
        }

        [Test]
        public void ALevelThatIsUnlockedButNeedsEquipment_SaysSo()
        {
            GameSession session = NewSession();
            EndDays(session, 5);
            using (UiFlow flow = OpenAppraisal(session))
            {
                var levels = flow.AppraisalScreen.Levels;

                Assert.AreEqual("Hazır", levels[0].StatusText);
                Assert.AreEqual("Hazır", levels[1].StatusText);
                Assert.AreEqual("Hazır", levels[2].StatusText);
                Assert.AreEqual("Cihaz gerekir", levels[3].StatusText);
                Assert.IsTrue(levels.All(l => !l.IsLocked));
            }
        }

        [Test]
        public void SelectLevel_MarksTheLevel_AndAnUnknownLevelIsRefused()
        {
            using (UiFlow flow = OpenAppraisal(NewSession()))
            {
                int raised = 0;
                flow.Changed += () => raised++;

                Assert.IsTrue(flow.SelectLevel("s1"));

                Assert.AreEqual("s1", flow.AppraisalScreen.SelectedLevelId);
                Assert.AreEqual(1, flow.AppraisalScreen.Levels.Count(l => l.IsSelected));
                Assert.IsTrue(flow.AppraisalScreen.Levels.Single(l => l.LevelId == "s1").IsSelected);
                Assert.AreEqual(1, raised);

                Assert.IsFalse(flow.SelectLevel("s9"));

                Assert.AreEqual("s1", flow.AppraisalScreen.SelectedLevelId);
                Assert.AreEqual("Bilinmeyen ekspertiz seviyesi.", flow.StatusMessage);
                Assert.AreEqual(2, raised);
            }
        }

        [Test]
        public void SelectingALevel_ClearsAnEarlierMessage()
        {
            GameSession session = NewSession();
            using (UiFlow flow = OpenAppraisal(session))
            {
                flow.SelectLevel("s2");
                flow.PerformAppraisal();
                Assert.AreEqual("Bu ekspertiz seviyesi hen\u00FCz a\u00E7\u0131lmad\u0131.", flow.StatusMessage);

                flow.SelectLevel("s0");

                Assert.IsNull(flow.StatusMessage);
            }
        }

        [Test]
        public void OpeningTheAppraisal_ClearsAnEarlierMessage()
        {
            GameSession session = NewSession();
            using (UiFlow flow = Flow(session))
            {
                flow.OpenListing(flow.Listings[0].ListingId);
                flow.RequestNegotiation();
                Assert.IsNotNull(flow.StatusMessage);

                Assert.IsTrue(flow.OpenAppraisal());

                Assert.IsNull(flow.StatusMessage);
            }
        }

        [Test]
        public void SelectLevel_OutsideTheAppraisalScreen_DoesNothing()
        {
            using (UiFlow flow = Flow(NewSession()))
            {
                Assert.IsFalse(flow.SelectLevel("s0"));
                Assert.IsNull(flow.AppraisalScreen);
            }
        }

        [Test]
        public void ActionText_FollowsTheSelectedLevelState()
        {
            GameSession session = NewSession();
            using (UiFlow flow = OpenAppraisal(session))
            {
                Assert.AreEqual("Ekspertiz Yaptır", flow.AppraisalScreen.ActionText);
                flow.SelectLevel("s0");
                Assert.AreEqual("Ekspertiz Yaptır", flow.AppraisalScreen.ActionText);

                Assert.IsTrue(flow.PerformAppraisal().IsSuccess);

                Assert.AreEqual("Sonucu Göster", flow.AppraisalScreen.ActionText);
            }
        }

        [Test]
        public void PerformAppraisal_WithoutALevel_ExplainsAndChangesNothing()
        {
            GameSession session = NewSession();
            using (UiFlow flow = OpenAppraisal(session))
            {
                string digest = session.Api.GetStateDigest();

                Result<AppraisalView> result = flow.PerformAppraisal();

                Assert.IsTrue(result.IsFailure);
                Assert.AreEqual("Önce bir ekspertiz seviyesi seç.", flow.StatusMessage);
                Assert.AreEqual(digest, session.Api.GetStateDigest());
            }
        }

        [Test]
        public void PerformAppraisal_OutsideTheAppraisalScreen_IsRefusedAndChangesNothing()
        {
            GameSession session = NewSession();
            using (UiFlow flow = Flow(session))
            {
                string digest = session.Api.GetStateDigest();

                Assert.IsTrue(flow.PerformAppraisal().IsFailure);

                Assert.AreEqual(digest, session.Api.GetStateDigest());
            }
        }

        [Test]
        public void AFreeAppraisal_ShowsTheApiResult_AndTheListingKeepsIt()
        {
            GameSession session = NewSession();
            using (UiFlow flow = OpenAppraisal(session))
            {
                long listingId = flow.SelectedListingId.Value;
                Money cash = session.Api.GetCash();
                flow.SelectLevel("s0");

                Result<AppraisalView> result = flow.PerformAppraisal();

                Assert.IsTrue(result.IsSuccess);
                Assert.IsNull(flow.StatusMessage);
                Assert.AreEqual(cash, session.Api.GetCash(), "s0 ücretsiz");
                AppraisalView known = session.Api.GetAppraisals(listingId).Single();
                Assert.AreEqual(known.ResultId, result.Value.ResultId);
                AppraisalResultViewModel shown = flow.AppraisalScreen.Result;
                Assert.AreEqual("Göz muayenesi", shown.LevelName);
                Assert.AreEqual("Ödenen ücret: 0 ₺", shown.FeeLine);
                Assert.AreEqual(known.Findings.Count, shown.FindingLines.Count);
                for (int i = 0; i < known.Findings.Count; i++)
                {
                    FindingView f = known.Findings[i];
                    Assert.AreEqual(TurkishTexts.Finding(f.Found, f.WordingKey, f.Attribute, f.Confidence), shown.FindingLines[i]);
                }

                Assert.AreEqual(TurkishTexts.Battery(known.BatteryRange), shown.BatteryLine);
                Assert.AreEqual(TurkishTexts.Body(known.BodyRange), shown.BodyLine);
                Assert.AreEqual(TurkishTexts.ValueRange(known.ValueRange), shown.ValueLine);
                Assert.IsTrue(flow.AppraisalScreen.Levels.Single(l => l.LevelId == "s0").IsDone);
                Assert.AreEqual("Yapıldı", flow.AppraisalScreen.Levels.Single(l => l.LevelId == "s0").StatusText);
            }
        }

        [Test]
        public void ALevelWithoutAValueRange_ExplainsWhyThereIsNoRiskCard()
        {
            GameSession session = NewSession();
            using (UiFlow flow = OpenAppraisal(session))
            {
                flow.SelectLevel("s0");
                flow.PerformAppraisal();
                long resultId = session.Api.GetAppraisals(flow.SelectedListingId.Value).Single().ResultId;
                Assert.AreEqual("appraisal.no_value_range", session.Api.GetRiskCard(resultId, session.Api.GetListings()[0].AskingPrice).ErrorCode, "varsayım: s0 değer aralığı vermez");

                AppraisalResultViewModel shown = flow.AppraisalScreen.Result;

                Assert.AreEqual(0, shown.RiskLines.Count);
                Assert.AreEqual("Bu seviye değer aralığı vermediği için risk kartı yok.", shown.RiskNote);
                Assert.AreEqual("Çok", "Çok");
            }
        }

        [Test]
        public void APaidAppraisal_TakesTheRealFee_FromTheCash_AndShowsTheRiskCardFromTheApi()
        {
            GameSession session = NewSession();
            EndDays(session, 2);
            using (UiFlow flow = OpenAppraisal(session))
            {
                ListingView listing = session.Api.GetListings().Single(l => l.ListingId == flow.SelectedListingId.Value);
                Money before = session.Api.GetCash();
                flow.SelectLevel("s1");

                Result<AppraisalView> result = flow.PerformAppraisal();

                Assert.IsTrue(result.IsSuccess, result.ErrorCode);
                Assert.IsTrue(result.Value.Fee.IsPositive);
                Assert.AreEqual(before - result.Value.Fee, session.Api.GetCash(), "ücret gerçek API sonucundan düşer");
                Assert.AreEqual(TurkishTexts.Cash(session.Api.GetCash()), flow.TopBar.CashText);
                AppraisalResultViewModel shown = flow.AppraisalScreen.Result;
                Assert.AreEqual("Ödenen ücret: " + MoneyFormatter.Format(result.Value.Fee), shown.FeeLine);
                Assert.AreEqual(TurkishTexts.ValueRange(result.Value.ValueRange), shown.ValueLine);

                RiskCard card = session.Api.GetRiskCard(result.Value.ResultId, listing.AskingPrice).Value;
                Assert.AreEqual(TurkishTexts.RiskTitle(listing.AskingPrice), shown.RiskTitle);
                Assert.AreEqual(card.Scenarios.Count, shown.RiskLines.Count);
                for (int i = 0; i < card.Scenarios.Count; i++)
                {
                    Assert.AreEqual(TurkishTexts.RiskScenarioLine(card.Scenarios[i]), shown.RiskLines[i]);
                }

                Assert.AreEqual(TurkishTexts.MissProbability(card.MissProbability), shown.MissLine);
                Assert.IsNull(shown.RiskNote);
            }
        }

        [Test]
        public void RepeatingALevel_DoesNotChargeAgain_AndShowsTheSameResult()
        {
            GameSession session = NewSession();
            EndDays(session, 2);
            using (UiFlow flow = OpenAppraisal(session))
            {
                flow.SelectLevel("s1");
                long first = flow.PerformAppraisal().Value.ResultId;
                Money afterFirst = session.Api.GetCash();

                Result<AppraisalView> again = flow.PerformAppraisal();

                Assert.IsTrue(again.IsSuccess);
                Assert.AreEqual(first, again.Value.ResultId);
                Assert.AreEqual(afterFirst, session.Api.GetCash(), "I5: aynı ilan + aynı seviye ikinci kez ücret almaz");
                Assert.AreEqual(1, session.Api.GetAppraisals(flow.SelectedListingId.Value).Count);
            }
        }

        [Test]
        public void ALockedLevel_IsRefusedByTheApi_WithTheTurkishMessage_AndNothingIsCharged()
        {
            GameSession session = NewSession();
            using (UiFlow flow = OpenAppraisal(session))
            {
                Money cash = session.Api.GetCash();
                string digest = session.Api.GetStateDigest();
                flow.SelectLevel("s2");

                Result<AppraisalView> result = flow.PerformAppraisal();

                Assert.IsTrue(result.IsFailure);
                Assert.AreEqual("appraisal.level_locked", result.ErrorCode);
                Assert.AreEqual("Bu ekspertiz seviyesi henüz açılmadı.", flow.StatusMessage);
                Assert.AreEqual(cash, session.Api.GetCash());
                Assert.AreEqual(digest, session.Api.GetStateDigest());
                Assert.IsNull(flow.AppraisalScreen.Result);
                Assert.IsFalse(flow.AppraisalScreen.Levels.Single(l => l.LevelId == "s2").IsDone);
            }
        }

        [Test]
        public void ALevelThatNeedsEquipment_IsRefusedByTheApi_WithItsOwnMessage()
        {
            GameSession session = NewSession();
            EndDays(session, 5);
            using (UiFlow flow = OpenAppraisal(session))
            {
                Money cash = session.Api.GetCash();
                flow.SelectLevel("s3");

                Result<AppraisalView> result = flow.PerformAppraisal();

                Assert.IsTrue(result.IsFailure);
                Assert.AreEqual("appraisal.equipment_missing", result.ErrorCode);
                Assert.AreEqual("Bu ekspertiz için gerekli cihaz yok.", flow.StatusMessage);
                Assert.AreEqual(cash, session.Api.GetCash());
            }
        }

        [Test]
        public void TheResultOfADoneLevel_IsShownAgainWhenTheLevelIsSelected_AfterLeavingAndReturning()
        {
            GameSession session = NewSession();
            EndDays(session, 2);
            using (UiFlow flow = OpenAppraisal(session))
            {
                flow.SelectLevel("s1");
                flow.PerformAppraisal();
                string value = flow.AppraisalScreen.Result.ValueLine;
                flow.Back();
                Assert.AreEqual(UiScreen.Detail, flow.CurrentScreen);

                Assert.IsTrue(flow.OpenAppraisal());
                Assert.IsNull(flow.AppraisalScreen.SelectedLevelId);
                Assert.IsNull(flow.AppraisalScreen.Result);
                Assert.IsTrue(flow.AppraisalScreen.Levels.Single(l => l.LevelId == "s1").IsDone);
                flow.SelectLevel("s1");

                Assert.AreEqual(value, flow.AppraisalScreen.Result.ValueLine);
            }
        }

        [Test]
        public void SelectingALevelThatIsNotDone_ShowsNoResult()
        {
            using (UiFlow flow = OpenAppraisal(NewSession()))
            {
                flow.SelectLevel("s0");

                Assert.IsNull(flow.AppraisalScreen.Result);
                Assert.AreEqual("Ekspertiz Yaptır", flow.AppraisalScreen.ActionText);
            }
        }

        [Test]
        public void Back_FromTheAppraisal_GoesToTheDetail_AndFromTheDetailToTheListings()
        {
            using (UiFlow flow = OpenAppraisal(NewSession()))
            {
                long id = flow.SelectedListingId.Value;
                flow.SelectLevel("s0");
                flow.SelectLevel("s9");
                Assert.IsNotNull(flow.StatusMessage);

                flow.Back();

                Assert.AreEqual(UiScreen.Detail, flow.CurrentScreen);
                Assert.AreEqual(id, flow.SelectedListingId);
                Assert.IsNull(flow.AppraisalScreen);
                Assert.IsNull(flow.StatusMessage);

                flow.Back();

                Assert.AreEqual(UiScreen.Listings, flow.CurrentScreen);
                Assert.AreEqual(id, flow.SelectedListingId);
            }
        }

        [Test]
        public void IfTheListingLeavesTheMarket_TheAppraisalScreenClosesToTheListings()
        {
            GameSession session = NewSession();
            using (UiFlow flow = OpenAppraisal(session))
            {
                long listingId = flow.SelectedListingId.Value;
                session.Api.StartNegotiation(listingId);
                var view = session.Api.GetNegotiation();

                Assert.IsTrue(session.Api.MakeOffer(view.ShownPrice).IsSuccess);

                Assert.AreEqual(UiScreen.Listings, flow.CurrentScreen);
                Assert.IsNull(flow.AppraisalScreen);
            }
        }

        [Test]
        public void TheAppraisalOfAnotherListing_IsNotShown()
        {
            GameSession session = NewSession();
            using (UiFlow flow = Flow(session))
            {
                flow.OpenListing(flow.Listings[0].ListingId);
                flow.OpenAppraisal();
                flow.SelectLevel("s0");
                flow.PerformAppraisal();
                flow.Back();
                flow.Back();

                flow.OpenListing(flow.Listings[1].ListingId);
                flow.OpenAppraisal();

                Assert.IsTrue(flow.AppraisalScreen.Levels.All(l => !l.IsDone));
                flow.SelectLevel("s0");
                Assert.IsNull(flow.AppraisalScreen.Result);
            }
        }
    }
}
