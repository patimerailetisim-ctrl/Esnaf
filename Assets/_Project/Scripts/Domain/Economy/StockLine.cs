using Esnaf.Core;

namespace Esnaf.Domain.Economy
{
    /// <summary>Rafta duran bir ürünün özeti: gün sonu özetinde "Stok" satırı (MVP'de maliyetle gösterilir).</summary>
    public sealed class StockLine
    {
        public long InstanceId { get; }
        public string DefinitionId { get; }
        public Money CostBasis { get; }

        public StockLine(long instanceId, string definitionId, Money costBasis)
        {
            InstanceId = instanceId;
            DefinitionId = definitionId;
            CostBasis = costBasis;
        }
    }
}
