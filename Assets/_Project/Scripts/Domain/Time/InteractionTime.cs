namespace Esnaf.Domain.Time
{
    /// <summary>
    /// Müşteri/satış aksiyonlarının mağaza saatinden harcadığı SABİT süreler (Gün 12.4; dakika). Kişiliğe, fiyata ya da şansa bağlı değildir; rastgelelik yoktur. Kod sabitidir
    /// (içerik/şema değişmez). Süre, aksiyon BAŞARILI olduktan sonra <see cref="StoreClock.Spend"/> ile harcanır; başarısız çağrı zaman harcamaz. Kapanışı (21:00) aşan süre kırpılır.
    /// Satışın tamamlanması (ödeme/devir) için ayrıca süre yoktur: anlaşmaya varan istek/kabul kendi süresini harcar. Alış, ilan, ekspertiz ve toptancı zaman harcamaz.
    /// </summary>
    public static class InteractionTime
    {
        public const int StartSale = 5;
        public const int AskPrice = 5;
        public const int ShowReport = 5;

        /// <summary>Teklifi Kabul Et (müşterinin teklifi ya da son teklifi).</summary>
        public const int AcceptOffer = 5;

        public const int LetCustomerGo = 3;

        /// <summary>Her başarılı aksesuar satışı.</summary>
        public const int AccessorySale = 3;

        /// <summary>Aktif kuyruk müşterisini (ör. ürünü olmayanı) gönderme.</summary>
        public const int CompleteCustomer = 2;
    }
}
