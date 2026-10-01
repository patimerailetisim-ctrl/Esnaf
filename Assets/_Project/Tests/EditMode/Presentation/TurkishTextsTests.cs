using System.Collections.Generic;
using System.Linq;
using Esnaf.Core;
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
            { "offer.invalid", "Geçersiz teklif." },
            { "card.unknown", "Bilinmeyen koz kartı." },
            { "card.already_used", "Bu koz kartı zaten kullanıldı." }
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
    }
}
