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
