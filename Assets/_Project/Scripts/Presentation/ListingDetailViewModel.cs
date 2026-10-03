using System;
using System.Collections.Generic;
using System.Linq;
using Esnaf.Domain.Appraisal;
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

        /// <summary>Büyük telefon görseli için model kimliği (Gün 13.3).</summary>
        public string DefinitionId { get; }

        public string ConditionLine { get; }
        public string AskingLine { get; }

        /// <summary>"Tahmini değer: ~9.400 ₺" (piyasa referansı) ve açıklaması; "kârlı/kötü ilan" kararı DEĞİL.</summary>
        public string EstimatedLine { get; }

        public string EstimatedHint { get; }

        /// <summary>"Ekspertiz: yapılmadı" ya da "Ekspertiz: yapıldı (Seviye)".</summary>
        public string AppraisalStatusLine { get; }

        /// <summary>Ekspertiz yapıldıysa mevcut sistemin değer aralığı; yapılmadıysa null.</summary>
        public string AppraisalRangeLine { get; }

        public string AppraisalButtonText { get; }
        public string NegotiationButtonText { get; }
        public string BackButtonText { get; }

        private ListingDetailViewModel(ListingView listing, ContentPresentation content, IReadOnlyList<AppraisalView> appraisals)
        {
            DefinitionId = listing.DefinitionId;
            ConditionLine = TurkishTexts.Condition(listing.AgeMonths, listing.HasBox, listing.HasInvoice, listing.Tags.Contains(ListingTags.UrgentSale));
            AskingLine = TurkishTexts.AskingFull(listing.AskingPrice);
            EstimatedLine = ListingEstimate.Text(appraisals);
            EstimatedHint = TurkishTexts.EstimatedHint;
            AppraisalButtonText = TurkishTexts.DoAppraisalButton;
            NegotiationButtonText = TurkishTexts.DoNegotiationButton;
            BackButtonText = TurkishTexts.BackButton;
            AppraisalStatusLine = TurkishTexts.AppraisalNotDone;
            if (appraisals != null && appraisals.Count > 0)
            {
                AppraisalView last = appraisals[appraisals.Count - 1];
                string levelName = last.LevelId;
                foreach (AppraisalLevelInfo level in content.AppraisalLevels)
                {
                    if (level.Id == last.LevelId)
                    {
                        levelName = level.Name;
                    }
                }

                AppraisalStatusLine = TurkishTexts.AppraisalDone(levelName);
                AppraisalRangeLine = last.ValueRange == null ? null : TurkishTexts.AppraisalValueRange(last.ValueRange.Min, last.ValueRange.Max);
            }

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

        public static ListingDetailViewModel From(ListingView listing, ContentPresentation content, IReadOnlyList<AppraisalView> appraisals = null)
        {
            if (listing == null)
            {
                throw new ArgumentNullException(nameof(listing));
            }

            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            return new ListingDetailViewModel(listing, content, appraisals);
        }
    }
}
