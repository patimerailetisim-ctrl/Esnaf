using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Esnaf.Core;

namespace Esnaf.Domain.Products
{
    /// <summary>
    /// ÜRÜN TANIMI (şablon): "Elma E13 Pro" gibi bir modelin değişmez bilgileri. İçerik dosyasından (JSON) gelir,
    /// yüklendikten sonra salt okunurdur (GDD K10) ve KAYDA KOPYALANMAZ; kayıtlar yalnızca <see cref="Id"/> ile ona bakar.
    /// Oyuncunun sahip olduğu gerçek telefon <see cref="ProductInstance"/>'tır.
    /// Bu sınıf değer doğrulaması yapmaz; kuralları <c>ContentValidator</c> uygular.
    /// </summary>
    public sealed class ProductDefinition
    {
        private readonly ReadOnlyCollection<StorageOption> _storageOptions;

        public string Id { get; }
        public string Sector { get; }
        public string Name { get; }
        public string Brand { get; }
        public ProductSegment Segment { get; }
        public int ReleaseYear { get; }
        public Money BasePrice { get; }
        public int BaseStorageGb { get; }
        public int MinAgeMonths { get; }
        public int MaxAgeMonths { get; }
        public string IconKey { get; }
        public bool IsDeprecated { get; }

        /// <summary>Salt okunur liste (dışarıdan değiştirilemez).</summary>
        public IReadOnlyList<StorageOption> StorageOptions
        {
            get { return _storageOptions; }
        }

        public ProductDefinition(
            string id,
            string sector,
            string name,
            string brand,
            ProductSegment segment,
            int releaseYear,
            Money basePrice,
            int baseStorageGb,
            IEnumerable<StorageOption> storageOptions,
            int minAgeMonths,
            int maxAgeMonths,
            string iconKey,
            bool isDeprecated)
        {
            if (storageOptions == null)
            {
                throw new ArgumentNullException(nameof(storageOptions));
            }

            Id = id;
            Sector = sector;
            Name = name;
            Brand = brand;
            Segment = segment;
            ReleaseYear = releaseYear;
            BasePrice = basePrice;
            BaseStorageGb = baseStorageGb;
            MinAgeMonths = minAgeMonths;
            MaxAgeMonths = maxAgeMonths;
            IconKey = iconKey;
            IsDeprecated = isDeprecated;
            _storageOptions = new ReadOnlyCollection<StorageOption>(new List<StorageOption>(storageOptions));
        }

        /// <summary>Verilen hafıza için fiyat çarpanı; modelde o hafıza seçeneği yoksa false.</summary>
        public bool TryGetStorageMultiplier(int gb, out double multiplier)
        {
            for (int i = 0; i < _storageOptions.Count; i++)
            {
                if (_storageOptions[i].Gb == gb)
                {
                    multiplier = _storageOptions[i].Multiplier;
                    return true;
                }
            }

            multiplier = 0.0;
            return false;
        }
    }
}
