using Esnaf.Core;

namespace Esnaf.Domain.Appraisal
{
    /// <summary>
    /// KOZ KARTI (GDD v0.2 7.4): her ekspertiz bulgusu bir karttır. Gücü = kanıt gücü; <see cref="ProblemValue"/> = kusurun götürdüğü
    /// TL değeri. Kartın pazarlıkta kullanımı Gün 7'dedir; yanlış alarm kartı etkisizdir (<see cref="IsFalseAlarm"/> gizlidir).
    /// </summary>
    public sealed class TrumpCard
    {
        public string Attribute { get; }
        public string WordingKey { get; }
        public AppraisalConfidence Confidence { get; }
        public double EvidencePower { get; }
        public Money ProblemValue { get; }
        public bool IsFalseAlarm { get; }

        public TrumpCard(string attribute, string wordingKey, AppraisalConfidence confidence, double evidencePower, Money problemValue, bool isFalseAlarm)
        {
            Attribute = attribute;
            WordingKey = wordingKey;
            Confidence = confidence;
            EvidencePower = evidencePower;
            ProblemValue = problemValue;
            IsFalseAlarm = isFalseAlarm;
        }
    }
}
