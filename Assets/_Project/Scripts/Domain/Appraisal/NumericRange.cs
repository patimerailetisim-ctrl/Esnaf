namespace Esnaf.Domain.Appraisal
{
    /// <summary>Pil/kasa gibi sayısal bir alanın ekspertiz aralığı (puan, uçlar dahil).</summary>
    public sealed class NumericRange
    {
        public int Min { get; }
        public int Max { get; }

        public NumericRange(int min, int max)
        {
            Min = min;
            Max = max;
        }

        public bool Contains(int value)
        {
            return value >= Min && value <= Max;
        }
    }
}
