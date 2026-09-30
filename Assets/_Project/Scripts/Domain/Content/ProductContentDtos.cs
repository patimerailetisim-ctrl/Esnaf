using System.Collections.Generic;

namespace Esnaf.Domain.Content
{
    // JSON şekli. Alanlar nullable: "eksik alan" ile "0 değeri"ni ayırt etmek için.
    // Bunlar yalnızca ayrıştırma içindir; oyun kodu bunları görmez (ProductDefinition kullanır).

    internal sealed class ProductModelsFileDto
    {
        public int? SchemaVersion { get; set; }
        public List<ProductModelDto> Models { get; set; }
    }

    internal sealed class ProductModelDto
    {
        public string Id { get; set; }
        public string Sector { get; set; }
        public string Name { get; set; }
        public string Brand { get; set; }
        public string Segment { get; set; }
        public int? ReleaseYear { get; set; }
        public long? BasePrice { get; set; }
        public int? BaseStorageGb { get; set; }
        public List<StorageOptionDto> StorageOptions { get; set; }
        public AgeRangeDto AgeMonths { get; set; }
        public string IconKey { get; set; }
        public bool? Deprecated { get; set; }
    }

    internal sealed class StorageOptionDto
    {
        public int? Gb { get; set; }
        public double? Mult { get; set; }
    }

    internal sealed class AgeRangeDto
    {
        public int? Min { get; set; }
        public int? Max { get; set; }
    }

    internal sealed class IdManifestFileDto
    {
        public int? SchemaVersion { get; set; }
        public List<string> Ids { get; set; }
    }
}
