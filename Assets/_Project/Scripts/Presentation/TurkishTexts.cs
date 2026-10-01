using System.Collections.Generic;
using System.Globalization;
using Esnaf.Core;

namespace Esnaf.Presentation
{
    /// <summary>
    /// Arayüz metinleri (Türkçe). Oyun kuralı değil, yalnızca gösterim: hata kodu → kullanıcı mesajı.
    /// Bilinmeyen kod için genel mesaj kodu da taşır (hata yutulmaz).
    /// </summary>
    public static class TurkishTexts
    {
        private static readonly Dictionary<string, string> ErrorMessages = new Dictionary<string, string>
        {
            { "listing.unknown", "Bu ilan artık yok." },
            { "appraisal.level_unknown", "Bilinmeyen ekspertiz seviyesi." },
            { "appraisal.level_locked", "Bu ekspertiz seviyesi henüz açılmadı." },
            { "appraisal.equipment_missing", "Bu ekspertiz için gerekli cihaz yok." },
            { "cash.insufficient", "Yeterli nakit yok." },
            { "negotiation.in_progress", "Önce süren pazarlığı bitirmelisin." },
            { "inventory.full", "Raf dolu." },
            { "negotiation.none", "Süren bir pazarlık yok." },
            { "negotiation.closed", "Bu pazarlık sona erdi." },
            { "negotiation.final_offer_only", "Satıcı son fiyatını söyledi; kabul et ya da kalk." },
            { "negotiation.no_final_offer", "Satıcı henüz son fiyat vermedi." },
            { "offer.invalid", "Geçersiz teklif." },
            { "card.unknown", "Bilinmeyen koz kartı." },
            { "card.already_used", "Bu koz kartı zaten kullanıldı." }
        };

        public static IReadOnlyCollection<string> KnownErrorCodes
        {
            get { return ErrorMessages.Keys; }
        }

        public static string Day(int day)
        {
            return "Gün " + day.ToString(CultureInfo.InvariantCulture);
        }

        public static string Cash(Money cash)
        {
            return "Nakit: " + MoneyFormatter.Format(cash);
        }

        public static string Error(string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                return "Bir sorun oluştu.";
            }

            string message;
            return ErrorMessages.TryGetValue(code, out message) ? message : "Bir sorun oluştu (" + code + ").";
        }
    }
}
