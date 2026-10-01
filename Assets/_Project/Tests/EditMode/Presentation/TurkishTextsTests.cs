using System.Collections.Generic;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Appraisal;
using Esnaf.Presentation;
using NUnit.Framework;

namespace Esnaf.Tests.Presentation
{
    public class TurkishTextsTests
    {
        [Test]
        public void Day_IsWrittenInTurkish()
        {
            Assert.AreEqual("Gün 1", TurkishTexts.Day(1));
            Assert.AreEqual("Gün 17", TurkishTexts.Day(17));
        }

        [Test]
        public void Cash_ShowsTheLabelAndTheFormattedAmount()
        {
            Assert.AreEqual("Nakit: 250.000 ₺", TurkishTexts.Cash(Money.FromTl(250000)));
            Assert.AreEqual("Nakit: -1.500 ₺", TurkishTexts.Cash(Money.FromTl(-1500)));
            Assert.AreEqual("Nakit: 0 ₺", TurkishTexts.Cash(Money.Zero));
        }

        private static readonly Dictionary<string, string> Expected = new Dictionary<string, string>
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

        [TestCaseSource(nameof(ExpectedCodes))]
        public void Error_KnownCodes_GiveTheirTurkishMessage(string code)
        {
            Assert.AreEqual(Expected[code], TurkishTexts.Error(code));
        }

        private static IEnumerable<string> ExpectedCodes()
        {
            return Expected.Keys;
        }

        [Test]
        public void KnownErrorCodes_AreExactlyTheListedOnes_AndEveryMessageIsDistinct()
        {
            CollectionAssert.AreEquivalent(Expected.Keys, TurkishTexts.KnownErrorCodes);
            Assert.AreEqual(Expected.Count, TurkishTexts.KnownErrorCodes.Select(TurkishTexts.Error).Distinct().Count());
        }

        [Test]
        public void Error_UnknownCode_FallsBackToAGenericMessageThatKeepsTheCode()
        {
            Assert.AreEqual("Bir sorun oluştu (price.invalid).", TurkishTexts.Error("price.invalid"));
        }

        [TestCase(null)]
        [TestCase("")]
        public void Error_MissingCode_GivesTheGenericMessage(string code)
        {
            Assert.AreEqual("Bir sorun oluştu.", TurkishTexts.Error(code));
        }

        [TestCase(64, "64 GB")]
        [TestCase(128, "128 GB")]
        [TestCase(1024, "1024 GB")]
        public void Storage_IsWrittenInGigabytes(int gb, string expected)
        {
            Assert.AreEqual(expected, TurkishTexts.Storage(gb));
        }

        [TestCase(0, "0 ay")]
        [TestCase(1, "1 ay")]
        [TestCase(14, "14 ay")]
        [TestCase(48, "48 ay")]
        public void Age_IsWrittenInMonths(int months, string expected)
        {
            Assert.AreEqual(expected, TurkishTexts.Age(months));
        }

        [Test]
        public void Asking_ShowsTheRequestedPrice()
        {
            Assert.AreEqual("\u0130stenen: 9.500 \u20BA", TurkishTexts.Asking(Money.FromTl(9500)));
        }

        [TestCase(-1, "Son g\u00FCn")]
        [TestCase(0, "Son g\u00FCn")]
        [TestCase(1, "Son g\u00FCn")]
        [TestCase(2, "2 g\u00FCn kald\u0131")]
        [TestCase(4, "4 g\u00FCn kald\u0131")]
        public void Remaining_SaysLastDayOrTheDaysLeft(int days, string expected)
        {
            Assert.AreEqual(expected, TurkishTexts.Remaining(days));
        }

        [Test]
        public void BoxAndInvoice_SayPresentOrAbsent()
        {
            Assert.AreEqual("Kutu: var", TurkishTexts.Box(true));
            Assert.AreEqual("Kutu: yok", TurkishTexts.Box(false));
            Assert.AreEqual("Fatura: var", TurkishTexts.Invoice(true));
            Assert.AreEqual("Fatura: yok", TurkishTexts.Invoice(false));
        }

        [Test]
        public void Seller_ShowsTheName()
        {
            Assert.AreEqual("Sat\u0131c\u0131: Kemal Abi", TurkishTexts.Seller("Kemal Abi"));
        }

