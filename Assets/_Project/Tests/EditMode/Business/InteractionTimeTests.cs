using System;
using System.Collections.Generic;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Accessories;
using Esnaf.Domain.Appraisal;
using Esnaf.Domain.Business;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Game;
using Esnaf.Domain.Negotiation;
using Esnaf.Domain.Products;
using Esnaf.Domain.Time;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Business
{
    /// <summary>
    /// Müşteri/satış aksiyonlarının saat maliyeti (Gün 12.4): sabit, deterministik süreler; hem eski 5-yuva müşterilerine hem kuyruk müşterilerine uygulanır; başarısız çağrı
    /// 0 dk; kapanışta (21:00) kırpılır, başlamış satış tamamlanır; alış/ilan/ekspertiz/toptan zaman harcamaz; ekonomi, fiyat, Direct Accept, Teklifi Kabul Et, RNG ve kuyruk planı değişmez.
    /// </summary>
    public class InteractionTimeTests
    {
        private static GameSession New(ulong seed, IEventBus bus = null)
        {
            return GameSession.NewGame(MarketHarness.RealContent(), seed, bus);
        }

        private static GameSession WithShelf(ulong seed, IEventBus bus = null, long listPriceTl = 5900)
        {
            GameSession s = New(seed, bus);
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

        private static int Now(GameSession s)
        {
            return s.Api.GetClock().MinuteOfDay;
        }

        private static IReadOnlyList<QueuedCustomer> Plan(GameSession s)
        {
            return s.CustomerQueue.PlanFor(s.Time.Day);
        }

        // Eski 5-yuva lobisinden bir müşterisi olan oturum (isteğe bağlı NPC).
        private static GameSession WithSlotCustomer(out CustomerView customer, string npc = null)
        {
            for (ulong seed = 1; seed <= 300; seed++)
            {
                GameSession s = WithShelf(seed);
                CustomerView v = s.Api.GetCustomers().FirstOrDefault(x => npc == null || x.NpcId == npc);
                if (v != null)
                {
                    customer = v;
                    return s;
                }
            }

            throw new InvalidOperationException("No seed gives a slot customer.");
        }

        // İlk kuyruk müşterisinin geldiği ve ilgilendiği ürünün olduğu oturum (isteğe bağlı koşul).
        private static GameSession WithActive(out CustomerView active, Func<CustomerView, bool> accept = null, IEventBus bus = null)
        {
            for (ulong seed = 1; seed <= 400; seed++)
            {
                GameSession s = WithShelf(seed, bus);
                GoTo(s, Plan(s)[0].ArrivalMinute);
                CustomerView v = s.Api.GetActiveCustomer();
                if (v != null && v.InstanceId != 0 && (accept == null || accept(v)))
                {
                    active = v;
                    return s;
                }
            }

            throw new InvalidOperationException("No seed gives the wanted active queue customer.");
        }

        private static long MaxOfQueue(GameSession s, CustomerView active)
        {
            QueuedCustomer q;
            CustomerSlot slot;
            Assert.IsTrue(s.CustomerQueue.TryResolve(active.CustomerId, out q, out slot));
            return (long)s.Customers.MaxFor(slot, s.Store.Get(active.InstanceId), false);
        }

        private static long MaxOfSlot(GameSession s, CustomerView customer)
        {
            CustomerSlot slot = s.Customers.State.Slots.Single(x => x.CustomerId == customer.CustomerId);
            return (long)s.Customers.MaxFor(slot, s.Store.Get(customer.InstanceId), false);
        }

        private static long AskNear(long max)
        {
            return (long)Math.Floor(max * 0.98 / 10.0) * 10L;
        }

        private static long InjectReport(GameSession s, long instanceId, string level, long id = 900)
        {
            s.Knowledge.Add(new AppraisalResult(
                id, instanceId, "phone.yildiz_y5", level, 1, Money.Zero, 1UL, new AttributeFinding[0],
                new NumericRange(80, 90), new NumericRange(95, 100), new MoneyRange(Money.FromTl(5000), Money.FromTl(5500)), new TrumpCard[0]));
            return id;
        }

        // ---------- tablo ----------

        [Test]
        public void TheCostTable_IsExactlyTheApprovedOne()
        {
            Assert.AreEqual(5, InteractionTime.StartSale);
            Assert.AreEqual(5, InteractionTime.AskPrice);
            Assert.AreEqual(5, InteractionTime.ShowReport);
            Assert.AreEqual(5, InteractionTime.AcceptOffer);
            Assert.AreEqual(3, InteractionTime.LetCustomerGo);
            Assert.AreEqual(3, InteractionTime.AccessorySale);
            Assert.AreEqual(2, InteractionTime.CompleteCustomer);
        }

        // ---------- StoreClock.Spend ----------

        [Test]
        public void Spend_AdvancesTheClock_ClampsAtClosing_NeverFails_AndSpendsNothingWhenClosed()
        {
            GameSession s = New(1UL);

            Assert.AreEqual(5, s.Clock.Spend(5));
            Assert.AreEqual(545, Now(s));
            Assert.AreEqual(0, s.Clock.Spend(0));
            Assert.AreEqual(0, s.Clock.Spend(-4));
            GoTo(s, 1258);
            Assert.AreEqual(2, s.Clock.Spend(5), "21:00'e kırpılır");
            Assert.AreEqual(StoreHours.CloseMinute, Now(s));
            Assert.AreEqual(0, s.Clock.Spend(5), "kapalıyken 0 harcar, hata vermez");
            Assert.AreEqual(StoreHours.CloseMinute, Now(s));
        }

        // ---------- StartSale ----------

        [Test]
        public void StartSale_CostsFiveMinutes_ForQueueCustomersAndForOldSlotCustomers()
        {
            CustomerView active;
            GameSession q = WithActive(out active);
            int before = Now(q);
            Assert.IsTrue(q.Api.StartSale(active.CustomerId).IsSuccess);
            Assert.AreEqual(before + 5, Now(q), "kuyruk müşterisi");

            CustomerView slot;
            GameSession o = WithSlotCustomer(out slot);
            Assert.AreEqual(540, Now(o));
            Assert.IsTrue(o.Api.StartSale(slot.CustomerId).IsSuccess);
            Assert.AreEqual(545, Now(o), "eski yuva müşterisi");
        }

        // ---------- AskPrice: normal pazarlık, Direct Accept, anlaşma ----------

        [Test]
        public void EachBargainingRound_CostsFiveMinutes_AndTheCounterOfferIsNotChanged()
        {
            CustomerView slot;
            GameSession s = WithSlotCustomer(out slot, "npc.kemal");
            long ask = AskNear(MaxOfSlot(s, slot));
            s.Api.StartSale(slot.CustomerId);
            int t = Now(s);

            SaleView first = s.Api.AskPrice(Money.FromTl(ask)).Value;
            Assert.AreEqual(t + 5, Now(s), "1. tur");
            Assert.AreNotEqual(NegotiationPhase.Deal, first.Phase);
            SaleView second = s.Api.AskPrice(Money.FromTl(ask)).Value;

            Assert.AreEqual(t + 10, Now(s), "2. tur");
            Assert.AreEqual(2, second.Round);
        }

        [Test]
        public void ADirectAccept_CostsOnlyTheAskRound_NoExtraPaymentTime()
        {
            CustomerView slot;
            GameSession s = WithSlotCustomer(out slot, "npc.selin");
            long ask = AskNear(MaxOfSlot(s, slot));
            s.Api.StartSale(slot.CustomerId);
            int t = Now(s);

            SaleView deal = s.Api.AskPrice(Money.FromTl(ask)).Value;

            Assert.AreEqual(NegotiationPhase.Deal, deal.Phase);
            Assert.AreEqual(Money.FromTl(ask), deal.DealPrice, "Direct Accept fiyatı değişmedi");
            Assert.AreEqual(t + 5, Now(s), "yalnızca istek süresi; ödeme/devir için ayrı süre yok");
        }

        [Test]
        public void AnOrdinaryDeal_ThroughAnAsk_CostsFiveMinutes()
        {
            CustomerView slot;
            GameSession s = WithSlotCustomer(out slot);
            s.Api.StartSale(slot.CustomerId);
            int t = Now(s);

            SaleView deal = s.Api.AskPrice(Money.FromTl(10)).Value;

            Assert.AreEqual(NegotiationPhase.Deal, deal.Phase);
            Assert.AreEqual(t + 5, Now(s));
        }

        // ---------- Teklifi Kabul Et ----------

        [Test]
        public void AcceptingTheCustomersOffer_CostsFiveMinutes_AtTheCustomersPrice()
        {
            CustomerView slot;
            GameSession s = WithSlotCustomer(out slot, "npc.kemal");
            s.Api.StartSale(slot.CustomerId);
            Money offer = s.Api.AskPrice(Money.FromTl(AskNear(MaxOfSlot(s, slot)))).Value.ShownPrice;
            int t = Now(s);

            Result<SaleView> accepted = s.Api.AcceptCustomerOffer();

            Assert.IsTrue(accepted.IsSuccess, accepted.ErrorCode);
            Assert.AreEqual(offer, accepted.Value.DealPrice, "müşterinin teklif fiyatı");
            Assert.AreEqual(t + 5, Now(s));
        }

        [Test]
        public void AcceptingTheFinalOffer_CostsFiveMinutes()
        {
            CustomerView slot;
            GameSession s = WithSlotCustomer(out slot);
            s.Api.StartSale(slot.CustomerId);
            long high = MaxOfSlot(s, slot) * 2;
            for (int i = 0; i < 12 && s.Api.GetSale() != null && s.Api.GetSale().Phase == NegotiationPhase.Active; i++)
            {
                s.Api.AskPrice(Money.FromTl(high));
            }

            Assert.AreEqual(NegotiationPhase.FinalOffer, s.Api.GetSale().Phase);
            int t = Now(s);

            Result<SaleView> accepted = s.Api.AcceptCustomerFinalOffer();

            Assert.IsTrue(accepted.IsSuccess, accepted.ErrorCode);
            Assert.AreEqual(t + 5, Now(s));
        }

        // ---------- ShowReport, Leave ----------

        [Test]
        public void ShowingAReport_CostsFiveMinutes()
        {
            CustomerView slot;
            GameSession s = WithSlotCustomer(out slot, "npc.kemal");
            long report = InjectReport(s, slot.InstanceId, "s3");
            s.Api.StartSale(slot.CustomerId);
            int t = Now(s);

            Assert.IsTrue(s.Api.ShowReport(report).IsSuccess);

            Assert.AreEqual(t + 5, Now(s));
        }

        [Test]
        public void LettingTheCustomerGo_CostsThreeMinutes_ForSlotAndQueueCustomers()
        {
            CustomerView slot;
            GameSession o = WithSlotCustomer(out slot);
            o.Api.StartSale(slot.CustomerId);
            int to = Now(o);
            Assert.IsTrue(o.Api.LetCustomerGo().IsSuccess);
            Assert.AreEqual(to + 3, Now(o), "eski yuva müşterisi");

            CustomerView active;
            GameSession q = WithActive(out active);
            q.Api.StartSale(active.CustomerId);
            int tq = Now(q);
            Assert.IsTrue(q.Api.LetCustomerGo().IsSuccess);
            Assert.AreEqual(tq + 3, Now(q), "kuyruk müşterisi");
        }

        // ---------- aksesuar ----------

        [Test]
        public void EachSuccessfulAccessorySale_CostsThreeMinutes_AndTheEconomyIsUnchanged()
        {
            AddOnDeal deal = AddOnDeals.WithCount(2);
            GameSession s = deal.Session;
            Assert.AreEqual(540 + 10, Now(s), "eski yuva satışı: StartSale 5 + AskPrice 5");
            foreach (string id in deal.Requested)
            {
                s.AccessoryStock.Add(id, 10, Money.FromTl(500));
            }

            int t = Now(s);
            Money cash = s.Api.GetCash();

            AccessorySaleReceipt a = s.Api.SellAccessoryAddOn(deal.Requested[0]).Value;
            Assert.AreEqual(t + 3, Now(s));
            AccessorySaleReceipt b = s.Api.SellAccessoryAddOn(deal.Requested[1]).Value;

            Assert.AreEqual(t + 6, Now(s));
            Assert.AreEqual(cash + a.SalePrice + b.SalePrice, s.Api.GetCash(), "aksesuar ekonomisi aynı");
            Assert.AreEqual(a.Profit + b.Profit, s.Api.GetAccessoryAddOns().AccessoryProfit);
        }

        [Test]
        public void AFailedAccessorySale_SpendsNoTime()
        {
            AddOnDeal deal = AddOnDeals.WithCount(1);
            GameSession s = deal.Session;
            string notRequested = s.Content.Accessories.Definitions.First(d => d.Id != deal.Requested[0]).Id;
            s.AccessoryStock.Add(notRequested, 5, Money.FromTl(250));
            int t = Now(s);

            Assert.AreEqual("stock.insufficient", s.Api.SellAccessoryAddOn(deal.Requested[0]).ErrorCode);
            Assert.AreEqual("addon.not_in_request", s.Api.SellAccessoryAddOn(notRequested).ErrorCode);
            Assert.AreEqual("accessory.unknown", s.Api.SellAccessoryAddOn("accessory.ghost").ErrorCode);

            Assert.AreEqual(t, Now(s));
        }

        // ---------- CompleteCurrentCustomer ----------

        [Test]
        public void DismissingAnActiveCustomer_CostsTwoMinutes_AndWithoutOneNothing()
        {
            // Gün 12.6: raf boşsa müşteri gelmez; ürünü olmayan müşteri için fiyatlı ama çok pahalı bir ürün rafta.
            GameSession s = WithShelf(5UL, null, 90000);
            QueuedCustomer first = Plan(s)[0];
            int t0 = Now(s);
            Assert.AreEqual("queue.no_active_customer", s.Api.CompleteCurrentCustomer().ErrorCode);
            Assert.AreEqual(t0, Now(s), "başarısız çağrı 0 dk");

            GoTo(s, first.ArrivalMinute);
            int t = Now(s);

            Assert.IsTrue(s.Api.CompleteCurrentCustomer().IsSuccess);

            Assert.AreEqual(t + 2, Now(s));
            Assert.AreEqual(1, s.Api.GetCustomerQueue().Served);
        }

        // ---------- başarısız çağrılar 0 dk ----------

        [Test]
        public void FailedCalls_SpendNoTime_AndChangeNothing()
        {
            CustomerView slot;
            GameSession s = WithSlotCustomer(out slot);
            int t = Now(s);
            string digest = s.Api.GetStateDigest();

            Assert.IsTrue(s.Api.AskPrice(Money.FromTl(100)).IsFailure, "satış yok");
            Assert.IsTrue(s.Api.ShowReport(1).IsFailure);
            Assert.IsTrue(s.Api.AcceptCustomerOffer().IsFailure);
            Assert.IsTrue(s.Api.AcceptCustomerFinalOffer().IsFailure);
            Assert.IsTrue(s.Api.LetCustomerGo().IsFailure);
            Assert.IsTrue(s.Api.StartSale(987654L).IsFailure, "bilinmeyen müşteri");
            Assert.IsTrue(s.Api.StartSale(QueueCustomerId.For(s.Time.Day, 3)).IsFailure, "aktif olmayan kuyruk müşterisi");
            Assert.AreEqual(t, Now(s));
            Assert.AreEqual(digest, s.Api.GetStateDigest());

            s.Api.StartSale(slot.CustomerId);
            int started = Now(s);
            Assert.IsTrue(s.Api.AskPrice(Money.FromTl(0)).IsFailure, "geçersiz istek");
            Assert.IsTrue(s.Api.AskPrice(Money.FromTl(6005)).IsFailure);
            Assert.IsTrue(s.Api.AcceptCustomerOffer().IsFailure, "teklif yok");
            Assert.IsTrue(s.Api.AcceptCustomerFinalOffer().IsFailure, "son teklif yok");
            Assert.IsTrue(s.Api.ShowReport(777).IsFailure, "rapor yok");
            Assert.IsTrue(s.Api.StartSale(slot.CustomerId).IsFailure, "zaten satışta");
            Assert.AreEqual(started, Now(s));
        }

        // ---------- kapanış ----------

        [Test]
        public void ASaleThatCrossesClosing_IsClampedAtNine_AndStillCompletes()
        {
            var bus = new EventBus();
            var closed = new List<StoreClosed>();
            bus.Subscribe<StoreClosed>(closed.Add);
            CustomerView active;
            GameSession s = WithActive(out active, null, bus);
            // Gün 12.6: bekleyen müşteri 60 dk sonra çıkar; bu yüzden satış gelişte başlar, sürerken saat 20:58'e gelir (satıştaki müşteri çıkmaz).
            Assert.IsTrue(s.Api.StartSale(active.CustomerId).IsSuccess);
            GoTo(s, 1258); // 20:58
            Assert.IsNotNull(s.Api.GetSale(), "20:58'de satış sürüyor");
            Assert.AreEqual(0, closed.Count);

            Result<SaleView> deal = s.Api.AskPrice(Money.FromTl(10));

            Assert.IsTrue(deal.IsSuccess, deal.ErrorCode);
            Assert.AreEqual(NegotiationPhase.Deal, deal.Value.Phase, "başlamış satış 21:00'de de tamamlanır");
            Assert.AreEqual(StoreHours.CloseMinute, Now(s), "saat 21:00'i geçmez");
            Assert.AreEqual(1, closed.Count, "StoreClosed bir kez");
            CustomerQueueView view = s.Api.GetCustomerQueue();
            Assert.AreEqual(0, view.Waiting, "kapalı: bekleyen yok");
            Assert.IsNull(s.Api.GetActiveCustomer());
        }

        [Test]
        public void AtClosing_NoNewQueueCustomerOrSaleCanStart_AndOldRosterSalesCostNothing()
        {
            CustomerView active;
            GameSession q = WithActive(out active);
            GoTo(q, StoreHours.CloseMinute);
            Assert.IsNull(q.Api.GetActiveCustomer());
            Assert.AreEqual("customer.unknown", q.Api.StartSale(active.CustomerId).ErrorCode);
            Assert.AreEqual(StoreHours.CloseMinute, Now(q));

            // eski yuva lobisi saate bağlı değildir (12.3'teki gibi); kapalıyken süre harcamaz.
            CustomerView slot;
            GameSession o = WithSlotCustomer(out slot);
            GoTo(o, StoreHours.CloseMinute);
            Assert.IsTrue(o.Api.StartSale(slot.CustomerId).IsSuccess);
            Assert.AreEqual(StoreHours.CloseMinute, Now(o), "kapalıyken 0 dk");
        }

        // ---------- alış tarafı zaman harcamaz ----------

        [Test]
        public void BuyingListingsAppraisalsAndWholesale_SpendNoTime()
        {
            GameSession s = New(1UL);
            int t = Now(s);
            var listing = s.Api.GetListings()[0];

            Assert.IsTrue(s.Api.BuyListing(listing.ListingId).IsSuccess);
            Assert.IsTrue(s.Api.SetPrice(s.Api.GetInventory()[0].InstanceId, Money.FromTl(6000)).IsSuccess);
            Assert.IsTrue(s.Api.StartNegotiation(s.Api.GetListings()[0].ListingId).IsSuccess);
            Assert.IsTrue(s.Api.MakeOffer(Money.FromTl(100)).IsSuccess);
            Assert.IsTrue(s.Api.WalkAway().IsSuccess);
            Assert.IsTrue(s.WholesaleService.BuyPack("supplier.ucuz_toptan", "accessory.phone_case", 1).IsSuccess);
            Assert.IsTrue(s.Api.BuyWholesalePack("supplier.ucuz_toptan", "accessory.charger_adapter").IsSuccess);

            Assert.AreEqual(t, Now(s));
        }

        [Test]
        public void EndingTheDay_CostsNothing_AndStartsTheNextDayAtNine()
        {
            CustomerView slot;
            GameSession s = WithSlotCustomer(out slot);
            s.Api.StartSale(slot.CustomerId);
            s.Api.LetCustomerGo();
            Assert.Greater(Now(s), 540);

            Assert.IsTrue(s.Api.EndDay().IsSuccess);

            Assert.AreEqual(540, Now(s));
        }

        // ---------- ekonomi, fiyat, RNG, kuyruk planı ----------

        [Test]
        public void TheClock_DoesNotChangeTheEconomyOrThePrices_OfTheSameSale()
        {
            CustomerView slotA;
            GameSession a = WithSlotCustomer(out slotA, "npc.selin");
            GameSession b = WithShelf(a.Time.MasterSeed);
            b.Api.AdvanceTime(300); // b başka saatte
            CustomerView slotB = b.Api.GetCustomers().Single(x => x.CustomerId == slotA.CustomerId);
            long ask = AskNear(MaxOfSlot(a, slotA));
            Assert.AreEqual(ask, AskNear(MaxOfSlot(b, slotB)), "aynı Max (bütçe saatten bağımsız)");
            a.Api.StartSale(slotA.CustomerId);
            b.Api.StartSale(slotB.CustomerId);

            SaleView da = a.Api.AskPrice(Money.FromTl(ask)).Value;
            SaleView db = b.Api.AskPrice(Money.FromTl(ask)).Value;

            Assert.AreEqual(da.Phase, db.Phase);
            Assert.AreEqual(da.DealPrice, db.DealPrice, "Direct Accept fiyatı aynı");
            Assert.AreEqual(a.Api.GetCash(), b.Api.GetCash());
            Assert.AreEqual(
                a.EconomyState.Ledger.Records.Last(r => r.TypeId == TransactionTypeIds.Sale).Amount,
                b.EconomyState.Ledger.Records.Last(r => r.TypeId == TransactionTypeIds.Sale).Amount);
            Assert.AreEqual(5 + 5, Now(a) - 540, "a: 10 dk");
            Assert.AreEqual(300 + 10, Now(b) - 540, "b: 300 + 10 dk");
        }

        [Test]
        public void TheTimeCosts_DoNotTouchTheRngStreams_NorTheQueuePlan()
        {
            CustomerView active;
            GameSession s = WithActive(out active);
            string plan = string.Join(";", Plan(s).Select(c => c.CustomerId + "|" + c.NpcId + "|" + c.ArrivalMinute));
            var rng = s.Capture().Rng;

            s.Api.StartSale(active.CustomerId);
            s.Api.AskPrice(Money.FromTl(MaxOfQueue(s, active) * 2));
            s.Api.LetCustomerGo();
            s.Api.CompleteCurrentCustomer();

            Assert.IsNull(DeepCompare.FirstDifference(rng, s.Capture().Rng), "RNG akışları aynı");
            Assert.AreEqual(plan, string.Join(";", Plan(s).Select(c => c.CustomerId + "|" + c.NpcId + "|" + c.ArrivalMinute)), "kuyruk planı aynı");
        }

        [Test]
        public void TheSameActions_GiveTheSameClock_Deterministically()
        {
            CustomerView a;
            GameSession sa = WithActive(out a);
            GameSession sb = WithShelf(sa.Time.MasterSeed);
            GoTo(sb, Plan(sb)[0].ArrivalMinute);

            foreach (GameSession s in new[] { sa, sb })
            {
                s.Api.StartSale(a.CustomerId);
                s.Api.AskPrice(Money.FromTl(MaxOfQueue(s, a) * 2));
                s.Api.LetCustomerGo();
            }

            Assert.AreEqual(Now(sa), Now(sb));
            Assert.AreEqual(sa.Api.GetStateDigest(), sb.Api.GetStateDigest());
        }

        // ---------- save / load ----------

        [Test]
        public void TheSpentTime_SurvivesSaveAndLoad_AndAnOpenSaleKeepsCostingAfterLoading()
        {
            CustomerView active;
            GameSession s = WithActive(out active);
            s.Api.StartSale(active.CustomerId);
            s.Api.AskPrice(Money.FromTl(MaxOfQueue(s, active) * 2));
            int t = Now(s);

            GameSession r = GameSession.Restore(MarketHarness.RealContent(), s.Capture()).Value;

            Assert.AreEqual(t, Now(r), "harcanan süre kayıtlı");
            Assert.AreEqual(s.Api.GetStateDigest(), r.Api.GetStateDigest());
            Assert.IsNotNull(r.Api.GetSale());
            Assert.IsTrue(r.Api.AskPrice(Money.FromTl(10)).IsSuccess);
            Assert.AreEqual(t + 5, Now(r), "yüklenen satış da süre harcar");
        }
    }
}
