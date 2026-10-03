using System;
using System.Collections.Generic;
using System.Linq;
using Esnaf.Domain.Appraisal;
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

        /// <summary>Telefon görseli için model kimliği (Gün 13.3).</summary>
        public string DefinitionId { get; }

        /// <summary>"Kondisyon: 14 ay kullanılmış • Kutu var • Fatura yok".</summary>
        public string ConditionText { get; }

        /// <summary>"İstenen fiyat: 12.750 ₺".</summary>
        public string AskingText { get; }

        /// <summary>"Tahmini değer: ~9.400 ₺" (piyasa referansı; "kârlı/kötü ilan" kararı DEĞİL).</summary>
        public string EstimatedText { get; }

        public string InspectButtonText { get; }

        // ---- kart parçaları (Gün 13.4 UI revizyonu): yalnızca herkese açık ilan bilgisi; gizli durum etiketi uydurulmaz ----

        /// <summary>"14 ay" (kullanım süresi etiketi).</summary>
        public string AgeChip { get; }

        /// <summary>"Kutu var" / "Kutu yok".</summary>
        public string BoxChip { get; }

        public bool HasBox { get; }

        /// <summary>"Fatura var" / "Fatura yok".</summary>
        public string InvoiceChip { get; }

        public bool HasInvoice { get; }

        /// <summary>İlan "Acil satış" ise "Acil satış", değilse null.</summary>
        public string UrgentChip { get; }

        /// <summary>"İstenen fiyat" (etiket) ve "31.650 ₺" (altın vurgulu değer).</summary>
        public string AskingLabelText { get; }

        public string PriceValueText { get; }

        private ListingRowViewModel(ListingView listing, ContentPresentation content, bool isSelected, IReadOnlyList<AppraisalView> appraisals)
        {
            DefinitionId = listing.DefinitionId;
            ConditionText = TurkishTexts.Condition(listing.AgeMonths, listing.HasBox, listing.HasInvoice, listing.Tags.Contains(ListingTags.UrgentSale));
            AskingText = TurkishTexts.AskingFull(listing.AskingPrice);
            EstimatedText = ListingEstimate.Text(appraisals);
            InspectButtonText = TurkishTexts.InspectButton;
            AgeChip = TurkishTexts.Age(listing.AgeMonths);
            HasBox = listing.HasBox;
            HasInvoice = listing.HasInvoice;
            BoxChip = listing.HasBox ? "Kutu var" : "Kutu yok";
            InvoiceChip = listing.HasInvoice ? "Fatura var" : "Fatura yok";
            UrgentChip = listing.Tags.Contains(ListingTags.UrgentSale) ? "Acil sat\u0131\u015F" : null;
            AskingLabelText = "\u0130stenen fiyat";
            PriceValueText = MoneyFormatter.Format(listing.AskingPrice);
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

        public static ListingRowViewModel From(ListingView listing, ContentPresentation content, bool isSelected, IReadOnlyList<AppraisalView> appraisals = null)
        {
            if (listing == null)
            {
                throw new ArgumentNullException(nameof(listing));
            }

            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            return new ListingRowViewModel(listing, content, isSelected, appraisals);
        }
    }
}