        [Test]
        public void ScreenTexts_AreFixed()
        {
            Assert.AreEqual("\u0130lanlar", TurkishTexts.ListingsTitle);
            Assert.AreEqual("Bug\u00FCn ilan yok.", TurkishTexts.NoListings);
            Assert.AreEqual("G\u00FCn\u00FC Bitir", TurkishTexts.EndDayButton);
        }

        [Test]
        public void DetailLines_AreLabelled()
        {
            Assert.AreEqual("Depolama: 256 GB", TurkishTexts.DetailStorage(256));
            Assert.AreEqual("Ya\u015F: 26 ay", TurkishTexts.DetailAge(26));
            Assert.AreEqual("\u0130stenen fiyat: 12.750 \u20BA", TurkishTexts.DetailPrice(Money.FromTl(12750)));
            Assert.AreEqual("S\u00FCre: 4 g\u00FCn kald\u0131", TurkishTexts.DetailRemaining(4));
            Assert.AreEqual("S\u00FCre: Son g\u00FCn", TurkishTexts.DetailRemaining(1));
        }

        [Test]
        public void DetailButtonTexts_AreFixed()
        {
            Assert.AreEqual("Geri", TurkishTexts.BackButton);
            Assert.AreEqual("Ekspertiz", TurkishTexts.AppraisalButton);
            Assert.AreEqual("Pazarl\u0131k", TurkishTexts.NegotiationButton);
        }

        // ---------- ekspertiz ----------

        [Test]
        public void AppraisalScreenTexts_AreFixed()
        {
            Assert.AreEqual("Ekspertiz", TurkishTexts.AppraisalTitle);
            Assert.AreEqual("Seviyeler", TurkishTexts.LevelsHeader);
            Assert.AreEqual("Sonu\u00E7", TurkishTexts.ResultHeader);
            Assert.AreEqual("Ekspertiz Yapt\u0131r", TurkishTexts.ActionPerformAppraisal);
            Assert.AreEqual("Sonucu G\u00F6ster", TurkishTexts.ActionShowAppraisal);
            Assert.AreEqual("\u00D6nce bir ekspertiz seviyesi se\u00E7.", TurkishTexts.NoLevelSelected);
            Assert.AreEqual("Bu seviye hen\u00FCz yap\u0131lmad\u0131.", TurkishTexts.LevelNotDone);
        }

        [Test]
        public void LevelTexts_SayFeeAndState()
        {
            Assert.AreEqual("\u00DCcretsiz", TurkishTexts.LevelFee(Money.Zero));
            Assert.AreEqual("\u00DCcret: 350 \u20BA", TurkishTexts.LevelFee(Money.FromTl(350)));
            Assert.AreEqual("Yap\u0131ld\u0131", TurkishTexts.LevelDone);
            Assert.AreEqual("Haz\u0131r", TurkishTexts.LevelReady);
            Assert.AreEqual("Cihaz gerekir", TurkishTexts.LevelNeedsEquipment);
            Assert.AreEqual("Kilitli (G\u00FCn 3'te a\u00E7\u0131l\u0131r)", TurkishTexts.LevelLocked(3));
            Assert.AreEqual("Kilitli (G\u00FCn 6'te a\u00E7\u0131l\u0131r)", TurkishTexts.LevelLocked(6));
        }

        [TestCase("appraisal.finding.screen_replaced", "screen", true, AppraisalConfidence.Medium, "\u2022 Ekran de\u011Fi\u015Ftirilmi\u015F olabilir \u2014 orta g\u00FCven")]
        [TestCase("appraisal.finding.camera_problem", "camera", true, AppraisalConfidence.Certain, "\u2022 Kamerada sorun olabilir \u2014 kesin")]
        [TestCase("appraisal.finding.screen_replaced", "screen", false, AppraisalConfidence.Low, "\u2022 Ekran: sorun g\u00F6r\u00FCnm\u00FCyor \u2014 d\u00FC\u015F\u00FCk g\u00FCven")]
        [TestCase("appraisal.finding.camera_problem", "camera", false, AppraisalConfidence.Hint, "\u2022 Kamera: sorun g\u00F6r\u00FCnm\u00FCyor \u2014 ipucu")]
        [TestCase("appraisal.finding.other", "battery", true, AppraisalConfidence.Hint, "\u2022 appraisal.finding.other \u2014 ipucu")]
        [TestCase("appraisal.finding.other", "battery", false, AppraisalConfidence.Hint, "\u2022 battery: sorun g\u00F6r\u00FCnm\u00FCyor \u2014 ipucu")]
        public void Finding_NeverSaysThereIsNoProblem_OnlyThatNoneIsVisible(string key, string attribute, bool found, AppraisalConfidence confidence, string expected)
        {
            Assert.AreEqual(expected, TurkishTexts.Finding(found, key, attribute, confidence));
        }

