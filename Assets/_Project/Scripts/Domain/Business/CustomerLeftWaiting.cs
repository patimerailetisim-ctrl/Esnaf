namespace Esnaf.Domain.Business
{
    /// <summary>Bekleyen müşterinin mağazadan çıkma nedeni.</summary>
    public enum QueueLeaveReason
    {
        /// <summary>En çok 60 oyun dakikası bekledi, sıra gelmedi.</summary>
        Timeout = 0,

        /// <summary>Mağaza kapandı (21:00).</summary>
        StoreClosed = 1
    }

    /// <summary>
    /// Bildirim (Gün 12.6): sırada bekleyen müşteri satış yapmadan mağazadan çıktı (bekleme süresi doldu ya da mağaza kapandı). Satış sayılmaz, kaçan müşteri istatistiği değişmez.
    /// Arayüz bununla müşterinin doğal Türkçe çıkış sözünü gösterir; durumu değiştirmez.
    /// </summary>
    public sealed class CustomerLeftWaiting
    {
        public int Day { get; }
        public long CustomerId { get; }
        public string NpcId { get; }
        public QueueLeaveReason Reason { get; }

        public CustomerLeftWaiting(int day, long customerId, string npcId, QueueLeaveReason reason)
        {
            Day = day;
            CustomerId = customerId;
            NpcId = npcId;
            Reason = reason;
        }
    }
}
