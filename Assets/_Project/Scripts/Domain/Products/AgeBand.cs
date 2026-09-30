namespace Esnaf.Domain.Products
{
    /// <summary>Yaş çarpanı bandı: yaşı <see cref="FromMonths"/> ve üstü olan (bir sonraki banda kadar) ürünler için çarpan.</summary>
    public sealed class AgeBand
    {
        public int FromMonths { get; }
        public double Multiplier { get; }

        public AgeBand(int fromMonths, double multiplier)
        {
            FromMonths = fromMonths;
            Multiplier = multiplier;
        }
    }
}
