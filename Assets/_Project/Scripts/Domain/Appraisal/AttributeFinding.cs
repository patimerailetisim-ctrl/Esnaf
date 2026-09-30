namespace Esnaf.Domain.Appraisal
{
    /// <summary>
    /// Kontrol edilen bir nitelik için ekspertiz bulgusu. <see cref="Found"/> = false ise "sorun görünmüyor" (asla "sorun yok" değil).
    /// <see cref="IsFalseAlarm"/> GİZLİDİR (sağlam parçada çıkan bulgu); UI'ya giden görünümler bunu taşımaz.
    /// </summary>
    public sealed class AttributeFinding
    {
        public string Attribute { get; }
        public string WordingKey { get; }
        public bool Found { get; }
        public AppraisalConfidence Confidence { get; }
        public double EvidencePower { get; }
        public bool IsFalseAlarm { get; }

        public AttributeFinding(string attribute, string wordingKey, bool found, AppraisalConfidence confidence, double evidencePower, bool isFalseAlarm)
        {
            Attribute = attribute;
            WordingKey = wordingKey;
            Found = found;
            Confidence = confidence;
            EvidencePower = evidencePower;
            IsFalseAlarm = isFalseAlarm;
        }
    }
}
