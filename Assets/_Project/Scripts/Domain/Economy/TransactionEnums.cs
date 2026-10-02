namespace Esnaf.Domain.Economy
{
    /// <summary>Defter satırı kategorisi (GDD v0.3 2.3). Sonradan "financing" gibi kategoriler eklenebilir.</summary>
    public enum TransactionCategory
    {
        Capital = 0,
        Trade = 1,
        Expense = 2,
        Investment = 3
    }

    /// <summary>Tutarın işareti: giriş (+), çıkış (−) veya nakitsiz (0).</summary>
    public enum TransactionDirection
    {
        Inflow = 0,
        Outflow = 1,
        Neutral = 2
    }

    /// <summary>Satırın gün net kârına etkisi. None: kâr etkisi yok (alış/ekspertiz/tamir/yatırım maliyete veya varlığa eklenir).</summary>
    public enum ProfitEffect
    {
        None = 0,
        Expense = 1,
        Sale = 2,
        WriteOff = 3,

        /// <summary>Aksesuar ek satışı: kâr = satış − stoktan çıkan gerçek maliyet. Ürün örneği gerektirmez (Gün 11.3.1).</summary>
        AccessorySale = 4
    }

    /// <summary>transaction_types.json'daki metin karşılıkları.</summary>
    public static class TransactionTypeEnums
    {
        public static bool TryParseCategory(string text, out TransactionCategory category)
        {
            switch (text)
            {
                case "capital":
                    category = TransactionCategory.Capital;
                    return true;
                case "trade":
                    category = TransactionCategory.Trade;
                    return true;
                case "expense":
                    category = TransactionCategory.Expense;
                    return true;
                case "investment":
                    category = TransactionCategory.Investment;
                    return true;
                default:
                    category = TransactionCategory.Capital;
                    return false;
            }
        }

        public static bool TryParseDirection(string text, out TransactionDirection direction)
        {
            switch (text)
            {
                case "inflow":
                    direction = TransactionDirection.Inflow;
                    return true;
                case "outflow":
                    direction = TransactionDirection.Outflow;
                    return true;
                case "neutral":
                    direction = TransactionDirection.Neutral;
                    return true;
                default:
                    direction = TransactionDirection.Inflow;
                    return false;
            }
        }

        public static bool TryParseProfitEffect(string text, out ProfitEffect effect)
        {
            switch (text)
            {
                case "none":
                    effect = ProfitEffect.None;
                    return true;
                case "expense":
                    effect = ProfitEffect.Expense;
                    return true;
                case "sale":
                    effect = ProfitEffect.Sale;
                    return true;
                case "write_off":
                    effect = ProfitEffect.WriteOff;
                    return true;
                case "accessory_sale":
                    effect = ProfitEffect.AccessorySale;
                    return true;
                default:
                    effect = ProfitEffect.None;
                    return false;
            }
        }
    }
}
