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
            { "appraisal.unknown", "Bilinmeyen ekspertiz sonucu." },
            { "price.invalid", "Bu ilanın fiyatı geçersiz." },
            { "sale.none", "Süren bir satış yok." },
            { "customer.unknown", "Bu müşteri artık dükkânda değil." },
            { "customer.no_interest", "Müşteri rafta ilgilendiği bir ürün bulamadı." },
            { "ask.invalid", "Geçersiz fiyat: pozitif ve 10 ₺'nin katı olmalı." },
            { "report.unknown", "Bu rapor bu ürüne ait değil." },
            { "report.not_eligible", "Bu seviyedeki rapor müşteriye gösterilemez." },
            { "report.already_shown", "Raporu zaten gösterdin." }
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

        /// <summary>"31 Aylık" (üst bar altyazısı).</summary>
        public static string AgeAdjective(int months)
        {
            return months.ToString(CultureInfo.InvariantCulture) + " Ayl\u0131k";
        }

        /// <summary>"256 GB • 31 Aylık".</summary>
        public static string DeviceSubtitle(int gigabytes, int months)
        {
            return Storage(gigabytes) + " \u2022 " + AgeAdjective(months);
        }

        /// <summary>"S2 • Ayrıntılı kontrol".</summary>
        public static string LevelBadge(string code, string name)
        {
            return code + " \u2022 " + name;
        }

        public static string PhoneAngleName(PhoneAngle angle)
        {
            switch (angle)
            {
                case PhoneAngle.Back:
                    return "Arka";
                case PhoneAngle.Side:
                    return "Yan";
                case PhoneAngle.TopBottom:
                    return "Alt/\u00DCst";
                case PhoneAngle.CameraClose:
                    return "Kamera";
                default:
                    return "\u00D6n";
            }
        }

        public const string LevelsCardHeader = "Ekspertiz seviyesi";
        public const string FindingsHeader = "Bulgular";
        public const string MarketValueHeader = "Tahmini Piyasa De\u011Feri";
        public const string RiskHeader = "Risk";
        public const string ScreenCardTitle = "Ekran";
        public const string CameraCardTitle = "Kamera";
        public const string BatteryCardTitle = "Pil";
        public const string BodyCardTitle = "Kasa";
        public const string FindingClean = "sorun g\u00F6r\u00FCnm\u00FCyor";
        public const string FindingSuspected = "sorun olabilir";
        public const string NotMeasured = "Bu seviyede \u00F6l\u00E7\u00FClmez";

        public static string FindingSummary(int warnings, int clean)
        {
            if (warnings + clean == 0)
            {
                return string.Empty;
            }

            return warnings.ToString(CultureInfo.InvariantCulture) + " uyar\u0131 \u2022 " + clean.ToString(CultureInfo.InvariantCulture) + " temiz";
        }

        public static string PercentRangeText(NumericRange range)
        {
            return PercentRange(range);
        }

        public static string MoneyRangeText(MoneyRange range)
        {
            return MoneyFormatter.Format(range.Min) + " \u2013 " + MoneyFormatter.Format(range.Max);
        }

        public static string FindingText(bool found, string wordingKey, string attribute)
        {
            return found ? FindingWording(wordingKey) : AttributeName(attribute) + ": " + FindingClean;
        }

        public static string ConfidenceText(AppraisalConfidence confidence)
        {
            return ConfidenceName(confidence);
        }

        public static string AttributeTitle(string attribute)
        {
            return AttributeName(attribute);
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

        // ---- müşteri satış ekranı ----
        public const string CustomersTitle = "M\u00FC\u015Fteriler";

        // ---- günlük müşteri akışı (Gün 12.5, 12.6) ----
        public const string DismissButton = "G\u00F6nder";
        public const string StoreClosedLine = "Ma\u011Faza kapand\u0131. G\u00FCn\u00FC bitirebilirsin.";
        public const string NoCustomersLine = "\u015Eu an d\u00FCkk\u00E2nda m\u00FC\u015Fteri yok.";
        public const string NoInterestLine = "\u0130lgilendi\u011Fi bir \u00FCr\u00FCn\u00FC raftada bulamad\u0131.";
        public const string StoreClosedButton = "Ma\u011Faza kapal\u0131";
        public const string NoCustomersButton = "M\u00FC\u015Fteri yok";
        public const string SaleInProgressButton = "Sat\u0131\u015F s\u00FCr\u00FCyor";
        public const string WaitingHeader = "S\u0131rada bekleyenler";

        /// <summary>"09:41'de geldi"</summary>
        public static string ArrivedAt(string time)
        {
            return time + "'de geldi";
        }

        /// <summary>"Müşteri geldi: Kemal Abi" ya da sırada bekleyen varsa "Müşteri geldi: Kemal Abi  (+2 sırada)".</summary>
        public static string CustomerArrivedButton(string name, int waiting = 0)
        {
            string text = "M\u00FC\u015Fteri geldi: " + name;
            return waiting > 0 ? text + "  (+" + waiting.ToString(CultureInfo.InvariantCulture) + " s\u0131rada)" : text;
        }

        /// <summary>Günlük müşteri akışı hatası (Türkçe): Gönder. Bilinmeyen kod genel mesaja düşer.</summary>
        public static string QueueError(string code)
        {
            switch (code)
            {
                case "queue.no_active_customer":
                    return "\u015Eu an d\u00FCkk\u00E2nda bekleyen m\u00FC\u015Fteri yok.";
                case "queue.sale_in_progress":
                case "ui.sale_in_progress":
                    return "\u00D6nce s\u00FCren sat\u0131\u015F\u0131 bitir.";
                case "time.store_closed":
                    return "Ma\u011Faza kapand\u0131.";
                default:
                    return Error(code);
            }
        }


        public const string NoCustomers = "\u015Eu an d\u00FCkk\u00E2nda ilgilenen m\u00FC\u015Fteri yok. Rafa etiket fiyat\u0131 konulmu\u015F \u00FCr\u00FCnlere m\u00FC\u015Fteri bakar.";
        public const string SaleDoneButton = "Ba\u015Fka m\u00FC\u015Fteriye bakal\u0131m.";
        public const string ReplyGreet = "Tabii abi, buyur.";
        public const string ReplyReport = "Ekspertizi yap\u0131ld\u0131, raporu g\u00F6stereyim.";
        public const string ReplyLetGo = "Olmad\u0131 abi, ba\u015Fka sefere.";
        public const string AcceptOfferButton = "Teklifi Kabul Et";
        public const string ReplyAcceptFinalPrefix = "Tamam abi, ";
        public const string PriceStepperHint = "Se\u00E7ti\u011Fin fiyat";

        // ---- aksesuar ek satışı (Gün 11.3.3) ----
        public const string AddOnTitle = "AKSESUAR TALEB\u0130";
        public const string AddOnAddedBadge = "Eklendi";
        public const string AddOnDeclineButton = "\u0130stemiyorum / Devam Et";
        public const string AddOnContinueButton = "Devam Et";
        public const string AddOnAddButton = "Ekle";
        public const string AddOnOutOfStock = "Stokta yok";
        public const string AddOnPhoneLabel = "Telefon";
        public const string AddOnAccessoriesLabel = "Aksesuarlar";
        public const string AddOnTotalLabel = "Toplam";
        public const string AddOnPhoneProfitLabel = "Telefon k\u00E2r\u0131";
        public const string AddOnAccessoryProfitLabel = "Aksesuar k\u00E2r\u0131";
        public const string AddOnTotalProfitLabel = "Toplam k\u00E2r";

        /// <summary>İlerleme: "1/4" (eklenen / müşterinin istediği).</summary>
        public static string AddOnProgress(int added, int requested)
        {
            return added.ToString(CultureInfo.InvariantCulture) + "/" + requested.ToString(CultureInfo.InvariantCulture);
        }

        public static string AddOnPriceLine(Money price)
        {
            return MoneyFormatter.Format(price);
        }

        public static string AddOnStockLine(int stock)
        {
            return "Stok: " + stock.ToString(CultureInfo.InvariantCulture);
        }

        public static string AddOnAdded(string accessoryName, Money price)
        {
            return accessoryName + " eklendi (+" + MoneyFormatter.Format(price) + ").";
        }

        public static string AddOnError(string code)
        {
            switch (code)
            {
                case "stock.insufficient":
                    return AddOnOutOfStock + ".";
                case "addon.not_requested":
                case "addon.not_in_request":
                    return "M\u00FC\u015Fteri bunu istemedi.";
                case "addon.request_limit":
                    return "Bu aksesuar zaten eklendi.";
                case "addon.no_sale":
                case "addon.sale_closed":
                case "sale.unknown":
                    return "Bu sat\u0131\u015Fa art\u0131k aksesuar eklenemez.";
                case "accessory.unknown":
                    return "Bu aksesuar art\u0131k yok.";
                case "amount.invalid":
                    return "Bu aksesuar\u0131n tutar\u0131 ge\u00E7ersiz.";
                default:
                    return Error(code);
            }
        }

        // ---- toptancı ve aksesuar stoğu (Gün 11.2.3) ----
        public const string WholesaleTitle = "Toptanc\u0131";
        public const string WholesaleButton = "Toptanc\u0131";
        public const string BuyPackButton = "Sat\u0131n Al";
        public const string AccessoryStockTitle = "Aksesuar Sto\u011Fu";
        public const string AccessoryStockEmpty = "Aksesuar sto\u011Fun bo\u015F. Toptanc\u0131dan paket alabilirsin.";
        public const string WholesaleNoOffers = "Toptanc\u0131n\u0131n \u015Fu an teklifi yok.";
        public const string ToWholesale = "Toptanc\u0131ya git";
        public const string ToAccessoryStock = "Aksesuar sto\u011Funa bak";

        /// <summary>İlanlar ekranındaki düğme: "Aksesuar (12/60)". Telefon rafıyla ("Raf (n/kapasite)") karışmasın diye adı farklıdır.</summary>
        public static string AccessoryStockButton(int units, int capacity)
        {
            return "Aksesuar (" + units.ToString(CultureInfo.InvariantCulture) + "/" + capacity.ToString(CultureInfo.InvariantCulture) + ")";
        }

        public static string PackLine(int packSize)
        {
            return "Paket: " + packSize.ToString(CultureInfo.InvariantCulture) + " adet";
        }

        public static string UnitCostLine(Money unitCost)
        {
            return "Birim maliyet: " + MoneyFormatter.Format(unitCost);
        }

        public static string PackPriceLine(Money packCost)
        {
            return "Paket fiyat\u0131: " + MoneyFormatter.Format(packCost);
        }

        /// <summary>Kilitli düğmenin yazısı: "Gün 3'te açılır".</summary>
        public static string OpensOnButton(int day)
        {
            return "G\u00FCn " + day.ToString(CultureInfo.InvariantCulture) + "'te a\u00E7\u0131l\u0131r";
        }

        /// <summary>Kilitli ürünün notu: "Bu ürün Gün 3'te açılacak."</summary>
        public static string OpensOnNote(int day)
        {
            return "Bu \u00FCr\u00FCn G\u00FCn " + day.ToString(CultureInfo.InvariantCulture) + "'te a\u00E7\u0131lacak.";
        }

        /// <summary>"10 adet Şarj Adaptörü stoğa eklendi."</summary>
        public static string PackAdded(int quantity, string accessoryName)
        {
            return quantity.ToString(CultureInfo.InvariantCulture) + " adet " + accessoryName + " sto\u011Fa eklendi.";
        }

        /// <summary>Toptan alış hatası (Türkçe). Gün kilidi için teklifin açılış günü verilir. Bilinmeyen kod genel mesaja düşer.</summary>
        public static string WholesaleError(string code, int opensOnDay)
        {
            switch (code)
            {
                case "cash.insufficient":
                    return "Bu paketi almak i\u00E7in yeterli paran yok.";
                case "stock.full":
                    return "Aksesuar sto\u011Funda yeterli yer yok.";
                case "wholesale.not_available_yet":
                    return OpensOnNote(opensOnDay);
                case "supplier.unknown":
                case "accessory.unknown":
                case "offer.unknown":
                    return "Bu teklif art\u0131k yok.";
                case "amount.invalid":
                    return "Bu paketin tutar\u0131 ge\u00E7ersiz.";
                default:
                    return Error(code);
            }
        }

        public static string StockCapacityLine(int units, int capacity)
        {
            return "Stok: " + units.ToString(CultureInfo.InvariantCulture) + " / " + capacity.ToString(CultureInfo.InvariantCulture) + " adet";
        }

        public static string StockTotalCostLine(Money total)
        {
            return "Stok maliyeti: " + MoneyFormatter.Format(total);
        }

        public static string StockQuantityLine(int quantity)
        {
            return "Adet: " + quantity.ToString(CultureInfo.InvariantCulture);
        }

        public static string StockAverageCostLine(Money average)
        {
            return "Ortalama maliyet: " + MoneyFormatter.Format(average);
        }

        public static string StockLineTotalLine(Money total)
        {
            return "Toplam maliyet: " + MoneyFormatter.Format(total);
        }

        public static string StockShareLine(int percent)
        {
            return "Kapasitenin %" + percent.ToString(CultureInfo.InvariantCulture) + "'\u0131";
        }

        public static string CustomersButton(int count)
        {
            return count == 0 ? CustomersTitle : CustomersTitle + " (" + count.ToString(CultureInfo.InvariantCulture) + ")";
        }

        /// <summary>"Elma E13 Pro'ya bakıyor".</summary>
        public static string CustomerInterest(string model)
        {
            return model + " i\u00E7in geldi";
        }

        /// <summary>Oyuncunun cevabı olarak söylediği fiyat: "6.500 ₺ olur abi."</summary>
        public static string ReplyPrice(Money price)
        {
            return MoneyFormatter.Format(price) + " olur abi.";
        }

        public static string ReplyAcceptFinal(Money price)
        {
            return ReplyAcceptFinalPrefix + MoneyFormatter.Format(price) + "'ye olsun.";
        }

        public static string BudgetOf(NegotiationLevel level)
        {
            switch (level)
            {
                case NegotiationLevel.Low:
                    return "B\u00FCt\u00E7e: s\u0131n\u0131rl\u0131";
                case NegotiationLevel.High:
                    return "B\u00FCt\u00E7e: rahat";
                default:
                    return "B\u00FCt\u00E7e: normal";
            }
        }

        public static string CustomerMood(NegotiationLevel level)
        {
            switch (level)
            {
                case NegotiationLevel.Low:
                    return "Mesafeli";
                case NegotiationLevel.High:
                    return "Rahat";
                default:
                    return "Il\u0131ml\u0131";
            }
        }

        public static string CustomerPatience(NegotiationLevel level)
        {
            switch (level)
            {
                case NegotiationLevel.Low:
                    return "Sab\u0131rs\u0131z";
                case NegotiationLevel.High:
                    return "Sab\u0131rl\u0131";
                default:
                    return "Makul";
            }
        }

        public static string SaleDeal(Money price)
        {
            return "Sat\u0131ld\u0131: " + MoneyFormatter.Format(price);
        }
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

        public const string ShelfTitle = "Raf";
        public const string ShelfEmpty = "Rafta \u00FCr\u00FCn yok.";

        public static string BuyNowLabel(Money price)
        {
            return "Sat\u0131n Al \u2014 " + MoneyFormatter.Format(price);
        }

        public static string ShelfButton(int count, int capacity)
        {
            return "Raf (" + count.ToString(CultureInfo.InvariantCulture) + "/" + capacity.ToString(CultureInfo.InvariantCulture) + ")";
        }

        public static string ShelfCapacity(int count, int capacity)
        {
            return "Doluluk: " + count.ToString(CultureInfo.InvariantCulture) + "/" + capacity.ToString(CultureInfo.InvariantCulture);
        }

        // ---- Dükkan ana ekranı ve alt navigasyon (Gün 13.4) ----
        public const string NavShop = "D\u00FCkkan";
        public const string NavWholesale = "Toptanc\u0131";
        public const string NavListings = "\u0130lanlar";
        public const string NavProfile = "Profil";
        public const string ShopOffSale = "Sat\u0131\u015Fta de\u011Fil";
        public const string ShopOffSaleHeader = "SATI\u015E D\u0130\u015EI / F\u0130YATSIZ";
        public const string ShopShelfEmpty = "Rafta telefon yok. \u0130lanlar'dan telefon alabilirsin.";
        public const string ShopAccessoriesEmpty = "Aksesuar stoku bo\u015F. Toptanc\u0131'dan alabilirsin.";
        public const string ShopCustomerHeader = "M\u00DC\u015ETER\u0130";
        public const string ShopNoCustomer = "\u015Eu an ma\u011Fazada m\u00FC\u015Fteri yok.";
        public const string ProfilePlaceholder = "Profil ekran\u0131 yak\u0131nda.";

        public static string ShopShelfHeader(int count, int capacity)
        {
            return "RAF  " + count.ToString(CultureInfo.InvariantCulture) + "/" + capacity.ToString(CultureInfo.InvariantCulture);
        }

        public static string ShopAccessoryHeader(int units, int capacity)
        {
            return "AKSESUARLAR  " + units.ToString(CultureInfo.InvariantCulture) + "/" + capacity.ToString(CultureInfo.InvariantCulture);
        }

        public static string ShopCustomerTitle(string name)
        {
            return "M\u00FC\u015Fteri ma\u011Fazada: " + name;
        }

        public static string ShopWaiting(int count)
        {
            return "S\u0131rada " + count.ToString(CultureInfo.InvariantCulture) + " m\u00FC\u015Fteri daha var";
        }

        // ---- İlanlar pazarı (Gün 13.3) ----
        public const string InspectButton = "\u0130ncele";
        public const string ListingsSubtitle = "\u0130kinci el telefon ilanlar\u0131";
        public const string DoAppraisalButton = "Ekspertiz Yap";
        public const string DoNegotiationButton = "Pazarl\u0131k Yap";
        public const string NoListingsHint = "Yeni ilanlar i\u00E7in g\u00FCn\u00FC bitirebilirsin.";
        public const string EstimatedHint = "Tahmin, ekspertiz sonucundan gelir; ilan hakk\u0131nda kar\u0131 vermez.";
        public const string AppraisalNotDone = "Ekspertiz: yap\u0131lmad\u0131";

        public static string ListingsEmptyNote
        {
            get { return NoListings + " " + NoListingsHint; }
        }

        /// <summary>"Kondisyon: 14 ay kullanılmış • Kutu var • Fatura yok [• Acil satış]": yalnızca herkese açık ilan bilgisi (gizli pil/ekran/kasa durumu ekspertizle görülür).</summary>
        public static string Condition(int ageMonths, bool hasBox, bool hasInvoice, bool urgent)
        {
            string text = "Kondisyon: " + ageMonths.ToString(CultureInfo.InvariantCulture) + " ay kullan\u0131lm\u0131\u015F \u2022 "
                + (hasBox ? "Kutu var" : "Kutu yok") + " \u2022 " + (hasInvoice ? "Fatura var" : "Fatura yok");
            return urgent ? text + " \u2022 Acil sat\u0131\u015F" : text;
        }

        public static string AskingFull(Money price)
        {
            return "\u0130stenen fiyat: " + MoneyFormatter.Format(price);
        }

        /// <summary>
        /// Tahmini değer (Gün 13.3): R (referans fiyat) ve gerçek değer oyuncudan gizli kalır (ListingView gizli bilgi taşımaz); tahmin yalnızca mevcut ekspertiz sisteminin
        /// değer aralığından gelir. Ekspertiz yoksa "ekspertizle öğrenilir".
        /// </summary>
        public static string EstimatedFromAppraisal(Money min, Money max)
        {
            return "Tahmini de\u011Fer: " + MoneyFormatter.Format(min) + " \u2013 " + MoneyFormatter.Format(max);
        }

        public const string EstimatedUnknown = "Tahmini de\u011Fer: ekspertizle \u00F6\u011Frenilir";

        public static string AppraisalDone(string levelName)
        {
            return "Ekspertiz: yap\u0131ld\u0131 (" + levelName + ")";
        }

        public static string AppraisalValueRange(Money min, Money max)
        {
            return "Ekspertiz de\u011Fer aral\u0131\u011F\u0131: " + MoneyFormatter.Format(min) + " \u2013 " + MoneyFormatter.Format(max);
        }

        // ---- Global müşteri bildirimi (Gün 13.1) ----
        public const string GoToCustomerButton = "M\u00FC\u015Fteriye Git";
        public const string NoticeDismissButton = "Kapat";

        public static string NoticeArrived(string name)
        {
            return "M\u00FC\u015Fteri geldi: " + name;
        }

        public static string NoticeLeft(string name)
        {
            return "M\u00FC\u015Fteri ayr\u0131ld\u0131: " + name;
        }

        public static string NoticeSpeech(string line)
        {
            return "\u201C" + line + "\u201D";
        }

        // ---- Raf fiyatlandırma (Gün 12.7) ----
        public const string ShelfNoPriceLine = "Fiyat girilmedi \u2014 sat\u0131\u015Fta de\u011Fil";
        public const string ShelfPriceButtonHint = "Fiyat belirlemek i\u00E7in \u00FCr\u00FCne dokun";
        public const string ShelfNoSavedPrice = "Kay\u0131tl\u0131 fiyat yok";
        public const string ShelfSavePriceButton = "Fiyat\u0131 Kaydet";
        public const string ShelfCloseEditorButton = "Vazge\u00E7";

        public const string ShelfRemoveButton = "Sat\u0131\u015Ftan \u00C7\u0131kar";

        public static string ShelfStock(int units)
        {
            return "Stok: " + units.ToString(CultureInfo.InvariantCulture) + " adet";
        }

        public static string ShelfRemoved(string model)
        {
            return model + " sat\u0131\u015Ftan \u00E7\u0131kar\u0131ld\u0131. Telefon rafta duruyor; fiyat girerek yeniden sat\u0131\u015Fa alabilirsin.";
        }

        public const string ShelfExpensiveWarning = "Bu fiyat\u0131n \u00FCzerinde m\u00FC\u015Fteriler bu telefonu pahal\u0131 bulabilir.";

        public static string ShelfCeilingLine(Money ceiling)
        {
            return "M\u00FC\u015Fteri tavan\u0131: " + MoneyFormatter.Format(ceiling);
        }

        public static string ShelfSellableLine(Money price)
        {
            return "Sat\u0131\u015F fiyat\u0131: " + MoneyFormatter.Format(price);
        }

        public static string ShelfAcquisitionCost(Money costBasis)
        {
            return "Al\u0131\u015F maliyeti: " + MoneyFormatter.Format(costBasis);
        }

        public static string ShelfSavedPrice(Money price)
        {
            return "Kay\u0131tl\u0131 fiyat: " + MoneyFormatter.Format(price);
        }

        public static string ShelfEditPrice(Money price)
        {
            return "Sat\u0131\u015F fiyat\u0131: " + MoneyFormatter.Format(price);
        }

        /// <summary>"Tahmini k\u00E2r: 1.200 \u20BA" ya da zararsa "Tahmini zarar: 500 \u20BA".</summary>
        public static string ShelfProfit(Money profit)
        {
            return profit.Tl >= 0
                ? "Tahmini k\u00E2r: " + MoneyFormatter.Format(profit)
                : "Tahmini zarar: " + MoneyFormatter.Format(-profit);
        }

        public static string ShelfMargin(long percent)
        {
            return "Marj: %" + percent.ToString(CultureInfo.InvariantCulture);
        }

        public static string ShelfPriceSaved(string model, Money price)
        {
            return model + " i\u00E7in sat\u0131\u015F fiyat\u0131 kaydedildi: " + MoneyFormatter.Format(price) + ". Art\u0131k sat\u0131labilir.";
        }

        /// <summary>Fiyat belirleme hatas\u0131 (T\u00FCrk\u00E7e). Bilinmeyen kod genel mesaja d\u00FC\u015Fer.</summary>
        public static string PriceError(string code)
        {
            switch (code)
            {
                case "price.invalid":
                    return "Fiyat ge\u00E7erli olmal\u0131: s\u0131f\u0131rdan b\u00FCy\u00FCk ve 10 \u20BA'nin kat\u0131.";
                case "instance.not_in_inventory":
                case "instance.unknown":
                    return "Bu \u00FCr\u00FCn rafta de\u011Fil.";
                case "price.item_in_sale":
                    return "Bu telefonla s\u00FCren bir sat\u0131\u015F var; \u00F6nce onu bitir.";
                default:
                    return "Fiyat kaydedilemedi.";
            }
        }

        public static string ShelfCost(Money costBasis)
        {
            return "Maliyet: " + MoneyFormatter.Format(costBasis);
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
