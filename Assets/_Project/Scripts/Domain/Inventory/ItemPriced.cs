using Esnaf.Core;

namespace Esnaf.Domain.Inventory
{
    /// <summary>Bildirim: bir ürüne etiket fiyatı konuldu ya da değiştirildi (durum değişikliğinden SONRA yayınlanır).</summary>
    public sealed class ItemPriced
    {
        public long InstanceId { get; }
        public Money Price { get; }

        public ItemPriced(long instanceId, Money price)
        {
            InstanceId = instanceId;
            Price = price;
        }
    }
}
