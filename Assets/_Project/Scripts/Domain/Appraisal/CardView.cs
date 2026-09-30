using Esnaf.Core;

namespace Esnaf.Domain.Appraisal
{
    /// <summary>UI'ya giden koz kartı görünümü: yanlış alarm bayrağını TAŞIMAZ.</summary>
    public sealed class CardView
    {
        public string Attribute { get; }
        public string WordingKey { get; }
        public AppraisalConfidence Confidence { get; }
        public double EvidencePower { get; }
        public Money ProblemValue { get; }

        public CardView(string attribute, string wordingKey, AppraisalConfidence confidence, double evidencePower, Money problemValue)
        {
            Attribute = attribute;
            WordingKey = wordingKey;
            Confidence = confidence;
            EvidencePower = evidencePower;
            ProblemValue = problemValue;
        }
    }
}
