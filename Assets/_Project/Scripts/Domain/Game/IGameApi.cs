using System.Collections.Generic;
using Esnaf.Core;
using Esnaf.Domain.Wholesale;
using Esnaf.Domain.Accessories;
using Esnaf.Domain.Appraisal;
using Esnaf.Domain.Business;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Market;
using Esnaf.Domain.Negotiation;
using Esnaf.Domain.Time;

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

        /// <summary>
        /// Pazarlıksız alış (Gün 10 Adım 6, kullanıcı kararı): ilanı İSTENEN fiyattan satın alıp rafa koyar; döndürülen değer ödenen fiyattır.
        /// Alış hattı pazarlıktakiyle aynıdır (defter, raf, bekleyen ekspertiz ücretinin maliyete eklenmesi, olaylar, NPC kaydı); rastgelelik kullanmaz.
        /// Pazarlık/satış sürerken yapılamaz. Hatalar: negotiation.in_progress, listing.unknown, inventory.full, cash.insufficient (durum değişmez).
        /// </summary>
        Result<Money> BuyListing(long listingId);

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

        /// <summary>
        /// Müşterinin ŞU ANKİ teklifini aynen kabul eder (fiyat = müşterinin teklifi; satış normal akıştan tamamlanır). Müşteri bir teklif vermiş olmalı
        /// (son teklif ya da en az bir tur). Hatalar: sale.none, negotiation.no_offer (müşteri henüz teklif vermedi), negotiation.closed.
        /// </summary>
        Result<SaleView> AcceptCustomerOffer();

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

        /// <summary>
        /// Günlük müşteri kuyruğu (Gün 12.2): bugünün 8–12 müşterisi, geliş saatleri ve durumları; aynı anda en çok 1 aktif müşteri (<c>Current</c>). Mağaza saatine bağlıdır
        /// (<see cref="GetClock"/>); kapalıyken yeni müşteri çağrılmaz. Durumu değiştirmez.
        /// </summary>
        CustomerQueueView GetCustomerQueue();

        /// <summary>
        /// Günlük kuyruğun şu an aktif müşterisi (Gün 12.3), mevcut müşteri görünümüyle (kimlik, NPC, ilgilendiği ürün, kişilik profili): geliş saati geldiyse ve mağaza açıksa;
        /// yoksa null. <c>InstanceId</c> 0 ise ilgilendiği ürün yoktur (<see cref="CompleteCurrentCustomer"/> ile gönderilir). Satış <see cref="StartSale"/> ile (kimliğiyle)
        /// başlar; satış bitince (anlaşma ya da ayrılma) müşteri kuyrukta kendiliğinden tamamlanır. Eski lobiden (<see cref="GetCustomers"/>) bağımsızdır; durumu değiştirmez.
        /// </summary>
        CustomerView GetActiveCustomer();

        /// <summary>
        /// Aktif müşteriyi tamamlar ve sıradakine geçer (mağaza kapalıysa kalanlar gönderilir). Süren bir satış/pazarlık varken tamamlanamaz.
        /// Hatalar: queue.no_active_customer, queue.sale_in_progress. Zaman maliyeti bu adımda yoktur.
        /// </summary>
        Result<CustomerQueueView> CompleteCurrentCustomer();

        /// <summary>Günün saati (Gün 12.1): 09:00 açılış, 21:00 kapanış; gerçek zamanlı değil, oyun aksiyonlarıyla ilerler. Durumu değiştirmez.</summary>
        ClockView GetClock();

        /// <summary>
        /// Saati <paramref name="minutes"/> dakika ilerletir (oyun aksiyonu karşılığı); kapanışı aşacak kadarsa 21:00'de durur. Gün BİTMEZ ("Günü Bitir" ayrıdır).
        /// Hatalar: time.invalid (dakika ≤ 0), time.store_closed (saat zaten 21:00).
        /// </summary>
        Result<ClockView> AdvanceTime(int minutes);

        Money GetCash();

        /// <summary>Şu an pazardaki ilanların görünür bilgisi (gizli bilgi yok), pazardaki sırayla.</summary>
        IReadOnlyList<ListingView> GetListings();

        /// <summary>Dükkândaki ürünler ve maliyet tabanları.</summary>
        IReadOnlyList<StockLine> GetInventory();

        /// <summary>
        /// Toptancı teklifleri (Gün 11.2.3), dosyadaki sırayla. Kilitli teklifler de listelenir (<c>IsAvailableToday</c> = false); gizli bilgi yoktur.
        /// Kaynak: WholesaleCatalog + bugünün günü.
        /// </summary>
        IReadOnlyList<WholesaleOfferView> GetWholesaleOffers();

        /// <summary>
        /// Toptancıdan bir PAKET alır (miktar teklifin packSize'ı), bugünün günüyle. Tek kaynak WholesaleService'tir; atomiktir.
        /// Hatalar: supplier.unknown, accessory.unknown, offer.unknown, wholesale.not_available_yet, cash.insufficient, stock.full, amount.invalid.
        /// </summary>
        Result<WholesalePurchaseReceipt> BuyWholesalePack(string supplierId, string accessoryId);

        /// <summary>Aksesuar stoğu: kalemler, toplam birim ve maliyet, kapasite. Telefon rafından (GetInventory) ayrıdır.</summary>
        AccessoryStockView GetAccessoryStock();

        /// <summary>
        /// Aksesuar ek satış fırsatı (Gün 11.3.2): bugünün SON tamamlanmış telefon satışı (<c>PhoneSaleRecordId</c>) ve ona eklenebilecek aksesuarlar
        /// (sabit fiyat, stok). Yalnızca MÜŞTERİNİN İSTEDİĞİ aksesuarlar listelenir (Gün 11.3.4; en çok 5, her biri 1 adet); müşteri bir şey istemediyse
        /// <c>HasRequest</c> false ve seçenek yoktur. Bugün tamamlanmış telefon satışı yoksa <c>HasPhoneSale</c> false. Durumu değiştirmez.
        /// </summary>
        AccessoryAddOnView GetAccessoryAddOns();

        /// <summary>
        /// Bugünün son tamamlanmış telefon satışına BİR aksesuar ek satar (stoktan 1 adet, sabit retailPrice, gerçek maliyet). Telefon satışı tekrarlanmaz.
        /// Yalnızca müşterinin istediği aksesuar satılır. Hatalar: addon.no_sale (bugün tamamlanmış telefon satışı yok), accessory.unknown,
        /// addon.not_requested (müşteri aksesuar istemedi), addon.not_in_request (bu aksesuarı istemedi), addon.request_limit (istediği adet zaten satıldı),
        /// stock.insufficient, amount.invalid, ledger hataları.
        /// </summary>
        Result<AccessorySaleReceipt> SellAccessoryAddOn(string accessoryId);

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
