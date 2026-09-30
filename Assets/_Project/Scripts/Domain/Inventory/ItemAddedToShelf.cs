namespace Esnaf.Domain.Inventory
{
    /// <summary>Bildirim: bir ürün rafa eklendi (durum değişikliğinden SONRA yayınlanır).</summary>
    public sealed class ItemAddedToShelf
    {
        public long InstanceId { get; }

        public ItemAddedToShelf(long instanceId)
        {
            InstanceId = instanceId;
        }
    }
}