        [Test]
        public void RangeLines_ShowTheRangeOrSayTheLevelDoesNotGiveIt()
        {
            Assert.AreEqual("Pil: %80\u2013%90", TurkishTexts.Battery(new NumericRange(80, 90)));
            Assert.AreEqual("Pil: bu seviyede \u00F6l\u00E7\u00FClmez", TurkishTexts.Battery(null));
            Assert.AreEqual("G\u00F6vde: %70\u2013%85", TurkishTexts.Body(new NumericRange(70, 85)));
            Assert.AreEqual("G\u00F6vde: bu seviyede \u00F6l\u00E7\u00FClmez", TurkishTexts.Body(null));
            Assert.AreEqual("Tahmini de\u011Fer: 8.000 \u20BA \u2013 9.500 \u20BA", TurkishTexts.ValueRange(new MoneyRange(Money.FromTl(8000), Money.FromTl(9500))));
            Assert.AreEqual("Tahmini de\u011Fer: bu seviyede verilmez", TurkishTexts.ValueRange(null));
            Assert.AreEqual("\u00D6denen \u00FCcret: 350 \u20BA", TurkishTexts.PaidFee(Money.FromTl(350)));
        }

        [Test]
        public void RiskLines_AreWrittenInTurkish()
        {
            Assert.AreEqual("Risk kart\u0131 (istenen fiyatla al\u0131rsan: 9.500 \u20BA)", TurkishTexts.RiskTitle(Money.FromTl(9500)));
            var scenario = new RiskScenario("bad", Money.FromTl(8000), Money.FromTl(8160), Money.FromTl(-1340));
            Assert.AreEqual("K\u00F6t\u00FC: de\u011Fer 8.000 \u20BA, beklenen sat\u0131\u015F 8.160 \u20BA, k\u00E2r/zarar -1.340 \u20BA", TurkishTexts.RiskScenarioLine(scenario));
            Assert.AreEqual("Orta: de\u011Fer 1 \u20BA, beklenen sat\u0131\u015F 2 \u20BA, k\u00E2r/zarar 3 \u20BA", TurkishTexts.RiskScenarioLine(new RiskScenario("mid", Money.FromTl(1), Money.FromTl(2), Money.FromTl(3))));
            Assert.AreEqual("\u0130yi: de\u011Fer 1 \u20BA, beklenen sat\u0131\u015F 2 \u20BA, k\u00E2r/zarar 3 \u20BA", TurkishTexts.RiskScenarioLine(new RiskScenario("good", Money.FromTl(1), Money.FromTl(2), Money.FromTl(3))));
            Assert.AreEqual("x: de\u011Fer 1 \u20BA, beklenen sat\u0131\u015F 2 \u20BA, k\u00E2r/zarar 3 \u20BA", TurkishTexts.RiskScenarioLine(new RiskScenario("x", Money.FromTl(1), Money.FromTl(2), Money.FromTl(3))));
        }

        [TestCase(0.0, "Aral\u0131k d\u0131\u015F\u0131 kalma olas\u0131l\u0131\u011F\u0131: %0")]
        [TestCase(0.05, "Aral\u0131k d\u0131\u015F\u0131 kalma olas\u0131l\u0131\u011F\u0131: %5")]
        [TestCase(0.125, "Aral\u0131k d\u0131\u015F\u0131 kalma olas\u0131l\u0131\u011F\u0131: %13")]
        [TestCase(0.124, "Aral\u0131k d\u0131\u015F\u0131 kalma olas\u0131l\u0131\u011F\u0131: %12")]
        [TestCase(1.0, "Aral\u0131k d\u0131\u015F\u0131 kalma olas\u0131l\u0131\u011F\u0131: %100")]
        public void MissProbability_IsShownAsAWholePercent(double probability, string expected)
        {
            Assert.AreEqual(expected, TurkishTexts.MissProbability(probability));
        }

