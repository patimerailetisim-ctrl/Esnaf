using System;
using System.Collections.Generic;
using Esnaf.Core;

namespace Esnaf.Domain.Products
{
    /// <summary>
    /// ÜRÜN ÖRNEĞİ (durum): pazardaki veya oyuncunun elindeki SOMUT telefon (pil %78, ekran değişmiş, alış 27.000 TL...).
    /// Modeli yalnızca <see cref="DefinitionId"/> ile gösterir; tanımın hiçbir alanını (baz fiyat, ad...) kopyalamaz.
    /// Bu sınıf GERÇEK nitelikleri tutar. Oyuncunun BİLDİKLERİ (ekspertiz sonuçları) ayrıdır (Appraisal/KnowledgeState).
    /// Gerçek değer kaydedilmez; formülle hesaplanır. Düz veri sınıfıdır; iş kuralı sistemlerdedir.
    /// </summary>
    public sealed class ProductInstance
    {
        private readonly Dictionary<string, AttributeValue> _attributes = new Dictionary<string, AttributeValue>(StringComparer.Ordinal);

        public long InstanceId { get; set; }

        /// <summary>Bağlı olduğu <c>ProductDefinition.Id</c> (örn. "phone.elma_e13_pro").</summary>
        public string DefinitionId { get; set; }

        public int StorageGb { get; set; }
        public int AgeMonths { get; set; }

        /// <summary>Gerçek nitelikler (anahtar → değer). Anahtar adları sektör paketine aittir.</summary>
        public IDictionary<string, AttributeValue> Attributes
        {
            get { return _attributes; }
        }

        // Kaynak bilgisi (provenance)
        public string SellerNpcId { get; set; }
        public long ListingId { get; set; }

        /// <summary>Oyuncunun satın aldığı gün; henüz satın alınmadıysa null.</summary>
        public int? AcquiredDay { get; set; }

        // Maliyet
        public Money PurchasePrice { get; set; }

        /// <summary>Alış fiyatı + ekspertiz + tamir (kâr hesabı için).</summary>
        public Money CostBasis { get; set; }

        /// <summary>Dükkândaki etiket fiyatı (satışa çıkarılan fiyat); 0 = etiketsiz (müşteri ilgilenmez).</summary>
        public Money ListPrice { get; set; }

        public ProductLocation Location { get; set; }

        public long GetNumber(string key)
        {
            return Get(key).Number;
        }

        public string GetText(string key)
        {
            return Get(key).Text;
        }

        public bool GetFlag(string key)
        {
            return Get(key).Flag;
        }

        private AttributeValue Get(string key)
        {
            AttributeValue value;
            if (key == null || !_attributes.TryGetValue(key, out value))
            {
                throw new KeyNotFoundException("Product instance " + InstanceId + " has no attribute '" + key + "'.");
            }

            return value;
        }
    }
}
