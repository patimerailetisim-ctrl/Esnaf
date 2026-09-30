namespace Esnaf.Domain.Market
{
    /// <summary>Günlük üst sınır kotası; <see cref="FromDay"/> gününden itibaren (bir sonraki banda kadar) geçerlidir.</summary>
    public sealed class QuotaBand
    {
        public int FromDay { get; }
        public int Max { get; }

        public QuotaBand(int fromDay, int max)
        {
            FromDay = fromDay;
            Max = max;
        }
    }
}
