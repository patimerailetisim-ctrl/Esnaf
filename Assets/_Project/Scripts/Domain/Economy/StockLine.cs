using Esnaf.Core;

namespace Esnaf.Domain.Economy
{
    /// <summary>Rafta duran bir ürünün özeti: gün sonu özetinde "Stok" satırı (MVP'de maliyetle gösterilir).</summary>
    public sealed class StockLine
    {
        public long InstanceId { get; }
        public string DefinitionId { get; }
        public Money CostBasis { get; }

        /// <summary>Etiket (satış) fiyatı; 0 = henüz fiyat girilmedi (satılabilir stok sayılmaz, müşteri talep havuzuna girmez).</summary>
        public Money ListPrice { get; }

        /// <summary>
        /// Müşterilerin gelebileceği en yüksek etiket fiyatı (yalnızca bilgi, Gün 13.2): mevcut Max/IsEligible hesabından türer; 0 = bilinmiyor (uygun müşteri tipi yok).
        /// Raf ekranı fiyat önerisi ve "pahalı bulabilir" uyarısı için kullanılır; hiçbir kural buna bağlı değildir.
        /// </summary>
        public Money DemandCeiling { get; }

        public StockLine(long instanceId, string definitionId, Money costBasis, Money listPrice = default(Money), Money demandCeiling = default(Money))
        {
            InstanceId = instanceId;
            DefinitionId = definitionId;
            CostBasis = costBasis;
            ListPrice = listPrice;
            DemandCeiling = demandCeiling;
        }
    }
}
