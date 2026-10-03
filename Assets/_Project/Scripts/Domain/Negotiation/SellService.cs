using System;
using System.Collections.Generic;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Appraisal;
using Esnaf.Domain.Business;
using Esnaf.Domain.Content;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Inventory;
using Esnaf.Domain.Npc;
using Esnaf.Domain.Products;
using Esnaf.Domain.Time;

namespace Esnaf.Domain.Negotiation
{
    /// <summary>
    /// Satış akışı (GDD v0.3 3.2 "Trade.SellFlow"): gelen müşteriyi pazarlığa alır, istenen fiyatları/raporu motora iletir, anlaşmada ürünü
    /// müşteriye satıp deftere yazar, talep baskısını ve NPC hafızasını günceller. Kurallar <see cref="SaleEngine"/>'dedir; burası yalnızca birleştirir.
    ///
    /// Her komut ya tamamen uygulanır ya da HİÇ uygulanmaz (motor önce kopya üzerinde çalışır). Aynı anda en fazla bir pazarlık vardır
    /// (alış ya da satış). Müşteriyi yolcu etmek ürünü rafta bırakır; müşteri o gün geri gelmez.
    /// </summary>
    public sealed class SellService
    {
        private readonly ContentDatabase _content;
        private readonly CustomerService _customers;
        private readonly TradeState _state;
        private readonly InventoryService _inventory;
        private readonly InstanceStore _store;
        private readonly NpcStateStore _npcs;
        private readonly DemandModel _demand;
        private readonly KnowledgeState _knowledge;
        private readonly TimeState _time;
        private readonly IEventBus _events;
        private readonly SaleEngine _engine;
        private readonly CustomerQueueService _queue;
        private readonly StoreClock _clock;

        public SellService(
            ContentDatabase content,
            CustomerService customers,
            TradeState state,
            InventoryService inventory,
            InstanceStore store,
            NpcStateStore npcs,
            DemandModel demand,
            KnowledgeState knowledge,
            TimeState time,
            IEventBus events,
            CustomerQueueService queue,
            StoreClock clock)
        {
            if (content == null || customers == null || state == null || inventory == null || store == null
                || npcs == null || demand == null || knowledge == null || time == null || queue == null || clock == null)
            {
                throw new ArgumentNullException(nameof(content), "SellService needs all of its collaborators.");
            }

            _content = content;
            _customers = customers;
            _state = state;
            _inventory = inventory;
            _store = store;
            _npcs = npcs;
            _demand = demand;
            _knowledge = knowledge;
            _time = time;
            _events = events;
            _queue = queue;
            _clock = clock;
            _engine = new SaleEngine(content.Negotiation);
        }

        /// <summary>Şu an dükkâna gelmiş ve bir ürünle ilgilenen müşteriler.</summary>
        public IReadOnlyList<CustomerView> GetCustomers()
        {
            var views = new List<CustomerView>();
            foreach (KeyValuePair<CustomerSlot, long> pair in _customers.Interested())
            {
                views.Add(new CustomerView(pair.Key.CustomerId, pair.Key.NpcId, pair.Value, _customers.ProfileFor(pair.Key)));
            }

            return views;
        }

        /// <summary>
        /// Günlük kuyruğun şu an aktif müşterisi (Gün 12.3): geliş saati geldiyse ve mağaza açıksa; yoksa null. İlgilendiği ürün yoksa <c>InstanceId</c> 0'dır (oyuncu
        /// <c>CompleteCurrentCustomer</c> ile gönderir). Eski yuva lobisinden (<see cref="GetCustomers"/>) bağımsız paralel yoldur; durumu değiştirmez.
        /// </summary>
        public CustomerView GetActiveCustomer()
        {
            QueuedCustomer customer;
            CustomerSlot slot;
            if (!_queue.TryGetActive(out customer, out slot))
            {
                return null;
            }

            return new CustomerView(customer.CustomerId, customer.NpcId, _customers.FindInterest(slot), _customers.ProfileFor(slot));
        }