        [Test]
        public void AppraisalErrors_HaveTurkishMessages()
        {
            Assert.AreEqual("Bu seviye de\u011Fer aral\u0131\u011F\u0131 vermedi\u011Fi i\u00E7in risk kart\u0131 yok.", TurkishTexts.Error("appraisal.no_value_range"));
            Assert.AreEqual("Bilinmeyen ekspertiz sonucu.", TurkishTexts.Error("appraisal.unknown"));
        }

        // ---------- pazarl\u0131k ----------

        [Test]
        public void NegotiationTexts_AreFixed()
        {
            Assert.AreEqual("Pazarl\u0131k", TurkishTexts.NegotiationTitle);
            Assert.AreEqual("Teklif Ver", TurkishTexts.MakeOfferButton);
            Assert.AreEqual("Koz Kullan", TurkishTexts.UseCardButton);
            Assert.AreEqual("Son Fiyat\u0131 Kabul Et", TurkishTexts.AcceptFinalButton);
            Assert.AreEqual("Vazge\u00E7", TurkishTexts.WalkAwayButton);
            Assert.AreEqual("Teklifin (\u20BA):", TurkishTexts.OfferLabel);
            Assert.AreEqual("Koz kartlar\u0131", TurkishTexts.CardsHeader);
            Assert.AreEqual("\u00D6nce bir koz kart\u0131 se\u00E7.", TurkishTexts.NoCardSelected);
            Assert.AreEqual("Elinde koz kart\u0131 yok (ekspertiz yapt\u0131r\u0131nca kartlar \u00E7\u0131kabilir).", TurkishTexts.NoCards);
            Assert.AreEqual("Bir teklif tutar\u0131 gir.", TurkishTexts.OfferEmpty);
            Assert.AreEqual("Teklif yaln\u0131zca rakamlardan olu\u015Fmal\u0131.", TurkishTexts.OfferNotNumber);
            Assert.AreEqual("Teklif negatif olamaz.", TurkishTexts.OfferNegative);
            Assert.AreEqual("Teklif s\u0131f\u0131rdan b\u00FCy\u00FCk olmal\u0131.", TurkishTexts.OfferNotPositive);
            Assert.AreEqual("Teklif \u00E7ok b\u00FCy\u00FCk.", TurkishTexts.OfferTooLarge);
        }

        [Test]
        public void NegotiationLines_AreWrittenInTurkish()
        {
            Money price = Money.FromTl(9500);
            Assert.AreEqual("\u0130lan fiyat\u0131: 9.500 \u20BA", TurkishTexts.AskingLine(price));
            Assert.AreEqual("Sat\u0131c\u0131n\u0131n g\u00FCncel fiyat\u0131: 9.500 \u20BA", TurkishTexts.ShownPriceLine(price));
            Assert.AreEqual("Nakdin: 9.500 \u20BA", TurkishTexts.CashLine(price));
            Assert.AreEqual("Tur: 3", TurkishTexts.Round(3));
            Assert.AreEqual("Son teklifin: 9.500 \u20BA", TurkishTexts.YourOffer(price));
            Assert.AreEqual("Sat\u0131c\u0131: \u0130stedi\u011Fim fiyat 9.500 \u20BA.", TurkishTexts.ReplyOpening(price));
            Assert.AreEqual("Sat\u0131c\u0131 kar\u015F\u0131 teklif verdi: 9.500 \u20BA.", TurkishTexts.ReplyCounter(price));
            Assert.AreEqual("Sat\u0131c\u0131 fiyat\u0131nda direniyor: 9.500 \u20BA.", TurkishTexts.ReplyHolding(price));
            Assert.AreEqual("Sat\u0131c\u0131: Son fiyat\u0131m 9.500 \u20BA. Kabul et ya da kalk.", TurkishTexts.ReplyFinal(price));
            Assert.AreEqual("Teklifin sat\u0131c\u0131y\u0131 g\u00FCcendirdi.", TurkishTexts.Insulted);
            Assert.AreEqual("Sat\u0131n al\u0131nd\u0131: Nova N3 Pro \u2014 9.500 \u20BA. Rafa eklendi (2/6).", TurkishTexts.Purchased("Nova N3 Pro", price, 2, 6));
            Assert.AreEqual("Pazarl\u0131ktan vazge\u00E7tin; ilan pazardan kalkt\u0131.", TurkishTexts.WalkedAway);
        }

