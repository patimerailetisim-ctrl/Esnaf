namespace Esnaf.Domain.Business
{
    /// <summary>
    /// UI'ya giden, DEĞİŞMEZ müşteri görünümü: kim geldi ve hangi ürünle ilgileniyor. Müşterinin Max'ı, güveni, sabrı ve çekimleri
    /// bu tipe ASLA eklenmez (bir test üyeleri denetler).
    /// </summary>
    public sealed class CustomerView
    {
        public long CustomerId { get; }
        public string NpcId { get; }
        public long InstanceId { get; }

        public CustomerView(long customerId, string npcId, long instanceId)
        {
            CustomerId = customerId;
            NpcId = npcId;
            InstanceId = instanceId;
        }
    }
}
