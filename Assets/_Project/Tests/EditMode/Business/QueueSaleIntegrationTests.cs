using System;
using System.Collections.Generic;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Accessories;
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
    /// Kuyruk ↔ mevcut müşteri/satış entegrasyonu (Gün 12.3): aktif kuyruk müşterisi mevcut satış/pazarlık sistemiyle çalışır (Direct Accept ve Teklifi Kabul Et dahil),
    /// satış bitince müşteri kuyrukta tamamlanır, sıradaki ancak kendi saatinde aktif olur; save/load, RNG ve eski 5-yuva/lobi davranışı korunur.
    /// </summary>
    public class QueueSaleIntegrationTests
    {
        private static GameSession New(ulong seed)
        {
            return GameSession.NewGame(MarketHarness.RealContent(), seed);
        }

        // Raftaki etiketli bir telefon (kuyruk müşterisi ilgilenebilsin).
        private static GameSession WithShelf(ulong seed, long listPriceTl = 5900)
        {
            GameSession s = New(seed);
            var guided = s.Market.Listings.Single(l => l.IsGuided);
            Assert.IsTrue(s.Api.StartNegotiation(guided.ListingId).IsSuccess);
            Assert.IsTrue(s.Api.MakeOffer(Money.FromTl(5800)).IsSuccess);
            Assert.IsTrue(s.Api.SetPrice(guided.InstanceId, Money.FromTl(listPriceTl)).IsSuccess);
            return s;
        }

        private static void GoTo(GameSession s, int minute)
        {
            int now = s.Api.GetClock().MinuteOfDay;
            if (minute > now)
            {
                Assert.IsTrue(s.Api.AdvanceTime(minute - now).IsSuccess);
            }
        }

        private static IReadOnlyList<QueuedCustomer> Plan(GameSession s)
        {
            return s.CustomerQueue.PlanFor(s.Time.Day);
        }

        /// <summary>İlk kuyruk müşterisinin geldiği ve ilgilendiği ürünün olduğu, koşulu sağlayan bir oturum.</summary>
        private static GameSession WithActive(Func<CustomerView, bool> accept, out CustomerView active)
        {
            for (ulong seed = 1; seed <= 400; seed++)
            {
                GameSession s = WithShelf(seed);
                GoTo(s, Plan(s)[0].ArrivalMinute);
                CustomerView v = s.Api.GetActiveCustomer();
                if (v != null && v.InstanceId != 0 && accept(v))
                {
                    active = v;
                    return s;
                }
            }

            throw new InvalidOperationException("No seed gives the wanted active queue customer.");
        }

        private static long MaxOf(GameSession s, CustomerView active)
        {
            QueuedCustomer q;
            CustomerSlot slot;
            Assert.IsTrue(s.CustomerQueue.TryResolve(active.CustomerId, out q, out slot));
            return (long)s.Customers.MaxFor(slot, s.Store.Get(active.InstanceId), false);
        }

        // Max'ın %98'i (10 ₺'ye aşağı yuvarlı): hoşgörülü müşterinin tavanının altında, ilk tur teklifinin üstünde.
        private static long AskNear(GameSession s, CustomerView active)
        {
            return (long)Math.Floor(MaxOf(s, active) * 0.98 / 10.0) * 10L;
        }

        private static int SalesRows(GameSession s)
        {
            return s.EconomyState.Ledger.Records.Count(r => r.TypeId == TransactionTypeIds.Sale);
        }

        // ---------- kimlik ----------

        [Test]
        public void QueueCustomerIds_ComeFromOneHelper_AreUniquePerDayAndIndex_AndNeverClashWithRosterIds()
        {
            var seen = new HashSet<long>();
            for (int day = 1; day <= 30; day++)
            {
                for (int index = 0; index < 12; index++)
                {
                    long id = QueueCustomerId.For(day, index);
                    Assert.IsTrue(seen.Add(id), "benzersiz");
                    Assert.IsTrue(QueueCustomerId.IsQueueId(id));
                    int d;
                    int i;
                    Assert.IsTrue(QueueCustomerId.TryDecode(id, out d, out i));
                    Assert.AreEqual(day, d);
                    Assert.AreEqual(index, i);
                }
            }

            GameSession s = New(1UL);
            Assert.IsTrue(s.Customers.State.Slots.All(x => !QueueCustomerId.IsQueueId(x.CustomerId)), "yuva kimlikleri kuyruk aralığında değil");
            int dd;
            int ii;
            Assert.IsFalse(QueueCustomerId.TryDecode(5L, out dd, out ii));
        }

        [Test]
        public void TheQueuePlan_CarriesTheSameIdsTheSaleUses()
        {
            GameSession s = New(2UL);

            foreach (QueuedCustomer c in Plan(s))
            {
                Assert.AreEqual(QueueCustomerId.For(s.Time.Day, c.Index), c.CustomerId);
            }
        }

        // ---------- geliş saati ----------

        [Test]
        public void NoCustomerIsActive_BeforeTheirArrivalTime_AndTheRightOneIsActiveAtIt()
        {
            for (ulong seed = 1; seed <= 100; seed++)
            {
                GameSession s = WithShelf(seed);
                QueuedCustomer first = Plan(s)[0];
                if (first.ArrivalMinute <= StoreHours.OpenMinute)
                {
                    continue;
                }

                Assert.IsNull(s.Api.GetActiveCustomer(), "açılışta aktif müşteri yok");
                GoTo(s, first.ArrivalMinute - 1);
                Assert.IsNull(s.Api.GetActiveCustomer(), "geliş saatinden bir dakika önce yok");
                Assert.AreEqual("customer.unknown", s.Api.StartSale(first.CustomerId).ErrorCode, "gelmeden satış başlatılamaz");

                GoTo(s, first.ArrivalMinute);

                CustomerView active = s.Api.GetActiveCustomer();
                Assert.IsNotNull(active);
                Assert.AreEqual(first.CustomerId, active.CustomerId);
                Assert.AreEqual(first.NpcId, active.NpcId, "doğru müşteri");
                Assert.IsNotNull(active.Profile, "mevcut kişilik profili");
                Assert.AreEqual(first.NpcId, active.Profile.NpcId);
                return;
            }

            Assert.Fail("No seed with a late first arrival.");
        }

        [Test]
        public void OnlyOneCustomerIsActive_EvenWhenManyHaveArrived()
        {
            GameSession s = WithShelf(3UL);
            GoTo(s, Plan(s)[0].ArrivalMinute + QueuePolicy.MaxWaitMinutes - 1); // Gün 12.6: ilk müşterinin sabrı bitmeden, birden çok müşteri gelmiş olabilir

            CustomerQueueView view = s.Api.GetCustomerQueue();
            CustomerView active = s.Api.GetActiveCustomer();

            Assert.AreEqual(1, view.Entries.Count(e => e.Status == QueueStatus.Active));
            Assert.AreEqual(view.Current.CustomerId, active.CustomerId, "aktif müşteri kuyruğun aktifiyle aynı");
            Assert.AreEqual(0, view.Current.Index);
        }

        // ---------- satış akışı ----------

        [Test]
        public void TheActiveQueueCustomer_SellsThroughTheExistingSaleFlow_AndIsCompletedWhenTheSaleEnds()
        {
            CustomerView active;
            GameSession s = WithActive(v => true, out active);
            Money cash = s.Api.GetCash();
            int sales = SalesRows(s);

            Result<SaleView> started = s.Api.StartSale(active.CustomerId);
            Assert.IsTrue(started.IsSuccess, started.ErrorCode);
            Assert.AreEqual(active.CustomerId, started.Value.CustomerId, "satışın müşteri kimliği = kuyruk kimliği");
            Assert.AreEqual(active.NpcId, started.Value.NpcId);
            Assert.AreEqual(active.InstanceId, started.Value.InstanceId);
            Assert.AreEqual(0, s.Api.GetCustomerQueue().Served);

            Result<SaleView> deal = s.Api.AskPrice(Money.FromTl(10)); // çok düşük istek: müşterinin teklifinden anlaşır

            Assert.IsTrue(deal.IsSuccess, deal.ErrorCode);
            Assert.AreEqual(NegotiationPhase.Deal, deal.Value.Phase);
            Assert.AreEqual(sales + 1, SalesRows(s), "normal satış satırı");
            TransactionRecord row = s.EconomyState.Ledger.Records.Last(r => r.TypeId == TransactionTypeIds.Sale);
            Assert.AreEqual(deal.Value.DealPrice, row.Amount);
            Assert.AreEqual(active.NpcId, row.NpcId);
            Assert.AreEqual(cash + deal.Value.DealPrice, s.Api.GetCash());
            Assert.AreEqual(1, s.Api.GetCustomerQueue().Served, "satış bitince müşteri tamamlandı");
            Assert.IsNull(s.Api.GetSale());
        }

        [Test]
        public void ALeavingQueueCustomer_IsCompletedToo_AndTheItemStaysOnTheShelf()
        {
            CustomerView active;
            GameSession s = WithActive(v => true, out active);
            Assert.IsTrue(s.Api.StartSale(active.CustomerId).IsSuccess);

            Result<SaleView> left = s.Api.LetCustomerGo();

            Assert.IsTrue(left.IsSuccess, left.ErrorCode);
            Assert.AreEqual(1, s.Api.GetCustomerQueue().Served);
            Assert.AreEqual(1, s.Api.GetInventory().Count, "ürün rafta kaldı");
            Assert.AreEqual(0, SalesRows(s));
        }

        [Test]
        public void TheNextCustomer_BecomesActiveOnlyAtTheirOwnArrivalTime()
        {
            CustomerView active;
            GameSession s = WithActive(v => true, out active);
            IReadOnlyList<QueuedCustomer> plan = Plan(s);
            s.Api.StartSale(active.CustomerId);
            s.Api.LetCustomerGo();
            Assert.AreEqual(1, s.Api.GetCustomerQueue().Served);

            Assert.IsNull(s.Api.GetActiveCustomer(), "ikinci müşterinin saati gelmedi");
            Assert.AreEqual("customer.unknown", s.Api.StartSale(plan[1].CustomerId).ErrorCode);
            GoTo(s, plan[1].ArrivalMinute - 1);
            Assert.IsNull(s.Api.GetActiveCustomer());

            GoTo(s, plan[1].ArrivalMinute);

            CustomerView next = s.Api.GetActiveCustomer();
            Assert.IsNotNull(next);
            Assert.AreEqual(plan[1].CustomerId, next.CustomerId);
            Assert.AreEqual(plan[1].NpcId, next.NpcId);
        }

        [Test]
        public void OnlyTheActiveCustomerCanBeStarted_NotALaterOneNorAnUnknownId()
        {
            CustomerView active;
            GameSession s = WithActive(v => true, out active);
            IReadOnlyList<QueuedCustomer> plan = Plan(s);
            // Gün 12.6: saat ilerletilmez (aktif müşterinin sabrı bitip çıkardı); ikinci müşteri aktif olmadığı için başlatılamaz.
            Assert.AreEqual("customer.unknown", s.Api.StartSale(plan[1].CustomerId).ErrorCode, "ikinci müşteri aktif değil");
            Assert.AreEqual("customer.unknown", s.Api.StartSale(QueueCustomerId.For(s.Time.Day + 1, 0)).ErrorCode, "başka günün müşterisi");
            Assert.AreEqual("customer.unknown", s.Api.StartSale(QueueCustomerId.For(s.Time.Day, 50)).ErrorCode);
            Assert.IsNull(s.Api.GetSale());
            Assert.IsTrue(s.Api.StartSale(active.CustomerId).IsSuccess);
            Assert.AreEqual("negotiation.in_progress", s.Api.StartSale(plan[1].CustomerId).ErrorCode, "aynı anda tek satış");
        }

        // ---------- mağaza kapalı ----------

        [Test]
        public void WhenTheStoreIsClosed_NoCustomerIsActive_AndNoSaleCanStart_ButTheWaitingOneCanBeDismissed()
        {
            CustomerView active;
            GameSession s = WithActive(v => true, out active);
            GoTo(s, StoreHours.CloseMinute);

            Assert.IsNull(s.Api.GetActiveCustomer(), "kapalıyken yeni müşteri aktif olmaz");
            Assert.AreEqual("customer.unknown", s.Api.StartSale(active.CustomerId).ErrorCode);

            // Gün 12.6: bekleyenler sabrı bitince/kapanışta kendiliğinden çıkar; gönderilecek kimse kalmaz.
            Assert.AreEqual("queue.no_active_customer", s.Api.CompleteCurrentCustomer().ErrorCode);
            Assert.AreEqual(0, s.Api.GetCustomerQueue().Waiting);
            Assert.IsNull(s.Api.GetActiveCustomer());
        }

        [Test]
        public void ASaleThatStartedBeforeClosing_ContinuesAfterClosing_AndThenTheQueueIsClosed()
        {
            CustomerView active;
            GameSession s = WithActive(v => true, out active);
            Assert.IsTrue(s.Api.StartSale(active.CustomerId).IsSuccess);
            GoTo(s, StoreHours.CloseMinute);

            Result<SaleView> deal = s.Api.AskPrice(Money.FromTl(10));

            Assert.IsTrue(deal.IsSuccess, deal.ErrorCode);
            Assert.AreEqual(NegotiationPhase.Deal, deal.Value.Phase);
            CustomerQueueView view = s.Api.GetCustomerQueue();
            Assert.AreEqual(view.Total, view.Served, "mağaza kapalı: yeni müşteri çağrılmaz");
            Assert.IsNull(s.Api.GetActiveCustomer());
        }

        // ---------- ürünü olmayan müşteri ----------

        [Test]
        public void ACustomerWithNoProductOnTheShelf_IsActiveButNotSellable_AndCompleteCurrentCustomerSendsThemAway()
        {
            GameSession s = WithShelf(5UL, 90000); // Gün 12.6: raf boşsa müşteri gelmez; fiyatlı ama hiç kimsenin ilgilenmeyeceği kadar pahalı ürün
            GoTo(s, Plan(s)[0].ArrivalMinute);

            CustomerView active = s.Api.GetActiveCustomer();

            Assert.IsNotNull(active);
            Assert.AreEqual(0, active.InstanceId, "ilgilendiği ürün yok");
            Assert.AreEqual("customer.no_interest", s.Api.StartSale(active.CustomerId).ErrorCode);
            Assert.IsTrue(s.Api.CompleteCurrentCustomer().IsSuccess);
            Assert.AreEqual(1, s.Api.GetCustomerQueue().Served);
        }

        [Test]
        public void DismissingQueueCustomers_DoesNotTouchTheMissedCustomerStatistics()
        {
            GameSession a = New(5UL);
            GameSession b = New(5UL);
            GoTo(b, StoreHours.CloseMinute - 1);
            b.Api.CompleteCurrentCustomer();
            b.Api.CompleteCurrentCustomer();

            int missedA = a.Api.EndDay().Value.MissedCustomers;
            int missedB = b.Api.EndDay().Value.MissedCustomers;

            Assert.AreEqual(missedA, missedB, "kaçan müşteri sayısı yuva sistemine bağlı kalır");
            Assert.AreEqual(a.Customers.State.MissedTotal, b.Customers.State.MissedTotal);
        }

        // ---------- Direct Accept ve Teklifi Kabul Et ----------

        [Test]
        public void ADirectlyAcceptingQueueCustomer_BuysAtTheAskedPrice()
        {
            CustomerView active;
            GameSession s = WithActive(v => v.NpcId == "npc.selin", out active);
            long ask = AskNear(s, active);
            Assert.IsTrue(s.Api.StartSale(active.CustomerId).IsSuccess);

            Result<SaleView> result = s.Api.AskPrice(Money.FromTl(ask));

            Assert.IsTrue(result.IsSuccess, result.ErrorCode);
            Assert.AreEqual(NegotiationPhase.Deal, result.Value.Phase, "pazarlıksız kabul");
            Assert.AreEqual(Money.FromTl(ask), result.Value.DealPrice, "satış fiyatı = istenen fiyat");
            Assert.AreEqual(1, result.Value.Round);
            TransactionRecord row = s.EconomyState.Ledger.Records.Last(r => r.TypeId == TransactionTypeIds.Sale);
            Assert.AreEqual(Money.FromTl(ask), row.Amount);
            Assert.AreEqual(1, s.Api.GetCustomerQueue().Served, "kuyruk müşterisi tamamlandı");
        }

        [Test]
        public void ABargainingQueueCustomer_KeepsNegotiating_AndTheCounterOfferCanBeAccepted()
        {
            CustomerView active;
            GameSession s = WithActive(v => v.NpcId == "npc.kemal", out active);
            long ask = AskNear(s, active);
            Assert.IsTrue(s.Api.StartSale(active.CustomerId).IsSuccess);

            SaleView counter = s.Api.AskPrice(Money.FromTl(ask)).Value;

            Assert.AreNotEqual(NegotiationPhase.Deal, counter.Phase, "pazarlıkçı direkt kabul etmez");
            Assert.IsNotNull(s.Api.GetSale());
            Assert.AreEqual(0, s.Api.GetCustomerQueue().Served, "satış sürerken müşteri tamamlanmadı");
            Money offer = counter.ShownPrice;
            Assert.Less(offer.Tl, ask);

            Result<SaleView> accepted = s.Api.AcceptCustomerOffer();

            Assert.IsTrue(accepted.IsSuccess, accepted.ErrorCode);
            Assert.AreEqual(offer, accepted.Value.DealPrice, "Teklifi Kabul Et: müşterinin teklif fiyatı");
            Assert.AreEqual(offer, s.EconomyState.Ledger.Records.Last(r => r.TypeId == TransactionTypeIds.Sale).Amount);
            Assert.AreEqual(1, s.Api.GetCustomerQueue().Served);
        }

        [Test]
        public void AfterAQueueSale_TheAccessoryAddOnFlowUsesTheSameSaleAndBuyer()
        {
            CustomerView active;
            GameSession s = WithActive(v => true, out active);
            s.Api.StartSale(active.CustomerId);
            s.Api.AskPrice(Money.FromTl(10));

            AccessoryAddOnView view = s.Api.GetAccessoryAddOns();

            Assert.IsTrue(view.HasPhoneSale);
            Assert.AreEqual(active.NpcId, view.BuyerNpcId);
            Assert.AreEqual(s.EconomyState.Ledger.Records.Last(r => r.TypeId == TransactionTypeIds.Sale).Id, view.PhoneSaleRecordId);
        }

        // ---------- save / load ----------

        [Test]
        public void AnOpenQueueSale_SurvivesSaveAndLoad_WithTheQueueLinkIntact_AndCanBeFinished()
        {
            CustomerView active;
            GameSession s = WithActive(v => true, out active);
            s.Api.StartSale(active.CustomerId);
            s.Api.AskPrice(Money.FromTl(MaxOf(s, active) * 2)); // pahalı istek: pazarlık sürer
            Assert.IsNotNull(s.Api.GetSale());

            Result<GameSession> back = GameSession.Restore(MarketHarness.RealContent(), s.Capture());

            Assert.IsTrue(back.IsSuccess, back.ErrorCode + ": " + back.Message);
            GameSession r = back.Value;
            Assert.AreEqual(active.CustomerId, r.Api.GetSale().CustomerId, "satışın kuyruk kimliği korundu");
            Assert.AreEqual(active.NpcId, r.Api.GetSale().NpcId);
            Assert.AreEqual(s.Api.GetStateDigest(), r.Api.GetStateDigest());
            Assert.AreEqual(s.Api.GetCustomerQueue().Current.CustomerId, r.Api.GetCustomerQueue().Current.CustomerId);
            Assert.AreEqual(0, r.Api.GetCustomerQueue().Served);

            Result<SaleView> deal = r.Api.AskPrice(Money.FromTl(10));
            Assert.IsTrue(deal.IsSuccess, deal.ErrorCode);
            Assert.AreEqual(NegotiationPhase.Deal, deal.Value.Phase);
            Assert.AreEqual(1, r.Api.GetCustomerQueue().Served, "yüklenen satış bitince müşteri kuyrukta tamamlandı");
        }

        [Test]
        public void AfterACompletedQueueSale_SaveAndLoadKeepTheQueueAndTheNextCustomer()
        {
            CustomerView active;
            GameSession s = WithActive(v => true, out active);
            IReadOnlyList<QueuedCustomer> plan = Plan(s);
            s.Api.StartSale(active.CustomerId);
            s.Api.AskPrice(Money.FromTl(10));
            GoTo(s, plan[1].ArrivalMinute);

            GameSession r = GameSession.Restore(MarketHarness.RealContent(), s.Capture()).Value;

            Assert.AreEqual(1, r.Api.GetCustomerQueue().Served);
            // Gün 12.6: satılan telefonla raf boşaldı; rafta satılabilir ürün yokken sıradaki müşteri gelmez (kayıtta da aynı).
            Assert.IsNull(r.Api.GetActiveCustomer(), "raf boş: sıradaki müşteri gelmedi");
            Assert.AreEqual(s.Api.GetCustomerQueue().Entries.Count(e => e.Status == QueueStatus.Active), r.Api.GetCustomerQueue().Entries.Count(e => e.Status == QueueStatus.Active));
            Assert.AreEqual(s.Api.GetStateDigest(), r.Api.GetStateDigest());
        }

        [Test]
        public void ARestoredQueueSale_ThatDoesNotMatchTodaysQueue_IsRefused()
        {
            CustomerView active;
            GameSession s = WithActive(v => true, out active);
            s.Api.StartSale(active.CustomerId);
            s.Api.AskPrice(Money.FromTl(MaxOf(s, active) * 2));
            long other = QueueCustomerId.For(s.Time.Day, 5);
            long otherDay = QueueCustomerId.For(s.Time.Day + 1, 0);
            long beyond = QueueCustomerId.For(s.Time.Day, 60);

            foreach (long bad in new[] { other, otherDay, beyond })
            {
                GameSnapshot snap = s.Capture();
                snap.ActiveSale.CustomerId = bad;
                Result<GameSession> back = GameSession.Restore(MarketHarness.RealContent(), snap);
                Assert.IsTrue(back.IsFailure, "kimlik " + bad);
                Assert.AreEqual("save.invalid", back.ErrorCode);
            }

            GameSnapshot wrongNpc = s.Capture();
            wrongNpc.ActiveSale.NpcId = active.NpcId == "npc.kemal" ? "npc.selin" : "npc.kemal";
            Assert.AreEqual("save.invalid", GameSession.Restore(MarketHarness.RealContent(), wrongNpc).ErrorCode, "farklı NPC");
        }

        // ---------- eski 5 yuva / lobi ----------

        [Test]
        public void TheOldRosterLobby_IsUnchanged_AndStillSellsAlongsideTheQueue()
        {
            GameSession a = null;
            CustomerView slotCustomer = null;
            for (ulong seed = 1; seed <= 200 && slotCustomer == null; seed++)
            {
                GameSession s = WithShelf(seed);
                if (s.Api.GetCustomers().Count > 0)
                {
                    a = s;
                    slotCustomer = s.Api.GetCustomers()[0];
                }
            }

            Assert.IsNotNull(slotCustomer);
            ulong sameSeed = a.Time.MasterSeed;
            GameSession b = WithShelf(sameSeed);
            GoTo(b, StoreHours.CloseMinute - 1);
            b.Api.GetActiveCustomer();

            Assert.AreEqual(a.Api.GetCustomers().Count, b.Api.GetCustomers().Count, "eski lobi kuyruktan etkilenmedi");
            Assert.AreEqual(slotCustomer.CustomerId, b.Api.GetCustomers()[0].CustomerId, "yuva kimlikleri aynı");
            Assert.IsFalse(QueueCustomerId.IsQueueId(b.Api.GetCustomers()[0].CustomerId));
            Assert.AreEqual(5, b.Customers.State.Slots.Count, "5 yuva duruyor");

            Assert.IsTrue(b.Api.StartSale(slotCustomer.CustomerId).IsSuccess, "yuva müşterisi eskisi gibi satılır");
            Assert.IsTrue(b.Api.AskPrice(Money.FromTl(10)).IsSuccess);
            Assert.AreEqual(0, b.Api.GetCustomerQueue().Served, "yuva satışı kuyruğu ilerletmedi");
        }

        // ---------- RNG ve belirlenimcilik ----------

        [Test]
        public void TheQueueIntegration_DoesNotChangeTheRegisteredRngStreams_NorRegistersNewOnes()
        {
            CustomerView active;
            GameSession s = WithActive(v => true, out active);
            var before = s.Capture().Rng;

            s.Api.GetActiveCustomer();
            s.Api.StartSale(active.CustomerId);
            s.Api.AskPrice(Money.FromTl(10));
            s.Api.GetActiveCustomer();

            Assert.IsNull(DeepCompare.FirstDifference(before, s.Capture().Rng), "kayıtlı akışlar aynı");
            Assert.IsFalse(s.Capture().Rng.Any(x => x.Name.StartsWith("customer_queue")), "yeni kayıtlı akış yok");
        }

        [Test]
        public void TheSameSeed_GivesTheSameActiveCustomer_ThePhoneChoice_AndTheSameSale()
        {
            CustomerView a;
            GameSession sa = WithActive(v => true, out a);
            GameSession sb = WithShelf(sa.Time.MasterSeed);
            GoTo(sb, Plan(sb)[0].ArrivalMinute);
            CustomerView b = sb.Api.GetActiveCustomer();

            Assert.AreEqual(a.CustomerId, b.CustomerId);
            Assert.AreEqual(a.NpcId, b.NpcId);
            Assert.AreEqual(a.InstanceId, b.InstanceId, "aynı telefon seçimi");
            Assert.AreEqual(MaxOf(sa, a), MaxOf(sb, b), "aynı bütçe (Max)");

            sa.Api.StartSale(a.CustomerId);
            sb.Api.StartSale(b.CustomerId);
            SaleView da = sa.Api.AskPrice(Money.FromTl(AskNear(sa, a))).Value;
            SaleView db = sb.Api.AskPrice(Money.FromTl(AskNear(sb, b))).Value;
            Assert.AreEqual(da.Phase, db.Phase);
            Assert.AreEqual(da.ShownPrice, db.ShownPrice);
            Assert.AreEqual(da.DealPrice, db.DealPrice);
            Assert.AreEqual(sa.Api.GetStateDigest(), sb.Api.GetStateDigest());
        }
    }
}
