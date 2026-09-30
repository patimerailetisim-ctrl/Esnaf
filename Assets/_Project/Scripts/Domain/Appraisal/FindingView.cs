namespace Esnaf.Domain.Appraisal
{
    /// <summary>UI'ya giden bulgu görünümü: yanlış alarm bayrağını TAŞIMAZ (oyuncu ayırt edemez).</summary>
    public sealed class FindingView
    {
        public string Attribute { get; }
        public string WordingKey { get; }
        public bool Found { get; }
        public AppraisalConfidence Confidence { get; }
        public double EvidencePower { get; }

        public FindingView(string attribute, string wordingKey, bool found, AppraisalConfidence confidence, double evidencePower)
        {
            Attribute = attribute;
            WordingKey = wordingKey;
            Found = found;
            Confidence = confidence;
            EvidencePower = evidencePower;
        }
    }
}
