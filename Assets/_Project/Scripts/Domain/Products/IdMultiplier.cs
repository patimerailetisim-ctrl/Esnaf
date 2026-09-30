namespace Esnaf.Domain.Products
{
    /// <summary>Bir durum kimliği (örn. "cracked") ve fiyat çarpanı.</summary>
    public sealed class IdMultiplier
    {
        public string Id { get; }
        public double Multiplier { get; }

        public IdMultiplier(string id, double multiplier)
        {
            Id = id;
            Multiplier = multiplier;
        }
    }
}
