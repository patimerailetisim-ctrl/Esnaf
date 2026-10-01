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
    /// <summary>Ekspertiz ekranının yeni görsel katmanı için view-model alanları: hepsi gerçek API sonucundan türer, sahte veri yoktur.</summary>
    public class AppraisalCardViewModelTests
    {
        private static GameSession NewSession()
        {
            return GameSession.NewGame(MarketHarness.RealContent(), 1UL);
        }

        private static UiFlow OpenAppraisal(GameSession session)
        {
            var flow = new UiFlow(session.Api, new ContentPresentation(session.Content), session.Bus);
            Assert.IsTrue(flow.OpenListing(flow.Listings[0].ListingId));
            Assert.IsTrue(flow.OpenAppraisal());
            return flow;
        }

        [Test]
        public void TheHeader_ShowsStorageAndAgeOfTheListing_AndItsModelId()
        {
            GameSession session = NewSession();
            using (UiFlow flow = OpenAppraisal(session))
            {
                ListingView listing = session.Api.GetListings()[0];

                Assert.AreEqual(listing.StorageGb + " GB • " + listing.AgeMonths + " Aylık", flow.AppraisalScreen.Subtitle);
                Assert.AreEqual(listing.DefinitionId, flow.AppraisalScreen.DefinitionId);
            }
        }

        [Test]
        public void TheLevelCodes_AreTheLevelIdsInCapitals_AndTheBadgeFollowsTheSelection()
        {
            GameSession session = NewSession();
            using (UiFlow flow = OpenAppraisal(session))
            {
                CollectionAssert.AreEqual(new[] { "S0", "S1", "S2", "S3" }, flow.AppraisalScreen.Levels.Select(l => l.Code).ToArray());
                Assert.IsNull(flow.AppraisalScreen.LevelBadge);

                flow.SelectLevel("s0");

                Assert.AreEqual("S0 • Göz muayenesi", flow.AppraisalScreen.LevelBadge);
            }
        }

        [Test]
        public void TheResultCards_AreBuiltFromTheRealFindingsAndRanges()
        {
            GameSession session = NewSession();
            using (UiFlow flow = OpenAppraisal(session))
            {
                long listingId = flow.SelectedListingId.Value;
                flow.SelectLevel("s0");
                Assert.IsTrue(flow.PerformAppraisal().IsSuccess);
                AppraisalView known = session.Api.GetAppraisals(listingId).Single();
                AppraisalResultViewModel shown = flow.AppraisalScreen.Result;

                Assert.AreEqual(known.Findings.Count, shown.Findings.Count);
                for (int i = 0; i < known.Findings.Count; i++)
                {
                    FindingView f = known.Findings[i];
                    Assert.AreEqual(f.Found ? UiTone.Warn : UiTone.Good, shown.Findings[i].Tone);
                    Assert.AreEqual(TurkishTexts.FindingText(f.Found, f.WordingKey, f.Attribute), shown.Findings[i].Text);
                    Assert.AreEqual(TurkishTexts.ConfidenceText(f.Confidence), shown.Findings[i].ConfidenceText);
                    Assert.AreEqual(TurkishTexts.AttributeTitle(f.Attribute), shown.Cards[i].Title);
                    Assert.AreEqual(shown.Findings[i].Tone, shown.Cards[i].Tone);
                }

                Assert.AreEqual(known.Findings.Count + 2, shown.Cards.Count, "bulgu kartları + Pil + Kasa");
                Assert.AreEqual("Pil", shown.Cards[known.Findings.Count].Title);
                Assert.AreEqual("Kasa", shown.Cards[known.Findings.Count + 1].Title);
                Assert.AreEqual(
                    known.BatteryRange == null ? TurkishTexts.NotMeasured : TurkishTexts.PercentRangeText(known.BatteryRange),
                    shown.Cards[known.Findings.Count].Value);
                Assert.AreEqual(
                    known.ValueRange == null ? null : TurkishTexts.MoneyRangeText(known.ValueRange),
                    shown.ValueHeadline);
                int warnings = known.Findings.Count(f => f.Found);
                Assert.AreEqual(TurkishTexts.FindingSummary(warnings, known.Findings.Count - warnings), shown.SummaryLine);
            }
        }

        [Test]
        public void TheMarketValue_IsShownAsAnEnDashedMoneyRange()
        {
            Assert.AreEqual("29.000 \u20BA \u2013 33.000 \u20BA", TurkishTexts.MoneyRangeText(new MoneyRange(Money.FromTl(29000), Money.FromTl(33000))));
        }

        [TestCase(0, 0, "")]
        [TestCase(2, 1, "2 uyarı • 1 temiz")]
        [TestCase(0, 3, "0 uyarı • 3 temiz")]
        public void TheFindingSummary_CountsWarningsAndCleanOnes(int warnings, int clean, string expected)
        {
            Assert.AreEqual(expected, TurkishTexts.FindingSummary(warnings, clean));
        }

        [TestCase(PhoneAngle.Front, "Ön")]
        [TestCase(PhoneAngle.Back, "Arka")]
        [TestCase(PhoneAngle.Side, "Yan")]
        [TestCase(PhoneAngle.TopBottom, "Alt/Üst")]
        [TestCase(PhoneAngle.CameraClose, "Kamera")]
        public void ThePhoneAngles_HaveTurkishNames(PhoneAngle angle, string expected)
        {
            Assert.AreEqual(expected, TurkishTexts.PhoneAngleName(angle));
        }
    }
}
