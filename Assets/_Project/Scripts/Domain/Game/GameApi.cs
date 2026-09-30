using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Esnaf.Core;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Market;
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
                summary));
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
