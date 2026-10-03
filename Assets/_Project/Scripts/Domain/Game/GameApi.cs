using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Accessories;
using Esnaf.Domain.Wholesale;
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

        public CustomerQueueView GetCustomerQueue()
        {
            return _session.CustomerQueue.GetView(_session.Clock.View);
        }

        public CustomerView GetActiveCustomer()
        {
            return _session.Sell.GetActiveCustomer();
        }

        public Result<CustomerQueueView> CompleteCurrentCustomer()
        {
            if (_session.TradeState.CurrentSale != null)
            {
                return Result<CustomerQueueView>.Fail("queue.sale_in_progress", "Finish or leave the sale before completing the customer.");
            }

            // Gün 12.4: başarılı gönderme CompleteCustomer süresi harcar (görünüm güncel saatle döner).
            return _session.CustomerQueue.CompleteCurrent(InteractionTime.CompleteCustomer);
        }

        public ClockView GetClock()
        {
            return _session.Clock.View;
        }

        public Result<ClockView> AdvanceTime(int minutes)
        {
            Result<int> advanced = _session.Clock.Advance(minutes);
            return advanced.IsFailure
                ? Result<ClockView>.Fail(advanced.ErrorCode, advanced.Message)
                : Result<ClockView>.Ok(_session.Clock.View);
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

        public Result ClearPrice(long instanceId)
        {
            SaleView running = GetSale();
            if (running != null && running.InstanceId == instanceId)
            {
                return Result.Fail("price.item_in_sale", "The phone is part of the running sale.");
            }

            return _session.InventoryService.ClearPrice(instanceId);
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

        public Result<SaleView> AcceptCustomerOffer()
        {
            return _session.Sell.AcceptOffer();
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

        public AccessoryAddOnView GetAccessoryAddOns()
        {
            TransactionRecord sale = _session.AccessoryAddOns.LatestPhoneSaleOn(_session.Time.Day);
            if (sale == null)
            {
                return new AccessoryAddOnView(false, 0L, null, Money.Zero, Money.Zero, 0, Money.Zero, Money.Zero, Money.Zero, new AccessoryAddOnOptionView[0]);
            }

            Money phoneProfit = sale.Amount - sale.SaleCostBasis.Value;
            Money accessoryProfit = Money.Zero;
            Money accessoryRevenue = Money.Zero;
            IReadOnlyList<TransactionRecord> addOns = _session.AccessoryAddOns.AddOnsOf(sale.Id);
            foreach (TransactionRecord addOn in addOns)
            {
                accessoryProfit += addOn.Amount - addOn.SaleCostBasis.Value;
                accessoryRevenue += addOn.Amount;
            }

            // Yalnızca müşterinin istediği aksesuarlar (Gün 11.3.4); her biri 1 adet, eklendiyse "eklendi" işaretli.
            var options = new List<AccessoryAddOnOptionView>();
            foreach (string requestedId in _session.AccessoryAddOns.RequestedAccessories(sale.Id))
            {
                AccessoryDefinition definition;
                if (!_session.Content.Accessories.TryGet(requestedId, out definition))
                {
                    continue;
                }

                bool added = false;
                foreach (TransactionRecord addOn in addOns)
                {
                    added |= addOn.DefinitionId == requestedId;
                }

                options.Add(new AccessoryAddOnOptionView(
                    definition.Id, definition.Name, definition.RetailPrice, _session.AccessoryStock.Quantity(definition.Id), added));
            }

            return new AccessoryAddOnView(
                true, sale.Id, sale.NpcId, sale.Amount, phoneProfit, addOns.Count, accessoryRevenue, accessoryProfit, phoneProfit + accessoryProfit, options);
        }

        public Result<AccessorySaleReceipt> SellAccessoryAddOn(string accessoryId)
        {
            TransactionRecord sale = _session.AccessoryAddOns.LatestPhoneSaleOn(_session.Time.Day);
            if (sale == null)
            {
                return Result<AccessorySaleReceipt>.Fail("addon.no_sale", "There is no completed phone sale today to add an accessory to.");
            }

            Result<AccessorySaleReceipt> sold = _session.AccessoryAddOns.SellAddOn(sale.Id, accessoryId, _session.Time.Day);
            if (sold.IsSuccess)
            {
                _session.Clock.Spend(InteractionTime.AccessorySale); // Gün 12.4: yalnızca başarılı aksesuar satışı süre harcar
            }

            return sold;
        }

        public IReadOnlyList<WholesaleOfferView> GetWholesaleOffers()
        {
            int day = _session.Time.Day;
            var views = new List<WholesaleOfferView>();
            foreach (WholesaleOffer offer in _session.Content.Wholesale.Offers)
            {
                views.Add(new WholesaleOfferView(
                    offer.SupplierId,
                    offer.SupplierName,
                    offer.AccessoryId,
                    AccessoryNameOf(offer.AccessoryId),
                    offer.UnitCost,
                    offer.PackSize,
                    offer.PackCost,
                    offer.AvailableFromDay,
                    offer.AvailableFromDay <= day));
            }

            return views;
        }

        public Result<WholesalePurchaseReceipt> BuyWholesalePack(string supplierId, string accessoryId)
        {
            return _session.WholesaleService.BuyPack(supplierId, accessoryId, _session.Time.Day);
        }

        public AccessoryStockView GetAccessoryStock()
        {
            AccessoryStock stock = _session.AccessoryStock;
            var lines = new List<AccessoryStockLineView>();
            foreach (string id in stock.AccessoryIds)
            {
                lines.Add(new AccessoryStockLineView(id, AccessoryNameOf(id), stock.Quantity(id), stock.TotalCost(id)));
            }

            return new AccessoryStockView(stock.Capacity, stock.TotalUnits, stock.StockCost, lines);
        }

        private string AccessoryNameOf(string accessoryId)
        {
            AccessoryDefinition definition;
            return _session.Content.Accessories.TryGet(accessoryId, out definition) ? definition.Name : accessoryId;
        }

        public IReadOnlyList<StockLine> GetInventory()
        {
            // Her satıra, mevcut müşteri hesabından türeyen "müşteri tavanı" (yalnızca bilgi) eklenir; gün sonu özeti bu zenginleştirmeyi kullanmaz.
            IReadOnlyList<StockLine> lines = _session.InventoryService.GetStockLines();
            var result = new List<StockLine>(lines.Count);
            foreach (StockLine line in lines)
            {
                result.Add(new StockLine(
                    line.InstanceId,
                    line.DefinitionId,
                    line.CostBasis,
                    line.ListPrice,
                    _session.Customers.DemandCeilingFor(_session.Store.Get(line.InstanceId))));
            }

            return new System.Collections.ObjectModel.ReadOnlyCollection<StockLine>(result);
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
