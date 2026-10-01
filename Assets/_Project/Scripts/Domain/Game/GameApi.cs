using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Appraisal;
using Esnaf.Domain.Business;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Market;
using Esnaf.Domain.Negotiation;
using Esnaf.Domain.Phone;
using Esnaf.Domain.Products;
using Esnaf.Domain.Time;

namespace Esnaf.Domain.Game
{
    /// <summary><see cref="IGameApi"/>'nin <see cref="GameSession"/> üzerindeki gerçeklemesi.</summary>
    public sealed class GameApi : IGameApi
    {
        private readonly GameSession _session;

        public GameApi(GameSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            _session = session;
        }

        public Result<DayEndReport> EndDay()
        {
            if (_session.TradeState.IsBusy)
            {
                return Result<DayEndReport>.Fail("negotiation.in_progress", "Finish or leave the negotiation before ending the day.");
            }

            var context = new DayEndContext(_session.Time.Day);
            Result result = _session.DayEnd.Run(context);
            if (result.IsFailure)
            {
                return Result<DayEndReport>.Fail(result.ErrorCode, result.Message);
            }

            // Biten günün özeti: yeni gün açılışı stok ve servet değiştirmediği için şimdi hesaplamak gün sonu anıyla aynıdır.
            DaySummary summary = _session.Summaries.Build(context.Day, _session.InventoryService.GetStockLines(), _session.Wealth.Calculate());
            return Result<DayEndReport>.Ok(new DayEndReport(
                context.Day,
                context.NewDay,
                context.ExpenseCharged,
                context.ExpiredListingIds,
                context.NewListingIds,
                context.ExecutedStepIds,
                summary,
                context.MissedCustomers));
        }

        public Result<NegotiationView> StartNegotiation(long listingId)
        {
            return _session.Trade.Start(listingId);
        }

        public Result<NegotiationView> MakeOffer(Money offer)
        {
            return _session.Trade.Offer(offer, null, 0);
        }

        public Result<NegotiationView> MakeOfferWithCard(Money offer, long appraisalId, int cardIndex)
        {
            return _session.Trade.Offer(offer, appraisalId, cardIndex);
        }

        public Result<NegotiationView> AcceptFinalPrice()
        {
            return _session.Trade.AcceptFinal();
        }

        public Result<Money> BuyListing(long listingId)
        {
            return _session.Trade.BuyNow(listingId);
        }

        public Result<NegotiationView> WalkAway()
        {
            return _session.Trade.WalkAway();
        }

        public Result SetPrice(long instanceId, Money price)
        {
            return _session.InventoryService.SetPrice(instanceId, price);
        }

        public Result<SaleView> StartSale(long customerId)
        {
            return _session.Sell.Start(customerId);
        }

        public Result<SaleView> AskPrice(Money ask)
        {
            return _session.Sell.Ask(ask);
        }

        public Result<SaleView> ShowReport(long appraisalId)
        {
            return _session.Sell.ShowReport(appraisalId);
        }

        public Result<SaleView> AcceptCustomerFinalOffer()
        {
            return _session.Sell.AcceptFinal();
        }

        public Result<SaleView> LetCustomerGo()
        {
            return _session.Sell.Leave();
        }

        public IReadOnlyList<CustomerView> GetCustomers()
        {
            return _session.Sell.GetCustomers();
        }

        public SaleView GetSale()
        {
            return _session.Sell.GetCurrent();
        }

        public NegotiationView GetNegotiation()
        {
            return _session.Trade.GetCurrent();
        }

        public Result<AppraisalView> StartAppraisal(long listingId, string levelId)
        {
            MarketListing listing;
            if (!_session.Market.TryGet(listingId, out listing))
            {
                return Result<AppraisalView>.Fail("listing.unknown", "Unknown listing " + listingId + ".");
            }

            Result<AppraisalResult> result = _session.Appraisal.Appraise(listing.InstanceId, levelId, _session.Time.Day);
            if (result.IsFailure)
            {
                return Result<AppraisalView>.Fail(result.ErrorCode, result.Message);
            }

            return Result<AppraisalView>.Ok(ViewOf(result.Value, listingId));
        }

        public IReadOnlyList<AppraisalView> GetAppraisals(long listingId)
        {
            MarketListing listing;
            if (!_session.Market.TryGet(listingId, out listing))
            {
                return new ReadOnlyCollection<AppraisalView>(new List<AppraisalView>());
            }

            return new ReadOnlyCollection<AppraisalView>(
                _session.Knowledge.ForInstance(listing.InstanceId).Select(r => ViewOf(r, listingId)).ToList());
        }

        public Result<RiskCard> GetRiskCard(long appraisalId, Money offer)
        {
            AppraisalResult result;
            if (!_session.Knowledge.TryGetById(appraisalId, out result))
            {
                return Result<RiskCard>.Fail("appraisal.unknown", "Unknown appraisal " + appraisalId + ".");
            }

            return new RiskCardBuilder(_session.Content.Appraisal).Build(result, offer);
        }

        private static AppraisalView ViewOf(AppraisalResult r, long listingId)
        {
            return new AppraisalView(
                r.ResultId,
                listingId,
                r.InstanceId,
                r.LevelId,
                r.Day,
                r.Fee,
                r.Findings.Select(f => new FindingView(f.Attribute, f.WordingKey, f.Found, f.Confidence, f.EvidencePower)),
                r.BatteryRange,
                r.BodyRange,
                r.ValueRange,
                r.Cards.Select(c => new CardView(c.Attribute, c.WordingKey, c.Confidence, c.EvidencePower, c.ProblemValue)));
        }

        public int GetDay()
        {
            return _session.Time.Day;
        }

        public Money GetCash()
        {
            return _session.EconomyState.Cash;
        }

        public IReadOnlyList<ListingView> GetListings()
        {
            var views = new List<ListingView>(_session.Market.Count);
            foreach (MarketListing listing in _session.Market.Listings)
            {
                ProductInstance instance = _session.Store.Get(listing.InstanceId);
                views.Add(new ListingView(
                    listing.ListingId,
                    listing.InstanceId,
                    instance.DefinitionId,
                    instance.StorageGb,
                    instance.AgeMonths,
                    instance.GetFlag(PhoneAttributes.Box),
                    instance.GetFlag(PhoneAttributes.Invoice),
                    listing.SellerNpcId,
                    listing.AskingPrice,
                    listing.DayListed,
                    listing.RemainingDays,
                    listing.Tags));
            }

            return new ReadOnlyCollection<ListingView>(views);
        }

        public IReadOnlyList<StockLine> GetInventory()
        {
            return _session.InventoryService.GetStockLines();
        }

        public DaySummary GetTodaySummary()
        {
            return _session.Summaries.Build(_session.Time.Day, _session.InventoryService.GetStockLines(), _session.Wealth.Calculate());
        }

        public string GetStateDigest()
        {
            return GameStateDigest.Compute(_session);
        }
    }
}
