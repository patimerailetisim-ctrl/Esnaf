namespace Esnaf.Domain.Products
{
    /// <summary>Ürün segmenti (ekspertiz ücretini belirler). İçerik dosyasındaki karşılıkları: entry, mid, upper.</summary>
    public enum ProductSegment
    {
        Entry = 0,
        Mid = 1,
        Upper = 2
    }

    public static class ProductSegments
    {
        public const string EntryText = "entry";
        public const string MidText = "mid";
        public const string UpperText = "upper";

        public static bool TryParse(string text, out ProductSegment segment)
        {
            switch (text)
            {
                case EntryText:
                    segment = ProductSegment.Entry;
                    return true;
                case MidText:
                    segment = ProductSegment.Mid;
                    return true;
                case UpperText:
                    segment = ProductSegment.Upper;
                    return true;
                default:
                    segment = ProductSegment.Entry;
                    return false;
            }
        }

        public static string ToContentString(ProductSegment segment)
        {
            switch (segment)
            {
                case ProductSegment.Entry:
                    return EntryText;
                case ProductSegment.Mid:
                    return MidText;
                default:
                    return UpperText;
            }
        }
    }
}
