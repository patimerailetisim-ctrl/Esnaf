using Esnaf.Core;

namespace Esnaf.Domain.Wholesale
{
    /// <summary>
    /// UI'ya giden, DEĞİŞMEZ toptan teklif görünümü (IGameApi.GetWholesaleOffers): teklifin içerik alanları + bugün alınabilir mi.
    /// Oyun kuralı değildir: "alınabilir" bilgisi servisle aynı gün kuralından (availableFromDay ≤ bugün) gelir; asıl karar BuyWholesalePack'tedir.
    /// </summary>
    public sealed class WholesaleOfferView
    {
        public string SupplierId { get; }
        public string SupplierName { get; }
        public string AccessoryId { get; }
        public string AccessoryName { get; }
        public Money UnitCost { get; }
        public int PackSize { get; }

        /// <summary>Bir paketin toplam fiyatı (unitCost × packSize).</summary>
        public Money PackCost { get; }

        public int AvailableFromDay { get; }

        /// <summary>Bugün (oyunun mevcut günü) bu teklif açık mı.</summary>
        public bool IsAvailableToday { get; }

        public WholesaleOfferView(
            string supplierId, string supplierName, string accessoryId, string accessoryName,
            Money unitCost, int packSize, Money packCost, int availableFromDay, bool isAvailableToday)
        {
            SupplierId = supplierId;
            SupplierName = supplierName;
            AccessoryId = accessoryId;
            AccessoryName = accessoryName;
            UnitCost = unitCost;
            PackSize = packSize;
            PackCost = packCost;
            AvailableFromDay = availableFromDay;
            IsAvailableToday = isAvailableToday;
        }
    }
}
