using System;
using Esnaf.Core;

namespace Esnaf.Domain.Time
{
    /// <summary>Gün sonu adım 8: otomatik kayıt isteği (GDD v0.3 3.4; sahibi Save). Yalnızca bildirim yayınlar; durumu değiştirmez.</summary>
    public sealed class AutoSaveStep : IDayEndStep
    {
        private readonly TimeState _time;
        private readonly IEventBus _events;

        public string Id
        {
            get { return "auto_save"; }
        }

        public int Order
        {
            get { return DayEndOrder.AutoSave; }
        }

        public AutoSaveStep(TimeState time, IEventBus events)
        {
            if (time == null)
            {
                throw new ArgumentNullException(nameof(time));
            }

            _time = time;
            _events = events;
        }

        public Result Execute(DayEndContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (_events != null)
            {
                _events.Publish(new AutoSaveRequested(_time.Day));
            }

            return Result.Ok();
        }
    }
}
