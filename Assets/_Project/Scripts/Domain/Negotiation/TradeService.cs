using System;
using System.Collections.Generic;
using Esnaf.Core;
using Esnaf.Domain.Appraisal;
using Esnaf.Domain.Business;
using Esnaf.Domain.Content;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Inventory;
using Esnaf.Domain.Market;
using Esnaf.Domain.Npc;
using Esnaf.Domain.Products;
using Esnaf.Domain.Time;

namespace Esnaf.Domain.Negotiation
{
    /// <summary>
    /// Alış akışı (GDD v0.3 3.2 "Trade"): ilanı pazarlığa açar, teklifleri/kartları motora iletir, anlaşmada ürünü satın alıp rafa koyar,
    /// başarısız pazarlıkta ilanı kaldırır. Kurallar <see cref="NegotiationEngine"/>'dedir; burası yalnızca sistemleri birleştirir.
    ///
    /// Her komut ya tamamen uygulanır ya da HİÇ uygulanmaz (motor önce kopya üzerinde çalışır; satın alma başarısızsa kopya atılır).
    /// Pazarlık başarısız bitince (masadan kalkma) ilan kalkar ve ürün silinir: aynı ilana bedava yeniden deneme yoktur;
    /// bekleyen ekspertiz ücreti gider yazılır (GDD v0.2 5.1 "boşa ekspertiz").
    /// </summary>
    public sealed class TradeService
    {
        private readonly ContentDatabase _content;
        private readonly MarketState _market;
        private readonly InstanceStore _store;
        private readonly InventoryState _inventoryState;
        private readonly InventoryService _inventory;
        private readonly EconomyService _economy;
        private readonly KnowledgeState _knowledge;
        private readonly NpcStateStore _npcs;
        private readonly RngStreams _rng;
        private readonly TimeState _time;
        private readonly TradeState _state;
        private readonly IEventBus _events;
        private readonly CustomerService _customers;
        private readonly NegotiationEngine _engine;
        private readonly NegotiationSetupFactory _factory;
        private readonly ValueCalculator _calculator;

        public TradeService(
            ContentDatabase content,
            MarketState market,
            InstanceStore store,
            InventoryState inventoryState,
            InventoryService inventory,
            EconomyService economy,
            KnowledgeState knowledge,
            NpcStateStore npcs,
            RngStreams rng,
            TimeState time,
            TradeState state,
            IEventBus events,
            CustomerService customers = null)
        {
            if (content == null || market == null || store == null || inventoryState == null || inventory == null || economy == null
                || knowledge == null || npcs == null || rng == null || time == null || state == null)
            {
                throw new ArgumentNullException(nameof(content), "TradeService needs all of its collaborators.");
            }

            _content = content;
            _market = market;
            _store = store;
            _inventoryState = inventoryState;
            _inventory = inventory;
            _economy = economy;
            _knowledge = knowledge;
            _npcs = npcs;
            _rng = rng;
            _time = time;
            _state = state;
            _events = events;
            _customers = customers;
            _engine = new NegotiationEngine(content.Negotiation);
            _factory = new NegotiationSetupFactory(content.Negotiation);
            _calculator = new ValueCalculator(content.ValueTables);
        }

        /// <summary>Süren pazarlığın görünümü; yoksa null.</summary>
        public NegotiationView GetCurrent()
        {
            ActiveNegotiation active = _state.Current;
            return active == null ? null : ViewOf(active, active.State, active.LastOfferInsulted);
        }

