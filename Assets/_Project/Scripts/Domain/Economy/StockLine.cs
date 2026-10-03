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

        public StockLine(long instanceId, string definitionId, Money costBasis, Money listPrice = default(Money))
        {
            InstanceId = instanceId;
            DefinitionId = definitionId;
            CostBasis = costBasis;
            ListPrice = listPrice;
        }
    }
}
