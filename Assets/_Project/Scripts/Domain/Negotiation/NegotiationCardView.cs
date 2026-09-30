using Esnaf.Core;
using Esnaf.Domain.Appraisal;

namespace Esnaf.Domain.Negotiation
{
    /// <summary>
    /// Pazarlıkta oynanabilecek bir koz kartı (ekspertiz bulgusu). Kartın doğru mu yanlış alarm mı olduğu GÖSTERİLMEZ
    /// (oyuncu ayırt edemez; GDD v0.2 5.2). Kart (AppraisalId, CardIndex) çiftiyle seçilir.
    /// </summary>
    public sealed class NegotiationCardView
    {
        public long AppraisalId { get; }
        public int CardIndex { get; }
        public string Attribute { get; }
        public string WordingKey { get; }
        public AppraisalConfidence Confidence { get; }
        public double EvidencePower { get; }
        public Money ProblemValue { get; }

        /// <summary>Bu pazarlıkta zaten oynandı.</summary>
        public bool IsUsed { get; }

        public NegotiationCardView(
            long appraisalId,
            int cardIndex,
            string attribute,
            string wordingKey,
            AppraisalConfidence confidence,
            double evidencePower,
            Money problemValue,
            bool isUsed)
        {
            AppraisalId = appraisalId;
            CardIndex = cardIndex;
            Attribute = attribute;
            WordingKey = wordingKey;
            Confidence = confidence;
            EvidencePower = evidencePower;
            ProblemValue = problemValue;
            IsUsed = isUsed;
        }
    }
}
