using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Esnaf.Domain.Business
{
    /// <summary>Bugünün müşteri havuzu ve sayaçlar (kayda girer; GDD v0.3 4.3).</summary>
    public sealed class CustomerState
    {
        private readonly List<CustomerSlot> _slots = new List<CustomerSlot>();
        private readonly ReadOnlyCollection<CustomerSlot> _view;

        public CustomerState()
        {
            _view = new ReadOnlyCollection<CustomerSlot>(_slots);
        }

        /// <summary>Bugünün yuvaları, geliş sırasıyla; ilk <see cref="Arrived"/> tanesi dükkâna gelmiştir.</summary>
        public IReadOnlyList<CustomerSlot> Slots
        {
            get { return _view; }
        }

        /// <summary>Bugün dükkâna gelen müşteri sayısı (rafın en yüksek doluluğuna göre; gün içinde azalmaz).</summary>
        public int Arrived { get; internal set; }

        /// <summary>Şimdiye kadar kaçan (satın almadan giden) müşteri toplamı.</summary>
        public int MissedTotal { get; internal set; }

        internal void Replace(IEnumerable<CustomerSlot> slots)
        {
            _slots.Clear();
            _slots.AddRange(slots);
        }
    }
}
