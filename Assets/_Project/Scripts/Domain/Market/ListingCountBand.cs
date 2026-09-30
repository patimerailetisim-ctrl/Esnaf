namespace Esnaf.Domain.Market
{
    /// <summary>Günlük ilan sayısı aralığı; <see cref="FromDay"/> gününden itibaren (bir sonraki banda kadar) geçerlidir.</summary>
    public sealed class ListingCountBand
    {
        public int FromDay { get; }
        public int Min { get; }
        public int Max { get; }

        public ListingCountBand(int fromDay, int min, int max)
        {
            FromDay = fromDay;
            Min = min;
            Max = max;
        }
    }
}
