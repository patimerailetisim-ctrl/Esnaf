namespace Esnaf.Domain.Business
{
    /// <summary>Günlük müşteri havuzundaki bir müşterinin durumu.</summary>
    public enum CustomerStatus
    {
        /// <summary>Henüz alışveriş yapmadı.</summary>
        Waiting = 0,

        /// <summary>Ürünü satın aldı.</summary>
        Sold = 1,

        /// <summary>Pazarlıktan vazgeçip gitti (oyuncu yolcu etti).</summary>
        Left = 2
    }
}
