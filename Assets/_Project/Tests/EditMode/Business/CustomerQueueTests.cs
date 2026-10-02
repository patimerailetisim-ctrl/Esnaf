using System.Collections.Generic;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Business;
using Esnaf.Domain.Game;
using Esnaf.Domain.Time;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Business
{
    /// <summary>
    /// Günlük müşteri kuyruğu (Gün 12.2): günde 8–12 müşteri, geliş saatleriyle sırayla, aynı anda 1 aktif; mevcut 10 NPC/kişilikten, tohum + günden
    /// deterministik; mağaza saatine (StoreClock) bağlı; kapalıyken yeni müşteri yok; kayıtla uyumlu; mevcut RNG/müşteri/satış durumu etkilenmez.
    /// </summary>
    public class CustomerQueueTests
    {
        private static GameSession New(ulong seed = 1UL)
        {
            return GameSession.NewGame(MarketHarness.RealContent(), seed);
        }

        private static IReadOnlyList<QueuedCustomer> Plan(GameSession s, int day)
        {
            return s.CustomerQueue.PlanFor(day);
        }

        private static string Fingerprint(IReadOnlyList<QueuedCustomer> plan)
        {
            return string.Join(";", plan.Select(c => c.Index + "|" + c.NpcId + "|" + c.ArrivalMinute));
        }

        // Saati verilen dakikaya getirir (yalnızca ileri).
        private static void GoTo(GameSession s, int minute)
        {
            int now = s.Api.GetClock().MinuteOfDay;
            if (minute > now)
            {
                Assert.IsTrue(s.Api.AdvanceTime(minute - now).IsSuccess);
            }
        }

        // ---------- plan ----------

        [Test]
        public void EveryDay_HasBetweenEightAndTwelveCustomers_ForEverySeed()
        {
            var counts = new HashSet<int>();
            for (ulong seed = 1; seed <= 40; seed++)
            {
                GameSession s = New(seed);
                for (int day = 1; day <= 12; day++)
                {
                    int n = Plan(s, day).Count;
                    Assert.GreaterOrEqual(n, 8, "seed " + seed + " gün " + day);
                    Assert.LessOrEqual(n, 12, "seed " + seed + " gün " + day);
                    counts.Add(n);
                }
            }

            CollectionAssert.AreEquivalent(new[] { 8, 9, 10, 11, 12 }, counts.OrderBy(x => x).ToArray(), "beş sayının hepsi çıkar");
        }

        [Test]
        public void ArrivalTimes_AreOrdered_InsideOpeningHours_AndBeforeClosing()
        {
            for (ulong seed = 1; seed <= 30; seed++)
            {
                GameSession s = New(seed);
                for (int day = 1; day <= 8; day++)
                {
                    IReadOnlyList<QueuedCustomer> plan = Plan(s, day);
                    for (int i = 0; i < plan.Count; i++)
                    {
                        Assert.AreEqual(i, plan[i].Index);
                        Assert.GreaterOrEqual(plan[i].ArrivalMinute, StoreHours.OpenMinute);
                        Assert.Less(plan[i].ArrivalMinute, StoreHours.CloseMinute, "kapanıştan önce gelir");
                        if (i > 0)
                        {
                            Assert.Greater(plan[i].ArrivalMinute, plan[i - 1].ArrivalMinute, "sırayla gelir");
                        }
                    }
                }
            }
        }

        [Test]
        public void TheCustomers_ComeFromTheExistingNpcPool_RespectingAvailabilityAndTheRichLimit()
        {
            GameSession s = New();
            var all = new HashSet<string>(s.Content.Npcs.Select(n => n.Id));
            Assert.AreEqual(10, all.Count, "mevcut 10 NPC");
            for (int day = 1; day <= 15; day++)
            {
                IReadOnlyList<QueuedCustomer> plan = Plan(s, day);
                Assert.IsTrue(plan.All(c => all.Contains(c.NpcId)));
                Assert.IsTrue(plan.All(c => s.Content.GetNpc(c.NpcId).Customer.AvailableFromDay <= day), "henüz açılmamış NPC gelmez (gün " + day + ")");
                Assert.LessOrEqual(plan.Count(c => s.Content.Customers.IsRich(c.NpcId)), s.Content.Customers.RichMaxPerDay, "zengin sınırı");
            }

            Assert.IsTrue(Plan(s, 1).All(c => c.NpcId == "npc.kemal" || c.NpcId == "npc.selin"), "gün 1'de yalnızca ilk iki müşteri");
        }

        [Test]
        public void EveryCustomer_CarriesTheExistingPersonalityProfile()
        {
            GameSession s = New();
            foreach (QueuedCustomer c in Plan(s, 6))
            {
                Assert.IsNotNull(c.Profile, c.NpcId);
                Assert.AreEqual(s.Customers.ProfileOf(c.NpcId).PersonalityId, c.Profile.PersonalityId);
                Assert.AreEqual(c.NpcId, c.Profile.NpcId);
            }
        }

        [Test]
        public void TheSameSeedAndDay_GiveTheSameQueue_RegardlessOfGameState()
        {
            GameSession a = New(9UL);
            GameSession b = New(9UL);
            b.Api.BuyListing(b.Api.GetListings()[0].ListingId);
            b.Api.AdvanceTime(120);

            for (int day = 1; day <= 5; day++)
            {
                Assert.AreEqual(Fingerprint(Plan(a, day)), Fingerprint(Plan(b, day)), "gün " + day);
                Assert.AreEqual(Fingerprint(Plan(a, day)), Fingerprint(Plan(a, day)), "tekrar çağrı");
            }
        }

        [Test]
        public void DifferentSeedsAndDays_GiveDifferentQueues()
        {
            Assert.AreNotEqual(Fingerprint(Plan(New(1UL), 5)), Fingerprint(Plan(New(2UL), 5)));
            GameSession s = New(1UL);
            Assert.AreNotEqual(Fingerprint(Plan(s, 5)), Fingerprint(Plan(s, 6)));
        }

        // ---------- sırayla gelme, tek aktif müşteri ----------

        [Test]
        public void OnlyOneCustomerIsActiveAtATime_AndTheOthersWait()
        {
            GameSession s = New();
            GoTo(s, StoreHours.CloseMinute); // herkesin geliş saati geçti

            CustomerQueueView view = s.Api.GetCustomerQueue();

            Assert.IsNotNull(view.Current);
            Assert.AreEqual(0, view.Current.Index, "ilk sıradaki");
            Assert.AreEqual(1, view.Entries.Count(e => e.Status == QueueStatus.Active), "aynı anda yalnızca 1 aktif");
            Assert.AreEqual(view.Total - 1, view.Waiting);
            Assert.AreEqual(0, view.Served);
        }

        [Test]
        public void NoCustomerIsActive_BeforeTheirArrivalTime_AndTheyBecomeActiveWhenTheClockReachesIt()
        {
            GameSession s = New();
            QueuedCustomer first = Plan(s, 1)[0];
            if (first.ArrivalMinute > StoreHours.OpenMinute)
            {
                CustomerQueueView before = s.Api.GetCustomerQueue();
                Assert.IsNull(before.Current, "açılışta henüz kimse gelmedi");
                Assert.AreEqual(first.ArrivalMinute, before.NextArrivalMinute);
                GoTo(s, first.ArrivalMinute - 1);
                Assert.IsNull(s.Api.GetCustomerQueue().Current);
            }

            GoTo(s, first.ArrivalMinute);

            Assert.AreEqual(first.NpcId, s.Api.GetCustomerQueue().Current.NpcId);
            Assert.IsNull(s.Api.GetCustomerQueue().NextArrivalMinute);
        }

        [Test]
        public void CompletingTheCurrentCustomer_MovesToTheNextOneInOrder()
        {
            GameSession s = New();
            GoTo(s, StoreHours.CloseMinute - 1);
            IReadOnlyList<QueuedCustomer> plan = Plan(s, 1);

            var served = new List<string>();
            for (int i = 0; i < 4; i++)
            {
                CustomerQueueView view = s.Api.GetCustomerQueue();
                Assert.AreEqual(plan[i].Index, view.Current.Index, "sırayla");
                served.Add(view.Current.NpcId);
                Result<CustomerQueueView> done = s.Api.CompleteCurrentCustomer();
                Assert.IsTrue(done.IsSuccess, done.ErrorCode);
                Assert.AreEqual(i + 1, done.Value.Served);
            }

            CollectionAssert.AreEqual(plan.Take(4).Select(c => c.NpcId).ToArray(), served.ToArray());
        }

        [Test]
        public void TheNextCustomerWaits_UntilTheirArrivalTime_AfterTheCurrentOneIsDone()
        {
            GameSession s = New();
            IReadOnlyList<QueuedCustomer> plan = Plan(s, 1);
            GoTo(s, plan[0].ArrivalMinute);
            Assert.IsNotNull(s.Api.GetCustomerQueue().Current);

            CustomerQueueView after = s.Api.CompleteCurrentCustomer().Value;

            Assert.IsNull(after.Current, "ikinci müşterinin saati gelmedi");
            Assert.AreEqual(plan[1].ArrivalMinute, after.NextArrivalMinute);
            GoTo(s, plan[1].ArrivalMinute);
            Assert.AreEqual(1, s.Api.GetCustomerQueue().Current.Index);
        }

        [Test]
        public void CompletingWithoutAnActiveCustomer_IsRefused_AndNothingChanges()
        {
            GameSession s = New();
            QueuedCustomer first = Plan(s, 1)[0];
            if (first.ArrivalMinute == StoreHours.OpenMinute)
            {
                // ilk müşteri açılışta gelir: aktif; önce tamamla, ikincinin saati gelmeden tekrar dene
                s.Api.CompleteCurrentCustomer();
            }

            string digest = s.Api.GetStateDigest();

            Result<CustomerQueueView> r = s.Api.CompleteCurrentCustomer();

            Assert.AreEqual("queue.no_active_customer", r.ErrorCode);
            Assert.AreEqual(digest, s.Api.GetStateDigest());
        }

        // ---------- mağaza kapalı ----------

        [Test]
        public void WhenTheStoreIsClosed_CompletingSendsTheWaitingCustomersAway_AndNoNewCustomerIsCalled()
        {
            GameSession s = New();
            GoTo(s, StoreHours.CloseMinute);
            Assert.IsFalse(s.Api.GetClock().IsOpen);

            CustomerQueueView after = s.Api.CompleteCurrentCustomer().Value;

            Assert.IsNull(after.Current, "kapalıyken yeni müşteri yok");
            Assert.AreEqual(after.Total, after.Served, "kalanlar gönderildi");
            Assert.AreEqual(0, after.Waiting);
            Assert.IsFalse(after.StoreOpen);
            Assert.IsTrue(after.Entries.All(e => e.Status == QueueStatus.Done));
            Assert.AreEqual("queue.no_active_customer", s.Api.CompleteCurrentCustomer().ErrorCode);
        }

        [Test]
        public void NoCustomerArrivesAfterClosingTime()
        {
            GameSession s = New();
            for (int day = 1; day <= 10; day++)
            {
                Assert.IsTrue(Plan(s, day).All(c => c.ArrivalMinute < StoreHours.CloseMinute));
            }
        }

        // ---------- gün sonu ----------

        [Test]
        public void EndingTheDay_StartsANewQueueForTheNewDay_FromTheBeginning()
        {
            GameSession s = New();
            GoTo(s, StoreHours.CloseMinute - 1);
            s.Api.CompleteCurrentCustomer();
            Assert.AreEqual(1, s.Api.GetCustomerQueue().Served);

            Assert.IsTrue(s.Api.EndDay().IsSuccess);

            CustomerQueueView view = s.Api.GetCustomerQueue();
            Assert.AreEqual(2, view.Day);
            Assert.AreEqual(0, view.Served, "yeni gün baştan");
            Assert.AreEqual(Fingerprint(Plan(s, 2)), Fingerprint(view.Entries.Select(e => e.Customer).ToList()), "yeni günün planı");
            Assert.AreNotEqual(Fingerprint(Plan(s, 1)), Fingerprint(view.Entries.Select(e => e.Customer).ToList()));
        }

        // ---------- kayıt ----------

        [Test]
        public void TheQueueProgress_SurvivesSaveAndLoad_AndTheQueueIsTheSame()
        {
            GameSession s = New(4UL);
            GoTo(s, StoreHours.CloseMinute - 1);
            s.Api.CompleteCurrentCustomer();
            s.Api.CompleteCurrentCustomer();

            Result<GameSession> back = GameSession.Restore(MarketHarness.RealContent(), s.Capture());

            Assert.IsTrue(back.IsSuccess, back.ErrorCode + ": " + back.Message);
            CustomerQueueView a = s.Api.GetCustomerQueue();
            CustomerQueueView b = back.Value.Api.GetCustomerQueue();
            Assert.AreEqual(2, b.Served);
            Assert.AreEqual(a.Current.Index, b.Current.Index);
            Assert.AreEqual(Fingerprint(a.Entries.Select(e => e.Customer).ToList()), Fingerprint(b.Entries.Select(e => e.Customer).ToList()));
            Assert.AreEqual(s.Api.GetStateDigest(), back.Value.Api.GetStateDigest());
        }

        [Test]
        public void AnUntouchedQueue_AddsNothingToTheSaveOrTheDigest()
        {
            GameSession s = New();

            GameSnapshot snap = s.Capture();

            Assert.IsNull(snap.Customers.QueueCursor);
            Assert.IsFalse(GameStateDigest.Describe(s).Contains("queueCursor"));
            Assert.IsFalse(Newtonsoft.Json.JsonConvert.SerializeObject(snap).Contains("QueueCursor"));
            Assert.AreEqual(0, GameSession.Restore(MarketHarness.RealContent(), snap).Value.Api.GetCustomerQueue().Served, "alan yoksa 0");
        }

        [Test]
        public void AnAdvancedQueue_ShowsUpInTheDigestAndTheSave()
        {
            GameSession s = New();
            GoTo(s, StoreHours.CloseMinute - 1);
            string before = s.Api.GetStateDigest();

            s.Api.CompleteCurrentCustomer();

            Assert.AreNotEqual(before, s.Api.GetStateDigest());
            Assert.AreEqual(1, s.Capture().Customers.QueueCursor);
            StringAssert.Contains("\"QueueCursor\":1", Newtonsoft.Json.JsonConvert.SerializeObject(s.Capture()));
        }

        [Test]
        public void AnInvalidSavedCursor_IsRefused()
        {
            GameSession s = New();
            foreach (int bad in new[] { -1, 13, 100 })
            {
                GameSnapshot snap = s.Capture();
                snap.Customers.QueueCursor = bad;

                Result<GameSession> back = GameSession.Restore(MarketHarness.RealContent(), snap);

                Assert.IsTrue(back.IsFailure, "imleç " + bad);
                Assert.AreEqual("save.invalid", back.ErrorCode);
            }
        }

        // ---------- mevcut sistemler ----------

        [Test]
        public void TheQueue_DoesNotTouchTheRngStreams_NorRegistersANewOne()
        {
            GameSession s = New(3UL);
            var before = s.Capture().Rng;

            for (int i = 0; i < 5; i++)
            {
                s.Api.GetCustomerQueue();
                Plan(s, i + 1);
            }

            GoTo(s, StoreHours.CloseMinute);
            s.Api.CompleteCurrentCustomer();

            Assert.IsNull(DeepCompare.FirstDifference(before, s.Capture().Rng), "kayıtlı akışlar aynı");
            Assert.IsFalse(s.Capture().Rng.Any(x => x.Name.StartsWith("customer_queue")), "yeni kayıtlı akış yok");
        }

        [Test]
        public void TheExistingCustomerRosterAndLobby_AreUnchangedByTheQueue()
        {
            GameSession a = New(6UL);
            GameSession b = New(6UL);
            b.Api.GetCustomerQueue();
            GoTo(b, 800);
            b.Api.CompleteCurrentCustomer();

            Assert.AreEqual(a.Customers.State.Slots.Count, b.Customers.State.Slots.Count);
            Assert.AreEqual(a.Customers.State.Arrived, b.Customers.State.Arrived);
            CollectionAssert.AreEqual(
                a.Customers.State.Slots.Select(x => x.CustomerId + x.NpcId).ToArray(),
                b.Customers.State.Slots.Select(x => x.CustomerId + x.NpcId).ToArray(), "mevcut yuvalar aynı");
            Assert.AreEqual(a.Api.GetCustomers().Count, b.Api.GetCustomers().Count);
        }

        [Test]
        public void CompletingIsRefused_WhileASaleIsInProgress()
        {
            for (ulong seed = 1; seed <= 200; seed++)
            {
                GameSession s = New(seed);
                var guided = s.Market.Listings.Single(l => l.IsGuided);
                s.Api.StartNegotiation(guided.ListingId);
                s.Api.MakeOffer(Money.FromTl(5800));
                s.Api.SetPrice(guided.InstanceId, Money.FromTl(5900));
                if (s.Api.GetCustomers().Count == 0)
                {
                    continue;
                }

                GoTo(s, StoreHours.CloseMinute - 1);
                Assert.IsTrue(s.Api.StartSale(s.Api.GetCustomers()[0].CustomerId).IsSuccess);

                Assert.AreEqual("queue.sale_in_progress", s.Api.CompleteCurrentCustomer().ErrorCode);
                Assert.AreEqual(0, s.Api.GetCustomerQueue().Served);
                return;
            }

            Assert.Fail("No seed gives a customer.");
        }

        // ---------- Core: türetilmiş akış ----------

        [Test]
        public void RngStreamsDerive_IsStateless_Repeatable_AndLeavesTheRegisteredStreamsAlone()
        {
            var streams = new RngStreams(42UL);
            IRandom registered = streams.Get("market");
            double expectedNext = new RngStreams(42UL).Get("market").NextDouble();

            IRandom d1 = streams.Derive("x");
            IRandom d2 = streams.Derive("x");

            Assert.AreEqual(d1.NextDouble(), d2.NextDouble(), "aynı ad: aynı dizi");
            Assert.AreNotEqual(streams.Derive("x").NextDouble(), streams.Derive("y").NextDouble());
            Assert.AreEqual(1, streams.Capture().Count, "türetilen akış kayda girmez");
            Assert.AreEqual(expectedNext, registered.NextDouble(), "kayıtlı akış etkilenmedi");
            Assert.Throws<System.ArgumentException>(() => streams.Derive(""));
        }
    }
}
