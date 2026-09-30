namespace Esnaf.Domain.Negotiation
{
    /// <summary>Pazarlığın aşaması. Deal ve Failed son durumlardır.</summary>
    public enum NegotiationPhase
    {
        /// <summary>Teklif verilebilir.</summary>
        Active = 0,

        /// <summary>Satıcının sabrı bitti: "Son fiyatım X, al ya da git". Yalnızca kabul ya da ayrılma kalır.</summary>
        FinalOffer = 1,

        /// <summary>Anlaşıldı.</summary>
        Deal = 2,

        /// <summary>Oyuncu masadan kalktı.</summary>
        Failed = 3
    }

    /// <summary>Bir turda oynanan koz kartının sonucu.</summary>
    public enum CardOutcome
    {
        None = 0,

        /// <summary>Kart doğruydu: satıcının ret fiyatı düştü, güven arttı.</summary>
        Effective = 1,

        /// <summary>Yanlış alarm: güven ve sabır kaybı.</summary>
        FalseAlarm = 2
    }
}
