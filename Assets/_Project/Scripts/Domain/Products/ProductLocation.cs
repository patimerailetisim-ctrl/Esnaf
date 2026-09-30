namespace Esnaf.Domain.Products
{
    /// <summary>Ürün örneği her an tam olarak BİR konumdadır (GDD invariant I8).</summary>
    public enum ProductLocation
    {
        Market = 0,
        Inventory = 1,
        Sold = 2
    }
}
