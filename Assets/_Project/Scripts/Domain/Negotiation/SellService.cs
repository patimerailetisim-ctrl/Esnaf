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
            IEventBus events)
        {
            if (content == null || customers == null || state == null || inventory == null || store == null
                || npcs == null || demand == null || knowledge == null || time == null)
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
            _engine = new SaleEngine(content.Negotiation);
        }

        /// <summary>Şu an dükkâna gelmiş ve bir ürünle ilgilenen müşteriler.</summary>
        public IReadOnlyList<CustomerView> GetCustomers()
        {
            var views = new List<CustomerView>();
            foreach (KeyValuePair<CustomerSlot, long> pair in _customers.Interested())
            {
                views.Add(new CustomerView(pair.Key.CustomerId, pair.Key.NpcId, pair.Value));
            }

            return views;
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
            if (!_customers.TryGetWaiting(customerId, out slot))
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
            Result<SaleRound> round = _engine.Ask(work, ask);
            if (round.IsFailure)
            {
                return Result<SaleView>.Fail(round.ErrorCode, round.Message);
            }

            SaleRound result = round.Value;
            if (work.Phase == NegotiationPhase.Deal)
            {
                return Sell(active, work, result);
            }

            active.State = work;
            active.LastAskTooExpensive = result.TooExpensive;
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

            CustomerSlot slot = FindSlot(active.CustomerId);
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

            return Sell(active, work, null);
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
            _customers.MarkLeft(FindSlot(active.CustomerId));
            _state.CurrentSale = null;
            if (_events != null)
            {
                _events.Publish(new CustomerNegotiationEnded(active.CustomerId, active.InstanceId, NegotiationPhase.Failed, Money.Zero));
            }

            return Result<SaleView>.Ok(view);
        }

        private Result<SaleView> Sell(ActiveSale active, SaleState work, SaleRound result)
        {
            Money price = work.DealPrice;
            ProductInstance instance = _store.Get(active.InstanceId);
            string modelId = instance.DefinitionId;
            Result<SaleReceipt> sold = _inventory.Sell(active.InstanceId, price, _time.Day, active.NpcId);
            if (sold.IsFailure)
            {
                return Result<SaleView>.Fail(sold.ErrorCode, sold.Message);
            }

            bool tooExpensive = result != null ? result.TooExpensive : active.LastAskTooExpensive;
            SaleView view = ViewOf(active, work, tooExpensive);
            _npcs.RecordBoughtFromPlayer(active.NpcId, active.InstanceId);
            _demand.RecordSale(modelId, _time.Day);
            _customers.MarkSold(FindSlot(active.CustomerId));
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
                reports);
        }
    }
}
