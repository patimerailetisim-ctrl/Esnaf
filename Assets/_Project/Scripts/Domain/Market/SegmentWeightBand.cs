using Esnaf.Domain.Products;

namespace Esnaf.Domain.Market
{
    /// <summary>İlanların segment ağırlıkları (Giriş/Orta/Üst); <see cref="FromDay"/> gününden itibaren geçerlidir.</summary>
    public sealed class SegmentWeightBand
    {
        public int FromDay { get; }
        public int Entry { get; }
        public int Mid { get; }
        public int Upper { get; }

        public SegmentWeightBand(int fromDay, int entry, int mid, int upper)
        {
            FromDay = fromDay;
            Entry = entry;
            Mid = mid;
            Upper = upper;
        }

        public int WeightFor(ProductSegment segment)
        {
            switch (segment)
            {
                case ProductSegment.Entry:
                    return Entry;
                case ProductSegment.Mid:
                    return Mid;
                default:
                    return Upper;
            }
        }
    }
}
