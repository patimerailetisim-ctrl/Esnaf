using System;
using Esnaf.Core;

namespace Esnaf.Domain.Negotiation
{
    /// <summary>
    /// Satış tarafı pazarlık motoru (GDD v0.2 7.2: "müşteri rolünde aynı motor ters yönde çalışır"). SAF hesaptır: rastgelelik yok.
    /// Oyuncu her turda bir fiyat İSTER; müşteri teklifini kendi Max'ına (M) doğru yükseltir. İstenen fiyat müşterinin yeni teklifine
    /// eşit ya da altındaysa anlaşma müşterinin teklifi üzerinden olur (I4: müşteri için ≤ M).
    ///
    /// Bir turun sırası: (1) "çok pahalı" denetimi (istenen &gt; oran × M) → (2) müşteri teklifini yükseltir → (3) anlaşma? →
    /// (4) yakın istek (≤ M / yakın oran) güven kazandırır, sabır düşer. Sayılar <see cref="NegotiationRules"/>'tandır ve alış motoruyla ortaktır.
    /// </summary>
    public sealed class SaleEngine
    {
        private readonly NegotiationRules _rules;

        public SaleEngine(NegotiationRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            _rules = rules;
        }

        public SaleState Begin(SaleSetup setup)
        {
            if (setup == null)
            {
                throw new ArgumentNullException(nameof(setup));
            }

            return new SaleState(setup);
        }

        public Result<SaleRound> Ask(SaleState state, Money ask)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (!state.IsOpen)
            {
                return Result<SaleRound>.Fail("negotiation.closed", "The negotiation is over.");
            }

            if (state.Phase == NegotiationPhase.FinalOffer)
            {
                return Result<SaleRound>.Fail("negotiation.final_offer_only", "The customer made a final offer: accept it or let them go.");
            }

            if (!ask.IsPositive || !ask.IsRoundedTo10)
            {
                return Result<SaleRound>.Fail("ask.invalid", "An asking price must be positive and a multiple of 10 TL.");
            }

            SaleSetup setup = state.Setup;
            double max = state.Max;
            int trust = state.Trust;
            int patience = state.Patience;
            double askTl = ask.Tl;

            // (1) çok pahalı
            bool tooExpensive = askTl > _rules.SellTooExpensiveRatio * max;
            if (tooExpensive && setup.Day >= _rules.InsultPenaltyFromDay)
            {
                trust = Math.Max(0, trust - _rules.InsultTrustLoss);
                patience = Math.Max(0, patience - _rules.InsultExtraPatienceLoss);
            }

            // (2) müşteri teklifini Max'a doğru yükseltir
            double t = _rules.PriceBaseShare + _rules.PriceTrustShare * (trust / 100.0) + _rules.PriceUrgencyShare * setup.Urgency;
            double offer = Math.Min(max, state.Offer + t * (max - state.Offer));
            Money shown = Money.FromTl(Math.Min(RoundTo10(offer), FloorTo10(max)));

            NegotiationPhase phase = NegotiationPhase.Active;
            Money deal = Money.Zero;
            if (ask <= shown)
            {
                // (3) anlaşma: istenen fiyata değil müşterinin teklifine
                phase = NegotiationPhase.Deal;
                deal = shown;
            }
            else
            {
                // (4) yakın istek güven kazandırır; her reddedilen istek sabırdan bir götürür
                if (askTl <= max / _rules.NearOfferRatio)
                {
                    trust = Math.Min(100, trust + _rules.NearOfferTrustGain);
                }

                patience = Math.Max(0, patience - 1);
                if (patience == 0)
                {
                    phase = NegotiationPhase.FinalOffer;
                }
            }

            state.Round += 1;
            state.Trust = trust;
            state.Patience = patience;
            state.Offer = offer;
            state.ShownPrice = shown;
            state.Phase = phase;
            state.DealPrice = deal;
            return Result<SaleRound>.Ok(new SaleRound(state.Round, ask, shown, phase, tooExpensive, deal));
        }

        /// <summary>
        /// "Rapor göster": müşterinin Max'ı yeni değere çekilir (değer hatası küçüldü) ve güven artar. Bir pazarlıkta bir kez.
        /// Yeni Max hesabı (σ'nın yarıya inmesi) motorun dışındadır: motor yalnızca sonucu uygular.
        /// </summary>
        public Result ApplyReport(SaleState state, double newMax, int trustGain)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (!state.IsOpen)
            {
                return Result.Fail("negotiation.closed", "The negotiation is over.");
            }

            if (state.ReportShown)
            {
                return Result.Fail("report.already_shown", "The report was already shown to this customer.");
            }

            state.Max = newMax;
            state.Offer = Math.Min(state.Offer, newMax);
            state.ShownPrice = Money.FromTl(Math.Min(state.ShownPrice.Tl, FloorTo10(newMax)));
            state.Trust = Math.Min(100, state.Trust + trustGain);
            state.ReportShown = true;
            return Result.Ok();
        }

        public Result AcceptFinal(SaleState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (!state.IsOpen)
            {
                return Result.Fail("negotiation.closed", "The negotiation is over.");
            }

            if (state.Phase != NegotiationPhase.FinalOffer)
            {
                return Result.Fail("negotiation.no_final_offer", "The customer has not made a final offer.");
            }

            state.Phase = NegotiationPhase.Deal;
            state.DealPrice = state.ShownPrice;
            return Result.Ok();
        }

        /// <summary>Müşteriyi yolcu eder; pazarlık başarısız biter.</summary>
        public Result Leave(SaleState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (!state.IsOpen)
            {
                return Result.Fail("negotiation.closed", "The negotiation is over.");
            }

            state.Phase = NegotiationPhase.Failed;
            return Result.Ok();
        }

        private static long RoundTo10(double tl)
        {
            return (long)Math.Floor(tl / 10.0 + 0.5) * 10L;
        }

        private static long FloorTo10(double tl)
        {
            return (long)Math.Floor(tl / 10.0) * 10L;
        }
    }
}
