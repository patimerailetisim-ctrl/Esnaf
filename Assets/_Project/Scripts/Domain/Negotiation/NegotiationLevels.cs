namespace Esnaf.Domain.Negotiation
{
    /// <summary>
    /// Güven ve sabır sayılarını oyuncuya gösterilen üç kademeye çevirir (alış ve satış görünümleri ortak kullanır).
    /// Eşikler negotiation_rules.json'dadır.
    /// </summary>
    public static class NegotiationLevels
    {
        /// <summary>Ruh hali: güven &lt; alt eşik → düşük; güven ≥ üst eşik → yüksek; arası orta.</summary>
        public static NegotiationLevel MoodOf(NegotiationRules rules, int trust)
        {
            if (trust < rules.MoodLowBelow)
            {
                return NegotiationLevel.Low;
            }

            return trust >= rules.MoodHighFrom ? NegotiationLevel.High : NegotiationLevel.Medium;
        }

        /// <summary>Sabır kademesi: sabır ≤ düşük eşik → düşük; ≤ orta eşik → orta; üstü yüksek.</summary>
        public static NegotiationLevel PatienceOf(NegotiationRules rules, int patience)
        {
            if (patience <= rules.PatienceLowAtMost)
            {
                return NegotiationLevel.Low;
            }

            return patience <= rules.PatienceMediumAtMost ? NegotiationLevel.Medium : NegotiationLevel.High;
        }
    }
}
