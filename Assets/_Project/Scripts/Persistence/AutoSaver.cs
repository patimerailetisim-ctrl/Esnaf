using System;
using System.Collections.Generic;
using Esnaf.Core;
using Esnaf.Domain.Appraisal;
using Esnaf.Domain.Game;
using Esnaf.Domain.Negotiation;
using Esnaf.Domain.Time;

namespace Esnaf.Persistence
{
    /// <summary>
    /// Otomatik kayıt tetikleyicileri (GDD v0.3 6.4): gün sonu (adım 8 <see cref="AutoSaveRequested"/>), alım anlaşması
    /// (<see cref="ListingPurchased"/>), satım (<see cref="ItemSold"/>), ekspertiz (<see cref="AppraisalCompleted"/>).
    /// Olaylar durum değişikliğinden SONRA yayınlandığı için kayıt tutarlı durumu yazar. Kayıt hatası oyunu bozmaz: <see cref="LastResult"/>'a yazılır.
    /// </summary>
    public sealed class AutoSaver : IDisposable
    {
        private readonly GameSession _session;
        private readonly SaveService _saves;
        private readonly Func<long> _playTimeSeconds;
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private bool _saving;

        /// <summary>Son otomatik kaydın sonucu (hiç kayıt yapılmadıysa başarılı sayılır).</summary>
        public Result LastResult { get; private set; }

        /// <summary>Başarıyla yazılan otomatik kayıt sayısı.</summary>
        public int SaveCount { get; private set; }

        public AutoSaver(GameSession session, SaveService saves, Func<long> playTimeSeconds)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (saves == null)
            {
                throw new ArgumentNullException(nameof(saves));
            }

            if (playTimeSeconds == null)
            {
                throw new ArgumentNullException(nameof(playTimeSeconds));
            }

            _session = session;
            _saves = saves;
            _playTimeSeconds = playTimeSeconds;
            _subscriptions.Add(session.Bus.Subscribe<AutoSaveRequested>(e => Trigger()));
            _subscriptions.Add(session.Bus.Subscribe<ListingPurchased>(e => Trigger()));
            _subscriptions.Add(session.Bus.Subscribe<ItemSold>(e => Trigger()));
            _subscriptions.Add(session.Bus.Subscribe<AppraisalCompleted>(e => Trigger()));
        }

        private void Trigger()
        {
            if (_saving)
            {
                return;
            }

            _saving = true;
            try
            {
                LastResult = _saves.Save(_session, _playTimeSeconds());
                if (LastResult.IsSuccess)
                {
                    SaveCount++;
                }
            }
            finally
            {
                _saving = false;
            }
        }

        public void Dispose()
        {
            foreach (IDisposable subscription in _subscriptions)
            {
                subscription.Dispose();
            }

            _subscriptions.Clear();
        }
    }
}
