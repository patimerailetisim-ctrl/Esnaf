using System;
using System.Collections.Generic;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Business;
using Esnaf.Domain.Game;
using Esnaf.Domain.Negotiation;
using Esnaf.Domain.Time;
using Esnaf.Presentation;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Presentation
{
    /// <summary>
    /// Günlük müşteri akışı arayüzü (Gün 12.5, UiFlow): satış ekranı lobisi artık günlük müşteri kuyruğunu gösterir (yalnızca şu an aktif müşteri), "Bekle" saati
    /// doğrudan sıradaki müşterinin geliş saatine ilerletir (21:00'i aşmaz; satış sürerken/kapalıyken kullanılamaz), "Gönder" ürünü olmayan müşteriyi gönderir,
    /// üst çubuk saati gösterir. Eski 5-yuva kartları ve altyapı korunur.
    /// </summary>
    public class UiFlowQueueLobbyTests
    {
        private static GameSession New(ulong seed)
        {
            return GameSession.NewGame(MarketHarness.RealContent(), seed);
        }

        private static GameSession WithShelf(ulong seed)
        {
            GameSession s = New(seed);
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

        private static UiFlow OpenLobby(GameSession s)
        {
            UiFlow flow = Flow(s);
            Assert.IsTrue(flow.OpenCustomers());
            return flow;
        }

        private static IReadOnlyList<QueuedCustomer> Plan(GameSession s)
        {
            return s.CustomerQueue.PlanFor(s.Time.Day);
        }

        private static int Now(GameSession s)
        {
            return s.Api.GetClock().MinuteOfDay;
        }

        // Rafta ürünü olan, ilk müşterisi açılıştan sonra gelen ve geldiğinde ilgilendiği ürünü olan bir oturum.
        private static GameSession LateInterestedFirstCustomer()
        {
            for (ulong seed = 1; seed <= 400; seed++)
            {
                GameSession probe = WithShelf(seed);
                QueuedCustomer first = Plan(probe)[0];
                if (first.ArrivalMinute <= StoreHours.OpenMinute)
                {
                    continue;
                }

                probe.Api.AdvanceTime(first.ArrivalMinute - Now(probe));
                CustomerView v = probe.Api.GetActiveCustomer();
                if (v != null && v.InstanceId != 0)
                {
                    return WithShelf(seed);
                }
            }

            throw new InvalidOperationException("No seed.");
        }

        // Rafı boş oturum: ilk müşteri açılıştan sonra gelir ve ilgilenecek ürün bulamaz.
        private static GameSession LateEmptyShelf()
        {
            for (ulong seed = 1; seed <= 100; seed++)
            {
                GameSession s = New(seed);
                if (Plan(s)[0].ArrivalMinute > StoreHours.OpenMinute)
                {
                    return s;
                }
            }

            throw new InvalidOperationException("No seed.");
        }

        // ---------- üst çubuk ----------

        [Test]
        public void TheTopBar_ShowsTheClock_NextToTheDayAndCash_AndFollowsIt()
        {
            GameSession s = New(1UL);
            using (UiFlow flow = Flow(s))
            {
                Assert.AreEqual("09:00", flow.TopBar.ClockText);
                Assert.AreEqual(TurkishTexts.Day(1), flow.TopBar.DayText, "gün aynı");
                Assert.AreEqual(TurkishTexts.Cash(s.Api.GetCash()), flow.TopBar.CashText, "nakit aynı");

                s.Api.AdvanceTime(135);
                flow.Refresh();

                Assert.AreEqual("11:15", flow.TopBar.ClockText);
            }
        }

        [Test]
        public void TheTopBarViewModel_KeepsItsEqualityRules_WithTheClockAsAnOptionalThirdValue()
        {
            var a = new TopBarViewModel("Gün 1", "Nakit: 5 ₺", "09:00");

            Assert.IsTrue(a.Equals(new TopBarViewModel("Gün 1", "Nakit: 5 ₺", "09:00")));
            Assert.IsFalse(a.Equals(new TopBarViewModel("Gün 1", "Nakit: 5 ₺", "09:05")));
            Assert.AreEqual(string.Empty, new TopBarViewModel("Gün 1", "Nakit: 5 ₺").ClockText);
            Assert.AreEqual(string.Empty, new TopBarViewModel("Gün 1", "Nakit: 5 ₺", null).ClockText);
        }

        // ---------- bekleyen müşteri ----------

        [Test]
        public void BeforeTheNextCustomerArrives_TheLobbyShowsTheNextTime_AndAWaitButton_ButNoCustomerCard()
        {
            GameSession s = LateInterestedFirstCustomer();
            QueuedCustomer first = Plan(s)[0];
            using (UiFlow flow = OpenLobby(s))
            {
                QueueLobbyViewModel queue = flow.SaleScreen.Queue;

                Assert.AreEqual(SaleMode.Lobby, flow.SaleScreen.Mode);
                Assert.AreEqual(QueueLobbyState.Waiting, queue.State);
                Assert.AreEqual("09:00", queue.ClockText);
                Assert.AreEqual(TurkishTexts.NextCustomerLine(first.ArrivalText), queue.StatusLine);
                Assert.AreEqual(first.ArrivalText, queue.NextArrivalText);
                Assert.IsNull(queue.Customer, "geliş saati gelmeden aktif müşteri yok");
                Assert.IsTrue(queue.CanWait);
                Assert.IsFalse(queue.CanDismiss);
                Assert.AreEqual("Bekle", queue.WaitButtonText);
                Assert.AreEqual(TurkishTexts.QueueProgress(0, Plan(s).Count), queue.ProgressLine);
                Assert.AreEqual(TurkishTexts.NextCustomerButton(first.ArrivalText), flow.QueueButtonText);
            }
        }

        [Test]
        public void Wait_AdvancesTheClockExactlyToTheNextArrival_AndTheCustomerAppearsAsTheOnlyCard()
        {
            GameSession s = LateInterestedFirstCustomer();
            QueuedCustomer first = Plan(s)[0];
            using (UiFlow flow = OpenLobby(s))
            {
                Result<ClockView> waited = flow.WaitForNextCustomer();

                Assert.IsTrue(waited.IsSuccess, waited.ErrorCode);
                Assert.AreEqual(first.ArrivalMinute, Now(s), "tam geliş saatine");
                Assert.AreEqual(first.ArrivalText, flow.TopBar.ClockText);
                QueueLobbyViewModel queue = flow.SaleScreen.Queue;
                Assert.AreEqual(QueueLobbyState.Arrived, queue.State);
                Assert.IsNotNull(queue.Customer);
                Assert.AreEqual(first.CustomerId, queue.Customer.CustomerId);
                Assert.AreEqual(first.NpcId, queue.Customer.NpcId, "doğru müşteri");
                Assert.AreEqual(flow.Content.CustomerName(first.CustomerId, first.NpcId), queue.Customer.Name, "mevcut ad/portre sistemi");
                Assert.IsFalse(queue.CanWait);
                Assert.AreEqual(TurkishTexts.CustomerArrivedButton(queue.Customer.Name), flow.QueueButtonText);
            }
        }

        [Test]
        public void WaitingAgain_WhileACustomerIsActive_IsRefused_AndTheClockDoesNotMove()
        {
            GameSession s = LateInterestedFirstCustomer();
            using (UiFlow flow = OpenLobby(s))
            {
                flow.WaitForNextCustomer();
                int t = Now(s);

                Result<ClockView> again = flow.WaitForNextCustomer();

                Assert.AreEqual("ui.nobody_to_wait_for", again.ErrorCode);
                Assert.AreEqual(t, Now(s));
                Assert.AreEqual(TurkishTexts.QueueError("ui.nobody_to_wait_for"), flow.StatusMessage);
            }
        }

        // ---------- aktif müşteri → mevcut satış ----------

        [Test]
        public void TappingTheActiveCustomer_StartsTheExistingSale_AndWaitIsRefusedWhileItRuns()
        {
            GameSession s = LateInterestedFirstCustomer();
            using (UiFlow flow = OpenLobby(s))
            {
                flow.WaitForNextCustomer();
                long id = flow.SaleScreen.Queue.Customer.CustomerId;

                Result<SaleView> started = flow.StartSale(id);

                Assert.IsTrue(started.IsSuccess, started.ErrorCode);
                Assert.AreEqual(SaleMode.Talking, flow.SaleScreen.Mode, "mevcut satış ekranı");
                Assert.AreEqual(id, s.Api.GetSale().CustomerId);
                Assert.AreEqual(TurkishTexts.SaleInProgressButton, flow.QueueButtonText);
                int t = Now(s);
                Result<ClockView> wait = flow.WaitForNextCustomer();
                Assert.AreEqual("ui.sale_in_progress", wait.ErrorCode, "satış sürerken Bekle yok");
                Assert.AreEqual(t, Now(s));
            }
        }

        [Test]
        public void AfterTheSale_TheCustomerIsCompleted_AndTheNextOneWaitsForTheirOwnTime()
        {
            GameSession s = LateInterestedFirstCustomer();
            IReadOnlyList<QueuedCustomer> plan = Plan(s);
            using (UiFlow flow = OpenLobby(s))
            {
                flow.WaitForNextCustomer();
                flow.StartSale(flow.SaleScreen.Queue.Customer.CustomerId);
                flow.SaleLetGo();
                Assert.IsTrue(flow.SaleNext());

                QueueLobbyViewModel queue = flow.SaleScreen.Queue;

                Assert.AreEqual(SaleMode.Lobby, flow.SaleScreen.Mode);
                Assert.AreEqual(TurkishTexts.QueueProgress(1, plan.Count), queue.ProgressLine, "müşteri otomatik tamamlandı");
                if (plan[1].ArrivalMinute > Now(s))
                {
                    Assert.AreEqual(QueueLobbyState.Waiting, queue.State, "sıradaki kendi saatini bekliyor");
                    Assert.AreEqual(plan[1].ArrivalText, queue.NextArrivalText);
                    Assert.IsNull(queue.Customer);
                }
                else
                {
                    Assert.AreEqual(plan[1].CustomerId, queue.Customer.CustomerId, "saati gelmiş sıradaki müşteri");
                }
            }
        }

        // ---------- ürünü olmayan müşteri ----------

        [Test]
        public void ACustomerWithNothingToBuy_ShowsADismissButton_AndDismissingUsesTheExistingFlow()
        {
            GameSession s = LateEmptyShelf();
            QueuedCustomer first = Plan(s)[0];
            using (UiFlow flow = OpenLobby(s))
            {
                flow.WaitForNextCustomer();
                QueueLobbyViewModel queue = flow.SaleScreen.Queue;
                Assert.AreEqual(QueueLobbyState.NoInterest, queue.State);
                Assert.IsTrue(queue.CanDismiss);
                Assert.IsFalse(queue.CanWait);
                Assert.AreEqual(first.CustomerId, queue.Customer.CustomerId);
                Assert.AreEqual(TurkishTexts.NoInterestLine, queue.StatusLine);
                int t = Now(s);

                Result<CustomerQueueView> done = flow.DismissActiveCustomer();

                Assert.IsTrue(done.IsSuccess, done.ErrorCode);
                Assert.AreEqual(t + InteractionTime.CompleteCustomer, Now(s), "mevcut 2 dk");
                Assert.AreEqual(TurkishTexts.QueueProgress(1, Plan(s).Count), flow.SaleScreen.Queue.ProgressLine);
            }
        }

        [Test]
        public void DismissingWithNobodyThere_FailsWithATurkishMessage_AndCostsNothing()
        {
            GameSession s = LateEmptyShelf();
            using (UiFlow flow = OpenLobby(s))
            {
                int t = Now(s);

                Result<CustomerQueueView> r = flow.DismissActiveCustomer();

                Assert.AreEqual("queue.no_active_customer", r.ErrorCode);
                Assert.AreEqual(TurkishTexts.QueueError("queue.no_active_customer"), flow.StatusMessage);
                Assert.AreEqual(t, Now(s));
            }
        }

        // ---------- kapalı mağaza, bitiş ----------

        [Test]
        public void WhenTheStoreIsClosed_NoCustomerIsShown_WaitIsRefused_AndNoSaleCanStart()
        {
            GameSession s = WithShelf(3UL);
            long firstId = Plan(s)[0].CustomerId;
            s.Api.AdvanceTime(5000);
            using (UiFlow flow = OpenLobby(s))
            {
                QueueLobbyViewModel queue = flow.SaleScreen.Queue;

                Assert.AreEqual(QueueLobbyState.Closed, queue.State);
                Assert.AreEqual("21:00", queue.ClockText);
                Assert.IsNull(queue.Customer, "21:00'de yeni müşteri yok");
                Assert.IsFalse(queue.CanWait);
                Assert.AreEqual(TurkishTexts.StoreClosedLine, queue.StatusLine);
                Assert.AreEqual(TurkishTexts.StoreClosedButton, flow.QueueButtonText);
                Assert.AreEqual("time.store_closed", flow.WaitForNextCustomer().ErrorCode);
                Assert.AreEqual(StoreHours.CloseMinute, Now(s), "saat 21:00'in üstüne çıkmaz");
                Assert.IsTrue(flow.StartSale(firstId).IsFailure, "kapalıyken satış başlatılamaz");
            }
        }

        [Test]
        public void WalkingThroughTheWholeDay_WithWaitAndDismiss_NeverPassesNineAndEndsInDone()
        {
            for (ulong seed = 1; seed <= 12; seed++)
            {
                GameSession s = New(seed); // raf boş: her müşteri gönderilir
                using (UiFlow flow = OpenLobby(s))
                {
                    int guard = 0;
                    while (guard++ < 60)
                    {
                        QueueLobbyViewModel queue = flow.SaleScreen.Queue;
                        Assert.LessOrEqual(Now(s), StoreHours.CloseMinute, "seed " + seed);
                        if (queue.CanWait)
                        {
                            int before = Now(s);
                            int next = Plan(s).First(c => c.ArrivalMinute > before).ArrivalMinute;
                            Assert.IsTrue(flow.WaitForNextCustomer().IsSuccess);
                            Assert.AreEqual(next, Now(s), "Bekle: tam sıradaki geliş saati, fazlası değil");
                        }
                        else if (queue.CanDismiss)
                        {
                            Assert.IsTrue(flow.DismissActiveCustomer().IsSuccess);
                        }
                        else
                        {
                            break;
                        }
                    }

                    QueueLobbyState end = flow.SaleScreen.Queue.State;
                    Assert.IsTrue(end == QueueLobbyState.Done || end == QueueLobbyState.Closed, "seed " + seed + ": " + end);
                    Assert.AreEqual("ui.nobody_to_wait_for", end == QueueLobbyState.Done ? flow.WaitForNextCustomer().ErrorCode : "ui.nobody_to_wait_for");
                    Assert.LessOrEqual(Now(s), StoreHours.CloseMinute);
                }
            }
        }

        // ---------- eski lobi korunur, domain'e dokunulmaz ----------

        [Test]
        public void TheOldRosterCardsAndTheOldButtonCount_AreKeptInTheViewModel_ButTheMainLobbyIsTheQueue()
        {
            GameSession s = null;
            for (ulong seed = 1; seed <= 200 && s == null; seed++)
            {
                GameSession c = WithShelf(seed);
                if (c.Api.GetCustomers().Count > 0)
                {
                    s = c;
                }
            }

            Assert.IsNotNull(s);
            using (UiFlow flow = OpenLobby(s))
            {
                Assert.AreEqual(s.Api.GetCustomers().Count, flow.SaleScreen.Customers.Count, "eski yuva kartları görünüm modelinde korunur");
                CollectionAssert.AreEqual(
                    s.Api.GetCustomers().Select(c => c.CustomerId).ToArray(), flow.SaleScreen.Customers.Select(c => c.CustomerId).ToArray());
                Assert.AreEqual(TurkishTexts.CustomersButton(s.Api.GetCustomers().Count), flow.CustomersButtonText, "eski düğme yazısı korunur");
                Assert.IsNotNull(flow.SaleScreen.Queue, "ana lobi kuyruktur");
                Assert.LessOrEqual(flow.SaleScreen.Queue.Customer == null ? 0 : 1, 1, "aynı anda en çok bir kart");
                Assert.IsTrue(flow.StartSale(flow.SaleScreen.Customers[0].CustomerId).IsSuccess, "eski yuva müşterisi hâlâ satılabilir");
            }
        }

        [Test]
        public void TheLobbyActions_DoNotTouchTheRngStreams()
        {
            GameSession s = LateInterestedFirstCustomer();
            var rng = s.Capture().Rng;
            using (UiFlow flow = OpenLobby(s))
            {
                flow.WaitForNextCustomer();
                flow.WaitForNextCustomer();
                flow.DismissActiveCustomer();
                flow.Refresh();
            }

            Assert.IsNull(DeepCompare.FirstDifference(rng, s.Capture().Rng));
        }
    }
}
