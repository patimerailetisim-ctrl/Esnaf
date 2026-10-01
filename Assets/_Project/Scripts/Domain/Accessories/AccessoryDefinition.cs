using Esnaf.Core;

namespace Esnaf.Domain.Accessories
{
    /// <summary>
    /// SIFIR AKSESUAR tanımı (şablon): şarj adaptörü, kılıf... İçerik dosyasından (accessories.json) gelir, yüklendikten sonra salt okunurdur
    /// ve kayda KOPYALANMAZ. Telefondan (<c>ProductDefinition</c>) bağımsız paralel bir modeldir: aksesuar miktarla tutulan, birbirinin aynısı
    /// stoktur; benzersiz bir ürün örneği, ekspertiz ya da pazarlık konusu değildir. Bu sınıf değer doğrulaması yapmaz (ContentValidator yapar).
    /// </summary>
    public sealed class AccessoryDefinition
    {
        /// <summary>Aksesuarlar her zaman sıfırdır (ikinci el telefonlardan ayrım).</summary>
        public const string NewCondition = "new";

        public string Id { get; }
        public string Name { get; }
        public string Category { get; }
        public string Condition { get; }

        /// <summary>Dükkândaki satış (etiket) fiyatı. Toptan maliyetten (<c>WholesaleOffer.UnitCost</c>) ayrı bir içerik alanıdır.</summary>
        public Money RetailPrice { get; }

        public string IconKey { get; }

        public AccessoryDefinition(string id, string name, string category, string condition, Money retailPrice, string iconKey)
        {
            Id = id;
            Name = name;
            Category = category;
            Condition = condition;
            RetailPrice = retailPrice;
            IconKey = iconKey;
        }
    }
}
