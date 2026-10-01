using System;
using Esnaf.Core;
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
        public void TheFlow_SubscribesToTheCashAndDayEventsOnly()
        {
            GameSession session = NewSession();
            using (Flow(session))
            {
                Assert.AreEqual(1, ((EventBus)session.Bus).SubscriberCount<Esnaf.Domain.Economy.CashChanged>());
                Assert.AreEqual(1, ((EventBus)session.Bus).SubscriberCount<Esnaf.Domain.Time.DayStarted>());
            }
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
    }
}
