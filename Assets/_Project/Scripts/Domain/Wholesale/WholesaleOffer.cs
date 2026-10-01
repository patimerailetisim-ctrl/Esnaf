using Esnaf.Core;

namespace Esnaf.Domain.Wholesale
{
    /// <summary>Bir toptancının bir aksesuar için teklifi (wholesale.json): birim maliyet, paket adedi, açılış günü. Satış fiyatından (<c>AccessoryDefinition.RetailPrice</c>) ayrı bir içerik alanıdır.</summary>
    public sealed class WholesaleOffer
    {
        public string SupplierId { get; }
        public string SupplierName { get; }
        public string AccessoryId { get; }

        /// <summary>Bir birimin toptan maliyeti.</summary>
        public Money UnitCost { get; }

        /// <summary>Toptancı yalnızca bu adedin katlarında (paket) satar.</summary>
        public int PackSize { get; }

        /// <summary>Teklifin geçerli olduğu ilk gün (≥ 1).</summary>
        public int AvailableFromDay { get; }

        public WholesaleOffer(string supplierId, string supplierName, string accessoryId, Money unitCost, int packSize, int availableFromDay)
        {
            SupplierId = supplierId;
            SupplierName = supplierName;
            AccessoryId = accessoryId;
            UnitCost = unitCost;
            PackSize = packSize;
            AvailableFromDay = availableFromDay;
        }

        /// <summary>Bir paketin toplam maliyeti.</summary>
        public Money PackCost
        {
            get { return Money.FromTl(UnitCost.Tl * PackSize); }
        }
    }
}
