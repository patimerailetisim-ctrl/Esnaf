namespace Esnaf.Domain.Npc
{
    /// <summary>
    /// Müşteri kişiliğinin gerçek mekaniğe bağlı boyutları. Her boyutun düzeyi (düşük/orta/yüksek) NPC'nin mevcut sayılarından okunur:
    /// sabır = müşteri sabrı, aciliyet = seller.urgency, bilgi = seller.valueSigma (küçük = bilgili), bütçe esnekliği = customer.valueRatio,
    /// pazarlık toleransı = customer.openingOfferRatio (küçük = daha çok pazarlık).
    /// </summary>
    public enum CustomerTrait
    {
        Patience = 0,
        Urgency = 1,
        Knowledge = 2,
        Budget = 3,
        Haggling = 4
    }
}