        public SaleView GetCurrent()
        {
            ActiveSale active = _state.CurrentSale;
            return active == null ? null : ViewOf(active, active.State, active.LastAskTooExpensive);
        }

        public Result<SaleView> Start(long customerId)
        {
            if (_state.IsBusy)
            {
                return Result<SaleView>.Fail("negotiation.in_progress", "Finish the current negotiation first.");
            }

            CustomerSlot slot;
            if (QueueCustomerId.IsQueueId(customerId))
            {
                // Kuyruk müşterisi (Gün 12.3): yalnızca şu an aktif olan başlatılabilir (geliş saati geldi, mağaza açık, aynı gün).
                QueuedCustomer queued;
                if (!_queue.TryGetActive(out queued, out slot) || queued.CustomerId != customerId)
                {
                    return Result<SaleView>.Fail("customer.unknown", "Unknown, not yet arrived or gone queue customer " + customerId + ".");
                }
            }
            else if (!_customers.TryGetWaiting(customerId, out slot))
            {
                return Result<SaleView>.Fail("customer.unknown", "Unknown or gone customer " + customerId + ".");
            }

            long instanceId = _customers.FindInterest(slot);
            if (instanceId == 0)
            {
                return Result<SaleView>.Fail("customer.no_interest", "The customer is not interested in anything on the shelf.");
            }

            ProductInstance instance = _store.Get(instanceId);
            var active = new ActiveSale(slot.CustomerId, slot.NpcId, instanceId, _engine.Begin(_customers.BuildSetup(slot, instance)));
            _state.CurrentSale = active;
            _clock.Spend(InteractionTime.StartSale); // Gün 12.4: başarılı başlangıç süre harcar

            if (_events != null)
            {
                _events.Publish(new CustomerNegotiationStarted(slot.CustomerId, slot.NpcId, instanceId));
            }

            return Result<SaleView>.Ok(ViewOf(active, active.State, false));
        }

        public Result<SaleView> Ask(Money ask)
        {
            ActiveSale active = _state.CurrentSale;
            if (active == null)
            {
                return Result<SaleView>.Fail("sale.none", "There is no sale in progress.");
            }

            SaleState work = active.State.Clone();
            Result<SaleRound> round = _engine.Ask(work, ask, _customers.DirectAcceptRatioOf(active.NpcId, work.Trust));
            if (round.IsFailure)
            {
                return Result<SaleView>.Fail(round.ErrorCode, round.Message);
            }

            SaleRound result = round.Value;
            if (work.Phase == NegotiationPhase.Deal)
            {
                return Sell(active, work, result, InteractionTime.AskPrice);
            }

            active.State = work;
            active.LastAskTooExpensive = result.TooExpensive;
            _clock.Spend(InteractionTime.AskPrice); // Gün 12.4: başarılı her pazarlık turu süre harcar
            PublishOffer(active, result);
            return Result<SaleView>.Ok(ViewOf(active, work, result.TooExpensive));
        }

        /// <summary>"Rapor göster" (GDD v0.2 7.5): S2/S3 raporu müşterinin değer hatasını yarıya indirir ve güvenini artırır.</summary>
        public Result<SaleView> ShowReport(long appraisalId)
        {
            ActiveSale active = _state.CurrentSale;
            if (active == null)
            {
                return Result<SaleView>.Fail("sale.none", "There is no sale in progress.");
            }

            AppraisalResult report;
            if (!_knowledge.TryGetById(appraisalId, out report) || report.InstanceId != active.InstanceId)
            {
                return Result<SaleView>.Fail("report.unknown", "Unknown report " + appraisalId + " for this item.");
            }

            if (!_content.Negotiation.SellReportLevelIds.Contains(report.LevelId))
            {
                return Result<SaleView>.Fail("report.not_eligible", "Only level " + string.Join("/", _content.Negotiation.SellReportLevelIds) + " reports can be shown.");
            }

            CustomerSlot slot = SlotOfActive(active);
            NpcCustomerRole customer = _content.GetNpc(active.NpcId).Customer;
            int gain = customer.ReportTrustGain ?? _content.Negotiation.SellReportTrustGain;
            double newMax = _customers.MaxFor(slot, _store.Get(active.InstanceId), true);

            SaleState work = active.State.Clone();
            Result applied = _engine.ApplyReport(work, newMax, gain);
            if (applied.IsFailure)
            {
                return Result<SaleView>.Fail(applied.ErrorCode, applied.Message);
            }

            active.State = work;
            _clock.Spend(InteractionTime.ShowReport);
            return Result<SaleView>.Ok(ViewOf(active, work, active.LastAskTooExpensive));
        }

