namespace Esnaf.Domain.Appraisal
{
    /// <summary>Bir ekspertiz bulgusunun güven düzeyi (GDD v0.2 5.2): ipucu, düşük, orta, kesin.</summary>
    public enum AppraisalConfidence
    {
        Hint = 0,
        Low = 1,
        Medium = 2,
        Certain = 3
    }

    public static class AppraisalConfidences
    {
        public static bool TryParse(string text, out AppraisalConfidence confidence)
        {
            switch (text)
            {
                case "hint":
                    confidence = AppraisalConfidence.Hint;
                    return true;
                case "low":
                    confidence = AppraisalConfidence.Low;
                    return true;
                case "medium":
                    confidence = AppraisalConfidence.Medium;
                    return true;
                case "certain":
                    confidence = AppraisalConfidence.Certain;
                    return true;
                default:
                    confidence = AppraisalConfidence.Hint;
                    return false;
            }
        }
    }
}
