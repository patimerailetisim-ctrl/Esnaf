namespace Esnaf.Domain.Market
{
    /// <summary>Bir modelin ilanlarda çıkabileceği ilk gün (örn. amiral model Gün 5'ten önce çıkmaz).</summary>
    public sealed class ModelAvailabilityRule
    {
        public string DefinitionId { get; }
        public int FromDay { get; }

        public ModelAvailabilityRule(string definitionId, int fromDay)
        {
            DefinitionId = definitionId;
            FromDay = fromDay;
        }
    }
}
