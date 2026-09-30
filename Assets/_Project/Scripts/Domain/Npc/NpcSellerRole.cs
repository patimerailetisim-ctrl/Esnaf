namespace Esnaf.Domain.Npc
{
    /// <summary>
    /// Bir NPC'nin SATICI rolündeki sayıları (GDD v0.2 6.1). Değişmez tanım verisidir; içerik dosyasından gelir.
    /// Doğrulamayı <c>ContentValidator</c> yapar.
    /// </summary>
    public sealed class NpcSellerRole
    {
        /// <summary>Satıcı olarak ilk ilan verebildiği gün (v0.2 Bölüm 12 "yeni kişilik" sütunu).</summary>
        public int AvailableFromDay { get; }

        /// <summary>İstenen fiyat = satıcının inandığı değer × bu çarpan.</summary>
        public double AskMultiplier { get; }

        /// <summary>Ret oranı R: satıcının inandığı değerin bu oranından aşağıya asla satmaz.</summary>
        public double RejectRatio { get; }

        public int Patience { get; }

        /// <summary>Değer bilgisi hatasının üst sınırı (σ): |hata| ≤ σ.</summary>
        public double ValueSigma { get; }

        /// <summary>Sistematik hata (Hatice Teyze: −0,28 = ~%28 eksik bilir). |sapma| ≤ σ.</summary>
        public double ValueBias { get; }

        public double Urgency { get; }
        public double Persuasion { get; }

        /// <summary>Kusuru saklama olasılığı (yalnızca kusuru varsa anlamlı).</summary>
        public double ConcealChance { get; }

        /// <summary>P2: Gün 1–4'te ilanların %45'ini alan "öğrenme dostu" satıcılar (Aceleci, Bilgisiz, Dürüst).</summary>
        public bool LearningFriendly { get; }

        /// <summary>"Acil satış" etiketinin görüneceği ilk gün; etiketsiz satıcıda null.</summary>
        public int? UrgentLabelFromDay { get; }

        /// <summary>Yanlış koz kartının güven/sabır cezasının çarpanı (Dr. Murat 2; varsayılan 1).</summary>
        public double WrongCardPenaltyMultiplier { get; }

        public NpcSellerRole(
            int availableFromDay,
            double askMultiplier,
            double rejectRatio,
            int patience,
            double valueSigma,
            double valueBias,
            double urgency,
            double persuasion,
            double concealChance,
            bool learningFriendly,
            int? urgentLabelFromDay,
            double wrongCardPenaltyMultiplier = 1.0)
        {
            AvailableFromDay = availableFromDay;
            AskMultiplier = askMultiplier;
            RejectRatio = rejectRatio;
            Patience = patience;
            ValueSigma = valueSigma;
            ValueBias = valueBias;
            Urgency = urgency;
            Persuasion = persuasion;
            ConcealChance = concealChance;
            LearningFriendly = learningFriendly;
            UrgentLabelFromDay = urgentLabelFromDay;
            WrongCardPenaltyMultiplier = wrongCardPenaltyMultiplier;
        }
    }
}
