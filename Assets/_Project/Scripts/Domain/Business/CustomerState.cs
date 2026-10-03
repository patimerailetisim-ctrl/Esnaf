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

        /// <summary>
        /// Günlük müşteri kuyruğunda (Gün 12.2) bugün tamamlanan / gönderilen müşteri sayısı. Kuyruğun kendisi (kimler, ne zaman) tohum + günden yeniden türetilir ve
        /// SAKLANMAZ; saklanan tek sayı budur. Yeni gün sıfırlar.
        /// </summary>
        public int QueueCursor { get; internal set; }

        /// <summary>
        /// Günlük kuyrukta (Gün 12.6) geliş anında rafta satılabilir ürün olmadığı için HİÇ GELMEYEN müşterilerin bit maskesi (bit i = kuyruktaki i. müşteri). Diğer her şey
        /// (geliş saati, bekleme süresi) plan + saat + imleçten türetilir. Yeni gün sıfırlar.
        /// </summary>
        public int QueueSkipped { get; internal set; }

        /// <summary>Şimdiye kadar kaçan (satın almadan giden) müşteri toplamı.</summary>
        public int MissedTotal { get; internal set; }

        internal void Replace(IEnumerable<CustomerSlot> slots)
        {
            _slots.Clear();
            _slots.AddRange(slots);
        }
    }
}
