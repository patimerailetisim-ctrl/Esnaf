using System;
using Esnaf.Core;

namespace Esnaf.Domain.Negotiation
{
    /// <summary>
    /// Alış pazarlığı motoru (GDD v0.2 7.1–7.4). SAF hesaptır: rastgelelik yok, durum yalnızca verilen <see cref="NegotiationState"/>'tedir;
    /// sayıların hepsi <see cref="NegotiationRules"/>'tan gelir. Fiyat ve R içeride kesin (double) tutulur, oyuncuya 10 TL'ye yuvarlı gösterilir;
    /// gösterilen fiyat hiçbir zaman ret fiyatının altına inmez (I4).
    ///
    /// Bir teklifin sırası: (0) varsa koz kartı → (1) hakaret denetimi → (2) satıcının indirimi → (3) anlaşma? → (4) yakın teklif / sabır.
    /// Başarısız bir çağrı durumu değiştirmez.
    /// </summary>
    public sealed class NegotiationEngine
    {
        private readonly NegotiationRules _rules;

        public NegotiationEngine(NegotiationRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            _rules = rules;
        }

        public NegotiationState Begin(NegotiationSetup setup)
        {
            if (setup == null)
            {
                throw new ArgumentNullException(nameof(setup));
            }

            return new NegotiationState(setup);
        }

        public Result<RoundResult> Offer(NegotiationState state, Money offer, TrumpPlay card)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (!state.IsOpen)
            {
                return Result<RoundResult>.Fail("negotiation.closed", "The negotiation is over.");
            }

            if (state.Phase == NegotiationPhase.FinalOffer)
            {
                return Result<RoundResult>.Fail("negotiation.final_offer_only", "The seller made a final offer: accept it or walk away.");
            }

            if (!offer.IsPositive || !offer.IsRoundedTo10)
            {
                return Result<RoundResult>.Fail("offer.invalid", "An offer must be positive and a multiple of 10 TL.");
            }

            if (card != null && state.HasUsedCard(card.Key))
            {
                return Result<RoundResult>.Fail("card.already_used", "This card was already played in this negotiation.");
            }

            NegotiationSetup setup = state.Setup;
            double reject = state.Reject;
            int trust = state.Trust;
            int patience = state.Patience;
            CardOutcome outcome = CardOutcome.None;

            // (0) koz kartı
            if (card != null)
            {
                if (card.IsValid)
                {
                    double persuasion = setup.Persuasion + (card.IsProfessionalReport ? _rules.ReportPersuasionBonus : 0.0);
                    double effect = card.ProblemValue.Tl * card.EvidencePower * persuasion;
                    reject = Math.Min(reject, Math.Max(setup.Floor, reject - effect)); // taban R'yi asla YÜKSELTMEZ
                    trust = Math.Min(100, trust + _rules.CardCorrectTrustGain);
                    outcome = CardOutcome.Effective;
                }
                else
                {
                    trust = Math.Max(0, trust - (int)(_rules.CardWrongTrustLoss * setup.WrongCardMultiplier));
                    patience = Math.Max(0, patience - (int)(_rules.CardWrongPatienceLoss * setup.WrongCardMultiplier));
                    outcome = CardOutcome.FalseAlarm;
                }
            }

            // (1) hakaret
            double offerTl = offer.Tl;
            bool insulted = offerTl < _rules.InsultRatio * reject;
            if (insulted && setup.Day >= _rules.InsultPenaltyFromDay)
            {
                trust = Math.Max(0, trust - _rules.InsultTrustLoss);
                patience = Math.Max(0, patience - _rules.InsultExtraPatienceLoss);
            }

            // (2) satıcı indirim yapar: boşluğun t kadarını kapatır
            double t = _rules.PriceBaseShare + _rules.PriceTrustShare * (trust / 100.0) + _rules.PriceUrgencyShare * setup.Urgency;
            double price = Math.Max(reject, state.Price - t * (state.Price - reject));
            Money shown = Money.FromTl(Math.Max(RoundTo10(price), CeilTo10(reject)));

            NegotiationPhase phase = NegotiationPhase.Active;
            Money deal = Money.Zero;
            if (offer >= shown)
            {
                // (3) anlaşma: teklife değil, satıcının o turdaki fiyatına
                phase = NegotiationPhase.Deal;
                deal = shown;
            }
            else
            {
                // (4) yakın teklif güven kazandırır; her reddedilen teklif sabırdan bir götürür
                if (offerTl >= _rules.NearOfferRatio * reject)
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
            state.Reject = reject;
            state.Trust = trust;
            state.Patience = patience;
            state.Price = price;
            state.ShownPrice = shown;
            state.Phase = phase;
            state.DealPrice = deal;
            if (card != null)
            {
                state.MarkCardUsed(card.Key);
            }

            return Result<RoundResult>.Ok(new RoundResult(state.Round, offer, shown, phase, insulted, outcome, deal));
        }

        /// <summary>Son teklifi (sabır bitince) gösterilen fiyattan kabul eder.</summary>
        public Result AcceptFinal(NegotiationState state)
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
                return Result.Fail("negotiation.no_final_offer", "The seller has not made a final offer.");
            }

            state.Phase = NegotiationPhase.Deal;
            state.DealPrice = state.ShownPrice;
            return Result.Ok();
        }

        /// <summary>Masadan kalkar; pazarlık başarısız biter.</summary>
        public Result WalkAway(NegotiationState state)
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

        private static long CeilTo10(double tl)
        {
            return (long)Math.Ceiling(tl / 10.0) * 10L;
        }
    }
}
