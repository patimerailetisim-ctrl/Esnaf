namespace Esnaf.Domain.Products
{
    /// <summary>Bir modelin hafıza seçeneği ve fiyat çarpanı (baz hafıza için 1,00).</summary>
    public sealed class StorageOption
    {
        public int Gb { get; }
        public double Multiplier { get; }

        public StorageOption(int gb, double multiplier)
        {
            Gb = gb;
            Multiplier = multiplier;
        }
    }
}
