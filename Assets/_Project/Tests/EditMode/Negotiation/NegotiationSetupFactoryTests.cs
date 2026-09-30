using System;
using System.Collections.Generic;
using Esnaf.Core;
using Esnaf.Domain.Market;
using Esnaf.Domain.Negotiation;
using Esnaf.Domain.Npc;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Negotiation
{
    public class NegotiationSetupFactoryTests
    {
        private static NegotiationSetupFactory Factory()
        {
            return new NegotiationSetupFactory(MarketHarness.RealContent().Negotiation);
        }

        private static MarketListing Listing(long ask = 30400, long reject = 24350, bool guided = false)
        {
            return new MarketListing(
                1, 1, "npc.kemal", Money.FromTl(ask), new string[0], 10, 3, Money.FromTl(reject), Money.FromTl(25000), false, false, false, guided);
        }

        private static NpcSellerRole Kemal()
        {
            return MarketHarness.RealContent().GetNpc("npc.kemal").Seller;
        }

        private static ScriptedRandom Draws(double mood, double trust)
        {
            return new ScriptedRandom(new bool[0], new[] { mood, trust });
        }

        [Test]
        public void MiddleDraws_GiveTheListingRejectPriceAndStartTrust()
        {
            NegotiationSetup s = Factory().Create(Listing(), Kemal(), Money.FromTl(22310), 10, Draws(0.5, 0.5));

            Assert.AreEqual(24350.0, s.Reject, 1e-9);
            Assert.AreEqual(50, s.Trust);
        }

        [Test]
        public void SellerAndListingFields_AreCopiedIntoTheSetup()
        {
            NpcSellerRole kemal = Kemal();

            NegotiationSetup s = Factory().Create(Listing(), kemal, Money.FromTl(22310), 7, Draws(0.5, 0.5));

            Assert.AreEqual(Money.FromTl(30400), s.Ask);
            Assert.AreEqual(kemal.Patience, s.Patience);
            Assert.AreEqual(kemal.Urgency, s.Urgency, 0.0);
            Assert.AreEqual(kemal.Persuasion, s.Persuasion, 0.0);
            Assert.AreEqual(kemal.WrongCardPenaltyMultiplier, s.WrongCardMultiplier, 0.0);
            Assert.AreEqual(7, s.Day);
        }

        [Test]
        public void Murat_CarriesTheDoubledWrongCardPenalty()
        {
            NpcSellerRole murat = MarketHarness.RealContent().GetNpc("npc.murat").Seller;

            NegotiationSetup s = Factory().Create(Listing(), murat, Money.FromTl(22310), 10, Draws(0.5, 0.5));

            Assert.AreEqual(2.0, s.WrongCardMultiplier, 0.0);
        }

        [Test]
        public void FloorIsTheConfiguredShareOfTheTrueValue()
        {
            NegotiationSetup s = Factory().Create(Listing(), Kemal(), Money.FromTl(22310), 10, Draws(0.5, 0.5));

            Assert.AreEqual(0.65 * 22310, s.Floor, 1e-9);
        }

        [Test]
        public void MoodDraw_MovesTheRejectPriceWithinThePlusMinusThreePercentBand()
        {
            NegotiationSetup low = Factory().Create(Listing(), Kemal(), Money.FromTl(22310), 10, Draws(0.0, 0.5));
            NegotiationSetup high = Factory().Create(Listing(), Kemal(), Money.FromTl(22310), 10, Draws(0.999999, 0.5));

            Assert.AreEqual(24350 * 0.97, low.Reject, 1e-6);
            Assert.AreEqual(24350 * 1.03, high.Reject, 0.05);
        }

        [Test]
        public void TrustDraw_MovesStartTrustWithinPlusMinusTen()
        {
            NegotiationSetup low = Factory().Create(Listing(), Kemal(), Money.FromTl(22310), 10, Draws(0.5, 0.0));
            NegotiationSetup high = Factory().Create(Listing(), Kemal(), Money.FromTl(22310), 10, Draws(0.5, 0.999999));
            NegotiationSetup quarter = Factory().Create(Listing(), Kemal(), Money.FromTl(22310), 10, Draws(0.5, 0.25));

            Assert.AreEqual(40, low.Trust);
            Assert.AreEqual(60, high.Trust);
            Assert.AreEqual(45, quarter.Trust);
        }

        [Test]
        public void GuidedListing_HasNoMoodAndNoTrustRandomness_ButStillConsumesTheSameDraws()
        {
            var rng = Draws(0.0, 0.0);

            NegotiationSetup s = Factory().Create(Listing(5800, 4750, guided: true), MarketHarness.RealContent().GetNpc("npc.ayse").Seller, Money.FromTl(5280), 1, rng);

            Assert.AreEqual(4750.0, s.Reject, 0.0);
            Assert.AreEqual(50, s.Trust);
            Assert.AreEqual(0, rng.Remaining, "akış tüketimi ilan türünden bağımsız olmalı");
        }

        [Test]
        public void ExactlyTwoDoublesAreDrawn()
        {
            var rng = new ScriptedRandom(new bool[0], new[] { 0.5, 0.5, 0.123 });

            Factory().Create(Listing(), Kemal(), Money.FromTl(22310), 10, rng);

            Assert.AreEqual(1, rng.Remaining);
        }

        [Test]
        public void RejectPriceNeverExceedsTheAskingPrice()
        {
            MarketListing tight = Listing(ask: 24400, reject: 24350);

            NegotiationSetup s = Factory().Create(tight, Kemal(), Money.FromTl(22310), 10, Draws(0.999999, 0.5));

            Assert.LessOrEqual(s.Reject, 24400.0);
        }

        [Test]
        public void SameSeed_SameSetup_AndBoundsHoldOverManyDraws()
        {
            var a = new RngStreams(9UL).Get("negotiation");
            var b = new RngStreams(9UL).Get("negotiation");
            NegotiationSetupFactory f = Factory();
            for (int i = 0; i < 2000; i++)
            {
                NegotiationSetup x = f.Create(Listing(), Kemal(), Money.FromTl(22310), 10, a);
                NegotiationSetup y = f.Create(Listing(), Kemal(), Money.FromTl(22310), 10, b);
                Assert.AreEqual(x.Reject, y.Reject, 0.0);
                Assert.AreEqual(x.Trust, y.Trust);
                Assert.That(x.Reject, Is.InRange(24350 * 0.97 - 1e-6, 24350 * 1.03 + 1e-6));
                Assert.That(x.Trust, Is.InRange(40, 60));
            }
        }

        [Test]
        public void NullArguments_AreProgrammerErrors()
        {
            NegotiationSetupFactory f = Factory();
            Assert.Throws<ArgumentNullException>(() => new NegotiationSetupFactory(null));
            Assert.Throws<ArgumentNullException>(() => f.Create(null, Kemal(), Money.FromTl(1), 1, Draws(0.5, 0.5)));
            Assert.Throws<ArgumentNullException>(() => f.Create(Listing(), null, Money.FromTl(1), 1, Draws(0.5, 0.5)));
            Assert.Throws<ArgumentNullException>(() => f.Create(Listing(), Kemal(), Money.FromTl(1), 1, null));
        }
    }
}
