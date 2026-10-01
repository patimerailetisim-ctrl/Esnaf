using System;
using Esnaf.Core;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Game;
using Esnaf.Domain.Time;

namespace Esnaf.Presentation
{
    /// <summary>
    /// Arayüzün TEK arka kapısı: Unity ekranları oyunla yalnızca buradan konuşur (IGameApi üzerinden). Oyun kuralı içermez;
    /// durumu okur, ekranlara hazır metin/görünüm modeli verir ve değişince <see cref="Changed"/> ile haber verir.
    /// Olay yolu (isteğe bağlı) verilirse nakit ve gün olayları üst barı kendiliğinden günceller; verilmezse <see cref="Refresh"/> çağrılır.
    /// </summary>
    public sealed class UiFlow : IDisposable
    {
        private readonly IGameApi _api;
        private readonly ContentPresentation _content;
        private readonly IDisposable _cashSubscription;
        private readonly IDisposable _daySubscription;
        private bool _disposed;

        public UiFlow(IGameApi api, ContentPresentation content, IEventBus events = null)
        {
            if (api == null)
            {
                throw new ArgumentNullException(nameof(api));
            }

            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            _api = api;
            _content = content;
            CurrentScreen = UiScreen.Listings;
            TopBar = BuildTopBar();
            if (events != null)
            {
                _cashSubscription = events.Subscribe<CashChanged>(e => Refresh());
                _daySubscription = events.Subscribe<DayStarted>(e => Refresh());
            }
        }

        /// <summary>Ekranın görünümü değişince çağrılır (ana iş parçacığında, olayı yayan çağrının içinde).</summary>
        public event Action Changed;

        public UiScreen CurrentScreen { get; private set; }

        public TopBarViewModel TopBar { get; private set; }

        public ContentPresentation Content
        {
            get { return _content; }
        }

        /// <summary>Durumu yeniden okur ve <see cref="Changed"/> olayını bir kez yayar.</summary>
        public void Refresh()
        {
            if (_disposed)
            {
                return;
            }

            TopBar = BuildTopBar();
            Action handler = Changed;
            if (handler != null)
            {
                handler();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_cashSubscription != null)
            {
                _cashSubscription.Dispose();
            }

            if (_daySubscription != null)
            {
                _daySubscription.Dispose();
            }
        }

        private TopBarViewModel BuildTopBar()
        {
            return new TopBarViewModel(TurkishTexts.Day(_api.GetDay()), TurkishTexts.Cash(_api.GetCash()));
        }
    }
}
