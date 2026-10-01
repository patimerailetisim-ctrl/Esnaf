using System;
using Esnaf.Core;
using Esnaf.Domain.Market;
using Esnaf.Presentation;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Presentation
{
    public class ListingRowViewModelTests
    {
        private static ListingView Listing(string definitionId = "phone.nova_n1_lite", string seller = "npc.kemal", int days = 3, bool box = true, bool invoice = false)
        {
            return new ListingView(11, 22, definitionId, 128, 14, box, invoice, seller, Money.FromTl(9500), 1, days, new string[0]);
        }

        private static ContentPresentation Content()
        {
            return new ContentPresentation(MarketHarness.RealContent());
        }

        [Test]
        public void TheRow_ShowsEveryFieldInTurkish()
        {
            ListingRowViewModel row = ListingRowViewModel.From(Listing(), Content(), false);

            Assert.AreEqual(11L, row.ListingId);
            Assert.AreEqual("Nova N1 Lite", row.Title);
            Assert.AreEqual("128 GB", row.StorageText);
            Assert.AreEqual("14 ay", row.AgeText);
            Assert.AreEqual("İstenen: 9.500 ₺", row.PriceText);
            Assert.AreEqual("3 gün kaldı", row.RemainingText);
            Assert.AreEqual("Kutu: var", row.BoxText);
            Assert.AreEqual("Fatura: yok", row.InvoiceText);
            Assert.AreEqual("Satıcı: Kemal Abi", row.SellerText);
            Assert.IsFalse(row.IsSelected);
        }

        [Test]
        public void TheRow_FlagsMapToTheirOwnTexts()
        {
            ListingRowViewModel row = ListingRowViewModel.From(Listing(box: false, invoice: true, days: 1), Content(), true);

            Assert.AreEqual("Kutu: yok", row.BoxText);
            Assert.AreEqual("Fatura: var", row.InvoiceText);
            Assert.AreEqual("Son gün", row.RemainingText);
            Assert.IsTrue(row.IsSelected);
        }

        [Test]
        public void UnknownModelOrSeller_FallBackToTheirIds_AndNeverBreakTheRow()
        {
            ListingRowViewModel row = ListingRowViewModel.From(Listing("phone.unknown", "npc.nobody"), Content(), false);

            Assert.AreEqual("phone.unknown", row.Title);
            Assert.AreEqual("Satıcı: npc.nobody", row.SellerText);
        }

        [Test]
        public void TheFactory_ChecksItsArguments()
        {
            Assert.Throws<ArgumentNullException>(() => ListingRowViewModel.From(null, Content(), false));
            Assert.Throws<ArgumentNullException>(() => ListingRowViewModel.From(Listing(), null, false));
        }
    }
}
