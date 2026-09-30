using System;
using System.Collections.Generic;
using Esnaf.Core;
using NUnit.Framework;

namespace Esnaf.Tests.Core
{
    public class RngStreamsTests
    {
        [Test]
        public void NamedStreams_MatchGoldenValues()
        {
            var streams = new RngStreams(777UL);

            IRandom market = streams.Get("market");
            Assert.AreEqual(2424954194u, market.NextUInt());
            Assert.AreEqual(2722055511u, market.NextUInt());
            Assert.AreEqual(1257190737u, market.NextUInt());

            IRandom npc = streams.Get("npc");
            Assert.AreEqual(3998791794u, npc.NextUInt());
            Assert.AreEqual(3331412551u, npc.NextUInt());
            Assert.AreEqual(2258271014u, npc.NextUInt());
        }

        [Test]
        public void Get_SameName_ReturnsSameStream()
        {
            var streams = new RngStreams(1UL);
            Assert.AreSame(streams.Get("market"), streams.Get("market"));
        }

        [Test]
        public void Streams_AreIndependent_ExtraDrawsDoNotShiftOthers()
        {
            var withExtraDraws = new RngStreams(42UL);
            var without = new RngStreams(42UL);

            IRandom marketA = withExtraDraws.Get("market");
            for (int i = 0; i < 100; i++)
            {
                marketA.NextUInt();
            }

            Assert.AreEqual(without.Get("npc").NextUInt(), withExtraDraws.Get("npc").NextUInt());
            Assert.AreEqual(without.Get("appraisal").NextUInt(), withExtraDraws.Get("appraisal").NextUInt());
        }

        [Test]
        public void DifferentMasterSeeds_GiveDifferentStreams()
        {
            Assert.AreNotEqual(new RngStreams(1UL).Get("market").NextUInt(), new RngStreams(2UL).Get("market").NextUInt());
        }

        [Test]
        public void Get_RequiresName()
        {
            var streams = new RngStreams(1UL);
            Assert.Throws<ArgumentException>(() => streams.Get(null));
            Assert.Throws<ArgumentException>(() => streams.Get(""));
        }

        [Test]
        public void CaptureRestore_ContinuesIdentically()
        {
            var original = new RngStreams(2026UL);
            original.Get("market").NextUInt();
            original.Get("market").NextUInt();
            original.Get("npc").NextUInt();

            Dictionary<string, RngState> snapshot = original.Capture();

            uint expectedMarket = original.Get("market").NextUInt();
            uint expectedNpc = original.Get("npc").NextUInt();

            var loaded = new RngStreams(2026UL);
            loaded.Restore(snapshot);
            Assert.AreEqual(expectedMarket, loaded.Get("market").NextUInt());
            Assert.AreEqual(expectedNpc, loaded.Get("npc").NextUInt());
        }

        [Test]
        public void Restore_StreamNotInSnapshot_StartsFreshFromMasterSeed()
        {
            var original = new RngStreams(9UL);
            original.Get("market").NextUInt();
            Dictionary<string, RngState> snapshot = original.Capture();

            var loaded = new RngStreams(9UL);
            loaded.Restore(snapshot);

            // "customer" kayıtta yoktu: taze akışın ilk değeriyle aynı olmalı.
            Assert.AreEqual(new RngStreams(9UL).Get("customer").NextUInt(), loaded.Get("customer").NextUInt());
        }

        [Test]
        public void Restore_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new RngStreams(1UL).Restore(null));
        }

        [Test]
        public void Fnv1a64_IsStableAcrossPlatforms()
        {
            // Bilinen FNV-1a 64 değerleri ("market", "npc") — Python ile bağımsız doğrulandı.
            Assert.AreEqual(0xeddcb52b15486b11UL, RngStreams.Fnv1a64("market"));
            Assert.AreEqual(0x20f8d119253bcc70UL, RngStreams.Fnv1a64("npc"));
        }
    }
}
