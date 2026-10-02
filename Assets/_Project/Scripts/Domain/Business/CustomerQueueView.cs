using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Esnaf.Domain.Business
{
    /// <summary>Kuyruktaki bir müşterinin bugünkü durumu.</summary>
    public enum QueueStatus
    {
        /// <summary>Tamamlandı ya da gün kapanınca gönderildi.</summary>
        Done = 0,

        /// <summary>Şu an mağazada aktif müşteri (aynı anda en çok 1).</summary>
        Active = 1,

        /// <summary>Henüz sırası gelmedi (geliş saati gelmedi ya da önünde müşteri var).</summary>
        Waiting = 2
    }

    /// <summary>Kuyruktaki bir müşteri ve bugünkü durumu.</summary>
    public sealed class QueueEntryView
    {
        public QueuedCustomer Customer { get; }
        public QueueStatus Status { get; }

        public QueueEntryView(QueuedCustomer customer, QueueStatus status)
        {
            Customer = customer;
            Status = status;
        }
    }

    /// <summary>
    /// Günlük müşteri kuyruğunun görünümü (IGameApi.GetCustomerQueue). Aynı anda yalnızca <see cref="Current"/> aktiftir. Mağaza kapalıyken yeni müşteri çağrılmaz.
    /// </summary>
    public sealed class CustomerQueueView
    {
        public int Day { get; }

        /// <summary>Bugünün müşteri sayısı (8–12).</summary>
        public int Total { get; }

        /// <summary>Tamamlanan (ya da kapanışta gönderilen) müşteri sayısı.</summary>
        public int Served { get; }

        /// <summary>Şu an aktif müşteri; yoksa null (sıradakinin saati gelmedi, kuyruk bitti ya da mağaza kapandı).</summary>
        public QueuedCustomer Current { get; }

        /// <summary>Sırası gelmemiş müşteri sayısı (aktif hariç).</summary>
        public int Waiting { get; }

        public bool StoreOpen { get; }

        /// <summary>Sıradaki müşterinin geliş saati (henüz gelmediyse); aksi halde null.</summary>
        public int? NextArrivalMinute { get; }

        public IReadOnlyList<QueueEntryView> Entries { get; }

        public CustomerQueueView(
            int day, int total, int served, QueuedCustomer current, int waiting, bool storeOpen, int? nextArrivalMinute, IEnumerable<QueueEntryView> entries)
        {
            Day = day;
            Total = total;
            Served = served;
            Current = current;
            Waiting = waiting;
            StoreOpen = storeOpen;
            NextArrivalMinute = nextArrivalMinute;
            Entries = new ReadOnlyCollection<QueueEntryView>(new List<QueueEntryView>(entries));
        }
    }
}
