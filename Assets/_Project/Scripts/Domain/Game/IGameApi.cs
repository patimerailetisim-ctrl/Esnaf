using System.Collections.Generic;
using Esnaf.Core;
using Esnaf.Domain.Appraisal;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Market;

namespace Esnaf.Domain.Game
{
    /// <summary>
    /// Oyuna girişin TEK kapısı (GDD K1, T6): UI, simülasyon aracı ve testler oyunu buradan oynar.
    /// Komut = niyet; <see cref="Result"/> döner, başarısızlıkta durum değişmez (istisna: bkz. <see cref="DayEndPipeline"/> notu).
    /// Sorgu = salt okunur; durum değiştirmez, kopya/değişmez görünüm döner (K3).
    /// Day 5 kapsamı: yalnızca "Günü Bitir" komutu ve temel sorgular. Alış/pazarlık/ekspertiz komutları kendi günlerinde eklenir.
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

        // ---- sorgular ----

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
