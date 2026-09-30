using System;
using System.Collections.Generic;
using System.IO;
using Esnaf.Core;
using Esnaf.Domain.Negotiation;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Negotiation
{
    /// <summary>Satış tarafı pazarlık motoru: müşteri rolünde aynı motor ters yönde (GDD v0.2 7.2).</summary>
    public class SaleEngineTests
    {
        private static NegotiationRules Rules()
        {
            return MarketHarness.RealContent().Negotiation;
        }

        private static SaleSetup Setup(double max = 10000, long opening = 8000, int patience = 5, int trust = 50, double urgency = 0.2, int day = 10)
        {
            return new SaleSetup(Money.FromTl(opening), max, patience, trust, urgency, day);
        }

        private static SaleRound Ask(SaleEngine e, SaleState s, long tl)
        {
            Result<SaleRound> r = e.Ask(s, Money.FromTl(tl));
            Assert.IsTrue(r.IsSuccess, r.ErrorCode);
            return r.Value;
        }

        // ---------- golden ----------

        private static Dictionary<string, object> Root()
        {
            return (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(Path.Combine(TestPaths.FixturesDirectory(), "sale_golden.json")));
        }

        public static IEnumerable<string> ScenarioNames()
        {
            foreach (object s in (List<object>)Root()["scenarios"])
            {
                yield return (string)((Dictionary<string, object>)s)["name"];
            }
        }

        private static Dictionary<string, object> Scenario(string name)
        {
            foreach (object s in (List<object>)Root()["scenarios"])
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
        public void Golden_EveryStepMatchesTheReferenceModelExactly(string name)
        {
            Dictionary<string, object> sc = Scenario(name);
            var su = (Dictionary<string, object>)sc["setup"];
            var setup = new SaleSetup(
                Money.FromTl((long)(double)su["opening"]),
                (double)su["M"],
                (int)(double)su["patience"],
                (int)(double)su["trust"],
                (double)su["urg"],
                su.ContainsKey("day") ? (int)(double)su["day"] : 10);
            var engine = new SaleEngine(Rules());
            SaleState state = engine.Begin(setup);

            int n = 0;
            foreach (object so in (List<object>)sc["steps"])
            {
                var st = (Dictionary<string, object>)so;
                n++;
                string at = name + " step " + n;
                if ((string)st["kind"] == "report")
                {
                    Result r = engine.ApplyReport(state, (double)st["newMax"], (int)(double)st["gain"]);
                    Assert.IsTrue(r.IsSuccess, at);
                }
                else
                {
                    SaleRound round = Ask(engine, state, (long)(double)st["ask"]);
                    Assert.AreEqual((bool)st["insult"], round.TooExpensive, at + " too expensive");
                    if (st["deal"] != null)
                    {
                        Assert.AreEqual(Money.FromTl((long)(double)st["deal"]), state.DealPrice, at + " deal");
                        Assert.AreEqual(state.DealPrice, round.DealPrice, at + " round deal");
                    }
                }

                Assert.AreEqual(Money.FromTl((long)(double)st["price"]), state.ShownPrice, at + " price");
                Assert.AreEqual((int)(double)st["trust"], state.Trust, at + " trust");
                Assert.AreEqual((int)(double)st["patience"], state.Patience, at + " patience");
                Assert.AreEqual((double)st["max"], state.Max, 1e-9, at + " max");
                string phase = (string)st["phase"];
                NegotiationPhase expected = phase == "active" ? NegotiationPhase.Active : phase == "final" ? NegotiationPhase.FinalOffer : NegotiationPhase.Deal;
                Assert.AreEqual(expected, state.Phase, at + " phase");
            }
        }

        [TestCase("hatice_sale")]
        [TestCase("berk_sale")]
        public void Golden_MatchesTheDocumentScenariosWithinTenTl(string name)
        {
            Dictionary<string, object> sc = Scenario(name);
            var docs = (List<object>)sc["docPrices"];
            var steps = (List<object>)sc["steps"];
            Assert.AreEqual(steps.Count, docs.Count);
            for (int i = 0; i < docs.Count; i++)
            {
                double golden = (double)((Dictionary<string, object>)steps[i])["price"];
                Assert.AreEqual((double)docs[i], golden, 10.0, name + " round " + (i + 1));
            }
        }

        // ---------- kurallar ----------

        [Test]
        public void Begin_StartsActiveAtTheOpeningOffer()
        {
            SaleState s = new SaleEngine(Rules()).Begin(Setup());

            Assert.AreEqual(NegotiationPhase.Active, s.Phase);
            Assert.AreEqual(Money.FromTl(8000), s.ShownPrice);
            Assert.AreEqual(10000.0, s.Max, 0.0);
            Assert.AreEqual(5, s.Patience);
            Assert.AreEqual(50, s.Trust);
            Assert.AreEqual(0, s.Round);
            Assert.IsFalse(s.ReportShown);
            Assert.IsTrue(s.DealPrice.IsZero);
        }

        [Test]
        public void AnAskAtOrBelowTheCustomersNewOffer_IsADealAtTheCustomersOffer()
        {
            var e = new SaleEngine(Rules());
            SaleState below = e.Begin(Setup());
            SaleState at = e.Begin(Setup());

            SaleRound a = Ask(e, below, 7000);
            SaleRound b = Ask(e, at, 8760); // ilk tur teklifi tam 8.760 (golden)

            Assert.AreEqual(Money.FromTl(8760), a.DealPrice);
            Assert.AreEqual(Money.FromTl(8760), b.DealPrice);
            Assert.AreEqual(5, at.Patience);
        }

        [Test]
        public void AnAskOneStepAboveTheNewOffer_IsNotADeal()
        {
            var e = new SaleEngine(Rules());
            SaleState s = e.Begin(Setup());

            SaleRound r = Ask(e, s, 8770);

            Assert.AreEqual(NegotiationPhase.Active, r.Phase);
        }

        [Test]
        public void TooExpensive_IsDetectedExactlyAboveOneFifteenOfMax()
        {
            var e = new SaleEngine(Rules());

            Assert.IsFalse(Ask(e, e.Begin(Setup()), 11500).TooExpensive);
            Assert.IsTrue(Ask(e, e.Begin(Setup()), 11510).TooExpensive);
        }

        [Test]
        public void TooExpensive_PenaltyStartsOnTheConfiguredDay()
        {
            var e = new SaleEngine(Rules());
            SaleState day4 = e.Begin(Setup(day: 4));
            SaleState day5 = e.Begin(Setup(day: 5));

            Ask(e, day4, 11600);
            Ask(e, day5, 11600);

            Assert.AreEqual(50, day4.Trust);
            Assert.AreEqual(4, day4.Patience);
            Assert.AreEqual(35, day5.Trust);
            Assert.AreEqual(3, day5.Patience, "sabır toplam −2: ek −1 ve tur −1");
        }

        [Test]
        public void AskInsideTheNearBand_GainsTrust_ButJustOutsideDoesNot()
        {
            var e = new SaleEngine(Rules());
            SaleState near = e.Begin(Setup(max: 9700));
            SaleState far = e.Begin(Setup(max: 9700));

            Ask(e, near, 10000); // 9700 / 0,97 = 10000
            Ask(e, far, 10010);

            Assert.AreEqual(55, near.Trust);
            Assert.AreEqual(50, far.Trust);
        }

        [Test]
        public void PatienceZero_MakesTheCustomersOfferFinal_AndOnlyAcceptOrLeaveRemain()
        {
            var e = new SaleEngine(Rules());
            SaleState s = e.Begin(Setup(patience: 1));

            SaleRound r = Ask(e, s, 10500);

            Assert.AreEqual(NegotiationPhase.FinalOffer, r.Phase);
            Assert.AreEqual("negotiation.final_offer_only", e.Ask(s, Money.FromTl(8760)).ErrorCode);
            Assert.IsTrue(e.AcceptFinal(s).IsSuccess);
            Assert.AreEqual(NegotiationPhase.Deal, s.Phase);
            Assert.AreEqual(Money.FromTl(8760), s.DealPrice);
        }

        [Test]
        public void AcceptFinal_IsRefusedWhileActive_AndLeaveEndsTheNegotiation()
        {
            var e = new SaleEngine(Rules());
            SaleState s = e.Begin(Setup());

            Assert.AreEqual("negotiation.no_final_offer", e.AcceptFinal(s).ErrorCode);
            Assert.IsTrue(e.Leave(s).IsSuccess);
            Assert.AreEqual(NegotiationPhase.Failed, s.Phase);
            Assert.AreEqual("negotiation.closed", e.Ask(s, Money.FromTl(9000)).ErrorCode);
            Assert.AreEqual("negotiation.closed", e.AcceptFinal(s).ErrorCode);
            Assert.AreEqual("negotiation.closed", e.Leave(s).ErrorCode);
            Assert.AreEqual("negotiation.closed", e.ApplyReport(s, 9000, 10).ErrorCode);
        }

        [TestCase(0L)]
        [TestCase(-10L)]
        [TestCase(9005L)]
        public void InvalidAsks_AreRefused_WithoutChangingState(long tl)
        {
            var e = new SaleEngine(Rules());
            SaleState s = e.Begin(Setup());

            Result<SaleRound> r = e.Ask(s, Money.FromTl(tl));

            Assert.AreEqual("ask.invalid", r.ErrorCode);
            Assert.AreEqual(0, s.Round);
            Assert.AreEqual(5, s.Patience);
            Assert.AreEqual(Money.FromTl(8000), s.ShownPrice);
        }

        [Test]
        public void Report_CanBeShownOnlyOnce_AndRaisesTrustCappedAtHundred()
        {
            var e = new SaleEngine(Rules());
            SaleState s = e.Begin(Setup(trust: 95));

            Assert.IsTrue(e.ApplyReport(s, 10000, 10).IsSuccess);
            Assert.AreEqual(100, s.Trust);
            Assert.IsTrue(s.ReportShown);
            Assert.AreEqual("report.already_shown", e.ApplyReport(s, 10000, 10).ErrorCode);
            Assert.AreEqual(100, s.Trust);
        }

        [Test]
        public void Report_LoweringMax_ClampsTheStandingOffer()
        {
            var e = new SaleEngine(Rules());
            SaleState s = e.Begin(Setup(max: 10000, opening: 8000));

            e.ApplyReport(s, 7000, 0);

            Assert.AreEqual(7000.0, s.Max, 0.0);
            Assert.AreEqual(Money.FromTl(7000), s.ShownPrice, "teklif yeni Max'ı aşamaz");
        }

        [Test]
        public void Fuzz_Invariants_HoldOverThousandsOfRandomSales()
        {
            var rnd = new Random(777);
            var e = new SaleEngine(Rules());
            for (int n = 0; n < 2000; n++)
            {
                double max = 3000 + rnd.NextDouble() * 40000;
                long opening = Money.RoundTo10((long)(max * (0.75 + rnd.NextDouble() * 0.2)));
                SaleState s = e.Begin(new SaleSetup(Money.FromTl(opening), max, 1 + rnd.Next(6), rnd.Next(101), rnd.NextDouble(), 1 + rnd.Next(12)));
                Money lastShown = s.ShownPrice;
                for (int step = 0; step < 12 && s.Phase == NegotiationPhase.Active; step++)
                {
                    if (step == 3 && !s.ReportShown)
                    {
                        e.ApplyReport(s, max * (0.8 + rnd.NextDouble() * 0.4), rnd.Next(2) == 0 ? 10 : 15);
                        lastShown = s.ShownPrice;
                    }

                    long ask = Money.RoundTo10((long)(max * (0.7 + rnd.NextDouble() * 0.6)));
                    SaleRound r = Ask(e, s, ask);
                    Assert.LessOrEqual((double)r.ShownPrice.Tl, s.Max + 1e-9, "I4: müşteri Max'ını aşmaz");
                    Assert.GreaterOrEqual(r.ShownPrice.Tl, lastShown.Tl, "müşteri teklifi düşmez");
                    lastShown = r.ShownPrice;
                    Assert.IsTrue(r.ShownPrice.IsRoundedTo10);
                    Assert.IsTrue(s.Trust >= 0 && s.Trust <= 100);
                    Assert.IsTrue(s.Patience >= 0);
                    if (s.Phase == NegotiationPhase.Deal)
                    {
                        Assert.GreaterOrEqual(s.DealPrice.Tl, ask, "anlaşma fiyatı istenen fiyattan düşük değil");
                    }
                }
            }
        }
    }
}
