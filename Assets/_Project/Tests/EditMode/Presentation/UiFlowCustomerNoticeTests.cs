using System;
using System.Collections.Generic;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Business;
using Esnaf.Domain.Game;
using Esnaf.Domain.Time;
using Esnaf.Presentation;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Presentation
{
    /// <summary>
    /// Gün 13.1 — global müşteri bildirimi (UiFlow.Notices): CustomerArrived ile doğar, hangi ekranda olunursa olunsun görünür, ekranı zorla değiştirmez, aynı müşteri için bir kez çıkar,
    /// "Müşteriye Git" doğru müşteriyi açar, müşteri 60 oyun dakikası sonunda ayrılınca "ayrıldı" bildirimine döner.
    /// </summary>
    public class UiFlowCustomerNoticeTests
    {
        private static readonly double OneMinute = 1.0 / UiFlow.GameMinutesPerRealSecond;

        private static GameSession WithShelf(ulong seed)
        {
            GameSession s = GameSession.NewGame(MarketHarness.RealContent(), seed);
            var guided = s.Market.Listings.Single(l => l.IsGuided);
            Assert.IsTrue(s.Api.StartNegotiation(guided.ListingId).IsSuccess);
            Assert.IsTrue(s.Api.MakeOffer(Money.FromTl(5800)).IsSuccess);
            Assert.IsTrue(s.Api.SetPrice(guided.InstanceId, Money.FromTl(5900)).IsSuccess);
            return s;
        }

        private static UiFlow Flow(GameSession s)
        {
            return new UiFlow(s.Api, new ContentPresentation(s.Content), s.Bus);
        }

        private static int Now(GameSession s)
        {
            return s.Api.GetClock().MinuteOfDay;
        }

        private static void TickUntil(GameSession s, UiFlow flow, int minute)
        {
            int guard = 0;
            while (Now(s) < minute && guard++ < 2000)
            {
                flow.Tick(OneMinute);
            }
        }

        private static IReadOnlyList<QueuedCustomer> Plan(GameSession s)
        {
            return s.CustomerQueue.PlanFor(s.Time.Day);
        }

        // İlk müşterisi geldiğinde ilgilendiği ürünü olan tohum.
        private static GameSession InterestedFirst()
        {
            for (ulong seed = 1; seed <= 400; seed++)
            {
                GameSession probe = WithShelf(seed);
                probe.Api.AdvanceTime(Plan(probe)[0].ArrivalMinute - Now(probe));
                CustomerView v = probe.Api.GetActiveCustomer();
                if (v != null && v.InstanceId != 0)
                {
                    return WithShelf(seed);
                }
            }

            throw new InvalidOperationException("No seed.");
        }

        // İlk iki müşterisi 45 dakikadan yakın gelen (ikincisi kuyruğa girer) tohum.
        private static GameSession TwoClose()
        {
            for (ulong seed = 1; seed <= 1500; seed++)
            {
                GameSession probe = WithShelf(seed);
                IReadOnlyList<QueuedCustomer> p = Plan(probe);
                if (p[1].ArrivalMinute - p[0].ArrivalMinute > 45)
                {
                    continue;
                }

                probe.Api.AdvanceTime(p[0].ArrivalMinute - Now(probe));
                CustomerView v = probe.Api.GetActiveCustomer();
                if (v != null && v.InstanceId != 0)
                {
                    return WithShelf(seed);
                }
            }

            throw new InvalidOperationException("No seed.");
        }

        // ---------- doğuş ----------

        [Test]
        public void WhenACustomerArrives_ANoticeWithNameAndANaturalLineAppears()
        {
            GameSession s = InterestedFirst();
            QueuedCustomer first = Plan(s)[0];
            using (UiFlow flow = Flow(s))
            {
                TickUntil(s, flow, first.ArrivalMinute - 1);
                Assert.AreEqual(0, flow.Notices.Count, "geliş saatinden önce bildirim yok");

                flow.Tick(OneMinute);

                Assert.AreEqual(1, flow.Notices.Count);
                CustomerNoticeViewModel n = flow.Notices[0];
                string name = flow.Content.CustomerName(first.CustomerId, first.NpcId);
                Assert.AreEqual(first.CustomerId, n.CustomerId);
                Assert.AreEqual(first.NpcId, n.NpcId);
                Assert.AreEqual(TurkishTexts.NoticeArrived(name), n.Title);
                StringAssert.Contains(name, n.Title);
                StringAssert.StartsWith("“", n.Line, "kısa müşteri cümlesi");
                Assert.IsFalse(string.IsNullOrWhiteSpace(n.Line));
                Assert.IsTrue(n.CanGo);
                Assert.AreEqual("Müşteriye Git", n.GoButtonText);
            }
        }

        [Test]
        public void TheArrivalEvent_RaisesTheScreenChangedSignal_SoTheUiRedraws()
        {
            GameSession s = InterestedFirst();
            QueuedCustomer first = Plan(s)[0];
            using (UiFlow flow = Flow(s))
            {
                TickUntil(s, flow, first.ArrivalMinute - 1);
                int changed = 0;
                flow.Changed += () => changed++;

                flow.Tick(OneMinute);

                Assert.GreaterOrEqual(changed, 1, "bildirim doğunca arayüze haber verilir");
            }
        }

        // ---------- ekrandan bağımsız ----------

        [Test]
        public void TheNotice_IsVisibleOnEveryScreen_AndNeverMovesThePlayer()
        {
            GameSession s = InterestedFirst();
            QueuedCustomer first = Plan(s)[0];
            var screens = new Action<UiFlow>[]
            {
                f => { }, // İlanlar
                f => f.OpenShelf(),
                f => f.OpenWholesale(),
                f => f.OpenListing(f.Listings[0].ListingId),
            };
            foreach (Action<UiFlow> go in screens)
            {
                GameSession session = Same(s);
                using (UiFlow flow = Flow(session))
                {
                    go(flow);
                    UiScreen before = flow.CurrentScreen;

                    TickUntil(session, flow, first.ArrivalMinute);

                    Assert.AreEqual(1, flow.Notices.Count, "ekran " + before);
                    Assert.AreEqual(before, flow.CurrentScreen, "oyuncu zorla başka ekrana geçirilmez: " + before);
                }
            }
        }

        // Aynı tohumla yeni oturum.
        private static GameSession Same(GameSession s)
        {
            return WithShelf(s.Time.MasterSeed);
        }

        // ---------- spam ----------

        [Test]
        public void TheSameCustomer_GetsOneNotice_AndTicksDoNotRebuildTheScreenAgain()
        {
            GameSession s = InterestedFirst();
            QueuedCustomer first = Plan(s)[0];
            using (UiFlow flow = Flow(s))
            {
                TickUntil(s, flow, first.ArrivalMinute);
                Assert.AreEqual(1, flow.Notices.Count);
                int changed = 0;
                flow.Changed += () => changed++;

                for (int i = 0; i < 3; i++)
                {
                    flow.Tick(OneMinute);
                }

                Assert.AreEqual(1, flow.Notices.Count(n => n.CustomerId == first.CustomerId), "tek bildirim");
                Assert.AreEqual(0, changed, "bildirim değişmedikçe arayüz her tikte yeniden kurulmaz");
            }
        }

        [Test]
        public void ARepeatedArrivalEventForTheSameCustomer_DoesNotDuplicateTheNotice()
        {
            GameSession s = InterestedFirst();
            QueuedCustomer first = Plan(s)[0];
            using (UiFlow flow = Flow(s))
            {
                TickUntil(s, flow, first.ArrivalMinute);

                s.Bus.Publish(new CustomerArrived(s.Time.Day, first.CustomerId, first.NpcId));
                flow.Refresh();

                Assert.AreEqual(1, flow.Notices.Count);
            }
        }

        [Test]
        public void ADismissedNotice_StaysGone_WhileTheCustomerKeepsWaiting()
        {
            GameSession s = InterestedFirst();
            QueuedCustomer first = Plan(s)[0];
            using (UiFlow flow = Flow(s))
            {
                TickUntil(s, flow, first.ArrivalMinute);

                Assert.IsTrue(flow.DismissNotice(first.CustomerId));
                TickUntil(s, flow, first.ArrivalMinute + 20);

                Assert.AreEqual(0, flow.Notices.Count, "kapatılan bildirim geri gelmez");
                Assert.AreEqual(first.CustomerId, s.Api.GetCustomerQueue().Current.CustomerId, "müşteri beklemeye devam eder");
                Assert.IsFalse(flow.DismissNotice(first.CustomerId), "ikinci kez kapatılacak bir şey yok");
            }
        }

        [Test]
        public void TheCustomerKeepsWaiting_WhileTheNoticeIsShown_AndNothingIsForced()
        {
            GameSession s = InterestedFirst();
            QueuedCustomer first = Plan(s)[0];
            using (UiFlow flow = Flow(s))
            {
                TickUntil(s, flow, first.ArrivalMinute + 30);

                Assert.AreEqual(1, flow.Notices.Count);
                Assert.AreEqual(UiScreen.Listings, flow.CurrentScreen);
                Assert.IsNull(s.Api.GetSale(), "satış kendiliğinden başlamaz");
                Assert.AreEqual(first.CustomerId, s.Api.GetActiveCustomer().CustomerId);
            }
        }

        // ---------- Müşteriye Git ----------

        [Test]
        public void GoToCustomer_OpensTheRightCustomersConversation_FromAnotherScreen()
        {
            GameSession s = InterestedFirst();
            QueuedCustomer first = Plan(s)[0];
            using (UiFlow flow = Flow(s))
            {
                flow.OpenShelf();
                TickUntil(s, flow, first.ArrivalMinute);
                Assert.AreEqual(UiScreen.Shelf, flow.CurrentScreen);

                Result go = flow.GoToCustomer(first.CustomerId);

                Assert.IsTrue(go.IsSuccess, go.ErrorCode);
                Assert.AreEqual(UiScreen.Sale, flow.CurrentScreen);
                Assert.AreEqual(first.CustomerId, s.Api.GetSale().CustomerId, "doğru müşteriyle konuşma açıldı");
                Assert.AreEqual(SaleMode.Talking, flow.SaleScreen.Mode);
            }
        }

        [Test]
        public void GoToCustomer_ForAQueuedCustomer_OpensTheLobby_WithoutStartingTheirSale()
        {
            GameSession s = TwoClose();
            IReadOnlyList<QueuedCustomer> plan = Plan(s);
            using (UiFlow flow = Flow(s))
            {
                TickUntil(s, flow, plan[1].ArrivalMinute);
                Assert.AreEqual(2, flow.Notices.Count);

                Result go = flow.GoToCustomer(plan[1].CustomerId);

                Assert.IsTrue(go.IsSuccess);
                Assert.AreEqual(UiScreen.Sale, flow.CurrentScreen);
                Assert.IsNull(s.Api.GetSale(), "sıradaki müşteri aktif değil: yalnızca lobi açılır");
                Assert.AreEqual(plan[0].CustomerId, flow.SaleScreen.Queue.Customer.CustomerId);
            }
        }

        [Test]
        public void GoToCustomer_IsRefused_ForUnknownOrLeftCustomers_AndTheScreenStays()
        {
            GameSession s = InterestedFirst();
            QueuedCustomer first = Plan(s)[0];
            using (UiFlow flow = Flow(s))
            {
                Assert.AreEqual("ui.notice_unknown", flow.GoToCustomer(first.CustomerId).ErrorCode, "henüz gelmedi");
                TickUntil(s, flow, first.ArrivalMinute + QueuePolicy.MaxWaitMinutes);
                Assert.AreEqual("ui.notice_unknown", flow.GoToCustomer(first.CustomerId).ErrorCode, "ayrıldı");
                Assert.AreEqual(UiScreen.Listings, flow.CurrentScreen);
            }
        }

        // ---------- ayrılma ----------

        [Test]
        public void WhenTheCustomerLeavesAfterSixtyMinutes_TheNoticeBecomesALeftNotice_AndThenExpires()
        {
            GameSession s = InterestedFirst();
            QueuedCustomer first = Plan(s)[0];
            using (UiFlow flow = Flow(s))
            {
                TickUntil(s, flow, first.ArrivalMinute + QueuePolicy.MaxWaitMinutes - 1);
                Assert.IsFalse(flow.Notices[0].IsLeft, "59. dakikada hâlâ bekliyor");

                flow.Tick(OneMinute);

                CustomerNoticeViewModel n = flow.Notices.Single(x => x.CustomerId == first.CustomerId);
                string name = flow.Content.CustomerName(first.CustomerId, first.NpcId);
                Assert.IsTrue(n.IsLeft);
                Assert.IsFalse(n.CanGo, "ayrılan müşteriye gidilmez");
                Assert.AreEqual(TurkishTexts.NoticeLeft(name), n.Title);
                StringAssert.StartsWith("“", n.Line);

                TickUntil(s, flow, Now(s) + UiFlow.LeftNoticeMinutes);
                Assert.IsFalse(flow.Notices.Any(x => x.CustomerId == first.CustomerId), "ayrıldı bildirimi süresi dolunca kalkar");
            }
        }

        [Test]
        public void WhenTheSaleStartsAndEnds_TheNoticeIsRemoved_NotLeftBehind()
        {
            GameSession s = InterestedFirst();
            QueuedCustomer first = Plan(s)[0];
            using (UiFlow flow = Flow(s))
            {
                TickUntil(s, flow, first.ArrivalMinute);
                Assert.IsTrue(flow.GoToCustomer(first.CustomerId).IsSuccess);
                Assert.AreEqual(0, flow.Notices.Count, "konuşulan müşterinin bildirimi gösterilmez");

                flow.SaleLetGo();
                Assert.IsTrue(flow.SaleNext());

                Assert.AreEqual(0, flow.Notices.Count, "satış bitti: bildirim kalmadı");
            }
        }

        [Test]
        public void ANewDay_ClearsTheNotices()
        {
            GameSession s = InterestedFirst();
            QueuedCustomer first = Plan(s)[0];
            using (UiFlow flow = Flow(s))
            {
                TickUntil(s, flow, first.ArrivalMinute);
                Assert.AreEqual(1, flow.Notices.Count);

                Assert.IsTrue(flow.EndDay().IsSuccess);

                Assert.AreEqual(0, flow.Notices.Count);
            }
        }

        // ---------- dokunulmayanlar ----------

        [Test]
        public void TheNotices_DoNotTouchTheRngStreams_OrTheSavedState()
        {
            GameSession s = InterestedFirst();
            QueuedCustomer first = Plan(s)[0];
            var rng = s.Capture().Rng;
            using (UiFlow flow = Flow(s))
            {
                TickUntil(s, flow, first.ArrivalMinute + 10);
                flow.DismissNotice(first.CustomerId);
            }

            Assert.IsNull(DeepCompare.FirstDifference(rng, s.Capture().Rng));
            Assert.IsNull(s.Capture().Customers.QueueSkipped);
        }
    }
}
