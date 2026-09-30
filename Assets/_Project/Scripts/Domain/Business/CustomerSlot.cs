namespace Esnaf.Domain.Business
{
    /// <summary>
    /// Günlük müşteri havuzunun bir yuvası (kayda girer). Müşterinin kim olduğu ve üç rastgele çekimi sabittir; değer hatası
    /// ve ilgilendiği ürün bu çekimlerden TÜRETİLİR, böylece aynı durum her zaman aynı sonucu verir. Çekimler gizlidir.
    /// </summary>
    public sealed class CustomerSlot
    {
        public long CustomerId { get; }
        public string NpcId { get; }

        /// <summary>Değer hatası çekimi [0,1): V_müşteri = V × (1 + σ × (2u − 1)).</summary>
        public double ValueDraw { get; }

        /// <summary>Başlangıç güveni çekimi [0,1): güven = 50 ± 10.</summary>
        public double TrustDraw { get; }

        /// <summary>Uygun ürünler arasından seçim çekimi [0,1).</summary>
        public double PickDraw { get; }

        public CustomerStatus Status { get; internal set; }

        public CustomerSlot(long customerId, string npcId, double valueDraw, double trustDraw, double pickDraw)
        {
            CustomerId = customerId;
            NpcId = npcId;
            ValueDraw = valueDraw;
            TrustDraw = trustDraw;
            PickDraw = pickDraw;
            Status = CustomerStatus.Waiting;
        }
    }
}
