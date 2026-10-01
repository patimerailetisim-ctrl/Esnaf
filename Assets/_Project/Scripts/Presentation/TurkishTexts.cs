using System.Collections.Generic;
using System;
using System.Globalization;
using Esnaf.Core;
using Esnaf.Domain.Appraisal;

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
            { "card.already_used", "Bu koz kartı zaten kullanıldı." },
            { "appraisal.no_value_range", "Bu seviye değer aralığı vermediği için risk kartı yok." },
            { "appraisal.unknown", "Bilinmeyen ekspertiz sonucu." }
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

        public const string BackButton = "Geri";
        public const string AppraisalButton = "Ekspertiz";
        public const string NegotiationButton = "Pazarl\u0131k";
        public const string NegotiationComingSoon = "Pazarl\u0131k bir sonraki ad\u0131mda eklenecek.";

        public static string DetailStorage(int gigabytes)
        {
            return "Depolama: " + Storage(gigabytes);
        }

        public static string DetailAge(int months)
        {
            return "Ya\u015F: " + Age(months);
        }

        public static string DetailPrice(Money price)
        {
            return "\u0130stenen fiyat: " + MoneyFormatter.Format(price);
        }

        public static string DetailRemaining(int days)
        {
            return "S\u00FCre: " + Remaining(days);
        }

        public const string AppraisalTitle = "Ekspertiz";
        public const string LevelsHeader = "Seviyeler";
        public const string ResultHeader = "Sonu\u00E7";
        public const string ActionPerformAppraisal = "Ekspertiz Yapt\u0131r";
        public const string ActionShowAppraisal = "Sonucu G\u00F6ster";
        public const string NoLevelSelected = "\u00D6nce bir ekspertiz seviyesi se\u00E7.";
        public const string LevelNotDone = "Bu seviye hen\u00FCz yap\u0131lmad\u0131.";
        public const string LevelDone = "Yap\u0131ld\u0131";
        public const string LevelReady = "Haz\u0131r";
        public const string LevelNeedsEquipment = "Cihaz gerekir";

        public static string LevelFee(Money fee)
        {
            return fee.IsZero ? "\u00DCcretsiz" : "\u00DCcret: " + MoneyFormatter.Format(fee);
        }

        public static string LevelLocked(int unlockDay)
        {
            return "Kilitli (G\u00FCn " + unlockDay.ToString(CultureInfo.InvariantCulture) + "'te a\u00E7\u0131l\u0131r)";
        }

        /// <summary>Bulgu satırı. Bulunmadıysa asla "sorun yok" denmez, yalnızca "sorun görünmüyor" (GDD: ekspertiz kesinlik vermez).</summary>
        public static string Finding(bool found, string wordingKey, string attribute, AppraisalConfidence confidence)
        {
            string text;
            if (found)
            {
                text = FindingWording(wordingKey);
            }
            else
            {
                text = AttributeName(attribute) + ": sorun g\u00F6r\u00FCnm\u00FCyor";
            }

            return "\u2022 " + text + " \u2014 " + ConfidenceName(confidence);
        }

        private static string FindingWording(string wordingKey)
        {
            switch (wordingKey)
            {
                case "appraisal.finding.screen_replaced":
                    return "Ekran de\u011Fi\u015Ftirilmi\u015F olabilir";
                case "appraisal.finding.camera_problem":
                    return "Kamerada sorun olabilir";
                default:
                    return wordingKey;
            }
        }

        private static string AttributeName(string attribute)
        {
            switch (attribute)
            {
                case "screen":
                    return "Ekran";
                case "camera":
                    return "Kamera";
                default:
                    return attribute;
            }
        }

        private static string ConfidenceName(AppraisalConfidence confidence)
        {
            switch (confidence)
            {
                case AppraisalConfidence.Hint:
                    return "ipucu";
                case AppraisalConfidence.Low:
                    return "d\u00FC\u015F\u00FCk g\u00FCven";
                case AppraisalConfidence.Medium:
                    return "orta g\u00FCven";
                default:
                    return "kesin";
            }
        }

        public static string Battery(NumericRange range)
        {
            return range == null ? "Pil: bu seviyede \u00F6l\u00E7\u00FClmez" : "Pil: " + PercentRange(range);
        }

        public static string Body(NumericRange range)
        {
            return range == null ? "G\u00F6vde: bu seviyede \u00F6l\u00E7\u00FClmez" : "G\u00F6vde: " + PercentRange(range);
        }

        private static string PercentRange(NumericRange range)
        {
            return "%" + range.Min.ToString(CultureInfo.InvariantCulture) + "\u2013%" + range.Max.ToString(CultureInfo.InvariantCulture);
        }

        public static string ValueRange(MoneyRange range)
        {
            return range == null
                ? "Tahmini de\u011Fer: bu seviyede verilmez"
                : "Tahmini de\u011Fer: " + MoneyFormatter.Format(range.Min) + " \u2013 " + MoneyFormatter.Format(range.Max);
        }

        public static string PaidFee(Money fee)
        {
            return "\u00D6denen \u00FCcret: " + MoneyFormatter.Format(fee);
        }

        public static string RiskTitle(Money offer)
        {
            return "Risk kart\u0131 (istenen fiyatla al\u0131rsan: " + MoneyFormatter.Format(offer) + ")";
        }

        public static string RiskScenarioLine(RiskScenario scenario)
        {
            return ScenarioName(scenario.Name) + ": de\u011Fer " + MoneyFormatter.Format(scenario.TrueValue)
                + ", beklenen sat\u0131\u015F " + MoneyFormatter.Format(scenario.ExpectedSale)
                + ", k\u00E2r/zarar " + MoneyFormatter.Format(scenario.Profit);
        }

        private static string ScenarioName(string name)
        {
            switch (name)
            {
                case "bad":
                    return "K\u00F6t\u00FC";
                case "mid":
                    return "Orta";
                case "good":
                    return "\u0130yi";
                default:
                    return name;
            }
        }

        public static string MissProbability(double probability)
        {
            int percent = (int)Math.Round(probability * 100.0, MidpointRounding.AwayFromZero);
            return "Aral\u0131k d\u0131\u015F\u0131 kalma olas\u0131l\u0131\u011F\u0131: %" + percent.ToString(CultureInfo.InvariantCulture);
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
