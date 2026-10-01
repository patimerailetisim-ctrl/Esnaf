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

        public const string ListingsTitle = "\u0130lanlar";
        public const string NoListings = "Bug\u00FCn ilan yok.";
        public const string EndDayButton = "G\u00FCn\u00FC Bitir";

        public static string Storage(int gigabytes)
        {
            return gigabytes.ToString(CultureInfo.InvariantCulture) + " GB";
        }

        public static string Age(int months)
        {
            return months.ToString(CultureInfo.InvariantCulture) + " ay";
        }

        public static string Asking(Money price)
        {
            return "\u0130stenen: " + MoneyFormatter.Format(price);
        }

        /// <summary>1 ve altı: ilan bugün kalkar ("Son gün"); aksi halde kalan gün sayısı.</summary>
        public static string Remaining(int days)
        {
            return days <= 1 ? "Son g\u00FCn" : days.ToString(CultureInfo.InvariantCulture) + " g\u00FCn kald\u0131";
        }

        public static string Box(bool present)
        {
            return present ? "Kutu: var" : "Kutu: yok";
        }

        public static string Invoice(bool present)
        {
            return present ? "Fatura: var" : "Fatura: yok";
        }

        public static string Seller(string name)
        {
            return "Sat\u0131c\u0131: " + name;
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
