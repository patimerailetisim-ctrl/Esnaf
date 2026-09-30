using Esnaf.Core;

namespace Esnaf.Domain.Economy
{
    /// <summary>Gün sonu özetinde "Satılan: ... maliyet → satış → kâr" satırı.</summary>
    public sealed class SoldItemSummary
    {
        public long RecordId { get; }
        public long InstanceId { get; }
        public string DefinitionId { get; }
        public string NpcId { get; }
        public Money SalePrice { get; }
        public Money CostBasis { get; }

        public Money Profit
        {
            get { return SalePrice - CostBasis; }
        }

        public SoldItemSummary(long recordId, long instanceId, string definitionId, string npcId, Money salePrice, Money costBasis)
        {
            RecordId = recordId;
            InstanceId = instanceId;
            DefinitionId = definitionId;
            NpcId = npcId;
            SalePrice = salePrice;
            CostBasis = costBasis;
        }
    }
}
