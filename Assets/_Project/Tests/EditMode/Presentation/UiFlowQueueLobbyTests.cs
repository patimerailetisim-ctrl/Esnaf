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
    /// Gerçek zamanlı mağaza (Gün 12.6, UiFlow): 1 gerçek saniye = 1 oyun dakikası (Tick), müşteriler kendiliğinden gelir, bekleyenler kuyruk olur ve 60 dakika sonra
    /// konuşarak çıkar; toplam müşteri sayısı / "Bekle" / sıradaki geliş saati hiçbir yerde görünmez. Satış, Gönder ve eski 5-yuva korunur.
    /// </summary>
    public class UiFlowQueueLobbyTests
    {
        private static GameSession New(ulong seed)
        {
            return GameSession.NewGame(MarketHarness.RealContent(), seed);
        }

        private static GameSession WithShelf(ulong seed, long listPriceTl = 5900)
        {
            GameSession s = New(seed);
            var guided = s.Market.Listings.Single(l => l.IsGuided);
            Assert.IsTrue(s.Api.StartNegotiation(guided.ListingId).IsSuccess);
            Assert.IsTrue(s.Api.MakeOffer(Money.FromTl(5800)).IsSuccess);
            Assert.IsTrue(s.Api.SetPrice(guided.InstanceId, Money.FromTl(listPriceTl)).IsSuccess);
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

        // Gerçek zamanlı saat: n gerçek saniyeyi 1 saniyelik adımlarla işler.
        private static void TickSeconds(UiFlow flow, int seconds)
        {
            for (int i = 0; i < seconds; i++)
            {
                flow.Tick(1.0);
            }
        }

        // Saati verilen dakikaya kadar gerçek zamanlı akıtır.
        private static void TickUntil(GameSession s, UiFlow flow, int minuteOfDay)
        {
            int guard = 0;
            while (Now(s) < minuteOfDay && guard++ < 2000)
            {
                flow.Tick(1.0);
            }
        }

        // Rafta ürünü olan, ilk müşterisi geldiğinde ilgilendiği ürünü olan bir oturum (aksi halde NoInterest).
        private static GameSession InterestedFirstCustomer()
        {
            for (ulong seed = 1; seed <= 400; seed++)
            {
                GameSession probe = WithShelf(seed);
                QueuedCustomer first = Plan(probe)[0];
                probe.Api.AdvanceTime(first.ArrivalMinute - Now(probe));
                CustomerView v = probe.Api.GetActiveCustomer();
                if (v != null && v.InstanceId != 0 && v.CustomerId == first.CustomerId)
                {
                    return WithShelf(seed);
                }
            }

            throw new InvalidOperationException("No seed.");
        }

        // Rafta fiyatlı ürün var ama fiyatı o kadar yüksek ki hiçbir müşteri ilgilenmez (müşteri gelir, ürünü yoktur).
        private static GameSession UninterestedFirstCustomer()
        {
            GameSession s = WithShelf(1UL, 90000);
            QueuedCustomer first = Plan(s)[0];
            s.Api.AdvanceTime(first.ArrivalMinute - Now(s));
            CustomerView v = s.Api.GetActiveCustomer();
            Assert.IsNotNull(v, "fiyatlı ürün rafta: müşteri gelir");
            Assert.AreEqual(0L, v.InstanceId, "ama hiçbir ürüne ilgi duymaz");
            return WithShelf(1UL, 90000);
        }

        // İlk iki müşteri 45 dakikadan kısa arayla gelen, ilkinin ilgilendiği ürünü olan oturum.
        private static GameSession TwoCloseCustomers()
        {
            for (ulong seed = 1; seed <= 1500; seed++)
            {
                GameSession probe = WithShelf(seed);
                IReadOnlyList<QueuedCustomer> plan = Plan(probe);
                if (plan[1].ArrivalMinute - plan[0].ArrivalMinute > 45)
                {
                    continue;
                }

                probe.Api.AdvanceTime(plan[0].ArrivalMinute - Now(probe));
                CustomerView v = probe.Api.GetActiveCustomer();
                if (v != null && v.InstanceId != 0)
                {
                    return WithShelf(seed);
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

        // ---------- gerçek zamanlı saat ----------

        [Test]
        public void TheGameStartsAtNine_AndOneRealSecondIsOneGameMinute()
        {
            GameSession s = New(1UL);
            using (UiFlow flow = Flow(s))
            {
                Assert.AreEqual(StoreHours.OpenMinute, Now(s));

                int moved = flow.Tick(1.0);

                Assert.AreEqual(1, moved);
                Assert.AreEqual(StoreHours.OpenMinute + 1, Now(s));
                Assert.AreEqual("09:01", flow.TopBar.ClockText);
                TickSeconds(flow, 59);
                Assert.AreEqual("10:00", flow.TopBar.ClockText, "60 saniye = 60 dakika");
            }
        }

        [Test]
        public void SmallTicks_Accumulate_AndOnlyWholeMinutesAreProcessed()
        {
            GameSession s = New(1UL);
            using (UiFlow flow = Flow(s))
            {
                Assert.AreEqual(0, flow.Tick(0.4));
                Assert.AreEqual(0, flow.Tick(0.4));
                Assert.AreEqual(StoreHours.OpenMinute, Now(s));

                Assert.AreEqual(1, flow.Tick(0.4), "0.4+0.4+0.4 = 1.2 sn: 1 dakika, 0.2 birikir");
                Assert.AreEqual(StoreHours.OpenMinute + 1, Now(s));
                Assert.AreEqual(0, flow.Tick(0.7));
                Assert.AreEqual(1, flow.Tick(0.1), "0.2+0.7+0.1 = 1.0 sn");
                Assert.AreEqual(StoreHours.OpenMinute + 2, Now(s));
                Assert.AreEqual(0, flow.Tick(0.0));
                Assert.AreEqual(0, flow.Tick(-3.0), "negatif/sıfır süre saati oynatmaz");
            }
        }

        [Test]
        public void ALongPause_IsCapped_ToFiveSecondsPerTick()
        {
            GameSession s = New(1UL);
            using (UiFlow flow = Flow(s))
            {
                int moved = flow.Tick(600.0);

                Assert.AreEqual((int)UiFlow.MaxTickSeconds, moved, "duraklama sonrası saat sıçramaz");
                Assert.AreEqual(StoreHours.OpenMinute + 5, Now(s));
            }
        }

        [Test]
        public void TheClockStopsAtClosingTime_AndTicksDoNothingAfterwards()
        {
            GameSession s = New(1UL);
            using (UiFlow flow = Flow(s))
            {
                TickSeconds(flow, 900);

                Assert.AreEqual(StoreHours.CloseMinute, Now(s), "21:00'i aşmaz");
                Assert.AreEqual("21:00", flow.TopBar.ClockText);
                Assert.AreEqual(0, flow.Tick(1.0));
                Assert.AreEqual(StoreHours.CloseMinute, Now(s));
            }
        }

        [Test]
        public void ARealTimeDay_IsDeterministic_ForTheSameSeed_AndLeavesTheRngStreamsAlone()
        {
            GameSession a = WithShelf(7UL);
            GameSession b = WithShelf(7UL);
            var rngBefore = a.Capture().Rng;
            using (UiFlow fa = Flow(a))
            using (UiFlow fb = Flow(b))
            {
                TickSeconds(fa, 300);
                for (int i = 0; i < 100; i++)
                {
                    fb.Tick(3.0); // başka adım boyuyla aynı oyun dakikası
                }

                Assert.AreEqual(Now(a), Now(b));
                Assert.AreEqual(a.Api.GetCustomerQueue().Line.Count, b.Api.GetCustomerQueue().Line.Count);
                Assert.IsNull(DeepCompare.FirstDifference(rngBefore, a.Capture().Rng), "saat/kuyruk RNG akışlarına dokunmaz");
            }
        }

        [Test]
        public void TickingWithNothingChanging_RaisesTheClockEvent_ButDoesNotRebuildTheScreen()
        {
            GameSession s = New(1UL); // raf boş: kimse gelmez
            using (UiFlow flow = OpenLobby(s))
            {
                int changed = 0;
                int ticked = 0;
                flow.Changed += () => changed++;
                flow.ClockTicked += () => ticked++;

                TickSeconds(flow, 30);

                Assert.AreEqual(0, changed, "ekran yeniden kurulmaz");
                Assert.AreEqual(30, ticked, "üst çubuk saati her dakika güncellenir");
                Assert.AreEqual("09:30", flow.TopBar.ClockText);
            }
        }

        // ---------- otomatik geliş ----------

        [Test]
        public void BeforeAnyoneArrives_TheLobbyIsEmpty_WithNoCountsNoNextTimeAndNoWaitButton()
        {
            GameSession s = InterestedFirstCustomer();
            using (UiFlow flow = OpenLobby(s))
            {
                QueueLobbyViewModel queue = flow.SaleScreen.Queue;

                Assert.AreEqual(SaleMode.Lobby, flow.SaleScreen.Mode);
                Assert.AreEqual(QueueLobbyState.Empty, queue.State);
                Assert.IsNull(queue.Customer);
                Assert.AreEqual(0, queue.Waiting.Count);
                Assert.IsFalse(queue.CanDismiss);
                Assert.AreEqual(TurkishTexts.NoCustomersLine, queue.StatusLine);
                Assert.AreEqual(TurkishTexts.NoCustomersButton, flow.QueueButtonText);
            }
        }

        [Test]
        public void ACustomerArrivesByThemselves_AtTheirPlannedTime_AsTheActiveCard()
        {
            GameSession s = InterestedFirstCustomer();
            QueuedCustomer first = Plan(s)[0];
            using (UiFlow flow = OpenLobby(s))
            {
                TickUntil(s, flow, first.ArrivalMinute - 1);
                Assert.AreEqual(QueueLobbyState.Empty, flow.SaleScreen.Queue.State, "geliş saatinden bir dakika önce kimse yok");

                flow.Tick(1.0);

                QueueLobbyViewModel queue = flow.SaleScreen.Queue;
                Assert.AreEqual(first.ArrivalMinute, Now(s));
                Assert.AreEqual(QueueLobbyState.Arrived, queue.State);
                Assert.AreEqual(first.CustomerId, queue.Customer.CustomerId);
                Assert.AreEqual(first.NpcId, queue.Customer.NpcId);
                Assert.AreEqual(flow.Content.CustomerName(first.CustomerId, first.NpcId), queue.Customer.Name, "mevcut ad/portre sistemi");
                Assert.AreEqual(TurkishTexts.CustomerArrivedButton(queue.Customer.Name), flow.QueueButtonText);
            }
        }

        [Test]
        public void ACustomerWhoArrivesWhileAnotherIsActive_QueuesUp_AndIsListedLive()
        {
            GameSession s = TwoCloseCustomers();
            IReadOnlyList<QueuedCustomer> plan = Plan(s);
            using (UiFlow flow = OpenLobby(s))
            {
                TickUntil(s, flow, plan[0].ArrivalMinute);
                Assert.AreEqual(0, flow.SaleScreen.Queue.Waiting.Count);

                TickUntil(s, flow, plan[1].ArrivalMinute);

                QueueLobbyViewModel queue = flow.SaleScreen.Queue;
                Assert.AreEqual(plan[0].CustomerId, queue.Customer.CustomerId, "aktif müşteri sırasını korur");
                Assert.AreEqual(1, queue.Waiting.Count);
                Assert.AreEqual(plan[1].CustomerId, queue.Waiting[0].CustomerId);
                Assert.AreEqual(TurkishTexts.ArrivedAt(plan[1].ArrivalText), queue.Waiting[0].ArrivedText, "giriş saati kaydı");
                StringAssert.EndsWith("(+1 sırada)", flow.QueueButtonText);
            }
        }

        [Test]
        public void NoTotalOrProgressOrNextArrivalText_IsShownAnywhereInTheLobby()
        {
            GameSession s = TwoCloseCustomers();
            IReadOnlyList<QueuedCustomer> plan = Plan(s);
            using (UiFlow flow = OpenLobby(s))
            {
                for (int i = 0; i < 400; i++)
                {
                    flow.Tick(1.0);
                    var texts = new List<string> { flow.QueueButtonText, flow.SaleScreen.Queue.StatusLine, flow.StatusMessage };
                    texts.AddRange(flow.SaleScreen.Queue.Waiting.Select(w => w.ArrivedText));
                    foreach (string t in texts.Where(x => x != null))
                    {
                        Assert.IsFalse(t.Contains("müşteri") && t.Contains("/"), "toplam/ilerleme sayacı yok: " + t);
                        Assert.IsFalse(t.Contains("Bekle"), "Bekle yok: " + t);
                        Assert.IsFalse(t.Contains("Sıradaki:"), "sıradaki geliş saati yok: " + t);
                    }
                }

                StringAssert.DoesNotContain("Bugün", flow.SaleScreen.Queue.StatusLine ?? string.Empty);
                Assert.IsNotNull(plan);
            }
        }

        // ---------- bekleme süresi ----------

        [Test]
        public void ACustomerWhoWaitsSixtyMinutes_LeavesOnTheirOwn_WithAnEventAndANaturalLine()
        {
            GameSession s = InterestedFirstCustomer();
            QueuedCustomer first = Plan(s)[0];
            var left = new List<CustomerLeftWaiting>();
            using (s.Bus.Subscribe<CustomerLeftWaiting>(e => left.Add(e)))
            using (UiFlow flow = OpenLobby(s))
            {
                TickUntil(s, flow, first.ArrivalMinute + QueuePolicy.MaxWaitMinutes - 1);
                Assert.IsFalse(left.Any(e => e.CustomerId == first.CustomerId), "59. dakikada hâlâ bekliyor");
                Assert.IsNotNull(flow.SaleScreen.Queue.Customer);

                flow.Tick(1.0);

                CustomerLeftWaiting e0 = left.First(e => e.CustomerId == first.CustomerId);
                Assert.AreEqual(QueueLeaveReason.Timeout, e0.Reason);
                StringAssert.Contains(flow.Content.CustomerName(first.CustomerId, first.NpcId), flow.StatusMessage);
                StringAssert.Contains("\u201C", flow.StatusMessage, "müşterinin sözü tırnak içinde");
                Assert.AreNotEqual(first.CustomerId, flow.SaleScreen.Queue.Customer == null ? 0L : flow.SaleScreen.Queue.Customer.CustomerId);
            }
        }

        [Test]
        public void ACustomerWhoLeftByWaiting_IsNeverSold_AndNoSaleCanStartWithThem()
        {
            GameSession s = InterestedFirstCustomer();
            QueuedCustomer first = Plan(s)[0];
            long cashBefore = s.Api.GetCash().Tl;
            using (UiFlow flow = OpenLobby(s))
            {
                TickUntil(s, flow, first.ArrivalMinute + QueuePolicy.MaxWaitMinutes);

                Assert.IsTrue(flow.StartSale(first.CustomerId).IsFailure, "çıkan müşteriyle satış başlamaz");
                Assert.AreEqual(cashBefore, s.Api.GetCash().Tl, "satış yok");
            }
        }

        [Test]
        public void ACustomerInASale_DoesNotExpire_AndTheSaleCanFinishAfterAnHour()
        {
            GameSession s = InterestedFirstCustomer();
            QueuedCustomer first = Plan(s)[0];
            using (UiFlow flow = OpenLobby(s))
            {
                TickUntil(s, flow, first.ArrivalMinute);
                Result<SaleView> started = flow.StartSale(first.CustomerId);
                Assert.IsTrue(started.IsSuccess, started.ErrorCode);
                Assert.AreEqual(TurkishTexts.SaleInProgressButton, flow.QueueButtonText);

                TickUntil(s, flow, Math.Min(StoreHours.CloseMinute - 1, first.ArrivalMinute + 90));

                Assert.IsNotNull(s.Api.GetSale(), "satıştaki müşteri sabrı bitse bile çıkmaz");
                Assert.AreEqual(first.CustomerId, s.Api.GetSale().CustomerId);
            }
        }

        // ---------- aktif müşteri → mevcut satış ----------

        [Test]
        public void TappingTheActiveCustomer_StartsTheExistingSale()
        {
            GameSession s = InterestedFirstCustomer();
            QueuedCustomer first = Plan(s)[0];
            using (UiFlow flow = OpenLobby(s))
            {
                TickUntil(s, flow, first.ArrivalMinute);
                long id = flow.SaleScreen.Queue.Customer.CustomerId;

                Result<SaleView> started = flow.StartSale(id);

                Assert.IsTrue(started.IsSuccess, started.ErrorCode);
                Assert.AreEqual(SaleMode.Talking, flow.SaleScreen.Mode, "mevcut satış ekranı");
                Assert.AreEqual(id, s.Api.GetSale().CustomerId);
                Assert.AreEqual(TurkishTexts.SaleInProgressButton, flow.QueueButtonText);
            }
        }

        [Test]
        public void AfterTheSale_TheCustomerIsCompleted_AndAnyQueuedCustomerBecomesActive()
        {
            GameSession s = TwoCloseCustomers();
            IReadOnlyList<QueuedCustomer> plan = Plan(s);
            using (UiFlow flow = OpenLobby(s))
            {
                TickUntil(s, flow, plan[1].ArrivalMinute);
                flow.StartSale(flow.SaleScreen.Queue.Customer.CustomerId);
                flow.SaleLetGo();
                Assert.IsTrue(flow.SaleNext());

                QueueLobbyViewModel queue = flow.SaleScreen.Queue;

                Assert.AreEqual(SaleMode.Lobby, flow.SaleScreen.Mode);
                Assert.AreNotEqual(plan[0].CustomerId, queue.Customer == null ? 0L : queue.Customer.CustomerId, "biten müşteri tekrar gelmez");
                if (queue.Customer != null)
                {
                    Assert.AreEqual(plan[1].CustomerId, queue.Customer.CustomerId, "bekleyen sıradaki müşteri öne geçer");
                    Assert.AreEqual(0, queue.Waiting.Count);
                }
            }
        }

        // ---------- ürünü olmayan müşteri ----------

        [Test]
        public void ACustomerWithNothingToBuy_ShowsADismissButton_AndDismissingUsesTheExistingFlow()
        {
            GameSession s = UninterestedFirstCustomer();
            QueuedCustomer first = Plan(s)[0];
            using (UiFlow flow = OpenLobby(s))
            {
                TickUntil(s, flow, first.ArrivalMinute);
                QueueLobbyViewModel queue = flow.SaleScreen.Queue;
                Assert.AreEqual(QueueLobbyState.NoInterest, queue.State);
                Assert.IsTrue(queue.CanDismiss);
                Assert.AreEqual(first.CustomerId, queue.Customer.CustomerId);
                Assert.AreEqual(TurkishTexts.NoInterestLine, queue.StatusLine);
                int t = Now(s);

                Result<CustomerQueueView> done = flow.DismissActiveCustomer();

                Assert.IsTrue(done.IsSuccess, done.ErrorCode);
                Assert.AreEqual(t + InteractionTime.CompleteCustomer, Now(s), "mevcut 2 dk");
                Assert.AreNotEqual(first.CustomerId, flow.SaleScreen.Queue.Customer == null ? 0L : flow.SaleScreen.Queue.Customer.CustomerId);
            }
        }

        [Test]
        public void DismissingWithNobodyThere_FailsWithATurkishMessage_AndCostsNothing()
        {
            GameSession s = InterestedFirstCustomer();
            using (UiFlow flow = OpenLobby(s))
            {
                int t = Now(s);

                Result<CustomerQueueView> r = flow.DismissActiveCustomer();

                Assert.AreEqual("queue.no_active_customer", r.ErrorCode);
                Assert.AreEqual(TurkishTexts.QueueError("queue.no_active_customer"), flow.StatusMessage);
                Assert.AreEqual(t, Now(s));
            }
        }

        // ---------- raf boşsa kimse gelmez ----------

        [Test]
        public void WithAnEmptyShelf_NoCustomerEverArrives_AllDay()
        {
            for (ulong seed = 1; seed <= 6; seed++)
            {
                GameSession s = New(seed);
                var arrived = new List<CustomerArrived>();
                using (s.Bus.Subscribe<CustomerArrived>(e => arrived.Add(e)))
                using (UiFlow flow = OpenLobby(s))
                {
                    for (int i = 0; i < 721; i++)
                    {
                        flow.Tick(1.0);
                        Assert.AreEqual(QueueLobbyState.Empty == flow.SaleScreen.Queue.State || Now(s) >= StoreHours.CloseMinute, true, "seed " + seed);
                    }

                    Assert.AreEqual(0, arrived.Count, "seed " + seed);
                }
            }
        }

        // ---------- kapalı mağaza ----------

        [Test]
        public void WhenTheStoreIsClosed_NoCustomerIsShown_AndNoSaleCanStart()
        {
            GameSession s = InterestedFirstCustomer();
            long firstId = Plan(s)[0].CustomerId;
            s.Api.AdvanceTime(5000);
            using (UiFlow flow = OpenLobby(s))
            {
                QueueLobbyViewModel queue = flow.SaleScreen.Queue;

                Assert.AreEqual(QueueLobbyState.Closed, queue.State);
                Assert.IsNull(queue.Customer, "21:00'de yeni müşteri yok");
                Assert.AreEqual(TurkishTexts.StoreClosedLine, queue.StatusLine);
                Assert.AreEqual(TurkishTexts.StoreClosedButton, flow.QueueButtonText);
                Assert.AreEqual(StoreHours.CloseMinute, Now(s), "saat 21:00'in üstüne çıkmaz");
                Assert.IsTrue(flow.StartSale(firstId).IsFailure, "kapalıyken satış başlatılamaz");
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
                Assert.IsTrue(flow.StartSale(flow.SaleScreen.Customers[0].CustomerId).IsSuccess, "eski yuva müşterisi hâlâ satılabilir");
            }
        }
    }
}
