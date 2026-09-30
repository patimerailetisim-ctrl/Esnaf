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

    // ---- value_tables.json ----

    internal sealed class ValueTablesFileDto
    {
        public int? SchemaVersion { get; set; }
        public List<AgeBandDto> Age { get; set; }
        public BatteryDto Battery { get; set; }
        public BodyDto Body { get; set; }
        public List<IdMultiplierDto> Screen { get; set; }
        public List<IdMultiplierDto> Camera { get; set; }
        public PackageDto Package { get; set; }
    }

    internal sealed class AgeBandDto
    {
        public int? FromMonths { get; set; }
        public double? Mult { get; set; }
    }

    internal sealed class BatteryDto
    {
        public int? FullAtOrAbove { get; set; }
        public double? PenaltyPerPoint { get; set; }
    }

    internal sealed class BodyDto
    {
        public double? Base { get; set; }
        public double? Span { get; set; }
    }

    internal sealed class IdMultiplierDto
    {
        public string Id { get; set; }
        public double? Mult { get; set; }
    }

    internal sealed class PackageDto
    {
        public double? BoxBonus { get; set; }
        public double? InvoiceBonus { get; set; }
    }

    // ---- condition_profiles.json ----

    internal sealed class ConditionProfilesFileDto
    {
        public int? SchemaVersion { get; set; }
        public List<ConditionProfileDto> Profiles { get; set; }
    }

    internal sealed class ConditionProfileDto
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int? Weight { get; set; }
        public int? AvailableFromDay { get; set; }
        public IntRangeDto Battery { get; set; }
        public IntRangeDto Body { get; set; }
        public List<WeightedValueDto> Screen { get; set; }
        public List<WeightedValueDto> Camera { get; set; }
        public double? BoxChance { get; set; }
        public double? InvoiceChance { get; set; }
    }

    internal sealed class IntRangeDto
    {
        public int? Min { get; set; }
        public int? Max { get; set; }
    }

    internal sealed class WeightedValueDto
    {
        public string Value { get; set; }
        public int? Weight { get; set; }
    }

    // ---- transaction_types.json ----

    internal sealed class TransactionTypesFileDto
    {
        public int? SchemaVersion { get; set; }
        public List<TransactionTypeDto> Types { get; set; }
    }

    internal sealed class TransactionTypeDto
    {
        public string Id { get; set; }
        public string DisplayKey { get; set; }
        public string Category { get; set; }
        public string Direction { get; set; }
        public string ProfitEffect { get; set; }
    }

    // ---- economy_constants.json ----

    internal sealed class EconomyConstantsFileDto
    {
        public int? SchemaVersion { get; set; }
        public long? OpeningCapital { get; set; }
        public DailyExpenseDto DailyExpense { get; set; }
        public int? InitialShelfCapacity { get; set; }
    }

    internal sealed class DailyExpenseDto
    {
        public int? FromDay { get; set; }
        public long? Amount { get; set; }
    }
}