        public Result<NegotiationView> Start(long listingId)
        {
            if (_state.IsBusy)
            {
                return Result<NegotiationView>.Fail("negotiation.in_progress", "Finish the current negotiation first.");
            }

            MarketListing listing;
            if (!_market.TryGet(listingId, out listing))
            {
                return Result<NegotiationView>.Fail("listing.unknown", "Unknown listing " + listingId + ".");
            }

            if (_inventoryState.Count >= _inventoryState.Capacity)
            {
                return Result<NegotiationView>.Fail("inventory.full", "The shelf is full (" + _inventoryState.Capacity + ").");
            }

            ProductInstance instance = _store.Get(listing.InstanceId);
            ProductDefinition definition = _content.GetProduct(instance.DefinitionId);
            NpcSellerRole seller = _content.GetNpc(listing.SellerNpcId).Seller;
            Money trueValue = _calculator.TrueValue(instance, definition);
            NegotiationSetup setup = _factory.Create(listing, seller, trueValue, _time.Day, _rng.Get("negotiation"));

            var active = new ActiveNegotiation(listing.ListingId, listing.InstanceId, listing.SellerNpcId, _engine.Begin(setup));
            _state.Current = active;
            _npcs.RecordEncounter(listing.SellerNpcId);

            if (_events != null)
            {
                _events.Publish(new NegotiationStarted(listing.ListingId, listing.InstanceId, listing.SellerNpcId));
            }

            return Result<NegotiationView>.Ok(ViewOf(active, active.State, false));
        }

        /// <param name="card">Oynanacak kart (AppraisalId, CardIndex) ya da null.</param>
        public Result<NegotiationView> Offer(Money offer, long? appraisalId, int cardIndex)
        {
            ActiveNegotiation active = _state.Current;
            if (active == null)
            {
                return Result<NegotiationView>.Fail("negotiation.none", "There is no negotiation in progress.");
            }

            TrumpPlay play = null;
            if (appraisalId.HasValue)
            {
                Result<TrumpPlay> resolved = ResolveCard(active, appraisalId.Value, cardIndex);
                if (resolved.IsFailure)
                {
                    return Result<NegotiationView>.Fail(resolved.ErrorCode, resolved.Message);
                }

                play = resolved.Value;
            }

            NegotiationState work = active.State.Clone();
            Result<RoundResult> round = _engine.Offer(work, offer, play);
            if (round.IsFailure)
            {
                return Result<NegotiationView>.Fail(round.ErrorCode, round.Message);
            }

            RoundResult result = round.Value;
            if (work.Phase == NegotiationPhase.Deal)
            {
                return Buy(active, work, result);
            }

            active.State = work;
            active.LastOfferInsulted = result.Insulted;
            PublishOffer(active, result);
            return Result<NegotiationView>.Ok(ViewOf(active, work, result.Insulted));
        }

        public Result<NegotiationView> AcceptFinal()
        {
            ActiveNegotiation active = _state.Current;
            if (active == null)
            {
                return Result<NegotiationView>.Fail("negotiation.none", "There is no negotiation in progress.");
            }

            NegotiationState work = active.State.Clone();
            Result accepted = _engine.AcceptFinal(work);
            if (accepted.IsFailure)
            {
                return Result<NegotiationView>.Fail(accepted.ErrorCode, accepted.Message);
            }

            return Buy(active, work, null);
        }

        public Result<NegotiationView> WalkAway()
        {
            ActiveNegotiation active = _state.Current;
            if (active == null)
            {
                return Result<NegotiationView>.Fail("negotiation.none", "There is no negotiation in progress.");
            }

            NegotiationState work = active.State.Clone();
            Result walked = _engine.WalkAway(work);
            if (walked.IsFailure)
            {
                return Result<NegotiationView>.Fail(walked.ErrorCode, walked.Message);
            }

            NegotiationView view = ViewOf(active, work, active.LastOfferInsulted);
            _market.Remove(active.ListingId);
            ProductInstance instance;
            if (_store.TryGet(active.InstanceId, out instance) && instance.Location == ProductLocation.Market)
            {
                // "appraisal.nothing_pending" normaldir (ekspertiz yapılmamış).
                _economy.WriteOffAppraisals(active.InstanceId, _time.Day);
                _store.Remove(active.InstanceId);
            }

            _state.Current = null;
            if (_events != null)
            {
                _events.Publish(new NegotiationEnded(active.ListingId, active.InstanceId, NegotiationPhase.Failed, Money.Zero));
            }

            return Result<NegotiationView>.Ok(view);
        }

