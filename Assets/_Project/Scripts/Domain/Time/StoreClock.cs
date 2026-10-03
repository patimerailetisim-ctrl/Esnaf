using System;
using Esnaf.Core;

namespace Esnaf.Domain.Time
{
    /// <summary>
    /// Günün saati servisi (Gün 12.1). Saat <see cref="TimeState.MinuteOfDay"/>'tedir; açılış 09:00'da başlar, YALNIZCA <see cref="Advance"/> ile (oyun aksiyonlarıyla)
    /// ilerler, kapanışta (21:00) durur ve hiç geçmez. Rastgelelik ve gerçek saat yoktur: aynı aksiyon dizisi aynı saati verir. Gün sonu (yeni gün) saati 09:00'a sıfırlar.
    /// Hatalar: time.invalid (dakika ≤ 0), time.store_closed (saat zaten kapanışta).
    /// </summary>
    public sealed class StoreClock
    {
        private readonly TimeState _time;
        private readonly IEventBus _events;

        public StoreClock(TimeState time, IEventBus events)
        {
            if (time == null)
            {
                throw new ArgumentNullException(nameof(time));
            }

            _time = time;
            _events = events;
        }

        public ClockView View
        {
            get { return new ClockView(_time.Day, _time.MinuteOfDay); }
        }

        /// <summary>
        /// Bir aksiyonun süresini harcar (Gün 12.4): saati <paramref name="minutes"/> dakika ilerletir, kapanışta (21:00) kırpar; HATA DÖNDÜRMEZ ve aksiyonun sonucunu etkilemez.
        /// Saat zaten kapanıştaysa ya da dakika ≤ 0 ise 0 harcar. Gerçekten harcanan dakikayı döndürür. Kapanışa ulaşılırsa <see cref="StoreClosed"/> bir kez yayınlanır.
        /// </summary>
        public int Spend(int minutes)
        {
            if (minutes <= 0 || _time.MinuteOfDay >= StoreHours.CloseMinute)
            {
                return 0;
            }

            int advanced = _time.AdvanceClock(minutes);
            if (_time.MinuteOfDay >= StoreHours.CloseMinute && _events != null)
            {
                _events.Publish(new StoreClosed(_time.Day));
            }

            return advanced;
        }

        /// <summary>
        /// Saati <paramref name="minutes"/> dakika ilerletir; kapanışı aşacak kadarsa kapanışta durur (<see cref="ClockView.IsOpen"/> false olur ve
        /// <see cref="StoreClosed"/> bir kez yayınlanır). Gerçekten ilerleyen dakikayı döndürür.
        /// </summary>
        public Result<int> Advance(int minutes)
        {
            if (minutes <= 0)
            {
                return Result<int>.Fail("time.invalid", "The minutes to advance must be positive.");
            }

            if (_time.MinuteOfDay >= StoreHours.CloseMinute)
            {
                return Result<int>.Fail("time.store_closed", "The store is already closed for the day.");
            }

            int advanced = _time.AdvanceClock(minutes);
            if (_time.MinuteOfDay >= StoreHours.CloseMinute && _events != null)
            {
                _events.Publish(new StoreClosed(_time.Day));
            }

            return Result<int>.Ok(advanced);
        }
    }
}
