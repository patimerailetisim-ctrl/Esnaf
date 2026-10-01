using System;
using Esnaf.Core;
using Esnaf.Domain.Market;
using Esnaf.Presentation;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Presentation
{
    public class ListingDetailViewModelTests
    {
        private static ListingView Listing(string definitionId = "phone.nova_n3_pro", string seller = "npc.kemal", int days = 4, bool box = true, bool invoice = false)
        {
            return new ListingView(7, 8, definitionId, 256, 26, box, invoice, seller, Money.FromTl(12750), 1, days, new string[0]);
        }

        private static ContentPresentation Content()
        {
            return new ContentPresentation(MarketHarness.RealContent());
        }

        [Test]
        public void TheDetail_ShowsEveryFieldWithItsLabel()
        {
            ListingDetailViewModel d = ListingDetailViewModel.From(Listing(), Content());

            Assert.AreEqual(7L, d.ListingId);
            Assert.AreEqual("Nova N3 Pro", d.Title);
            Assert.AreEqual("Depolama: 256 GB", d.StorageLine);
            Assert.AreEqual("Yaş: 26 ay", d.AgeLine);
            Assert.AreEqual("İstenen fiyat: 12.750 ₺", d.PriceLine);
            Assert.AreEqual("Kutu: var", d.BoxLine);
            Assert.AreEqual("Fatura: yok", d.InvoiceLine);
            Assert.AreEqual("Satıcı: Kemal Abi", d.SellerLine);
            Assert.AreEqual("Süre: 4 gün kaldı", d.RemainingLine);
        }

        [Test]
        public void TheDetail_FlagsAndLastDayMapToTheirOwnLines()
        {
            ListingDetailViewModel d = ListingDetailViewModel.From(Listing(box: false, invoice: true, days: 1), Content());

            Assert.AreEqual("Kutu: yok", d.BoxLine);
            Assert.AreEqual("Fatura: var", d.InvoiceLine);
            Assert.AreEqual("Süre: Son gün", d.RemainingLine);
        }

        [Test]
        public void UnknownModelOrSeller_FallBackToTheirIds()
        {
            ListingDetailViewModel d = ListingDetailViewModel.From(Listing("phone.unknown", "npc.nobody"), Content());

            Assert.AreEqual("phone.unknown", d.Title);
            Assert.AreEqual("Satıcı: npc.nobody", d.SellerLine);
        }

        [Test]
        public void TheFactory_ChecksItsArguments()
        {
            Assert.Throws<ArgumentNullException>(() => ListingDetailViewModel.From(null, Content()));
            Assert.Throws<ArgumentNullException>(() => ListingDetailViewModel.From(Listing(), null));
        }
    }
}
