using System;
using System.Collections.Generic;
using Esnaf.Core;
using NUnit.Framework;

namespace Esnaf.Tests.Core
{
    public class EventBusTests
    {
        private struct Ping
        {
            public int Value;

            public Ping(int value)
            {
                Value = value;
            }
        }

        private struct Pong
        {
        }

        [Test]
        public void Publish_CallsSubscribersInSubscriptionOrder()
        {
            var bus = new EventBus();
            var log = new List<string>();
            bus.Subscribe<Ping>(_ => log.Add("A"));
            bus.Subscribe<Ping>(_ => log.Add("B"));
            bus.Subscribe<Ping>(_ => log.Add("C"));

            bus.Publish(new Ping(1));

            Assert.AreEqual(new[] { "A", "B", "C" }, log);
        }

        [Test]
        public void Publish_PassesEventData()
        {
            var bus = new EventBus();
            int received = 0;
            bus.Subscribe<Ping>(p => received = p.Value);

            bus.Publish(new Ping(42));

            Assert.AreEqual(42, received);
        }

        [Test]
        public void Publish_OnlyReachesMatchingEventType()
        {
            var bus = new EventBus();
            int pings = 0;
            int pongs = 0;
            bus.Subscribe<Ping>(_ => pings++);
            bus.Subscribe<Pong>(_ => pongs++);

            bus.Publish(new Ping(1));

            Assert.AreEqual(1, pings);
            Assert.AreEqual(0, pongs);
        }

        [Test]
        public void Publish_WithNoSubscribers_DoesNothing()
        {
            var bus = new EventBus();
            Assert.DoesNotThrow(() => bus.Publish(new Ping(1)));
        }

        [Test]
        public void Dispose_StopsDelivery_AndIsIdempotent()
        {
            var bus = new EventBus();
            int calls = 0;
            IDisposable subscription = bus.Subscribe<Ping>(_ => calls++);

            bus.Publish(new Ping(1));
            subscription.Dispose();
            subscription.Dispose();
            bus.Publish(new Ping(2));

            Assert.AreEqual(1, calls);
            Assert.AreEqual(0, bus.SubscriberCount<Ping>());
        }

        [Test]
        public void Unsubscribe_DuringPublish_PreventsLaterHandlerInSamePublish()
        {
            var bus = new EventBus();
            var log = new List<string>();
            IDisposable second = null;

            bus.Subscribe<Ping>(_ =>
            {
                log.Add("first");
                second.Dispose();
            });
            second = bus.Subscribe<Ping>(_ => log.Add("second"));

            bus.Publish(new Ping(1));

            Assert.AreEqual(new[] { "first" }, log);
        }

        [Test]
        public void Subscribe_DuringPublish_DoesNotFireInCurrentPublish()
        {
            var bus = new EventBus();
            var log = new List<string>();

            bus.Subscribe<Ping>(_ =>
            {
                log.Add("first");
                bus.Subscribe<Ping>(__ => log.Add("late"));
            });

            bus.Publish(new Ping(1));
            Assert.AreEqual(new[] { "first" }, log);

            log.Clear();
            bus.Publish(new Ping(2));
            Assert.AreEqual(new[] { "first", "late" }, log);
        }

        [Test]
        public void NestedPublish_IsDepthFirstAndSynchronous()
        {
            var bus = new EventBus();
            var log = new List<string>();

            bus.Subscribe<Ping>(_ =>
            {
                log.Add("ping-start");
                bus.Publish(new Pong());
                log.Add("ping-end");
            });
            bus.Subscribe<Pong>(_ => log.Add("pong"));

            bus.Publish(new Ping(1));

            Assert.AreEqual(new[] { "ping-start", "pong", "ping-end" }, log);
        }

        [Test]
        public void HandlerException_PropagatesToPublisher()
        {
            var bus = new EventBus();
            bus.Subscribe<Ping>(_ => throw new InvalidOperationException("boom"));

            Assert.Throws<InvalidOperationException>(() => bus.Publish(new Ping(1)));
        }

        [Test]
        public void Subscribe_NullHandler_Throws()
        {
            var bus = new EventBus();
            Assert.Throws<ArgumentNullException>(() => bus.Subscribe<Ping>(null));
        }

        [Test]
        public void SameHandlerSubscribedTwice_IsCalledTwice_AndRemovedIndependently()
        {
            var bus = new EventBus();
            int calls = 0;
            Action<Ping> handler = _ => calls++;
            IDisposable first = bus.Subscribe(handler);
            bus.Subscribe(handler);

            bus.Publish(new Ping(1));
            Assert.AreEqual(2, calls);

            first.Dispose();
            bus.Publish(new Ping(2));
            Assert.AreEqual(3, calls);
        }
    }
}
