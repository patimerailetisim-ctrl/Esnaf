using System;
using Esnaf.Domain.Market;

namespace Esnaf.Presentation
{
    /// <summary>İlanlar listesinde bir satırın ekranda gösterilen hazır Türkçe metinleri (gizli bilgi yok; ListingView'dan türer).</summary>
    public sealed class ListingRowViewModel
    {
        public long ListingId { get; }
        public string Title { get; }
        public string StorageText { get; }
        public string AgeText { get; }
        public string PriceText { get; }
        public string RemainingText { get; }
        public string BoxText { get; }
        public string InvoiceText { get; }
        public string SellerText { get; }
        public bool IsSelected { get; }

        private ListingRowViewModel(ListingView listing, ContentPresentation content, bool isSelected)
        {
            ListingId = listing.ListingId;
            Title = content.ModelName(listing.DefinitionId);
            StorageText = TurkishTexts.Storage(listing.StorageGb);
            AgeText = TurkishTexts.Age(listing.AgeMonths);
            PriceText = TurkishTexts.Asking(listing.AskingPrice);
            RemainingText = TurkishTexts.Remaining(listing.RemainingDays);
            BoxText = TurkishTexts.Box(listing.HasBox);
            InvoiceText = TurkishTexts.Invoice(listing.HasInvoice);
            SellerText = TurkishTexts.Seller(content.NpcName(listing.SellerNpcId));
            IsSelected = isSelected;
        }

        public static ListingRowViewModel From(ListingView listing, ContentPresentation content, bool isSelected)
        {
            if (listing == null)
            {
                throw new ArgumentNullException(nameof(listing));
            }

            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            return new ListingRowViewModel(listing, content, isSelected);
        }
    }
}
