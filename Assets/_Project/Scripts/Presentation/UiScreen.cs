namespace Esnaf.Presentation
{
    /// <summary>Arayüz ekranları. Gün 10: İlanlar, Telefon Detayı, Ekspertiz, Pazarlık, Raf (salt okunur), Müşteri satışı, Toptancı, Aksesuar stoğu.</summary>
    public enum UiScreen
    {
        Listings = 0,
        Detail = 1,
        Appraisal = 2,
        Negotiation = 3,
        Shelf = 4,
        Sale = 5,
        Wholesale = 6,
        AccessoryStock = 7,

        /// <summary>Dükkan ana ekranı (Gün 13.4): raf, aksesuarlar, aktif müşteri. Satış ekranı değildir.</summary>
        Shop = 8,

        /// <summary>Profil (Gün 13.4: yer tutucu; Gün 13.5'te yapılacak).</summary>
        Profile = 9
    }
}
