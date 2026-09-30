using System;
using System.Collections.Generic;
using Esnaf.Core;
using Esnaf.Domain.Business;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Game;
using Esnaf.Domain.Market;
using Esnaf.Domain.Time;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Time
{
    public class DayHooksAndStepsTests
    {
        private sealed class RecordingHook : INewDayHook
        {
            public readonly List<string> Log;
            private readonly string _name;
            private readonly TimeState _time;

            public RecordingHook(string name, List<string> log, TimeState time)
            {
                _name = name;
                Log = log;
                _time = time;
            }

            public void OnDayOpened(int day)
            {
                Log.Add(_name + ":" + day + ":time=" + _time.Day);
            }
        }

        private static NewDayStep Step(MarketHarness h, TimeState time, MarketState market, EventBus bus, params INewDayHook[] hooks)
        {
            return new NewDayStep(time, market, h.Generator, h.Rng, bus, hooks);
        }

        [Test]
        public void Hooks_RunOnTheFirstDay_AndAfterEveryNewDay_InOrder_AfterTheDayAdvanced()
        {
            var h = new MarketHarness(5UL);
            var time = new TimeState(1, 5UL);
            var log = new List<string>();
            NewDayStep step = Step(h, time, new MarketState(), new EventBus(), new RecordingHook("a", log, time), new RecordingHook("b", log, time));

            Assert.IsTrue(step.OpenFirstDay().IsSuccess);
            Assert.IsTrue(step.Execute(new DayEndContext(1)).IsSuccess);

            CollectionAssert.AreEqual(new[] { "a:1:time=1", "b:1:time=1", "a:2:time=2", "b:2:time=2" }, log);
        }

        [Test]
        public void Hooks_AreOptional()
        {
            var h = new MarketHarness(5UL);
            var time = new TimeState(1, 5UL);
            var step = new NewDayStep(time, new MarketState(), h.Generator, h.Rng, new EventBus());

            Assert.IsTrue(step.OpenFirstDay().IsSuccess);
            Assert.IsTrue(step.Execute(new DayEndContext(1)).IsSuccess);
            Assert.AreEqual(2, time.Day);
        }

        [Test]
        public void Hooks_DoNotRunWhenTheDayMismatchFails()
        {
            var h = new MarketHarness(5UL);
            var time = new TimeState(3, 5UL);
            var log = new List<string>();
            NewDayStep step = Step(h, time, new MarketState(), new EventBus(), new RecordingHook("a", log, time));

            Assert.AreEqual("time.day_mismatch", step.Execute(new DayEndContext(1)).ErrorCode);

            Assert.AreEqual(0, log.Count);
        }

        [Test]
        public void MissedCustomersStep_HasTheFixedIdentity_AndWritesTheCountIntoTheContext()
        {
            GameSession s = GameSession.NewGame(MarketHarness.RealContent(), 42UL);
            var step = new MissedCustomersStep(s.Customers);
            var context = new DayEndContext(1);

            Assert.AreEqual("missed_customers", step.Id);
            Assert.AreEqual(1, step.Order);
            Assert.IsTrue(step.Execute(context).IsSuccess);

            Assert.AreEqual(s.Customers.State.Arrived, context.MissedCustomers);
            Assert.Throws<ArgumentNullException>(() => step.Execute(null));
            Assert.Throws<ArgumentNullException>(() => new MissedCustomersStep(null));
        }

        [Test]
        public void DemandUpdateStep_HasTheFixedIdentity_AndUpdatesForTheNextDay()
        {
            GameSession s = GameSession.NewGame(MarketHarness.RealContent(), 42UL);
            var step = new DemandUpdateStep(s.Demand, s.Rng, s.Content);

            Assert.AreEqual("demand_update", step.Id);
            Assert.AreEqual(5, step.Order);
            Assert.IsTrue(step.Execute(new DayEndContext(3)).IsSuccess);
            Assert.IsTrue(System.Linq.Enumerable.All(s.Content.Products, p => s.Demand.Index(p.Id) == 1.0), "yeni gün 4: canlı değil");

            Assert.IsTrue(step.Execute(new DayEndContext(4)).IsSuccess);

            Assert.IsTrue(System.Linq.Enumerable.Any(s.Content.Products, p => s.Demand.Index(p.Id) != 1.0), "yeni gün 5: canlı");
            Assert.Throws<ArgumentNullException>(() => step.Execute(null));
            Assert.Throws<ArgumentNullException>(() => new DemandUpdateStep(null, s.Rng, s.Content));
            Assert.Throws<ArgumentNullException>(() => new DemandUpdateStep(s.Demand, null, s.Content));
            Assert.Throws<ArgumentNullException>(() => new DemandUpdateStep(s.Demand, s.Rng, null));
        }

        [Test]
        public void DemandUpdateStep_UsesItsOwnRandomStream()
        {
            GameSession a = GameSession.NewGame(MarketHarness.RealContent(), 42UL);
            GameSession b = GameSession.NewGame(MarketHarness.RealContent(), 42UL);
            b.Rng.Get("market").NextUInt();

            new DemandUpdateStep(a.Demand, a.Rng, a.Content).Execute(new DayEndContext(4));
            new DemandUpdateStep(b.Demand, b.Rng, b.Content).Execute(new DayEndContext(4));

            foreach (var p in a.Content.Products)
            {
                Assert.AreEqual(a.Demand.Index(p.Id), b.Demand.Index(p.Id), 0.0, p.Id);
            }
        }

        [Test]
        public void AutoSaveStep_HasTheFixedIdentity_AndAnnouncesTheCurrentDay()
        {
            var time = new TimeState(3, 1UL);
            var bus = new EventBus();
            var days = new List<int>();
            bus.Subscribe<AutoSaveRequested>(e => days.Add(e.Day));
            var step = new AutoSaveStep(time, bus);

            Assert.AreEqual("auto_save", step.Id);
            Assert.AreEqual(8, step.Order);
            Assert.IsTrue(step.Execute(new DayEndContext(2)).IsSuccess);

            CollectionAssert.AreEqual(new[] { 3 }, days);
        }

        [Test]
        public void AutoSaveStep_WorksWithoutABus_AndRejectsNulls()
        {
            var time = new TimeState(1, 1UL);

            Assert.IsTrue(new AutoSaveStep(time, null).Execute(new DayEndContext(1)).IsSuccess);
            Assert.Throws<ArgumentNullException>(() => new AutoSaveStep(null, new EventBus()));
            Assert.Throws<ArgumentNullException>(() => new AutoSaveStep(time, new EventBus()).Execute(null));
        }

        [Test]
        public void TheDayEnd_PublishesTheAutoSaveRequestLast_AfterTheNewDayStarted()
        {
            var bus = new EventBus();
            var log = new List<string>();
            bus.Subscribe<DayEnded>(e => log.Add("ended:" + e.Day));
            bus.Subscribe<DayStarted>(e => log.Add("started:" + e.Day));
            bus.Subscribe<AutoSaveRequested>(e => log.Add("autosave:" + e.Day));
            GameSession s = GameSession.NewGame(MarketHarness.RealContent(), 42UL, bus);
            log.Clear();

            s.Api.EndDay();

            CollectionAssert.AreEqual(new[] { "ended:1", "started:2", "autosave:2" }, log);
        }
    }
}
