namespace Esnaf.Domain.Npc
{
    /// <summary>
    /// Bir NPC'nin MÜŞTERİ rolündeki sayıları (GDD v0.2 6.2). Bu gün yalnızca veri olarak yüklenir; müşteri akışı Gün 8'dedir.
    /// </summary>
    public sealed class NpcCustomerRole
    {
        /// <summary>Açılış teklifi, müşterinin en çok ödeyeceği tutarın (Max) bu oranıdır.</summary>
        public double OpeningOfferRatio { get; }

        /// <summary>mRatio: müşterinin değer çarpanı.</summary>
        public double ValueRatio { get; }

        public int Patience { get; }

        /// <summary>Müşterinin değeri yanlış görme payı (yalnızca belirtilenlerde; Hatice Teyze %30).</summary>
        public double ValueSigma { get; }

        /// <summary>Paketli (kutu/fatura) üründe ek çarpan (Nermin 1,12); diğerlerinde 1.</summary>
        public double PackageRatio { get; }

        public NpcCustomerRole(double openingOfferRatio, double valueRatio, int patience, double valueSigma, double packageRatio)
        {
            OpeningOfferRatio = openingOfferRatio;
            ValueRatio = valueRatio;
            Patience = patience;
            ValueSigma = valueSigma;
            PackageRatio = packageRatio;
        }
    }
}
