using System;
using System.Collections.Generic;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Economy;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Economy
{
    public class DemandModelTests
    {
        private static DemandConstants C(double noise = 0.02, double reversion = 0.20, double min = 0.90, double max = 1.10, double perSale = 0.985, int window = 5, double floor = 0.90, int live = 5)
        {
            return new DemandConstants(live, noise, reversion, min, max, perSale, window, floor);
        }

        private static DemandModel Model(DemandConstants c = null, DemandState state = null)
        {
            return new DemandModel(c ?? ContentFixtures.Demand(), state ?? new DemandState());
        }

        private static ScriptedRandom Draws(params double[] values)
        {
            return new ScriptedRandom(new bool[0], values);
        }

        private static readonly string[] Ids = { "a", "b" };

        // ---------- indeks ----------

        [Test]
        public void EveryModelStartsAtOne()
        {
            DemandModel m = Model();

            Assert.AreEqual(1.0, m.Index("a"), 0.0);
            Assert.AreEqual(1.0, m.Multiplier("a", 1), 0.0);
        }

        [Test]
        public void BeforeTheLiveDay_TheUpdateChangesNothingAndDrawsNothing()
        {
            DemandModel m = Model();
            var rng = Draws(0.9, 0.9);

            m.UpdateForNewDay(4, rng, Ids);

            Assert.AreEqual(1.0, m.Index("a"), 0.0);
            Assert.AreEqual(2, rng.Remaining, "Gün 1–4 rastgelelik tüketilmez");
        }

        [Test]
        public void OnTheLiveDay_EachModelDrawsOnce_InOrder()
        {
            DemandModel m = Model();
            var rng = Draws(0.75, 0.25);

            m.UpdateForNewDay(5, rng, Ids);

            Assert.AreEqual(0, rng.Remaining);
            Assert.AreEqual(1.0 + (2 * 0.75 - 1) * 0.02, m.Index("a"), 1e-12);
            Assert.AreEqual(1.0 + (2 * 0.25 - 1) * 0.02, m.Index("b"), 1e-12);
        }

        [Test]
        public void TheIndexIsPulledTowardsOne()
        {
            var state = new DemandState();
            DemandModel m = Model(C(noise: 0.0), state);
            m.UpdateForNewDay(5, Draws(0.5, 0.5), Ids);
            state.SetIndex("a", 1.08);

            m.UpdateForNewDay(6, Draws(0.5, 0.5), Ids);

            Assert.AreEqual(1.08 + 0.20 * (1.0 - 1.08), m.Index("a"), 1e-12);
        }

        [Test]
        public void TheIndexIsClampedToTheConfiguredBand()
        {
            DemandModel high = Model(C(noise: 0.4, reversion: 0.0, max: 1.10));
            DemandModel low = Model(C(noise: 0.4, reversion: 0.0, min: 0.90));

            high.UpdateForNewDay(5, Draws(0.999, 0.999), Ids);
            low.UpdateForNewDay(5, Draws(0.001, 0.001), Ids);

            Assert.AreEqual(1.10, high.Index("a"), 0.0);
            Assert.AreEqual(0.90, low.Index("a"), 0.0);
        }

        [Test]
        public void ThirtyDaysOfUpdates_StayInsideTheBand()
        {
            DemandModel m = Model();
            var rng = new RngStreams(3UL).Get("demand");
            for (int day = 5; day < 40; day++)
            {
                m.UpdateForNewDay(day, rng, Ids);
                Assert.That(m.Index("a"), Is.InRange(0.90, 1.10));
                Assert.That(m.Index("b"), Is.InRange(0.90, 1.10));
            }
        }

        // ---------- satış baskısı ----------

        [Test]
        public void SellingTheSameModelThreeTimes_MultipliesDemandByPointNineEightFiveCubed()
        {
            DemandModel m = Model();

            m.RecordSale("a", 3);
            m.RecordSale("a", 3);
            m.RecordSale("a", 4);

            Assert.AreEqual(Math.Pow(0.985, 3), m.Multiplier("a", 4), 1e-12);
            Assert.AreEqual(0.955, m.Multiplier("a", 4), 0.001);
            Assert.AreEqual(1.0, m.Multiplier("b", 4), 0.0, "başka model etkilenmez");
        }

        [Test]
        public void SalesFallOutOfTheWindowAfterFiveDays()
        {
            DemandModel m = Model();
            m.RecordSale("a", 1);

            Assert.AreEqual(0.985, m.Multiplier("a", 5), 1e-12, "Gün 1 satışı Gün 5'te hâlâ sayılır");
            Assert.AreEqual(1.0, m.Multiplier("a", 6), 0.0, "Gün 6'da penceredan düşer");
        }

        [Test]
        public void PressureNeverDropsBelowTheFloor()
        {
            DemandModel m = Model();
            for (int i = 0; i < 40; i++)
            {
                m.RecordSale("a", 2);
            }

            Assert.AreEqual(0.90, m.Multiplier("a", 2), 0.0);
        }

        [Test]
        public void PressureAndIndexMultiply()
        {
            var state = new DemandState();
            DemandModel m = Model(C(noise: 0.0, reversion: 0.0), state);
            m.UpdateForNewDay(5, Draws(0.5, 0.5), Ids);
            state.SetIndex("a", 1.05);
            m.RecordSale("a", 5);

            Assert.AreEqual(1.05 * 0.985, m.Multiplier("a", 5), 1e-12);
        }

        [Test]
        public void TheUpdatePrunesSalesThatCanNeverCountAgain()
        {
            var state = new DemandState();
            DemandModel m = Model(null, state);
            m.RecordSale("a", 1);
            m.RecordSale("a", 6);

            m.UpdateForNewDay(7, Draws(0.5, 0.5), Ids);

            Assert.AreEqual(1, state.Sales.Count);
            Assert.AreEqual(6, state.Sales[0].Day);
        }

        [Test]
        public void SalesAreCountedPerModelId()
        {
            var state = new DemandState();
            DemandModel m = Model(null, state);

            m.RecordSale("a", 2);
            m.RecordSale("b", 2);
            m.RecordSale("a", 3);

            Assert.AreEqual(3, state.Sales.Count);
            Assert.AreEqual("a", state.Sales[0].ModelId);
            Assert.AreEqual(2, state.Sales.Count(s => s.ModelId == "a"));
        }

        [Test]
        public void NullArguments_AreProgrammerErrors()
        {
            Assert.Throws<ArgumentNullException>(() => new DemandModel(null, new DemandState()));
            Assert.Throws<ArgumentNullException>(() => new DemandModel(ContentFixtures.Demand(), null));
            DemandModel m = Model();
            Assert.Throws<ArgumentNullException>(() => m.UpdateForNewDay(5, null, Ids));
            Assert.Throws<ArgumentNullException>(() => m.UpdateForNewDay(5, Draws(0.5), null));
        }
    }
}
