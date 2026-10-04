using Esnaf.Core;

namespace Esnaf.Domain.Wholesale
{
    /// <summary>
    /// UI'ya giden, DEĞİŞMEZ telefon toptan teklifi görünümü (IGameApi.GetPhoneWholesaleOffers): teklifin içerik alanları + bugün alınabilir mi. İçerikten otomatik keşfedilir;
    /// modele özel alan yoktur. Oyun kuralı değildir: asıl karar BuyPhonePack'tedir.
    /// </summary>
    public sealed class PhoneWholesaleOfferView
    {
        public string SupplierId { get; }
        public string SupplierName { get; }
        public string ProductId { get; }
        public string ProductName { get; }
        public Money UnitCost { get; }
        public int PackSize { get; }

        /// <summary>Bir paketin toplam fiyatı (unitCost × packSize); hesaplanır, içerikte yazılı değildir.</summary>
        public Money PackCost { get; }

        /// <summary>İçerikteki önerilen satış fiyatı (toptan maliyetten bağımsız); tanımsızsa 0.</summary>
        public Money SuggestedRetail { get; }

        public int AvailableFromDay { get; }
        public bool IsAvailableToday { get; }

        public PhoneWholesaleOfferView(
            string supplierId, string supplierName, string productId, string productName, Money unitCost, int packSize, Money packCost, Money suggestedRetail,
            int availableFromDay, bool isAvailableToday)
        {
            SupplierId = supplierId;
            SupplierName = supplierName;
            ProductId = productId;
            ProductName = productName;
            UnitCost = unitCost;
            PackSize = packSize;
            PackCost = packCost;
            SuggestedRetail = suggestedRetail;
            AvailableFromDay = availableFromDay;
            IsAvailableToday = isAvailableToday;
        }
    }
}
