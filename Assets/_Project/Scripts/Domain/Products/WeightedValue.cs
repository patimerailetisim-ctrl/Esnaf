namespace Esnaf.Domain.Products
{
    /// <summary>Ağırlıklı seçenek: bir durum kimliği ve seçilme ağırlığı (tam sayı).</summary>
    public sealed class WeightedValue
    {
        public string Value { get; }
        public int Weight { get; }

        public WeightedValue(string value, int weight)
        {
            Value = value;
            Weight = weight;
        }
    }
}
