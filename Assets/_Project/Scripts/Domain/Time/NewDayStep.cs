using System;
using System.Collections.Generic;
using Esnaf.Core;
using Esnaf.Domain.Market;

namespace Esnaf.Domain.Time
{
    /// <summary>
    /// Gün sonu adım 7: yeni günü başlatır ve yeni ilanları üretir. Sıra: önce ilanlar üretilir (başarısızsa hiçbir şey
    /// değişmez), sonra gün ilerler ve pazara eklenir; olaylar en sonda: <c>DayEnded</c> → <c>DayStarted</c> → <c>ListingsGenerated</c>.
    /// İlan üretimi <c>"market"</c> akışını her seferinde <see cref="RngStreams.Get"/> ile alır (akışlar yeniden yüklenebilir).
    /// </summary>
    public sealed class NewDayStep : IDayEndStep
    {
        private const string MarketStream = "market";

        private readonly TimeState _time;
        private readonly MarketState _market;
        private readonly ListingGenerator _generator;
        private readonly RngStreams _rng;
        private readonly IEventBus _events;
        private readonly List<INewDayHook> _hooks;

        public string Id
        {
            get { return "new_day"; }
        }

        public int Order
        {
            get { return DayEndOrder.NewDay; }
        }

        public NewDayStep(TimeState time, MarketState market, ListingGenerator generator, RngStreams rng, IEventBus events, IEnumerable<INewDayHook> hooks = null)
        {
            if (time == null)
            {
                throw new ArgumentNullException(nameof(time));
            }

            if (market == null)
            {
                throw new ArgumentNullException(nameof(market));
            }

            if (generator == null)
            {
                throw new ArgumentNullException(nameof(generator));
            }

            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            _time = time;
            _market = market;
            _generator = generator;
            _rng = rng;
            _events = events;
            _hooks = new List<INewDayHook>(hooks ?? new INewDayHook[0]);
        }

        public Result Execute(DayEndContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (_time.Day != context.Day)
            {
                return Result.Fail("time.day_mismatch", "The game is on day " + _time.Day + " but day " + context.Day + " is being ended.");
            }

            int newDay = context.Day + 1;
            Result<IReadOnlyList<long>> opened = Open(newDay);
            if (opened.IsFailure)
            {
                return Result.Fail(opened.ErrorCode, opened.Message);
            }

            _time.Advance();
            context.NewDay = _time.Day;
            RunHooks(_time.Day);
            foreach (long id in opened.Value)
            {
                context.NewListingIds.Add(id);
            }

            if (_events != null)
            {
                _events.Publish(new DayEnded(context.Day));
                _events.Publish(new DayStarted(_time.Day));
                _events.Publish(new ListingsGenerated(_time.Day, opened.Value));
            }

            return Result.Ok();
        }

        /// <summary>Oyunun ilk gününü açar (yeni oyun): mevcut gün için ilanları üretir, gün ilerlemez.</summary>
        public Result OpenFirstDay()
        {
            Result<IReadOnlyList<long>> opened = Open(_time.Day);
            if (opened.IsFailure)
            {
                return Result.Fail(opened.ErrorCode, opened.Message);
            }

            RunHooks(_time.Day);
            if (_events != null)
            {
                _events.Publish(new DayStarted(_time.Day));
                _events.Publish(new ListingsGenerated(_time.Day, opened.Value));
            }

            return Result.Ok();
        }

        private void RunHooks(int day)
        {
            for (int i = 0; i < _hooks.Count; i++)
            {
                _hooks[i].OnDayOpened(day);
            }
        }

        private Result<IReadOnlyList<long>> Open(int day)
        {
            IReadOnlyList<MarketListing> listings;
            try
            {
                listings = _generator.Generate(day, _rng.Get(MarketStream));
            }
            catch (InvalidOperationException ex)
            {
                return Result<IReadOnlyList<long>>.Fail("market.generation_failed", ex.Message);
            }

            var ids = new List<long>(listings.Count);
            foreach (MarketListing listing in listings)
            {
                _market.Add(listing);
                ids.Add(listing.ListingId);
            }

            return Result<IReadOnlyList<long>>.Ok(ids);
        }
    }
}
