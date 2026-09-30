namespace Esnaf.Domain.Market
{
    /// <summary>Bildirim: bir ilanın ömrü doldu ve pazardan kaldırıldı (durum değişikliğinden SONRA yayınlanır).</summary>
    public sealed class ListingExpired
    {
        public long ListingId { get; }
        public long InstanceId { get; }

        public ListingExpired(long listingId, long instanceId)
        {
            ListingId = listingId;
            InstanceId = instanceId;
        }
    }
}
