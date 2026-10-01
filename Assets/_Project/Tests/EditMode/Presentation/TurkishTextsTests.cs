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
            Assert.AreEqual("Ekspertiz bir sonraki ad\u0131mda eklenecek.", TurkishTexts.AppraisalComingSoon);
            Assert.AreEqual("Pazarl\u0131k bir sonraki ad\u0131mda eklenecek.", TurkishTexts.NegotiationComingSoon);
        }
    }
}
