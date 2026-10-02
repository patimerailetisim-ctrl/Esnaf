using Esnaf.Core;
using Esnaf.Domain.Negotiation;

namespace Esnaf.Presentation
{
    /// <summary>
    /// Müşterinin doğal Türkçe sözleri. SAF metindir: rastgelelik yok, oyun kuralı yok. Söz, oyundan dönen görünümden (aşama, fiyat,
    /// ruh hali, sabır, kişilik düzeyleri) seçilir; aynı girdi her zaman aynı cümleyi verir.
    /// </summary>
    public static class SaleDialogue
    {
        /// <summary>
        /// Müşterinin aksesuar talebi (telefon anlaşmasından sonra): istediği aksesuarların adlarından doğal Türkçe söz. Kişiliğe göre ton değişir;
        /// SAF metindir (rastgelelik yok). 0 aksesuar için boş döner.
        /// </summary>
        public static string AccessoryRequest(string personalityId, System.Collections.Generic.IReadOnlyList<string> accessoryNames)
        {
            if (accessoryNames == null || accessoryNames.Count == 0)
            {
                return string.Empty;
            }

            string list = JoinNames(accessoryNames);
            if (accessoryNames.Count == 1)
            {
                switch (personalityId)
                {
                    case "hurried":
                        return "Tamam abi, \u00E7abuk olal\u0131m. " + Capitalize(list) + " da var m\u0131?";
                    case "indecisive":
                        return "Hmm\u2026 bir de " + list + " olsa iyi olurdu galiba.";
                    case "budget_limited":
                        return "B\u00FCt\u00E7em dar ama bir " + list + " bakabilir miyim abi?";
                    default:
                        return "Telefon tamam abi. Bir de " + list + " var m\u0131?";
                }
            }

            switch (personalityId)
            {
                case "hurried":
                    return "Tamam abi, acelem var. " + Capitalize(list) + " lazım, hemen verir misin?";
                case "showoff":
                    return "Abi bunu tam tak\u0131m yapal\u0131m: " + list + ".";
                case "budget_limited":
                    return "B\u00FCt\u00E7em dar ama " + list + " da laz\u0131m, bakar m\u0131s\u0131n abi?";
                default:
                    return accessoryNames.Count == 2
                        ? "Abi bir de " + list + " alay\u0131m."
                        : "Abi bir de " + list + " laz\u0131m.";
            }
        }

        // "kılıf", "kılıf ve kablo", "kılıf, cam ve kablo": adlar küçük harfe çevrilir (Türkçe İ/I kuralıyla).
        private static string JoinNames(System.Collections.Generic.IReadOnlyList<string> names)
        {
            var lower = new System.Collections.Generic.List<string>();
            foreach (string name in names)
            {
                lower.Add(TurkishLower(name));
            }

            if (lower.Count == 1)
            {
                return lower[0];
            }

            string head = string.Join(", ", lower.GetRange(0, lower.Count - 1));
            return head + " ve " + lower[lower.Count - 1];
        }

        private static string TurkishLower(string text)
        {
            return text.Replace('I', '\u0131').Replace('\u0130', 'i').ToLowerInvariant();
        }

        private static string Capitalize(string text)
        {
            return text.Length == 0 ? text : text.Substring(0, 1).ToUpperInvariant() + text.Substring(1);
        }

        public static string Greeting(string personalityId, string model)
        {
            switch (personalityId)
            {
                case "haggler":
                    return "Selam abi, şu " + model + "'e bir bakayım. Ama baştan söyleyeyim, pazarlık yaparım.";
                case "hurried":
                    return "Abi selam, acelem var. Şu " + model + "'e bakabilir miyim?";
                case "indecisive":
                    return "Merhaba… şu " + model + "'e bakabilir miyim? Bir türlü karar veremiyorum da.";
                case "tech_enthusiast":
                    return "Selam abi, şu " + model + "'in özelliklerine bakabilir miyim?";
                case "price_focused":
                    return "Abi selam, şu " + model + "'in fiyatına bir bakayım.";
                case "easygoing":
                    return "Selam abi, hayırlı işler. Şu " + model + "'e bakabilir miyim?";
                case "informed_buyer":
                    return "Merhaba, şu " + model + "'i inceleyebilir miyim? Biraz araştırdım.";
                case "showoff":
                    return "Abi selam, şu " + model + " güzel durmuş, bakabilir miyim?";
                case "budget_limited":
                    return "Abi selam, bütçem dar ama şu " + model + "'e bakabilir miyim?";
                case "trust_seeker":
                    return "Merhaba abi, şu " + model + " temiz mi, bir bakabilir miyim?";
                default:
                    return "Abi selam, şu " + model + "'e bakabilir miyim?";
            }
        }

        /// <summary>Oyuncu "buyur" dedikten sonra: bakış yorumu + fiyat sorusu.</summary>
        public static string AfterGreeting(NegotiationLevel knowledge)
        {
            string look = knowledge == NegotiationLevel.High ? "Temiz duruyor. Pil nasıl bunun?" : "Fena durmuyor.";
            return look + "\nKaç yazdın buna?";
        }

        /// <summary>Oyuncunun fiyat cevabından sonra müşterinin sözü (yalnızca dönen görünümden).</summary>
        public static string AfterAsk(NegotiationPhase phase, Money shown, bool tooExpensive, NegotiationLevel mood, NegotiationLevel patience, NegotiationLevel haggling)
        {
            string price = MoneyFormatter.Format(shown);
            if (phase == NegotiationPhase.FinalOffer)
            {
                return "Son sözüm " + price + " abi. Olursa alırım, olmazsa gideyim.";
            }

            if (tooExpensive)
            {
                return haggling == NegotiationLevel.High
                    ? "Ya abi, ciddi misin? " + price + " veririm, o kadar."
                    : "Yok abi, bu çok fazla. " + price + "'ye bakarım.";
            }

            if (patience == NegotiationLevel.Low)
            {
                return "Abi vaktim yok, " + price + " diyorum.";
            }

            return mood == NegotiationLevel.High
                ? "Güzel telefon ama " + price + "'ye ne dersin?"
                : "Hmm… " + price + " olsa alırım.";
        }

        public static string AfterReport(NegotiationLevel mood)
        {
            return mood == NegotiationLevel.Low ? "Hmm, rapor fena değil." : "Rapor güzelmiş, içim rahatladı.";
        }

        public static string Deal(Money price)
        {
            return "Tamamdır abi, " + MoneyFormatter.Format(price) + "'ye alıyorum. Hayırlı olsun!";
        }

        public static string Left()
        {
            return "Peki abi, başka sefere. İyi çalışmalar.";
        }
    }
}
