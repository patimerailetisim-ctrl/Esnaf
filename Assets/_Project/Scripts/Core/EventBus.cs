using System;
using System.Collections.Generic;

namespace Esnaf.Core
{
    public interface IEventBus
    {
        /// <summary>Aboneliği sonlandırmak için dönen nesne Dispose edilir.</summary>
        IDisposable Subscribe<T>(Action<T> handler);

        void Publish<T>(T eventData);
    }

    /// <summary>
    /// Senkron, tipli olay veri yolu (GDD K5/K6). Yalnızca BİLDİRİM içindir; oyun doğruluğu olaylara bağlı olmamalı.
    /// - Dinleyiciler abone olma sırasıyla çağrılır.
    /// - Tip eşleşmesi tam tiptir (miras/arayüz ile dağıtım yok).
    /// - Publish sırasında eklenen dinleyici o yayında çağrılmaz; çıkan dinleyici artık çağrılmaz.
    /// - Dinleyici istisna fırlatırsa istisna Publish çağırana yayılır (sessiz yutulmaz).
    /// </summary>
    public sealed class EventBus : IEventBus
    {
        private abstract class Subscription
        {
            public bool Active = true;
        }

        private sealed class Subscription<T> : Subscription, IDisposable
        {
            private readonly EventBus _owner;
            public readonly Action<T> Handler;

            public Subscription(EventBus owner, Action<T> handler)
            {
                _owner = owner;
                Handler = handler;
            }

            public void Dispose()
            {
                if (!Active)
                {
                    return;
                }

                Active = false;
                _owner.Remove(typeof(T), this);
            }
        }

        private readonly Dictionary<Type, List<Subscription>> _subscriptions = new Dictionary<Type, List<Subscription>>();

        public IDisposable Subscribe<T>(Action<T> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            List<Subscription> list;
            if (!_subscriptions.TryGetValue(typeof(T), out list))
            {
                list = new List<Subscription>();
                _subscriptions.Add(typeof(T), list);
            }

            var subscription = new Subscription<T>(this, handler);
            list.Add(subscription);
            return subscription;
        }

        public void Publish<T>(T eventData)
        {
            List<Subscription> list;
            if (!_subscriptions.TryGetValue(typeof(T), out list) || list.Count == 0)
            {
                return;
            }

            Subscription[] snapshot = list.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                var subscription = (Subscription<T>)snapshot[i];
                if (subscription.Active)
                {
                    subscription.Handler(eventData);
                }
            }
        }

        public int SubscriberCount<T>()
        {
            List<Subscription> list;
            return _subscriptions.TryGetValue(typeof(T), out list) ? list.Count : 0;
        }

        private void Remove(Type type, Subscription subscription)
        {
            List<Subscription> list;
            if (_subscriptions.TryGetValue(type, out list))
            {
                list.Remove(subscription);
            }
        }
    }
}
