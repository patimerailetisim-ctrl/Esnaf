using Esnaf.Core;

namespace Esnaf.Domain.Wholesale
{
    /// <summary>Toptancı teklifinin neyi sattığı: aksesuar ya da ürün (telefon modeli, Gün 14).</summary>
    public enum WholesaleItemKind
    {
        Accessory = 0,
        Product = 1
    }

    /// <summary>
    /// Bir toptancının bir kalem için teklifi (wholesale.json): birim maliyet, paket adedi, açılış günü. Kalem bir aksesuar (<see cref="AccessoryId"/>) ya da bir ürün/telefon modeli
    /// (<see cref="ProductId"/>) olabilir; ikisi aynı yapıyı paylaşır. Satış fiyatı (<c>AccessoryDefinition.RetailPrice</c> ya da önerilen satış) ayrı bir içerik alanıdır.
    /// </summary>
    public sealed class WholesaleOffer
    {
        public WholesaleItemKind Kind { get; }
        public string SupplierId { get; }
        public string SupplierName { get; }

        /// <summary>Aksesuar teklifinde aksesuar kimliği; ürün teklifinde null.</summary>
        public string AccessoryId { get; }

        /// <summary>Ürün (telefon modeli) teklifinde <c>ProductDefinition</c> kimliği; aksesuar teklifinde null.</summary>
        public string ProductId { get; }

        /// <summary>Bir birimin toptan maliyeti.</summary>
        public Money UnitCost { get; }

        /// <summary>Toptancı yalnızca bu adedin katlarında (paket) satar.</summary>
        public int PackSize { get; }

        /// <summary>Teklifin geçerli olduğu ilk gün (≥ 1).</summary>
        public int AvailableFromDay { get; }

        /// <summary>İsteğe bağlı önerilen satış fiyatı (içerik; toptan maliyetten bağımsız). 0 = tanımsız. Yalnızca gösterim; satış fiyatı oyuncunun kararıdır.</summary>
        public Money SuggestedRetail { get; }

        /// <summary>Aksesuar teklifi.</summary>
        public WholesaleOffer(string supplierId, string supplierName, string accessoryId, Money unitCost, int packSize, int availableFromDay)
        {
            Kind = WholesaleItemKind.Accessory;
            SupplierId = supplierId;
            SupplierName = supplierName;
            AccessoryId = accessoryId;
            UnitCost = unitCost;
            PackSize = packSize;
            AvailableFromDay = availableFromDay;
        }

        private WholesaleOffer(string supplierId, string supplierName, string productId, Money unitCost, int packSize, int availableFromDay, Money suggestedRetail)
        {
            Kind = WholesaleItemKind.Product;
            SupplierId = supplierId;
            SupplierName = supplierName;
            ProductId = productId;
            UnitCost = unitCost;
            PackSize = packSize;
            AvailableFromDay = availableFromDay;
            SuggestedRetail = suggestedRetail;
        }

        /// <summary>Ürün (telefon modeli) teklifi: ProductDefinition → WholesaleOffer → paket → envanter.</summary>
        public static WholesaleOffer ForProduct(string supplierId, string supplierName, string productId, Money unitCost, int packSize, int availableFromDay, Money suggestedRetail = default(Money))
        {
            return new WholesaleOffer(supplierId, supplierName, productId, unitCost, packSize, availableFromDay, suggestedRetail);
        }

        /// <summary>Kalem kimliği: aksesuar ya da ürün kimliği.</summary>
        public string ItemId
        {
            get { return Kind == WholesaleItemKind.Accessory ? AccessoryId : ProductId; }
        }

        /// <summary>Bir paketin toplam maliyeti.</summary>
        public Money PackCost
        {
            get { return Money.FromTl(UnitCost.Tl * PackSize); }
        }
    }
}