        public Result<SaleView> AcceptFinal()
        {
            ActiveSale active = _state.CurrentSale;
            if (active == null)
            {
                return Result<SaleView>.Fail("sale.none", "There is no sale in progress.");
            }

            SaleState work = active.State.Clone();
            Result accepted = _engine.AcceptFinal(work);
            if (accepted.IsFailure)
            {
                return Result<SaleView>.Fail(accepted.ErrorCode, accepted.Message);
            }

            return Sell(active, work, null, InteractionTime.AcceptOffer);
        }

        /// <summary>Müşterinin şu anki teklifini aynen kabul eder (bkz. <see cref="SaleEngine.AcceptOffer"/>); satış normal akıştan tamamlanır.</summary>
        public Result<SaleView> AcceptOffer()
        {
            ActiveSale active = _state.CurrentSale;
            if (active == null)
            {
                return Result<SaleView>.Fail("sale.none", "There is no sale in progress.");
            }

            SaleState work = active.State.Clone();
            Result accepted = _engine.AcceptOffer(work);
            if (accepted.IsFailure)
            {
                return Result<SaleView>.Fail(accepted.ErrorCode, accepted.Message);
            }

            return Sell(active, work, null, InteractionTime.AcceptOffer);
        }

        public Result<SaleView> Leave()
        {
            ActiveSale active = _state.CurrentSale;
            if (active == null)
            {
                return Result<SaleView>.Fail("sale.none", "There is no sale in progress.");
            }

            SaleState work = active.State.Clone();
            Result left = _engine.Leave(work);
            if (left.IsFailure)
            {
                return Result<SaleView>.Fail(left.ErrorCode, left.Message);
            }

            SaleView view = ViewOf(active, work, active.LastAskTooExpensive);
            _clock.Spend(InteractionTime.LetCustomerGo); // süre, kuyruk müşterisi tamamlanmadan önce harcanır (kapanış kuralı güncel saate göre)
            FinishCustomer(active, false);
            _state.CurrentSale = null;
            if (_events != null)
            {
                _events.Publish(new CustomerNegotiationEnded(active.CustomerId, active.InstanceId, NegotiationPhase.Failed, Money.Zero));
            }

            return Result<SaleView>.Ok(view);
        }

        private Result<SaleView> Sell(ActiveSale active, SaleState work, SaleRound result, int minutes)
        {
            Money price = work.DealPrice;
            ProductInstance instance = _store.Get(active.InstanceId);
            string modelId = instance.DefinitionId;
            Result<SaleReceipt> sold = _inventory.Sell(active.InstanceId, price, _time.Day, active.NpcId);
            if (sold.IsFailure)
            {
                return Result<SaleView>.Fail(sold.ErrorCode, sold.Message);
            }

            // Gün 12.4: satış gerçekleştikten sonra aksiyonun süresi (istek ya da kabul) harcanır; ödeme/devir için ayrıca süre yoktur.
            _clock.Spend(minutes);
            bool tooExpensive = result != null ? result.TooExpensive : active.LastAskTooExpensive;
            SaleView view = ViewOf(active, work, tooExpensive);
            _npcs.RecordBoughtFromPlayer(active.NpcId, active.InstanceId);
            _demand.RecordSale(modelId, _time.Day);
            FinishCustomer(active, true);
            _state.CurrentSale = null;

            if (_events != null)
            {
                if (result != null)
                {
                    _events.Publish(new CustomerOfferMade(active.CustomerId, result.Round, result.Ask, result.ShownPrice, result.Phase, result.TooExpensive));
                }

                _events.Publish(new ItemSold(active.InstanceId, active.NpcId, price));
                _events.Publish(new CustomerNegotiationEnded(active.CustomerId, active.InstanceId, NegotiationPhase.Deal, price));
            }

            return Result<SaleView>.Ok(view);
        }

