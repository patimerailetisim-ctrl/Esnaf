using System.Collections.Generic;
using System;
using System.Globalization;
using Esnaf.Core;
using Esnaf.Domain.Appraisal;
using Esnaf.Domain.Negotiation;

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
            { "offer.invalid", "Geçersiz teklif: pozitif ve 10 ₺'nin katı olmalı." },
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

        public const string NegotiationTitle = "Pazarl\u0131k";
        public const string MakeOfferButton = "Teklif Ver";
        public const string UseCardButton = "Koz Kullan";
        public const string AcceptFinalButton = "Son Fiyat\u0131 Kabul Et";
        public const string WalkAwayButton = "Vazge\u00E7";
        public const string OfferLabel = "Teklifin (\u20BA):";
        public const string CardsHeader = "Koz kartlar\u0131";
        public const string NoCardSelected = "\u00D6nce bir koz kart\u0131 se\u00E7.";
        public const string NoCards = "Elinde koz kart\u0131 yok (ekspertiz yapt\u0131r\u0131nca kartlar \u00E7\u0131kabilir).";
        public const string OfferEmpty = "Bir teklif tutar\u0131 gir.";
        public const string OfferNotNumber = "Teklif yaln\u0131zca rakamlardan olu\u015Fmal\u0131.";
        public const string OfferNegative = "Teklif negatif olamaz.";
        public const string OfferNotPositive = "Teklif s\u0131f\u0131rdan b\u00FCy\u00FCk olmal\u0131.";
        public const string OfferTooLarge = "Teklif \u00E7ok b\u00FCy\u00FCk.";
        public const string Insulted = "Teklifin sat\u0131c\u0131y\u0131 g\u00FCcendirdi.";
        public const string WalkedAway = "Pazarl\u0131ktan vazge\u00E7tin; ilan pazardan kalkt\u0131.";

        public static string AskingLine(Money price)
        {
            return "\u0130lan fiyat\u0131: " + MoneyFormatter.Format(price);
        }

        public static string ShownPriceLine(Money price)
        {
            return "Sat\u0131c\u0131n\u0131n g\u00FCncel fiyat\u0131: " + MoneyFormatter.Format(price);
        }

        public static string CashLine(Money cash)
        {
            return "Nakdin: " + MoneyFormatter.Format(cash);
        }

        public static string Round(int round)
        {
            return "Tur: " + round.ToString(CultureInfo.InvariantCulture);
        }

        public static string YourOffer(Money offer)
        {
            return "Son teklifin: " + MoneyFormatter.Format(offer);
        }

        public static string ReplyOpening(Money price)
        {
            return "Sat\u0131c\u0131: \u0130stedi\u011Fim fiyat " + MoneyFormatter.Format(price) + ".";
        }

        public static string ReplyCounter(Money price)
        {
            return "Sat\u0131c\u0131 kar\u015F\u0131 teklif verdi: " + MoneyFormatter.Format(price) + ".";
        }

        public static string ReplyHolding(Money price)
        {
            return "Sat\u0131c\u0131 fiyat\u0131nda direniyor: " + MoneyFormatter.Format(price) + ".";
        }

        public static string ReplyFinal(Money price)
        {
            return "Sat\u0131c\u0131: Son fiyat\u0131m " + MoneyFormatter.Format(price) + ". Kabul et ya da kalk.";
        }

        /// <summary>
        /// Satıcının turdan sonraki cevabı, YALNIZCA dönen görünümden anlatılır: son fiyat geldiyse "son fiyatım", fiyat önceki fiyattan düştüyse
        /// "karşı teklif", düşmediyse "direniyor". Karar oyundadır; bu yalnızca metindir.
        /// </summary>
        public static string Reply(NegotiationPhase phase, Money shown, Money previousShown)
        {
            if (phase == NegotiationPhase.FinalOffer)
            {
                return ReplyFinal(shown);
            }

            return shown < previousShown ? ReplyCounter(shown) : ReplyHolding(shown);
        }

        public static string Purchased(string model, Money price, int shelfCount, int shelfCapacity)
        {
            return "Sat\u0131n al\u0131nd\u0131: " + model + " \u2014 " + MoneyFormatter.Format(price) + ". Rafa eklendi ("
                + shelfCount.ToString(CultureInfo.InvariantCulture) + "/" + shelfCapacity.ToString(CultureInfo.InvariantCulture) + ").";
        }

        public static string Phase(NegotiationPhase phase)
        {
            switch (phase)
            {
                case NegotiationPhase.Active:
                    return "Pazarl\u0131k s\u00FCr\u00FCyor";
                case NegotiationPhase.FinalOffer:
                    return "Sat\u0131c\u0131 son fiyat\u0131n\u0131 s\u00F6yledi";
                case NegotiationPhase.Deal:
                    return "Anla\u015F\u0131ld\u0131";
                default:
                    return "Pazarl\u0131k sona erdi";
            }
        }

        public static string Level(NegotiationLevel level)
        {
            switch (level)
            {
                case NegotiationLevel.Low:
                    return "D\u00FC\u015F\u00FCk";
                case NegotiationLevel.Medium:
                    return "Orta";
                default:
                    return "Y\u00FCksek";
            }
        }

        public static string MoodLine(NegotiationLevel mood)
        {
            return "Sat\u0131c\u0131n\u0131n ruh hali: " + Level(mood);
        }

        public static string PatienceLine(NegotiationLevel patience)
        {
            return "Sab\u0131r: " + Level(patience);
        }

        /// <summary>Koz kartı satırı: bulgunun adı, güveni ve ürün değerindeki etkisi (ekspertiz kartından); kullanıldıysa işaretlenir.</summary>
        public static string Card(string wordingKey, string attribute, AppraisalConfidence confidence, Money problemValue, bool used)
        {
            string line = Finding(true, wordingKey, attribute, confidence) + " \u2014 sorun de\u011Feri " + MoneyFormatter.Format(problemValue);
            return used ? line + " (kullan\u0131ld\u0131)" : line;
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
