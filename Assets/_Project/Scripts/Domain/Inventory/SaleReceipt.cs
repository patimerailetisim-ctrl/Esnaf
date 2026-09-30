using Esnaf.Core;

namespace Esnaf.Domain.Inventory
{
    /// <summary>Bir satışın sonucu: satış fiyatı, ürünün maliyet tabanı ve kâr/zarar.</summary>
    public sealed class SaleReceipt
    {
        public long RecordId { get; }
        public Money SalePrice { get; }
        public Money CostBasis { get; }

        public Money Profit
        {
            get { return SalePrice - CostBasis; }
        }

        public SaleReceipt(long recordId, Money salePrice, Money costBasis)
        {
            RecordId = recordId;
            SalePrice = salePrice;
            CostBasis = costBasis;
        }
    }
}