        private Result<NegotiationView> Buy(ActiveNegotiation active, NegotiationState work, RoundResult result)
        {
            Money price = work.DealPrice;
            Result acquired = _inventory.Acquire(active.InstanceId, price, _time.Day);
            if (acquired.IsFailure)
            {
                return Result<NegotiationView>.Fail(acquired.ErrorCode, acquired.Message);
            }

            bool insulted = result != null ? result.Insulted : active.LastOfferInsulted;
            NegotiationView view = ViewOf(active, work, insulted);
            _market.Remove(active.ListingId);
            _npcs.RecordSoldToPlayer(active.SellerNpcId, active.InstanceId);
            _state.Current = null;
            if (_customers != null)
            {
                _customers.RefreshArrivals();
            }

            if (_events != null)
            {
                if (result != null)
                {
                    _events.Publish(new OfferMade(active.ListingId, result.Round, result.Offer, result.ShownPrice, result.Phase, result.Insulted));
                }

                _events.Publish(new ListingPurchased(active.ListingId, active.InstanceId, active.SellerNpcId, price));
                _events.Publish(new NegotiationEnded(active.ListingId, active.InstanceId, NegotiationPhase.Deal, price));
            }

            return Result<NegotiationView>.Ok(view);
        }

        private void PublishOffer(ActiveNegotiation active, RoundResult result)
        {
            if (_events != null)
            {
                _events.Publish(new OfferMade(active.ListingId, result.Round, result.Offer, result.ShownPrice, result.Phase, result.Insulted));
            }
        }

        private Result<TrumpPlay> ResolveCard(ActiveNegotiation active, long appraisalId, int cardIndex)
        {
            AppraisalResult appraisal;
            if (!_knowledge.TryGetById(appraisalId, out appraisal) || appraisal.InstanceId != active.InstanceId
                || cardIndex < 0 || cardIndex >= appraisal.Cards.Count)
            {
                return Result<TrumpPlay>.Fail("card.unknown", "Unknown card " + appraisalId + "/" + cardIndex + " for this negotiation.");
            }

            TrumpCard card = appraisal.Cards[cardIndex];
            return Result<TrumpPlay>.Ok(new TrumpPlay(
                CardKey(appraisalId, cardIndex),
                !card.IsFalseAlarm,
                card.ProblemValue,
                card.EvidencePower,
                string.Equals(appraisal.LevelId, _content.Negotiation.ReportLevelId, StringComparison.Ordinal)));
        }

        private static string CardKey(long appraisalId, int cardIndex)
        {
            return appraisalId.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":"
                + cardIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private NegotiationView ViewOf(ActiveNegotiation active, NegotiationState state, bool insulted)
        {
            NegotiationRules rules = _content.Negotiation;
            NegotiationLevel mood = NegotiationLevels.MoodOf(rules, state.Trust);
            NegotiationLevel patience = NegotiationLevels.PatienceOf(rules, state.Patience);

            var cards = new List<NegotiationCardView>();
            foreach (AppraisalResult appraisal in _knowledge.ForInstance(active.InstanceId))
            {
                for (int i = 0; i < appraisal.Cards.Count; i++)
                {
                    TrumpCard c = appraisal.Cards[i];
                    cards.Add(new NegotiationCardView(
                        appraisal.ResultId,
                        i,
                        c.Attribute,
                        c.WordingKey,
                        c.Confidence,
                        c.EvidencePower,
                        c.ProblemValue,
                        state.HasUsedCard(CardKey(appraisal.ResultId, i))));
                }
            }

            return new NegotiationView(
                active.ListingId,
                active.InstanceId,
                active.SellerNpcId,
                state.Phase,
                state.Round,
                state.ShownPrice,
                state.DealPrice,
                mood,
                patience,
                insulted,
                cards);
        }
    }
}
