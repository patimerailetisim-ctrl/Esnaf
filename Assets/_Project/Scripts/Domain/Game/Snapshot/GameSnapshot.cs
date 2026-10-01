using System.Collections.Generic;

namespace Esnaf.Domain.Game
{
    // Kayda giren durumun DÜZ VERİ karşılığı (GDD v0.3 4.3, 6.2). Yalnızca ID + sayı tutar: tanım alanları kopyalanmaz, gerçek değer yazılmaz.
    // Kural: bu sınıflarda iş mantığı yoktur; JSON dönüşümü Esnaf.Persistence'tadır. ulong değerler JSON kesinliği için METİN olarak tutulur.
    // Listeler oyundaki sırayla, sözlükler anahtara göre sıralı yazılır (aynı durum → aynı metin).

    /// <summary>Bütün oyun durumunun bir anlık görüntüsü (<c>GameSession.Capture</c> üretir, <c>GameSession.Restore</c> tüketir).</summary>
    public sealed class GameSnapshot
    {
        public TimeSnapshot Time { get; set; }
        public List<RngStreamSnapshot> Rng { get; set; }
        public IdsSnapshot Ids { get; set; }
        public EconomySnapshot Economy { get; set; }
        public InventorySnapshot Inventory { get; set; }
        public BusinessSnapshot Business { get; set; }
        public MarketSnapshot Market { get; set; }
        public List<InstanceSnapshot> Instances { get; set; }
        public List<NpcStateSnapshot> NpcStates { get; set; }
        public List<AppraisalSnapshot> Knowledge { get; set; }
        public CustomersSnapshot Customers { get; set; }

        /// <summary>Süren alış pazarlığı (T17); yoksa null.</summary>
        public NegotiationSnapshot ActiveNegotiation { get; set; }

        /// <summary>Süren satış pazarlığı (T17); yoksa null.</summary>
        public SaleSnapshot ActiveSale { get; set; }

        /// <summary>
        /// Aksesuar stoğu (Day 11.2.3). İSTEĞE BAĞLI: stok boşken YAZILMAZ (null), eski kayıtlarda da yoktur; yoksa boş stok sayılır.
        /// Böylece aksesuarsız kayıtların metni ve sağlaması eskisiyle aynı kalır (Save v1 bozulmaz).
        /// </summary>
        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public AccessoriesSnapshot Accessories { get; set; }
    }

    /// <summary>Aksesuar stoğunun düz verisi: kalem başına adet ve toplam maliyet tabanı (kimliğe göre sıralı). Kapasite içerikten gelir.</summary>
    public sealed class AccessoriesSnapshot
    {
        public List<AccessoryStockLineSnapshot> Stock { get; set; }
    }

    public sealed class AccessoryStockLineSnapshot
    {
        public string AccessoryId { get; set; }
        public int Quantity { get; set; }
        public long TotalCost { get; set; }
    }

    public sealed class TimeSnapshot
    {
        public int Day { get; set; }

        /// <summary>Ana tohum (ulong, onluk metin).</summary>
        public string Seed { get; set; }
    }

    public sealed class RngStreamSnapshot
    {
        public string Name { get; set; }

        /// <summary>PCG durumu (ulong, onluk metin).</summary>
        public string State { get; set; }

        /// <summary>PCG artımı (ulong, onluk metin).</summary>
        public string Increment { get; set; }
    }

    public sealed class IdsSnapshot
    {
        public long Instance { get; set; }
        public long Listing { get; set; }
        public long Appraisal { get; set; }
        public long Customer { get; set; }
    }

    public sealed class LedgerRecordSnapshot
    {
        public long Id { get; set; }
        public int Day { get; set; }
        public string Type { get; set; }
        public long Amount { get; set; }
        public long BalanceAfter { get; set; }
        public long? InstanceId { get; set; }
        public string DefinitionId { get; set; }
        public string NpcId { get; set; }
        public long? SaleCostBasis { get; set; }
        public long? RelatedRecordId { get; set; }
        public string MemoKey { get; set; }
        public List<string> MemoArgs { get; set; }
    }

    public sealed class PendingAppraisalSnapshot
    {
        public long InstanceId { get; set; }
        public List<long> RecordIds { get; set; }
    }

    public sealed class DemandIndexSnapshot
    {
        public string ModelId { get; set; }
        public double Index { get; set; }
    }

    public sealed class DemandSaleSnapshot
    {
        public string ModelId { get; set; }
        public int Day { get; set; }
    }

    public sealed class DemandSnapshot
    {
        public List<DemandIndexSnapshot> Indexes { get; set; }
        public List<DemandSaleSnapshot> Sales { get; set; }
    }

    public sealed class EconomySnapshot
    {
        /// <summary>Defter satırları (nakit = satırların toplamı; ayrıca saklanmaz).</summary>
        public List<LedgerRecordSnapshot> Ledger { get; set; }

        public long BusinessAssets { get; set; }
        public List<PendingAppraisalSnapshot> PendingAppraisals { get; set; }
        public DemandSnapshot Demand { get; set; }
    }

    public sealed class InventorySnapshot
    {
        public int Capacity { get; set; }

        /// <summary>Rafta duran ürün kimlikleri (edinme sırasıyla).</summary>
        public List<long> ItemIds { get; set; }
    }

    public sealed class BusinessSnapshot
    {
        public List<string> Equipment { get; set; }
    }

    public sealed class ListingSnapshot
    {
        public long ListingId { get; set; }
        public long InstanceId { get; set; }
        public string SellerNpcId { get; set; }
        public long AskingPrice { get; set; }
        public List<string> Tags { get; set; }
        public int DayListed { get; set; }
        public int RemainingDays { get; set; }
        public long RejectPrice { get; set; }
        public long BelievedValue { get; set; }
        public bool IsOpportunity { get; set; }
        public bool IsTrap { get; set; }
        public bool IsJackpot { get; set; }
        public bool IsGuided { get; set; }
    }

