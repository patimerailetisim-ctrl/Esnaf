namespace Esnaf.Domain.Negotiation
{
    /// <summary>
    /// Alış pazarlığının kuralları (negotiation_rules.json; GDD v0.2 7.1–7.4). Değişmez veridir; motor hiçbir sayıyı kodda taşımaz.
    /// </summary>
    public sealed class NegotiationRules
    {
        /// <summary>Satıcının turdaki indirim payı t = baz + güven × (güven/100) + aciliyet × aciliyet.</summary>
        public double PriceBaseShare { get; }

        public double PriceTrustShare { get; }
        public double PriceUrgencyShare { get; }

        /// <summary>Teklif &lt; oran × R ise hakaret.</summary>
        public double InsultRatio { get; }

        public int InsultTrustLoss { get; }
        public int InsultExtraPatienceLoss { get; }

        /// <summary>Hakaretin ceza vermeye başladığı gün (öncesi yalnızca uyarıdır).</summary>
        public int InsultPenaltyFromDay { get; }

        /// <summary>Teklif ≥ oran × R ise "yakın teklif": güven artar.</summary>
        public double NearOfferRatio { get; }

        public int NearOfferTrustGain { get; }
        public int CardCorrectTrustGain { get; }
        public int CardWrongTrustLoss { get; }
        public int CardWrongPatienceLoss { get; }

        /// <summary>Bu seviyenin raporu "profesyonel rapor" sayılır (kart etkisine ek ikna payı).</summary>
        public string ReportLevelId { get; }

        public double ReportPersuasionBonus { get; }

        /// <summary>Koz kartları R'yi gerçek değerin bu oranının altına indiremez (taban).</summary>
        public double RejectFloorRatio { get; }

        public int StartTrust { get; }
        public int StartTrustSpread { get; }

        /// <summary>Satıcının o günkü ruh hali: R, ±bu oranda oynar.</summary>
        public double RejectMoodSwing { get; }

        public int MoodLowBelow { get; }
        public int MoodHighFrom { get; }
        public int PatienceLowAtMost { get; }
        public int PatienceMediumAtMost { get; }

        public NegotiationRules(
            double priceBaseShare,
            double priceTrustShare,
            double priceUrgencyShare,
            double insultRatio,
            int insultTrustLoss,
            int insultExtraPatienceLoss,
            int insultPenaltyFromDay,
            double nearOfferRatio,
            int nearOfferTrustGain,
            int cardCorrectTrustGain,
            int cardWrongTrustLoss,
            int cardWrongPatienceLoss,
            string reportLevelId,
            double reportPersuasionBonus,
            double rejectFloorRatio,
            int startTrust,
            int startTrustSpread,
            double rejectMoodSwing,
            int moodLowBelow,
            int moodHighFrom,
            int patienceLowAtMost,
            int patienceMediumAtMost)
        {
            PriceBaseShare = priceBaseShare;
            PriceTrustShare = priceTrustShare;
            PriceUrgencyShare = priceUrgencyShare;
            InsultRatio = insultRatio;
            InsultTrustLoss = insultTrustLoss;
            InsultExtraPatienceLoss = insultExtraPatienceLoss;
            InsultPenaltyFromDay = insultPenaltyFromDay;
            NearOfferRatio = nearOfferRatio;
            NearOfferTrustGain = nearOfferTrustGain;
            CardCorrectTrustGain = cardCorrectTrustGain;
            CardWrongTrustLoss = cardWrongTrustLoss;
            CardWrongPatienceLoss = cardWrongPatienceLoss;
            ReportLevelId = reportLevelId;
            ReportPersuasionBonus = reportPersuasionBonus;
            RejectFloorRatio = rejectFloorRatio;
            StartTrust = startTrust;
            StartTrustSpread = startTrustSpread;
            RejectMoodSwing = rejectMoodSwing;
            MoodLowBelow = moodLowBelow;
            MoodHighFrom = moodHighFrom;
            PatienceLowAtMost = patienceLowAtMost;
            PatienceMediumAtMost = patienceMediumAtMost;
        }
    }
}
