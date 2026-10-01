using System;
using Esnaf.Domain.Market;

namespace Esnaf.Presentation
{
    /// <summary>Telefon detayı ekranının hazır Türkçe satırları (etiketli; gizli bilgi yok, ListingView'dan türer).</summary>
    public sealed class ListingDetailViewModel
    {
        public long ListingId { get; }
        public string Title { get; }
        public string StorageLine { get; }
        public string AgeLine { get; }
        public string PriceLine { get; }
        public string BoxLine { get; }
        public string InvoiceLine { get; }
        public string SellerLine { get; }
        public string RemainingLine { get; }
        public string BuyButtonText { get; }

        private ListingDetailViewModel(ListingView listing, ContentPresentation content)
        {
            ListingId = listing.ListingId;
            Title = content.ModelName(listing.DefinitionId);
            StorageLine = TurkishTexts.DetailStorage(listing.StorageGb);
            AgeLine = TurkishTexts.DetailAge(listing.AgeMonths);
            PriceLine = TurkishTexts.DetailPrice(listing.AskingPrice);
            BoxLine = TurkishTexts.Box(listing.HasBox);
            InvoiceLine = TurkishTexts.Invoice(listing.HasInvoice);
            SellerLine = TurkishTexts.Seller(content.NpcName(listing.SellerNpcId));
            RemainingLine = TurkishTexts.DetailRemaining(listing.RemainingDays);
            BuyButtonText = TurkishTexts.BuyNowLabel(listing.AskingPrice);
        }

        public static ListingDetailViewModel From(ListingView listing, ContentPresentation content)
        {
            if (listing == null)
            {
                throw new ArgumentNullException(nameof(listing));
            }

            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            return new ListingDetailViewModel(listing, content);
        }
    }
}