        [Test]
        public void PhaseMoodAndPatience_AreWrittenInTurkish()
        {
            Assert.AreEqual("Pazarl\u0131k s\u00FCr\u00FCyor", TurkishTexts.Phase(Esnaf.Domain.Negotiation.NegotiationPhase.Active));
            Assert.AreEqual("Sat\u0131c\u0131 son fiyat\u0131n\u0131 s\u00F6yledi", TurkishTexts.Phase(Esnaf.Domain.Negotiation.NegotiationPhase.FinalOffer));
            Assert.AreEqual("Anla\u015F\u0131ld\u0131", TurkishTexts.Phase(Esnaf.Domain.Negotiation.NegotiationPhase.Deal));
            Assert.AreEqual("Pazarl\u0131k sona erdi", TurkishTexts.Phase(Esnaf.Domain.Negotiation.NegotiationPhase.Failed));
            Assert.AreEqual("D\u00FC\u015F\u00FCk", TurkishTexts.Level(Esnaf.Domain.Negotiation.NegotiationLevel.Low));
            Assert.AreEqual("Orta", TurkishTexts.Level(Esnaf.Domain.Negotiation.NegotiationLevel.Medium));
            Assert.AreEqual("Y\u00FCksek", TurkishTexts.Level(Esnaf.Domain.Negotiation.NegotiationLevel.High));
            Assert.AreEqual("Sat\u0131c\u0131n\u0131n ruh hali: Orta", TurkishTexts.MoodLine(Esnaf.Domain.Negotiation.NegotiationLevel.Medium));
            Assert.AreEqual("Sab\u0131r: Y\u00FCksek", TurkishTexts.PatienceLine(Esnaf.Domain.Negotiation.NegotiationLevel.High));
        }

        [TestCase(false, "\u2022 Ekran de\u011Fi\u015Ftirilmi\u015F olabilir \u2014 orta g\u00FCven \u2014 sorun de\u011Feri 800 \u20BA")]
        [TestCase(true, "\u2022 Ekran de\u011Fi\u015Ftirilmi\u015F olabilir \u2014 orta g\u00FCven \u2014 sorun de\u011Feri 800 \u20BA (kullan\u0131ld\u0131)")]
        public void CardLine_ShowsTheProblemAndMarksAUsedCard(bool used, string expected)
        {
            Assert.AreEqual(expected, TurkishTexts.Card("appraisal.finding.screen_replaced", "screen", AppraisalConfidence.Medium, Money.FromTl(800), used));
        }

        [Test]
        public void Reply_IsDescribedFromThePriceMovement_AndThePhase()
        {
            Money shown = Money.FromTl(9000);

            Assert.AreEqual(TurkishTexts.ReplyCounter(shown), TurkishTexts.Reply(Esnaf.Domain.Negotiation.NegotiationPhase.Active, shown, Money.FromTl(9500)));
            Assert.AreEqual(TurkishTexts.ReplyHolding(shown), TurkishTexts.Reply(Esnaf.Domain.Negotiation.NegotiationPhase.Active, shown, shown), "fiyat oynamadıysa karşı teklif denmez");
            Assert.AreEqual(TurkishTexts.ReplyHolding(shown), TurkishTexts.Reply(Esnaf.Domain.Negotiation.NegotiationPhase.Active, shown, Money.FromTl(8500)));
            Assert.AreEqual(TurkishTexts.ReplyFinal(shown), TurkishTexts.Reply(Esnaf.Domain.Negotiation.NegotiationPhase.FinalOffer, shown, Money.FromTl(9500)));
            Assert.AreEqual(TurkishTexts.ReplyFinal(shown), TurkishTexts.Reply(Esnaf.Domain.Negotiation.NegotiationPhase.FinalOffer, shown, shown));
        }
    }
}
