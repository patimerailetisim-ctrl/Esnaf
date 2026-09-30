using System.Collections.Generic;
using Esnaf.Core;
using Esnaf.Domain.Appraisal;
using Esnaf.Domain.Business;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Market;
using Esnaf.Domain.Negotiation;

namespace Esnaf.Domain.Game
{
    /// <summary>
    /// Oyuna girişin TEK kapısı (GDD K1, T6): UI, simülasyon aracı ve testler oyunu buradan oynar.
    /// Komut = niyet; <see cref="Result"/> döner, başarısızlıkta durum değişmez (istisna: bkz. <see cref="DayEndPipeline"/> notu).
    /// Sorgu = salt okunur; durum değiştirmez, kopya/değişmez görünüm döner (K3).
    /// Komutlar kendi günlerinde eklenir: Gün 5 günü bitir, Gün 6 ekspertiz, Gün 7 alış pazarlığı.
    /// Kayıt (Capture/Restore) bu arayüzden geçmez (UA2).
    /// </summary>
    public interface IGameApi
    {
        // ---- komutlar ----

        /// <summary>Günü bitirir: gün sonu adımları çalışır (gider düşer, ilanlar yaşlanır), yeni gün ve yeni ilanlar başlar.</summary>
        Result<DayEndReport> EndDay();

        /// <summary>
        /// İlandaki ürünü ekspertize sokar (Gün 6). Ücret anında ödenir ve "bekleyen"dir; aynı ilan + aynı seviye aynı kilitli sonucu
        /// verir ve ikinci kez ücret almaz (I5). Hatalar: listing.unknown, appraisal.level_unknown / level_locked / equipment_missing, cash.insufficient.
        /// </summary>
        Result<AppraisalView> StartAppraisal(long listingId, string levelId);

        /// <summary>
        /// İlandaki ürün için alış pazarlığı açar (Gün 7). Aynı anda tek pazarlık olur; sürerken gün bitirilemez.
        /// Hatalar: listing.unknown, negotiation.in_progress, inventory.full.
        /// </summary>
        Result<NegotiationView> StartNegotiation(long listingId);

        /// <summary>
        /// Süren pazarlıkta teklif verir. Teklif satıcının o anki fiyatına ulaşırsa anlaşılır ve ürün rafa girer (fiyat teklife değil satıcının fiyatınadır).
        /// Hatalar: negotiation.none / closed / final_offer_only, offer.invalid, cash.insufficient, inventory.full.
        /// </summary>
        Result<NegotiationView> MakeOffer(Money offer);

        /// <summary>Teklifle birlikte bir koz kartı oynar (kart, ekspertiz sonucundan: AppraisalId + CardIndex). Hatalar: MakeOffer'inkiler + card.unknown, card.already_used.</summary>
        Result<NegotiationView> MakeOfferWithCard(Money offer, long appraisalId, int cardIndex);

        /// <summary>Satıcının "son fiyatım" teklifini (sabır bitince) kabul eder. Hata: negotiation.no_final_offer, cash.insufficient.</summary>
        Result<NegotiationView> AcceptFinalPrice();

        /// <summary>Masadan kalkar: pazarlık biter, ilan kalkar, bekleyen ekspertiz ücreti gider yazılır.</summary>
        Result<NegotiationView> WalkAway();

        /// <summary>Raftaki ürüne etiket fiyatı koyar/değiştirir (Gün 8). Etiketsiz ürüne müşteri ilgilenmez. Hatalar: instance.unknown, instance.not_in_inventory, price.invalid.</summary>
        Result SetPrice(long instanceId, Money price);

        /// <summary>
        /// Gelen bir müşteriyle satış pazarlığı açar. Aynı anda tek pazarlık (alış ya da satış) olur; sürerken gün bitirilemez.
        /// Hatalar: customer.unknown, customer.no_interest, negotiation.in_progress.
        /// </summary>
        Result<SaleView> StartSale(long customerId);

        /// <summary>
        /// Süren satışta bir fiyat İSTER. İstenen fiyat müşterinin yeni teklifine eşit/altındaysa anlaşılır ve satış müşterinin teklifi üzerinden olur.
        /// Hatalar: sale.none, negotiation.closed / final_offer_only, ask.invalid.
        /// </summary>
        Result<SaleView> AskPrice(Money ask);

        /// <summary>"Rapor göster": S2/S3 raporu müşterinin değer hatasını yarıya indirir, güvenini artırır. Hatalar: sale.none, report.unknown / not_eligible / already_shown.</summary>
        Result<SaleView> ShowReport(long appraisalId);

        /// <summary>Müşterinin "son teklifim" fiyatını (sabır bitince) kabul eder. Hata: negotiation.no_final_offer.</summary>
        Result<SaleView> AcceptCustomerFinalOffer();

        /// <summary>Müşteriyi yolcu eder: pazarlık biter, ürün rafta kalır, müşteri o gün geri gelmez.</summary>
        Result<SaleView> LetCustomerGo();

        // ---- sorgular ----

        /// <summary>Şu an dükkânda olan ve bir ürünle ilgilenen müşteriler (gizli değer yok).</summary>
        IReadOnlyList<CustomerView> GetCustomers();

        /// <summary>Süren satış pazarlığının görünümü; yoksa null.</summary>
        SaleView GetSale();

        /// <summary>Süren pazarlığın görünümü (ruh hali/sabır kademesi, koz kartları); pazarlık yoksa null.</summary>
        NegotiationView GetNegotiation();

        int GetDay();

        Money GetCash();

        /// <summary>Şu an pazardaki ilanların görünür bilgisi (gizli bilgi yok), pazardaki sırayla.</summary>
        IReadOnlyList<ListingView> GetListings();

        /// <summary>Dükkândaki ürünler ve maliyet tabanları.</summary>
        IReadOnlyList<StockLine> GetInventory();

        /// <summary>Bir ilanın ürünü için bilinen ekspertiz sonuçları (sonuç sırasıyla); ilan yoksa boş.</summary>
        IReadOnlyList<AppraisalView> GetAppraisals(long listingId);

        /// <summary>Bir ekspertiz sonucu ve teklif tutarı için risk kartı (üç senaryo, aralık dışı kalma olasılığı).</summary>
        Result<RiskCard> GetRiskCard(long appraisalId, Money offer);

        /// <summary>Bugünün (şu ana kadarki) özeti. Biten günün özeti <see cref="DayEndReport.Summary"/>'dedir.</summary>
        DaySummary GetTodaySummary();

        /// <summary>Durumun 16 haneli özeti (I6 determinizm testi ve simülasyon için).</summary>
        string GetStateDigest();
    }
}
