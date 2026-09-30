using System;
using Esnaf.Core;
using NUnit.Framework;

namespace Esnaf.Tests.Core
{
    public class PcgRandomTests
    {
        [Test]
        public void MatchesPcgReferenceImplementation_Seed42_Stream54()
        {
            // Resmi PCG32 demo çıktısı (pcg32_srandom(42, 54)).
            var rng = new PcgRandom(42UL, 54UL);
            uint[] expected = { 0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e };
            foreach (uint value in expected)
            {
                Assert.AreEqual(value, rng.NextUInt());
            }
        }

        [Test]
        public void Golden_Seed12345_Stream1()
        {
            var rng = new PcgRandom(12345UL, 1UL);
            uint[] expected = { 2280515124u, 875822104u, 2165132003u, 3444695176u, 1217744654u };
            foreach (uint value in expected)
            {
                Assert.AreEqual(value, rng.NextUInt());
            }
        }

        [Test]
        public void SameSeed_SameSequence_DifferentSeed_DifferentSequence()
        {
            var a = new PcgRandom(1UL, 1UL);
            var b = new PcgRandom(1UL, 1UL);
            var c = new PcgRandom(2UL, 1UL);

            bool anyDifferent = false;
            for (int i = 0; i < 20; i++)
            {
                uint x = a.NextUInt();
                Assert.AreEqual(x, b.NextUInt());
                if (x != c.NextUInt())
                {
                    anyDifferent = true;
                }
            }

            Assert.IsTrue(anyDifferent);
        }

        [Test]
        public void StateRoundTrip_ContinuesIdentically()
        {
            var rng = new PcgRandom(99UL, 7UL);
            for (int i = 0; i < 10; i++)
            {
                rng.NextUInt();
            }

            RngState saved = rng.GetState();
            uint[] expectedNext = { rng.NextUInt(), rng.NextUInt(), rng.NextUInt() };

            PcgRandom restored = PcgRandom.FromState(saved);
            foreach (uint value in expectedNext)
            {
                Assert.AreEqual(value, restored.NextUInt());
            }
        }

        [Test]
        public void SetState_RejectsEvenIncrement()
        {
            var rng = new PcgRandom(1UL, 1UL);
            Assert.Throws<ArgumentException>(() => rng.SetState(new RngState(5UL, 2UL)));
        }

        [Test]
        public void NextInt_StaysWithinBounds()
        {
            var rng = new PcgRandom(5UL, 5UL);
            for (int i = 0; i < 10000; i++)
            {
                int v = rng.NextInt(7);
                Assert.That(v, Is.InRange(0, 6));
                int w = rng.NextInt(-3, 4);
                Assert.That(w, Is.InRange(-3, 3));
            }
        }

        [Test]
        public void NextInt_RangeOfOne_AlwaysReturnsMin()
        {
            var rng = new PcgRandom(5UL, 5UL);
            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(4, rng.NextInt(4, 5));
                Assert.AreEqual(0, rng.NextInt(1));
            }
        }

        [Test]
        public void NextInt_ExtremeRange_DoesNotOverflow()
        {
            var rng = new PcgRandom(3UL, 3UL);
            for (int i = 0; i < 1000; i++)
            {
                int v = rng.NextInt(int.MinValue, int.MaxValue);
                Assert.That(v, Is.InRange(int.MinValue, int.MaxValue - 1));
            }
        }

        [Test]
        public void NextInt_InvalidArguments_Throw()
        {
            var rng = new PcgRandom(1UL, 1UL);
            Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(-5));
            Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(5, 5));
            Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(6, 5));
        }

        [Test]
        public void NextInt_IsRoughlyUniform()
        {
            var rng = new PcgRandom(2024UL, 11UL);
            var counts = new int[6];
            const int draws = 60000;
            for (int i = 0; i < draws; i++)
            {
                counts[rng.NextInt(6)]++;
            }

            // Sabit seed olduğu için sonuç her çalışmada aynı; ±%5 içinde bekleniyor (beklenen 10000).
            foreach (int count in counts)
            {
                Assert.That(count, Is.InRange(9500, 10500));
            }
        }

        [Test]
        public void NextDouble_IsInUnitInterval_AndRoughlyCentered()
        {
            var rng = new PcgRandom(8UL, 8UL);
            double sum = 0;
            const int draws = 20000;
            for (int i = 0; i < draws; i++)
            {
                double d = rng.NextDouble();
                Assert.That(d, Is.GreaterThanOrEqualTo(0.0).And.LessThan(1.0));
                sum += d;
            }

            Assert.That(sum / draws, Is.InRange(0.49, 0.51));
        }

        [Test]
        public void Chance_ConsumesSameRandomnessRegardlessOfOutcome()
        {
            var a = new PcgRandom(4UL, 4UL);
            var b = new PcgRandom(4UL, 4UL);

            a.Chance(0.0);   // asla true
            b.Chance(1.0);   // her zaman true
            Assert.AreEqual(a.NextUInt(), b.NextUInt());
        }

        [Test]
        public void Chance_Extremes()
        {
            var rng = new PcgRandom(6UL, 6UL);
            for (int i = 0; i < 1000; i++)
            {
                Assert.IsFalse(rng.Chance(0.0));
                Assert.IsFalse(rng.Chance(-1.0));
                Assert.IsTrue(rng.Chance(1.0));
                Assert.IsTrue(rng.Chance(2.0));
            }

            Assert.Throws<ArgumentException>(() => rng.Chance(double.NaN));
        }

        [Test]
        public void Chance_HalfIsRoughlyHalf()
        {
            var rng = new PcgRandom(31UL, 31UL);
            int hits = 0;
            const int draws = 20000;
            for (int i = 0; i < draws; i++)
            {
                if (rng.Chance(0.5))
                {
                    hits++;
                }
            }

            Assert.That(hits, Is.InRange(9600, 10400));
        }
    }
}
