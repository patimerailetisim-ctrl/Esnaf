using System;
using System.Collections.Generic;
using Esnaf.Core;
using Esnaf.Domain.Content;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Products;

namespace Esnaf.Domain.Time
{
    /// <summary>
    /// Gün sonu adım 5: talep endekslerini günceller (GDD v0.2 10.2; sahibi Economy). Yeni gün canlı günden önceyse hiçbir şey yapmaz.
    /// Her model için tek çekim, içerik sırasıyla, <c>"demand"</c> akışından.
    /// </summary>
    public sealed class DemandUpdateStep : IDayEndStep
    {
        private const string Stream = "demand";

        private readonly DemandModel _demand;
        private readonly RngStreams _rng;
        private readonly ContentDatabase _content;

        public string Id
        {
            get { return "demand_update"; }
        }

        public int Order
        {
            get { return DayEndOrder.DemandUpdate; }
        }

        public DemandUpdateStep(DemandModel demand, RngStreams rng, ContentDatabase content)
        {
            if (demand == null)
            {
                throw new ArgumentNullException(nameof(demand));
            }

            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            _demand = demand;
            _rng = rng;
            _content = content;
        }

        public Result Execute(DayEndContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var ids = new List<string>(_content.Products.Count);
            foreach (ProductDefinition product in _content.Products)
            {
                ids.Add(product.Id);
            }

            _demand.UpdateForNewDay(context.Day + 1, _rng.Get(Stream), ids);
            return Result.Ok();
        }
    }
}
