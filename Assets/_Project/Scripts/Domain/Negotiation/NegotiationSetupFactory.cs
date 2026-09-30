using System;
using Esnaf.Core;
using Esnaf.Domain.Market;
using Esnaf.Domain.Npc;

namespace Esnaf.Domain.Negotiation
{
    /// <summary>
    /// Bir ilan için pazarlığın başlangıç durumunu kurar (GDD v0.2 7.2: satıcı "o günkü ruh haline" göre R'yi ±%3 oynatır,
    /// başlangıç güveni 50 ± 10). Rastgelelik SABİT sıradadır: her çağrıda tam iki <c>NextDouble</c> (ruh hali, güven);
    /// rehberli Gün 1 ilanında sonuçlar kullanılmaz ama çekim yine yapılır (akış tüketimi ilan türünden bağımsız kalır).
    /// </summary>
    public sealed class NegotiationSetupFactory
    {
        private readonly NegotiationRules _rules;

        public NegotiationSetupFactory(NegotiationRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            _rules = rules;
        }

        /// <param name="trueValue">Ürünün gerçek değeri (yalnızca koz kartı tabanı için; kayda yazılmaz).</param>
        public NegotiationSetup Create(MarketListing listing, NpcSellerRole seller, Money trueValue, int day, IRandom rng)
        {
            if (listing == null)
            {
                throw new ArgumentNullException(nameof(listing));
            }

            if (seller == null)
            {
                throw new ArgumentNullException(nameof(seller));
            }

            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            double moodDraw = rng.NextDouble();
            double trustDraw = rng.NextDouble();

            double reject = listing.RejectPrice.Tl;
            int trust = _rules.StartTrust;
            if (!listing.IsGuided)
            {
                reject *= 1.0 + _rules.RejectMoodSwing * (2.0 * moodDraw - 1.0);
                trust += (int)Math.Round(_rules.StartTrustSpread * (2.0 * trustDraw - 1.0), MidpointRounding.AwayFromZero);
            }

            reject = Math.Min(reject, listing.AskingPrice.Tl);
            trust = Math.Max(0, Math.Min(100, trust));

            return new NegotiationSetup(
                listing.AskingPrice,
                reject,
                _rules.RejectFloorRatio * trueValue.Tl,
                seller.Patience,
                trust,
                seller.Urgency,
                seller.Persuasion,
                seller.WrongCardPenaltyMultiplier,
                day);
        }
    }
}
