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
        public MarketDto Market { get; set; }
    }

    internal sealed class DailyExpenseDto
    {
        public int? FromDay { get; set; }
        public long? Amount { get; set; }
    }

    // ---- economy_constants.json "market" bölümü ----

    internal sealed class MarketDto
    {
        public List<ListingCountDto> ListingCounts { get; set; }
        public LifetimeDto ListingLifetimeDays { get; set; }
        public List<SegmentWeightDto> SegmentWeights { get; set; }
        public List<ModelAvailabilityDto> ModelAvailability { get; set; }
        public LearningFriendlyDto LearningFriendlySellers { get; set; }
        public OpportunityDto Opportunity { get; set; }
        public JackpotDto Jackpot { get; set; }
        public TrapDto Trap { get; set; }
        public int? AskingPriceStep { get; set; }
        public List<HiddenDefectDto> HiddenDefects { get; set; }
        public GuidedListingDto GuidedListing { get; set; }
    }

    internal sealed class ListingCountDto
    {
        public int? FromDay { get; set; }
        public int? Min { get; set; }
        public int? Max { get; set; }
    }

    internal sealed class LifetimeDto
    {
        public int? Min { get; set; }
        public int? Max { get; set; }
    }

    internal sealed class SegmentWeightDto
    {
        public int? FromDay { get; set; }
        public int? Entry { get; set; }
        public int? Mid { get; set; }
        public int? Upper { get; set; }
    }

    internal sealed class ModelAvailabilityDto
    {
        public string Id { get; set; }
        public int? FromDay { get; set; }
    }

    internal sealed class LearningFriendlyDto
    {
        public int? UntilDay { get; set; }
        public double? Share { get; set; }
    }

    internal sealed class OpportunityDto
    {
        public int? FromDay { get; set; }
        public int? MinPerDay { get; set; }
        public double? MaxRejectRatio { get; set; }
    }

    internal sealed class QuotaDto
    {
        public int? FromDay { get; set; }
        public int? Max { get; set; }
    }

    internal sealed class JackpotDto
    {
        public double? RejectRatioBelow { get; set; }
        public List<QuotaDto> MaxPerDay { get; set; }
    }

    internal sealed class TrapDto
    {
        public int? FromDay { get; set; }
        public int? MinPerDay { get; set; }
        public List<QuotaDto> MaxPerDay { get; set; }
        public double? ValueRatio { get; set; }
    }

    internal sealed class HiddenDefectDto
    {
        public string Attribute { get; set; }
        public List<string> HiddenValues { get; set; }
        public string CleanValue { get; set; }
    }

    internal sealed class GuidedListingDto
    {
        public string SellerNpcId { get; set; }
        public string DefinitionId { get; set; }
        public int? StorageGb { get; set; }
        public int? AgeMonths { get; set; }
        public int? Battery { get; set; }
        public int? Body { get; set; }
        public string Screen { get; set; }
        public string Camera { get; set; }
        public bool? Box { get; set; }
        public bool? Invoice { get; set; }
        public long? RejectPrice { get; set; }
    }

    // ---- npc_profiles.json ----

    internal sealed class NpcProfilesFileDto
    {
        public int? SchemaVersion { get; set; }
        public List<NpcDto> Npcs { get; set; }
    }

    internal sealed class NpcDto
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Personality { get; set; }
        public NpcSellerDto Seller { get; set; }
        public NpcCustomerDto Customer { get; set; }
    }

    internal sealed class NpcSellerDto
    {
        public int? AvailableFromDay { get; set; }
        public double? AskMultiplier { get; set; }
        public double? RejectRatio { get; set; }
        public int? Patience { get; set; }
        public double? ValueSigma { get; set; }
        public double? ValueBias { get; set; }
        public double? Urgency { get; set; }
        public double? Persuasion { get; set; }
        public double? ConcealChance { get; set; }
        public bool? LearningFriendly { get; set; }
        public int? UrgentLabelFromDay { get; set; }
    }

    internal sealed class NpcCustomerDto
    {
        public double? OpeningOfferRatio { get; set; }
        public double? ValueRatio { get; set; }
        public int? Patience { get; set; }
        public double? ValueSigma { get; set; }
        public double? PackageRatio { get; set; }
    }
}
