using System;
using System.Collections.Generic;
using System.Linq;
using Esnaf.Domain.Npc;
using NUnit.Framework;

namespace Esnaf.Tests.Npc
{
    public class NpcStateStoreTests
    {
        [Test]
        public void NewStore_HasNoStates()
        {
            var store = new NpcStateStore();
            NpcState state;

            Assert.AreEqual(0, store.All.Count);
            Assert.IsFalse(store.TryGet("npc.kemal", out state));
            Assert.IsNull(state);
            Assert.IsFalse(store.HasSoldToPlayer("npc.kemal", 1));
            Assert.IsFalse(store.HasBoughtFromPlayer("npc.kemal", 1));
        }

        [Test]
        public void RecordEncounter_CreatesTheState_AndCounts()
        {
            var store = new NpcStateStore();

            store.RecordEncounter("npc.kemal");
            store.RecordEncounter("npc.kemal");
            store.RecordEncounter("npc.selin");

            NpcState kemal;
            NpcState selin;
            Assert.IsTrue(store.TryGet("npc.kemal", out kemal));
            Assert.IsTrue(store.TryGet("npc.selin", out selin));
            Assert.AreEqual("npc.kemal", kemal.NpcId);
            Assert.AreEqual(2, kemal.EncounterCount);
            Assert.AreEqual(1, selin.EncounterCount);
        }

        [Test]
        public void RecordSoldToPlayer_IsRememberedPerNpcAndInstance()
        {
            var store = new NpcStateStore();

            store.RecordSoldToPlayer("npc.kemal", 7);

            Assert.IsTrue(store.HasSoldToPlayer("npc.kemal", 7));
            Assert.IsFalse(store.HasSoldToPlayer("npc.kemal", 8), "başka ürün");
            Assert.IsFalse(store.HasSoldToPlayer("npc.selin", 7), "başka NPC");
            Assert.IsFalse(store.HasBoughtFromPlayer("npc.kemal", 7), "yön karışmamalı");
        }

        [Test]
        public void RecordBoughtFromPlayer_IsRememberedPerNpcAndInstance()
        {
            var store = new NpcStateStore();

            store.RecordBoughtFromPlayer("npc.berk", 3);

            Assert.IsTrue(store.HasBoughtFromPlayer("npc.berk", 3));
            Assert.IsFalse(store.HasBoughtFromPlayer("npc.berk", 4));
            Assert.IsFalse(store.HasBoughtFromPlayer("npc.ozan", 3));
            Assert.IsFalse(store.HasSoldToPlayer("npc.berk", 3), "yön karışmamalı");
        }

        [Test]
        public void RecordingTheSameInstanceTwice_KeepsOneEntry()
        {
            var store = new NpcStateStore();

            store.RecordSoldToPlayer("npc.kemal", 7);
            store.RecordSoldToPlayer("npc.kemal", 7);
            store.RecordBoughtFromPlayer("npc.kemal", 7);
            store.RecordBoughtFromPlayer("npc.kemal", 7);

            NpcState state;
            store.TryGet("npc.kemal", out state);
            Assert.AreEqual(1, state.SoldToPlayer.Count);
            Assert.AreEqual(1, state.BoughtFromPlayer.Count);
        }

        [Test]
        public void State_ListsInstancesInAscendingOrder()
        {
            var store = new NpcStateStore();
            store.RecordSoldToPlayer("npc.kemal", 9);
            store.RecordSoldToPlayer("npc.kemal", 2);
            store.RecordSoldToPlayer("npc.kemal", 5);

            NpcState state;
            store.TryGet("npc.kemal", out state);

            CollectionAssert.AreEqual(new long[] { 2, 5, 9 }, state.SoldToPlayer.ToArray());
        }

        [Test]
        public void All_IsOrderedByNpcId()
        {
            var store = new NpcStateStore();
            store.RecordEncounter("npc.selin");
            store.RecordEncounter("npc.ayse");
            store.RecordEncounter("npc.kemal");

            CollectionAssert.AreEqual(
                new[] { "npc.ayse", "npc.kemal", "npc.selin" }, store.All.Select(s => s.NpcId).ToArray());
        }

        [Test]
        public void StateViews_CannotBeUsedToMutateTheStore()
        {
            var store = new NpcStateStore();
            store.RecordSoldToPlayer("npc.kemal", 1);
            NpcState state;
            store.TryGet("npc.kemal", out state);

            Assert.IsFalse(state.SoldToPlayer is List<long>);
            Assert.IsFalse(store.All is List<NpcState>);
        }

        [Test]
        public void InvalidArguments_AreRejected()
        {
            var store = new NpcStateStore();

            Assert.Throws<ArgumentException>(() => store.RecordEncounter(null));
            Assert.Throws<ArgumentException>(() => store.RecordEncounter(""));
            Assert.Throws<ArgumentException>(() => store.RecordSoldToPlayer(" ", 1));
            Assert.Throws<ArgumentException>(() => store.RecordBoughtFromPlayer(null, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => store.RecordSoldToPlayer("npc.kemal", 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => store.RecordBoughtFromPlayer("npc.kemal", -3));
            Assert.AreEqual(0, store.All.Count, "Geçersiz çağrı durum oluşturmamalı");
        }
    }
}