    public sealed class MarketSnapshot
    {
        public List<ListingSnapshot> Listings { get; set; }
    }

    public sealed class ProvenanceSnapshot
    {
        public string SellerNpcId { get; set; }
        public long ListingId { get; set; }
        public int? AcquiredDay { get; set; }
    }

    public sealed class InstanceSnapshot
    {
        public long InstanceId { get; set; }
        public string DefinitionId { get; set; }
        public int StorageGb { get; set; }
        public int AgeMonths { get; set; }

        /// <summary>Gerçek nitelikler: sayı (long), metin ya da bayrak (GDD 4.4 örneği); anahtara göre sıralı.</summary>
        public Dictionary<string, object> Attributes { get; set; }

        public ProvenanceSnapshot Provenance { get; set; }
        public long PurchasePrice { get; set; }
        public long CostBasis { get; set; }
        public long ListPrice { get; set; }

        /// <summary>Market, Inventory ya da Sold.</summary>
        public string Location { get; set; }
    }

    public sealed class NpcStateSnapshot
    {
        public string NpcId { get; set; }
        public int EncounterCount { get; set; }
        public List<long> SoldToPlayer { get; set; }
        public List<long> BoughtFromPlayer { get; set; }
    }

    public sealed class FindingSnapshot
    {
        public string Attribute { get; set; }
        public string WordingKey { get; set; }
        public bool Found { get; set; }

        /// <summary>Hint, Low, Medium, Certain.</summary>
        public string Confidence { get; set; }

        public double EvidencePower { get; set; }

        /// <summary>GİZLİ: sağlam parçada çıkan bulgu.</summary>
        public bool IsFalseAlarm { get; set; }
    }

    public sealed class CardSnapshot
    {
        public string Attribute { get; set; }
        public string WordingKey { get; set; }
        public string Confidence { get; set; }
        public double EvidencePower { get; set; }
        public long ProblemValue { get; set; }
        public bool IsFalseAlarm { get; set; }
    }

    public sealed class NumericRangeSnapshot
    {
        public int Min { get; set; }
        public int Max { get; set; }
    }

    public sealed class MoneyRangeSnapshot
    {
        public long Min { get; set; }
        public long Max { get; set; }
    }

    public sealed class AppraisalSnapshot
    {
        public long ResultId { get; set; }
        public long InstanceId { get; set; }
        public string DefinitionId { get; set; }
        public string LevelId { get; set; }
        public int Day { get; set; }
        public long Fee { get; set; }

        /// <summary>Kullanılan tohum (ulong, onluk metin).</summary>
        public string Seed { get; set; }

        public List<FindingSnapshot> Findings { get; set; }
        public NumericRangeSnapshot BatteryRange { get; set; }
        public NumericRangeSnapshot BodyRange { get; set; }
        public MoneyRangeSnapshot ValueRange { get; set; }
        public List<CardSnapshot> Cards { get; set; }
    }

    public sealed class CustomerSlotSnapshot
    {
        public long CustomerId { get; set; }
        public string NpcId { get; set; }
        public double ValueDraw { get; set; }
        public double TrustDraw { get; set; }
        public double PickDraw { get; set; }

        /// <summary>Waiting, Sold ya da Left.</summary>
        public string Status { get; set; }
    }

    public sealed class CustomersSnapshot
    {
        public int Arrived { get; set; }
        public int MissedTotal { get; set; }
        public List<CustomerSlotSnapshot> Slots { get; set; }
    }

    public sealed class NegotiationSetupSnapshot
    {
        public long Ask { get; set; }
        public double Reject { get; set; }
        public double Floor { get; set; }
        public int Patience { get; set; }
        public int Trust { get; set; }
        public double Urgency { get; set; }
        public double Persuasion { get; set; }
        public double WrongCardMultiplier { get; set; }
        public int Day { get; set; }
    }

    public sealed class NegotiationSnapshot
    {
        public long ListingId { get; set; }
        public long InstanceId { get; set; }
        public string SellerNpcId { get; set; }
        public bool LastOfferInsulted { get; set; }
        public NegotiationSetupSnapshot Setup { get; set; }

        /// <summary>Active ya da FinalOffer (kapanmış pazarlık kaydedilmez).</summary>
        public string Phase { get; set; }

        public int Round { get; set; }
        public int Patience { get; set; }
        public int Trust { get; set; }

        /// <summary>GİZLİ: satıcının şu anki ret fiyatı.</summary>
        public double Reject { get; set; }

        public double Price { get; set; }
        public long ShownPrice { get; set; }
        public long DealPrice { get; set; }
        public List<string> UsedCards { get; set; }
    }

    public sealed class SaleSetupSnapshot
    {
        public long Opening { get; set; }
        public double Max { get; set; }
        public int Patience { get; set; }
        public int Trust { get; set; }
        public double Urgency { get; set; }
        public int Day { get; set; }
    }

    public sealed class SaleSnapshot
    {
        public long CustomerId { get; set; }
        public string NpcId { get; set; }
        public long InstanceId { get; set; }
        public bool LastAskTooExpensive { get; set; }
        public SaleSetupSnapshot Setup { get; set; }
        public string Phase { get; set; }
        public int Round { get; set; }
        public int Patience { get; set; }
        public int Trust { get; set; }

        /// <summary>GİZLİ: müşterinin şu anki Max'ı.</summary>
        public double Max { get; set; }

        public double Offer { get; set; }
        public long ShownPrice { get; set; }
        public long DealPrice { get; set; }
        public bool ReportShown { get; set; }
    }
}
