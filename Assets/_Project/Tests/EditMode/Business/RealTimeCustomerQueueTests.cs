using System.Collections.Generic;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Business;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Game;
using Esnaf.Domain.Negotiation;
using Esnaf.Domain.Time;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Business
{
    /// <summary>
    /// Gün 12.6 — gerçek zamanlı mağaza (domain tarafı): müşteriler geliş saatinde kendiliğinden gelir (CustomerArrived), 60 dakika sonra çıkar (CustomerLeftWaiting),
    /// satıştaki müşteri çıkmaz, rafta satılabilir stok yoksa müşteri gelmez (QueueSkipped), hepsi deterministik ve kayıtla uyumlu; RNG akışlarına dokunulmaz.
    /// </summary>
    public class RealTimeCustomerQueueTests
    {
        private static GameSession New(ulong seed, IEventBus bus = null)
        {
            return GameSession.NewGame(MarketHarness.RealContent(), seed, bus);
        }

        // Rafta bir telefon: priced=false ise fiyatsız (satılabilir stok sayılmaz).
        private static GameSession Stocked(ulong seed, IEventBus bus = null, bool priced = true)
        {
            GameSession s = New(seed, bus);
            var guided = s.Market.Listings.Single(l => l.IsGuided);
            Assert.IsTrue(s.Api.StartNegotiation(guided.ListingId).IsSuccess);
            Assert.IsTrue(s.Api.MakeOffer(Money.FromTl(5800)).IsSuccess);
            if (priced)
            {
                Assert.IsTrue(s.Api.SetPrice(guided.InstanceId, Money.FromTl(5900)).IsSuccess);
            }

            return s;
        }

        private static IReadOnlyList<QueuedCustomer> Plan(GameSession s)
        {
            return s.CustomerQueue.PlanFor(s.Time.Day);
        }

        private static int Now(GameSession s)
        {
            return s.Api.GetClock().MinuteOfDay;
        }

        private static void GoTo(GameSession s, int minute)
        {
            int now = Now(s);
            if (minute > now)
            {
                Assert.IsTrue(s.Api.AdvanceTime(minute - now).IsSuccess);
            }
        }

        // Saati tek tek dakika ilerletir (gerçek zamanlı Tick'in domain karşılığı).
        private static void StepTo(GameSession s, int minute)
        {
            while (Now(s) < minute)
            {
                Assert.IsTrue(s.Api.AdvanceTime(1).IsSuccess);
            }
        }

        // ---------- saat olayı ----------

        [Test]
        public void TheClockRaisesAnAdvancedEvent_WithTheRangeItMoved_AndNothingWhenItDidNotMove()
        {
            GameSession s = New(1UL);
            var ranges = new List<string>();
            s.Clock.Advanced += (day, from, to) => ranges.Add(day + ":" + from + "-" + to);

            s.Api.AdvanceTime(30);
            s.Api.AdvanceTime(StoreHours.CloseMinute); // 21:00'e kırpılır
            s.Api.AdvanceTime(10); // kapalı: hareket yok

            CollectionAssert.AreEqual(
                new[] { "1:540-570", "1:570-" + StoreHours.CloseMinute }, ranges.ToArray());
        }

        // ---------- otomatik geliş ----------

        [Test]
        public void ACustomerArrives_ExactlyAtTheirPlannedMinute_Once_WithAnEvent()
        {
            var bus = new EventBus();
            var arrived = new List<CustomerArrived>();
            bus.Subscribe<CustomerArrived>(arrived.Add);
            GameSession s = Stocked(2UL, bus);
            QueuedCustomer first = Plan(s)[0];

            StepTo(s, first.ArrivalMinute - 1);
            Assert.AreEqual(0, arrived.Count, "bir dakika önce kimse gelmedi");
            StepTo(s, first.ArrivalMinute);
            StepTo(s, first.ArrivalMinute + 5);

            Assert.AreEqual(1, arrived.Count(a => a.CustomerId == first.CustomerId), "tam bir kez");
            Assert.AreEqual(first.NpcId, arrived.First().NpcId);
            Assert.AreEqual(1, arrived.First().Day);
        }

        [Test]
        public void NoCustomerArrivesAtTheOpeningMinute_TheClockStartsAtNine()
        {
            for (ulong seed = 1; seed <= 20; seed++)
            {
                GameSession s = Stocked(seed);
                Assert.AreEqual(StoreHours.OpenMinute, Now(s), "oyun 09:00'da başlar");
                Assert.IsNull(s.Api.GetCustomerQueue().Current, "açılışta kimse yok; seed " + seed);
                Assert.Greater(Plan(s)[0].ArrivalMinute, StoreHours.OpenMinute);
            }
        }

        [Test]
        public void JumpingTheClockAndSteppingItMinuteByMinute_GiveTheSameStoreState()
        {
            GameSession jump = Stocked(6UL);
            GameSession step = Stocked(6UL);

            GoTo(jump, 15 * 60);
            StepTo(step, 15 * 60);

            Assert.AreEqual(jump.Api.GetStateDigest(), step.Api.GetStateDigest());
            Assert.AreEqual(
                jump.Api.GetCustomerQueue().Entries.Select(e => e.Status).ToArray(),
                step.Api.GetCustomerQueue().Entries.Select(e => e.Status).ToArray());
        }

        // ---------- 60 dakika bekleme ----------

        [Test]
        public void ACustomerWhoIsNotServed_LeavesAfterExactlySixtyMinutes_WithATimeoutEvent()
        {
            Assert.AreEqual(60, QueuePolicy.MaxWaitMinutes);
            var bus = new EventBus();
            var left = new List<CustomerLeftWaiting>();
            bus.Subscribe<CustomerLeftWaiting>(left.Add);
            GameSession s = Stocked(2UL, bus);
            QueuedCustomer first = Plan(s)[0];

            StepTo(s, first.ArrivalMinute + 59);
            Assert.IsFalse(left.Any(e => e.CustomerId == first.CustomerId), "59. dakikada hâlâ dükkânda");
            Assert.AreEqual(QueueStatus.Active, s.Api.GetCustomerQueue().Entries.Single(e => e.Customer.CustomerId == first.CustomerId).Status);

            StepTo(s, first.ArrivalMinute + 60);

            CustomerLeftWaiting e0 = left.Single(e => e.CustomerId == first.CustomerId);
            Assert.AreEqual(QueueLeaveReason.Timeout, e0.Reason);
            Assert.AreEqual(QueueStatus.Left, s.Api.GetCustomerQueue().Entries.Single(e => e.Customer.CustomerId == first.CustomerId).Status);
        }

        [Test]
        public void ALeftCustomer_CannotBeSold_IsNotCounted_AsServed_AndCostsNoTime()
        {
            GameSession s = Stocked(2UL);
            QueuedCustomer first = Plan(s)[0];
            GoTo(s, first.ArrivalMinute + 60);
            int t = Now(s);

            Result<SaleView> r = s.Api.StartSale(first.CustomerId);

            Assert.IsTrue(r.IsFailure);
            Assert.AreEqual(t, Now(s), "başarısız çağrı 0 dk");
            Assert.AreEqual(0, s.Api.GetCustomerQueue().Served, "bekleyip çıkan müşteri satış/hizmet sayılmaz");
        }

        [Test]
        public void TheCustomerWhoIsNextInLine_BecomesActive_WhenTheFirstLeaves()
        {
            GameSession s = null;
            IReadOnlyList<QueuedCustomer> plan = null;
            for (ulong seed = 1; seed <= 1500 && s == null; seed++)
            {
                GameSession c = Stocked(seed);
                IReadOnlyList<QueuedCustomer> p = Plan(c);
                if (p[1].ArrivalMinute - p[0].ArrivalMinute <= 45)
                {
                    s = c;
                    plan = p;
                }
            }

            Assert.IsNotNull(s);
            GoTo(s, plan[0].ArrivalMinute + 60);

            CustomerQueueView view = s.Api.GetCustomerQueue();

            Assert.AreEqual(plan[1].CustomerId, view.Current.CustomerId, "ikinci müşteri öne geçer");
            Assert.AreEqual(QueueStatus.Left, view.Entries[0].Status);
        }

        [Test]
        public void ACustomerInASale_NeverLeavesByWaiting_EvenAfterAnHour()
        {
            var bus = new EventBus();
            var left = new List<CustomerLeftWaiting>();
            bus.Subscribe<CustomerLeftWaiting>(left.Add);
            GameSession s = null;
            CustomerView active = null;
            for (ulong seed = 1; seed <= 400 && s == null; seed++)
            {
                GameSession c = Stocked(seed, bus);
                GoTo(c, Plan(c)[0].ArrivalMinute);
                CustomerView v = c.Api.GetActiveCustomer();
                if (v != null && v.InstanceId != 0)
                {
                    s = c;
                    active = v;
                    left.Clear();
                }
            }

            Assert.IsNotNull(s);
            Assert.IsTrue(s.Api.StartSale(active.CustomerId).IsSuccess);

            StepTo(s, System.Math.Min(StoreHours.CloseMinute - 1, Now(s) + 90));

            Assert.IsFalse(left.Any(e => e.CustomerId == active.CustomerId));
            Assert.IsNotNull(s.Api.GetSale());
            Assert.AreEqual(active.CustomerId, s.Api.GetSale().CustomerId);
        }

        [Test]
        public void WhenTheStoreCloses_AWaitingCustomerLeavesWithTheStoreClosedReason()
        {
            var bus = new EventBus();
            var left = new List<CustomerLeftWaiting>();
            bus.Subscribe<CustomerLeftWaiting>(left.Add);
            GameSession s = null;
            QueuedCustomer late = null;
            for (ulong seed = 1; seed <= 600 && s == null; seed++)
            {
                GameSession c = Stocked(seed, bus);
                QueuedCustomer last = Plan(c).Last();
                if (last.ArrivalMinute > StoreHours.CloseMinute - QueuePolicy.MaxWaitMinutes + 5)
                {
                    s = c;
                    late = last;
                }
            }

            Assert.IsNotNull(s, "kapanışa 55 dakikadan az kala gelen bir müşteri bulunmalı");
            left.Clear();
            GoTo(s, late.ArrivalMinute);
            left.Clear();

            GoTo(s, StoreHours.CloseMinute);

            CustomerLeftWaiting e = left.Last(x => x.CustomerId == late.CustomerId);
            Assert.AreEqual(QueueLeaveReason.StoreClosed, e.Reason);
        }

        // ---------- raf boş / stok ----------

        [Test]
        public void WithAnEmptyShelf_NobodyArrivesAllDay_AndTheSkippedMaskIsSaved()
        {
            var bus = new EventBus();
            var arrived = new List<CustomerArrived>();
            bus.Subscribe<CustomerArrived>(arrived.Add);
            GameSession s = New(3UL, bus);
            int n = Plan(s).Count;

            GoTo(s, StoreHours.CloseMinute);

            Assert.AreEqual(0, arrived.Count);
            Assert.IsNull(s.Api.GetCustomerQueue().Current);
            Assert.AreEqual((1 << n) - 1, s.Capture().Customers.QueueSkipped, "gelmeyen müşteriler kayıtta");
            GameSession back = GameSession.Restore(MarketHarness.RealContent(), s.Capture()).Value;
            Assert.AreEqual(s.Api.GetStateDigest(), back.Api.GetStateDigest());
            Assert.AreEqual(0, back.Api.GetCustomerQueue().Waiting);
        }

        [Test]
        public void AnUnpricedPhone_IsNotSellableStock_SoNobodyArrives_UntilItGetsAPrice()
        {
            var bus = new EventBus();
            var arrived = new List<CustomerArrived>();
            bus.Subscribe<CustomerArrived>(arrived.Add);
            GameSession s = Stocked(2UL, bus, priced: false);
            IReadOnlyList<QueuedCustomer> plan = Plan(s);
            GoTo(s, plan[1].ArrivalMinute + 1);
            Assert.AreEqual(0, arrived.Count, "fiyatsız telefon: müşteri talebi yok");

            long instanceId = s.Api.GetInventory().Single().InstanceId;
            Assert.IsTrue(s.Api.SetPrice(instanceId, Money.FromTl(5900)).IsSuccess);
            GoTo(s, StoreHours.CloseMinute - 1);

            Assert.Greater(arrived.Count, 0, "fiyat girilince havuza döner, sonraki müşteriler gelir");
            Assert.IsTrue(arrived.All(a => plan.Skip(2).Any(p => p.CustomerId == a.CustomerId)), "daha önce kaçan müşteriler sonradan gelmez");
        }

        [Test]
        public void WhenTheLastPhoneIsSold_LaterCustomersNoLongerArrive()
        {
            var bus = new EventBus();
            var arrived = new List<CustomerArrived>();
            bus.Subscribe<CustomerArrived>(arrived.Add);
            GameSession s = null;
            CustomerView active = null;
            for (ulong seed = 1; seed <= 400 && s == null; seed++)
            {
                GameSession c = Stocked(seed, bus);
                GoTo(c, Plan(c)[0].ArrivalMinute);
                CustomerView v = c.Api.GetActiveCustomer();
                if (v != null && v.InstanceId != 0)
                {
                    s = c;
                    active = v;
                }
            }

            Assert.IsNotNull(s);
            Assert.IsTrue(s.Api.StartSale(active.CustomerId).IsSuccess);
            Assert.IsTrue(s.Api.AskPrice(Money.FromTl(10)).IsSuccess, "çok düşük fiyat: telefon satılır");
            int before = arrived.Count;

            GoTo(s, StoreHours.CloseMinute);

            Assert.AreEqual(before, arrived.Count, "raf boşaldı: yeni müşteri gelmez");
            Assert.IsFalse(s.Customers.HasSellableStock());
        }

        // ---------- kayıt / özet / RNG ----------

        [Test]
        public void AnUntouchedDay_AddsNothingToTheSaveOrTheDigest_ForTheNewField()
        {
            GameSession s = Stocked(2UL);

            GameSnapshot snap = s.Capture();

            Assert.IsNull(snap.Customers.QueueSkipped);
            Assert.IsFalse(GameStateDigest.Describe(s).Contains("queueSkipped"));
            Assert.IsFalse(Newtonsoft.Json.JsonConvert.SerializeObject(snap).Contains("QueueSkipped"));
        }

        [Test]
        public void ASavedMidDay_RestoresTheSameQueue_AndTheSameLaterDeparture()
        {
            GameSession s = Stocked(2UL);
            QueuedCustomer first = Plan(s)[0];
            GoTo(s, first.ArrivalMinute + 30);

            GameSession r = GameSession.Restore(MarketHarness.RealContent(), s.Capture()).Value;
            GoTo(s, first.ArrivalMinute + 60);
            GoTo(r, first.ArrivalMinute + 60);

            Assert.AreEqual(s.Api.GetStateDigest(), r.Api.GetStateDigest());
            Assert.AreEqual(QueueStatus.Left, r.Api.GetCustomerQueue().Entries[0].Status);
        }

        [Test]
        public void AnInvalidSavedSkippedMask_IsRefused()
        {
            GameSession s = New(1UL);
            foreach (int bad in new[] { -1, 1 << 12, int.MaxValue })
            {
                GameSnapshot snap = s.Capture();
                snap.Customers.QueueSkipped = bad;

                Result<GameSession> back = GameSession.Restore(MarketHarness.RealContent(), snap);

                Assert.IsTrue(back.IsFailure, "maske " + bad);
                Assert.AreEqual("save.invalid", back.ErrorCode);
            }
        }

        [Test]
        public void ARealTimeDay_LeavesTheRngStreamsUntouched()
        {
            GameSession s = Stocked(5UL);
            var before = s.Capture().Rng;

            StepTo(s, StoreHours.CloseMinute);

            Assert.IsNull(DeepCompare.FirstDifference(before, s.Capture().Rng));
        }

        [Test]
        public void ANewDay_ResetsTheSkippedMask_AndTheQueueStartsAgain()
        {
            GameSession s = New(3UL);
            GoTo(s, StoreHours.CloseMinute);
            Assert.IsNotNull(s.Capture().Customers.QueueSkipped);

            Assert.IsTrue(s.Api.EndDay().IsSuccess);

            Assert.IsNull(s.Capture().Customers.QueueSkipped);
            Assert.AreEqual(StoreHours.OpenMinute, Now(s));
            Assert.AreEqual(0, s.Api.GetCustomerQueue().Served);
        }
    }
}
