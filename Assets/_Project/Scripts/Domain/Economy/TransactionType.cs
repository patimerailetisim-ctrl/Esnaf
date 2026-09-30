namespace Esnaf.Domain.Economy
{
    /// <summary>
    /// Defter türü tanımı (transaction_types.json). Veri tablosudur: yeni tür (ör. ileride kredi) yalnızca yeni bir satır olarak eklenir.
    /// Bu sınıf doğrulama yapmaz; kuralları <c>ContentValidator.ValidateTransactionTypes</c> uygular.
    /// </summary>
    public sealed class TransactionType
    {
        public string Id { get; }

        /// <summary>Arayüz metin anahtarı (strings tablosunda karşılığı aranır).</summary>
        public string DisplayKey { get; }

        public TransactionCategory Category { get; }
        public TransactionDirection Direction { get; }
        public ProfitEffect ProfitEffect { get; }

        public TransactionType(string id, string displayKey, TransactionCategory category, TransactionDirection direction, ProfitEffect profitEffect)
        {
            Id = id;
            DisplayKey = displayKey;
            Category = category;
            Direction = direction;
            ProfitEffect = profitEffect;
        }
    }
}
