using Esnaf.Domain.Negotiation;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Negotiation
{
    /// <summary>Ruh hali ve sabır kademelerinin eşikleri (negotiation_rules.json "view": 40 / 70, 1 / 3).</summary>
    public class NegotiationLevelsTests
    {
        private static NegotiationRules Rules()
        {
            return MarketHarness.RealContent().Negotiation;
        }

        [TestCase(0, NegotiationLevel.Low)]
        [TestCase(39, NegotiationLevel.Low)]
        [TestCase(40, NegotiationLevel.Medium)]
        [TestCase(55, NegotiationLevel.Medium)]
        [TestCase(69, NegotiationLevel.Medium)]
        [TestCase(70, NegotiationLevel.High)]
        [TestCase(100, NegotiationLevel.High)]
        public void Mood_FollowsTheTrustThresholds(int trust, NegotiationLevel expected)
        {
            Assert.AreEqual(expected, NegotiationLevels.MoodOf(Rules(), trust));
        }

        [TestCase(0, NegotiationLevel.Low)]
        [TestCase(1, NegotiationLevel.Low)]
        [TestCase(2, NegotiationLevel.Medium)]
        [TestCase(3, NegotiationLevel.Medium)]
        [TestCase(4, NegotiationLevel.High)]
        [TestCase(9, NegotiationLevel.High)]
        public void Patience_FollowsTheThresholds(int patience, NegotiationLevel expected)
        {
            Assert.AreEqual(expected, NegotiationLevels.PatienceOf(Rules(), patience));
        }
    }
}
