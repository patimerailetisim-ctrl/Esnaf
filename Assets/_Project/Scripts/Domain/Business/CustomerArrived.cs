namespace Esnaf.Domain.Business
{
    /// <summary>Bildirim (Gün 12.6): kuyruk müşterisinin geliş saati geçti ve müşteri mağazaya girdi (rafta satılabilir ürün vardı). Durumu değiştirmez; yalnızca bilgidir.</summary>
    public sealed class CustomerArrived
    {
        public int Day { get; }
        public long CustomerId { get; }
        public string NpcId { get; }

        public CustomerArrived(int day, long customerId, string npcId)
        {
            Day = day;
            CustomerId = customerId;
            NpcId = npcId;
        }
    }
}
