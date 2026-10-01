using Esnaf.Domain.Npc;

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

        /// <summary>Kişilik profili (arketip + düzeyler + başlangıç ruh hali); kişilik kataloğu yoksa null. Max/güven sayısı içermez.</summary>
        public CustomerProfile Profile { get; }

        public CustomerView(long customerId, string npcId, long instanceId, CustomerProfile profile = null)
        {
            CustomerId = customerId;
            NpcId = npcId;
            InstanceId = instanceId;
            Profile = profile;
        }
    }
}