        private void PublishOffer(ActiveSale active, SaleRound result)
        {
            if (_events != null)
            {
                _events.Publish(new CustomerOfferMade(active.CustomerId, result.Round, result.Ask, result.ShownPrice, result.Phase, result.TooExpensive));
            }
        }

        // Satışı biten müşteri: yuva müşterisi işaretlenir (Sold/Left); kuyruk müşterisi kuyrukta tamamlanır (sıradakine geçilir).
        private void FinishCustomer(ActiveSale active, bool sold)
        {
            if (QueueCustomerId.IsQueueId(active.CustomerId))
            {
                _queue.CompleteForSale(active.CustomerId);
                return;
            }

            CustomerSlot slot = FindSlot(active.CustomerId);
            if (sold)
            {
                _customers.MarkSold(slot);
            }
            else
            {
                _customers.MarkLeft(slot);
            }
        }

        // Satıştaki müşterinin yuvası: gün yuvası ya da (kuyruk kimliğiyse) türetilmiş kuyruk yuvası.
        private CustomerSlot SlotOfActive(ActiveSale active)
        {
            if (QueueCustomerId.IsQueueId(active.CustomerId))
            {
                QueuedCustomer queued;
                CustomerSlot slot;
                if (!_queue.TryResolve(active.CustomerId, out queued, out slot))
                {
                    throw new InvalidOperationException("The queue customer " + active.CustomerId + " is not in today's queue.");
                }

                return slot;
            }

            return FindSlot(active.CustomerId);
        }

        private CustomerSlot FindSlot(long customerId)
        {
            foreach (CustomerSlot slot in _customers.State.Slots)
            {
                if (slot.CustomerId == customerId)
                {
                    return slot;
                }
            }

            throw new InvalidOperationException("The customer " + customerId + " is not in today's roster.");
        }

        private CustomerProfile ProfileOfSale(ActiveSale active)
        {
            if (QueueCustomerId.IsQueueId(active.CustomerId))
            {
                return _customers.ProfileFor(SlotOfActive(active));
            }

            foreach (CustomerSlot slot in _customers.State.Slots)
            {
                if (slot.CustomerId == active.CustomerId)
                {
                    return _customers.ProfileFor(slot);
                }
            }

            return _customers.ProfileOf(active.NpcId);
        }

        private SaleView ViewOf(ActiveSale active, SaleState state, bool tooExpensive)
        {
            NegotiationRules rules = _content.Negotiation;
            NegotiationLevel mood = NegotiationLevels.MoodOf(rules, state.Trust);
            NegotiationLevel patience = NegotiationLevels.PatienceOf(rules, state.Patience);

            var reports = new List<SaleReportView>();
            foreach (AppraisalResult r in _knowledge.ForInstance(active.InstanceId))
            {
                if (rules.SellReportLevelIds.Contains(r.LevelId))
                {
                    reports.Add(new SaleReportView(r.ResultId, r.LevelId));
                }
            }

            return new SaleView(
                active.CustomerId,
                active.NpcId,
                active.InstanceId,
                state.Phase,
                state.Round,
                state.ShownPrice,
                state.DealPrice,
                mood,
                patience,
                tooExpensive,
                state.ReportShown,
                reports,
                ProfileOfSale(active));
        }
    }
}
