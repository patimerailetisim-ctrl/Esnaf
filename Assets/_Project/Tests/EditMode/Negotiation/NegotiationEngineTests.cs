using System;
using System.Collections.Generic;
using System.IO;
using Esnaf.Core;
using Esnaf.Domain.Content;
using Esnaf.Domain.Negotiation;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Negotiation
{
    public class NegotiationEngineTests
    {
        private static NegotiationRules Rules()
        {
            return MarketHarness.RealContent().Negotiation;
        }

        private static NegotiationSetup Setup(
            long ask = 30400, double reject = 24350, int patience = 5, int trust = 50, double urgency = 0.2,
            double persuasion = 0.6, double floor = 0.0, double wrongMult = 1.0, int day = 10)
        {
            return new NegotiationSetup(Money.FromTl(ask), reject, floor, patience, trust, urgency, persuasion, wrongMult, day);
        }

        private static TrumpPlay Card(bool valid, long problem, double power, bool report = false, string key = "c")
        {
            return new TrumpPlay(key, valid, Money.FromTl(problem), power, report);
        }

        private static RoundResult Offer(NegotiationEngine e, NegotiationState s, long offer, TrumpPlay card = null)
        {
            Result<RoundResult> r = e.Offer(s, Money.FromTl(offer), card);
            Assert.IsTrue(r.IsSuccess, r.ErrorCode);
            return r.Value;
        }

        // ---------- golden: kesin değerler (bağımsız Python referans modeli) ----------

        public static IEnumerable<string> ScenarioNames()
        {
            var root = (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(Path.Combine(TestPaths.FixturesDirectory(), "negotiation_golden.json")));
            foreach (object s in (List<object>)root["scenarios"])
            {
                yield return (string)((Dictionary<string, object>)s)["name"];
            }
        }

        private static Dictionary<string, object> Scenario(string name)
        {
            var root = (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(Path.Combine(TestPaths.FixturesDirectory(), "negotiation_golden.json")));
            foreach (object s in (List<object>)root["scenarios"])
            {
                var d = (Dictionary<string, object>)s;
                if ((string)d["name"] == name)
                {
                    return d;
                }
            }

            throw new InvalidOperationException("No scenario " + name);
        }

        [Test]
        public void GoldenFile_HasThirteenScenarios()
        {
            Assert.AreEqual(13, new List<string>(ScenarioNames()).Count);
        }

        [TestCaseSource(nameof(ScenarioNames))]
        public void Golden_EveryRoundMatchesTheReferenceModelExactly(string name)
        {
            Dictionary<string, object> sc = Scenario(name);
            var su = (Dictionary<string, object>)sc["setup"];
            var setup = new NegotiationSetup(
                Money.FromTl((long)(double)su["ask"]),
                (double)su["reject"],
                (double)su["floor"],
                (int)(double)su["patience"],
                (int)(double)su["trust"],
                (double)su["urgency"],
                (double)su["persuasion"],
                (double)su["wrongMult"],
                (int)(double)su["day"]);
            var engine = new NegotiationEngine(Rules());
            NegotiationState state = engine.Begin(setup);

            int round = 0;
            foreach (object ro in (List<object>)sc["rounds"])
            {
                var r = (Dictionary<string, object>)ro;
                round++;
                TrumpPlay card = null;
                if (r["card"] != null)
                {
                    var c = (Dictionary<string, object>)r["card"];
                    card = Card((bool)c["valid"], (long)(double)c["problem"], (double)c["power"], (bool)c["report"], "card" + round);
                }

                RoundResult result = Offer(engine, state, (long)(double)r["offer"], card);
                string at = name + " round " + round;
                Assert.AreEqual(Money.FromTl((long)(double)r["price"]), result.ShownPrice, at + " price");
                Assert.AreEqual((int)(double)r["trust"], state.Trust, at + " trust");
                Assert.AreEqual((int)(double)r["patience"], state.Patience, at + " patience");
                Assert.AreEqual((double)r["reject"], state.Reject, 1e-6, at + " reject");
                Assert.AreEqual((bool)r["insult"], result.Insulted, at + " insult");
                string phase = (string)r["phase"];
                NegotiationPhase expected = phase == "active" ? NegotiationPhase.Active : phase == "final" ? NegotiationPhase.FinalOffer : NegotiationPhase.Deal;
                Assert.AreEqual(expected, state.Phase, at + " phase");
                if (r["deal"] != null)
                {
                    Assert.AreEqual(Money.FromTl((long)(double)r["deal"]), state.DealPrice, at + " deal");
                    Assert.AreEqual(state.DealPrice, result.DealPrice);
                }
            }
        }

        [TestCase("kemal")]
        [TestCase("hatice")]
        [TestCase("selin")]
        [TestCase("cengiz")]
        public void Golden_MatchesTheDocumentScenariosWithinTenTl(string name)
        {
            Dictionary<string, object> sc = Scenario(name);
            var docs = (List<object>)sc["docPrices"];
            var rounds = (List<object>)sc["rounds"];
            Assert.AreEqual(rounds.Count, docs.Count, name);
            for (int i = 0; i < docs.Count; i++)
            {
                double golden = (double)((Dictionary<string, object>)rounds[i])["price"];
                Assert.AreEqual((double)docs[i], golden, 10.0, name + " round " + (i + 1));
            }
        }

        // ---------- kurallar ----------

        [Test]
        public void Begin_StartsActiveAtTheAskingPrice()
        {
            NegotiationState s = new NegotiationEngine(Rules()).Begin(Setup());

            Assert.AreEqual(NegotiationPhase.Active, s.Phase);
            Assert.AreEqual(Money.FromTl(30400), s.ShownPrice);
            Assert.AreEqual(5, s.Patience);
            Assert.AreEqual(50, s.Trust);
            Assert.AreEqual(24350.0, s.Reject, 0.0);
            Assert.AreEqual(0, s.Round);
            Assert.IsTrue(s.DealPrice.IsZero);
        }

        [Test]
        public void AnOfferAtOrAboveTheShownPrice_IsAnImmediateDealAtTheShownPrice()
        {
            var e = new NegotiationEngine(Rules());
            NegotiationState s = e.Begin(Setup());

            RoundResult r = Offer(e, s, 40000);

            Assert.AreEqual(NegotiationPhase.Deal, r.Phase);
            Assert.AreEqual(Money.FromTl(28100), r.DealPrice); // teklife değil, satıcının o turdaki fiyatına
            Assert.AreEqual(5, s.Patience);
        }

        [Test]
        public void NearOffer_AtNinetySevenPercentOfReject_GainsTrust_ButAnOfferJustBelowDoesNot()
        {
            var e = new NegotiationEngine(Rules());
            NegotiationState near = e.Begin(Setup());
            NegotiationState far = e.Begin(Setup());

            Offer(e, near, 23620); // 0,97 × 24350 = 23619,5 → eşik üstü
            Offer(e, far, 23610);

            Assert.AreEqual(55, near.Trust);
            Assert.AreEqual(50, far.Trust);
            Assert.AreEqual(4, near.Patience);
            Assert.AreEqual(4, far.Patience);
        }


        [Test]
        public void AnOfferExactlyAtTheShownPrice_IsADeal()
        {
            var e = new NegotiationEngine(Rules());
            NegotiationState s = e.Begin(Setup());

            RoundResult r = Offer(e, s, 28100); // ilk tur fiyatı tam 28.100 (golden)

            Assert.AreEqual(NegotiationPhase.Deal, r.Phase);
            Assert.AreEqual(Money.FromTl(28100), r.DealPrice);
        }

        [Test]
        public void ANearOfferExactlyAtNinetySevenPercent_GainsTrust()
        {
            var e = new NegotiationEngine(Rules());
            NegotiationState at = e.Begin(Setup(reject: 20000)); // 0,97 × 20000 = 19400 (tam)
            NegotiationState below = e.Begin(Setup(reject: 20000));

            Offer(e, at, 19400);
            Offer(e, below, 19390);

            Assert.AreEqual(55, at.Trust);
            Assert.AreEqual(50, below.Trust);
        }

        [Test]
        public void Insult_BelowNinetyPercentOfReject_IsDetectedExactlyAtTheBoundary()
        {
            var e = new NegotiationEngine(Rules());
            NegotiationState below = e.Begin(Setup(reject: 20000));
            NegotiationState at = e.Begin(Setup(reject: 20000));

            RoundResult a = Offer(e, below, 17990);
            RoundResult b = Offer(e, at, 18000);

            Assert.IsTrue(a.Insulted);
            Assert.IsFalse(b.Insulted);
        }

        [Test]
        public void Insult_PenaltyStartsOnTheConfiguredDay()
        {
            var e = new NegotiationEngine(Rules());
            NegotiationState day4 = e.Begin(Setup(day: 4));
            NegotiationState day5 = e.Begin(Setup(day: 5));

            Offer(e, day4, 21900);
            Offer(e, day5, 21900);

            Assert.AreEqual(50, day4.Trust);
            Assert.AreEqual(4, day4.Patience);
            Assert.AreEqual(35, day5.Trust);
            Assert.AreEqual(3, day5.Patience);
        }

        [Test]
        public void Insult_TrustNeverGoesBelowZero()
        {
            var e = new NegotiationEngine(Rules());
            NegotiationState s = e.Begin(Setup(trust: 10, patience: 5));

            Offer(e, s, 10000);

            Assert.AreEqual(0, s.Trust);
        }

        [Test]
        public void TrustIsCappedAtOneHundred()
        {
            var e = new NegotiationEngine(Rules());
            NegotiationState s = e.Begin(Setup(trust: 98, patience: 5));

            Offer(e, s, 24000, Card(true, 3000, 0.7));

            Assert.AreEqual(100, s.Trust);
        }

        [Test]
        public void ValidCard_LowersRejectByProblemTimesPowerTimesPersuasion()
        {
            var e = new NegotiationEngine(Rules());
            NegotiationState s = e.Begin(Setup());

            RoundResult r = Offer(e, s, 22500, Card(true, 3040, 0.7));

            Assert.AreEqual(24350 - 3040 * 0.7 * 0.6, s.Reject, 1e-9);
            Assert.AreEqual(CardOutcome.Effective, r.CardOutcome);
            Assert.AreEqual(60, s.Trust); // +5 kart, +5 yakın teklif (22500 ≥ 0,97 × yeni R)
        }

        [Test]
        public void ProfessionalReport_AddsToPersuasion()
        {
            var e = new NegotiationEngine(Rules());
            NegotiationState plain = e.Begin(Setup(floor: 0));
            NegotiationState report = e.Begin(Setup(floor: 0));

            Offer(e, plain, 22500, Card(true, 3040, 1.0));
            Offer(e, report, 22500, Card(true, 3040, 1.0, report: true));

            Assert.AreEqual(24350 - 3040 * 0.6, plain.Reject, 1e-9);
            Assert.AreEqual(24350 - 3040 * 0.8, report.Reject, 1e-9);
        }

        [Test]
        public void ValidCard_NeverLowersRejectBelowTheFloor()
        {
            var e = new NegotiationEngine(Rules());
            NegotiationState s = e.Begin(Setup(floor: 24000));

            Offer(e, s, 22500, Card(true, 3040, 1.0, report: true));

            Assert.AreEqual(24000.0, s.Reject, 0.0);
        }

        [Test]
        public void ValidCard_NeverRaisesRejectWhenAlreadyBelowTheFloor()
        {
            var e = new NegotiationEngine(Rules());
            NegotiationState s = e.Begin(Setup(ask: 6750, reject: 5660, floor: 5800, persuasion: 0.8));

            Offer(e, s, 6000, Card(true, 1000, 1.0));

            Assert.AreEqual(5660.0, s.Reject, 0.0);
        }

        [Test]
        public void WrongCard_CostsTrustAndPatience_AndNeverMovesReject()
        {
            var e = new NegotiationEngine(Rules());
            NegotiationState s = e.Begin(Setup());

            RoundResult r = Offer(e, s, 23000, Card(false, 3040, 0.7));

            Assert.AreEqual(CardOutcome.FalseAlarm, r.CardOutcome);
            Assert.AreEqual(24350.0, s.Reject, 0.0);
            Assert.AreEqual(40, s.Trust);
            Assert.AreEqual(3, s.Patience);
        }

        [Test]
        public void WrongCard_PenaltyIsMultipliedForStrictSellers()
        {
            var e = new NegotiationEngine(Rules());
            NegotiationState s = e.Begin(Setup(wrongMult: 2.0));

            Offer(e, s, 23000, Card(false, 3040, 0.7));

            Assert.AreEqual(30, s.Trust);
            Assert.AreEqual(2, s.Patience);
        }

        [Test]
        public void ACardCannotBePlayedTwiceInOneNegotiation()
        {
            var e = new NegotiationEngine(Rules());
            NegotiationState s = e.Begin(Setup());
            Offer(e, s, 22500, Card(true, 3040, 0.7, key: "screen"));
            double reject = s.Reject;
            int trust = s.Trust;
            int round = s.Round;

            Result<RoundResult> again = e.Offer(s, Money.FromTl(22600), Card(true, 3040, 0.7, key: "screen"));

            Assert.IsTrue(again.IsFailure);
            Assert.AreEqual("card.already_used", again.ErrorCode);
            Assert.AreEqual(reject, s.Reject, 0.0);
            Assert.AreEqual(trust, s.Trust);
            Assert.AreEqual(round, s.Round);
        }

        [Test]
        public void PatienceZero_MakesTheSellersPriceFinal_AndOnlyAcceptOrWalkRemain()
        {
            var e = new NegotiationEngine(Rules());
            NegotiationState s = e.Begin(Setup(patience: 1));

            RoundResult r = Offer(e, s, 22000);

            Assert.AreEqual(NegotiationPhase.FinalOffer, r.Phase);
            Assert.AreEqual(Money.FromTl(28100), s.ShownPrice);
            Result<RoundResult> more = e.Offer(s, Money.FromTl(28100), null);
            Assert.IsTrue(more.IsFailure);
            Assert.AreEqual("negotiation.final_offer_only", more.ErrorCode);
        }

        [Test]
        public void AcceptFinal_ClosesTheDealAtTheFinalPrice()
        {
            var e = new NegotiationEngine(Rules());
            NegotiationState s = e.Begin(Setup(patience: 1));
            Offer(e, s, 22000);

            Result accepted = e.AcceptFinal(s);

            Assert.IsTrue(accepted.IsSuccess);
            Assert.AreEqual(NegotiationPhase.Deal, s.Phase);
            Assert.AreEqual(Money.FromTl(28100), s.DealPrice);
        }

        [Test]
        public void AcceptFinal_IsRejectedWhileTheNegotiationIsStillActive()
        {
            var e = new NegotiationEngine(Rules());
            NegotiationState s = e.Begin(Setup());

            Result r = e.AcceptFinal(s);

            Assert.AreEqual("negotiation.no_final_offer", r.ErrorCode);
            Assert.AreEqual(NegotiationPhase.Active, s.Phase);
        }

        [Test]
        public void WalkAway_FailsTheNegotiation_ThenNothingElseWorks()
        {
            var e = new NegotiationEngine(Rules());
            NegotiationState s = e.Begin(Setup());

            Assert.IsTrue(e.WalkAway(s).IsSuccess);

            Assert.AreEqual(NegotiationPhase.Failed, s.Phase);
            Assert.AreEqual("negotiation.closed", e.Offer(s, Money.FromTl(30000), null).ErrorCode);
            Assert.AreEqual("negotiation.closed", e.AcceptFinal(s).ErrorCode);
            Assert.AreEqual("negotiation.closed", e.WalkAway(s).ErrorCode);
        }

        [Test]
        public void AfterADeal_FurtherOffersAreRejected()
        {
            var e = new NegotiationEngine(Rules());
            NegotiationState s = e.Begin(Setup());
            Offer(e, s, 40000);

            Assert.AreEqual("negotiation.closed", e.Offer(s, Money.FromTl(30000), null).ErrorCode);
        }

        [TestCase(0L)]
        [TestCase(-1000L)]
        [TestCase(22005L)]
        public void InvalidOffers_AreRejectedWithoutChangingState(long tl)
        {
            var e = new NegotiationEngine(Rules());
            NegotiationState s = e.Begin(Setup());

            Result<RoundResult> r = e.Offer(s, Money.FromTl(tl), null);

            Assert.AreEqual("offer.invalid", r.ErrorCode);
            Assert.AreEqual(0, s.Round);
            Assert.AreEqual(5, s.Patience);
            Assert.AreEqual(Money.FromTl(30400), s.ShownPrice);
        }

        [Test]
        public void ShownPrice_IsRoundedToTen_AndNeverBelowTheRejectPrice()
        {
            var e = new NegotiationEngine(Rules());
            NegotiationState s = e.Begin(Setup(reject: 24336.4, trust: 100, urgency: 1.0, patience: 30));

            for (int i = 0; i < 25 && s.Phase == NegotiationPhase.Active; i++)
            {
                RoundResult r = Offer(e, s, 20000 + 10 * i);
                Assert.IsTrue(r.ShownPrice.IsRoundedTo10);
                Assert.GreaterOrEqual(r.ShownPrice.Tl, 24340L);
            }
        }

        [Test]
        public void Fuzz_Invariants_HoldOverThousandsOfRandomNegotiations()
        {
            var rnd = new Random(12345);
            var e = new NegotiationEngine(Rules());
            for (int n = 0; n < 2000; n++)
            {
                double reject = 3000 + rnd.NextDouble() * 40000;
                double floor = rnd.Next(3) == 0 ? reject * (0.6 + rnd.NextDouble() * 0.5) : 0;
                NegotiationSetup setup = new NegotiationSetup(
                    Money.FromTlRoundedTo10((long)(reject * (1.05 + rnd.NextDouble() * 0.3))),
                    reject,
                    floor,
                    1 + rnd.Next(6),
                    rnd.Next(101),
                    rnd.NextDouble(),
                    rnd.NextDouble(),
                    rnd.Next(2) == 0 ? 1.0 : 2.0,
                    1 + rnd.Next(12));
                NegotiationState s = e.Begin(setup);
                double lastReject = s.Reject;
                Money lastPrice = s.ShownPrice;
                for (int step = 0; step < 12 && s.Phase == NegotiationPhase.Active; step++)
                {
                    long offer = Money.RoundTo10((long)(reject * (0.5 + rnd.NextDouble() * 0.6)));
                    TrumpPlay card = null;
                    if (rnd.Next(3) == 0)
                    {
                        card = Card(rnd.Next(2) == 0, rnd.Next(100, 5000), 0.3 + rnd.NextDouble() * 0.7, rnd.Next(2) == 0, "k" + step);
                    }

                    RoundResult r = Offer(e, s, offer, card);
                    Assert.LessOrEqual(s.Reject, lastReject + 1e-9, "reject must never rise");
                    lastReject = s.Reject;
                    Assert.LessOrEqual(r.ShownPrice.Tl, lastPrice.Tl, "price must never rise");
                    lastPrice = r.ShownPrice;
                    Assert.GreaterOrEqual(r.ShownPrice.Tl, (long)Math.Ceiling(s.Reject / 10.0 - 1e-9) * 10L, "never below reject");
                    Assert.IsTrue(s.Trust >= 0 && s.Trust <= 100);
                    Assert.IsTrue(s.Patience >= 0);
                    if (s.Phase == NegotiationPhase.Deal)
                    {
                        Assert.LessOrEqual(s.DealPrice.Tl, offer, "deal never above the offer");
                        Assert.GreaterOrEqual((double)s.DealPrice.Tl, s.Reject, "I4: never below the reject price");
                    }
                }
            }
        }
    }
}
