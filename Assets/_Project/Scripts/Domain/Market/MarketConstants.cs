using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Esnaf.Domain.Market
{
    /// <summary>
    /// Pazar (ilan üretimi) sabitleri: economy_constants.json içindeki "market" bölümü (GDD 4.6, v0.3 P1–P5, v0.2 11.1).
    /// Değişmez tanım verisidir; doğrulamayı <c>ContentValidator</c> yapar. Gün'e bağlı değerler "fromDay" bantlarıyla verilir.
    /// </summary>
    public sealed class MarketConstants
    {
        private readonly ReadOnlyCollection<ListingCountBand> _listingCounts;
        private readonly ReadOnlyCollection<SegmentWeightBand> _segmentWeights;
        private readonly ReadOnlyCollection<ModelAvailabilityRule> _modelAvailability;
        private readonly ReadOnlyCollection<QuotaBand> _jackpotMaxPerDay;
        private readonly ReadOnlyCollection<QuotaBand> _trapMaxPerDay;
        private readonly ReadOnlyCollection<HiddenDefectRule> _hiddenDefects;

        public IReadOnlyList<ListingCountBand> ListingCounts
        {
            get { return _listingCounts; }
        }

        /// <summary>İlan ömrü (gün) alt ve üst sınırı, ikisi de dahil.</summary>
        public int LifetimeMinDays { get; }
        public int LifetimeMaxDays { get; }

        public IReadOnlyList<SegmentWeightBand> SegmentWeights
        {
            get { return _segmentWeights; }
        }

        public IReadOnlyList<ModelAvailabilityRule> ModelAvailability
        {
            get { return _modelAvailability; }
        }

        /// <summary>P2: bu güne kadar (dahil) öğrenme dostu satıcılar ilanların <see cref="LearningFriendlyShare"/> oranını alır.</summary>
        public int LearningFriendlyUntilDay { get; }
        public double LearningFriendlyShare { get; }

        /// <summary>P1: bu günden itibaren her gün en az <see cref="MinOpportunitiesPerDay"/> makul fırsat (R ≤ oran × değer).</summary>
        public int OpportunityFromDay { get; }
        public int MinOpportunitiesPerDay { get; }
        public double OpportunityMaxRejectRatio { get; }

        /// <summary>P3: ret oranı bu değerin altındaki satıcının ilanı "jackpot"tur; günlük üst sınır <see cref="JackpotMax"/>.</summary>
        public double JackpotRejectRatioBelow { get; }

        public IReadOnlyList<QuotaBand> JackpotMaxPerDay
        {
            get { return _jackpotMaxPerDay; }
        }

        /// <summary>P5: bu günden itibaren her gün en az <see cref="TrapMinPerDay"/> tuzak; üst sınır <see cref="TrapMax"/>.</summary>
        public int TrapFromDay { get; }
        public int TrapMinPerDay { get; }

        public IReadOnlyList<QuotaBand> TrapMaxPerDay
        {
            get { return _trapMaxPerDay; }
        }

        /// <summary>Tuzak: satıcı kusuru sakladı ve inandığı değer ≥ bu oran × gerçek değer.</summary>
        public double TrapValueRatio { get; }

        /// <summary>İstenen fiyatın yuvarlandığı adım (TL); 10'un katıdır.</summary>
        public int AskingPriceStep { get; }

        public IReadOnlyList<HiddenDefectRule> HiddenDefects
        {
            get { return _hiddenDefects; }
        }

        public GuidedListingSpec GuidedListing { get; }

        public MarketConstants(
            IEnumerable<ListingCountBand> listingCounts,
            int lifetimeMinDays,
            int lifetimeMaxDays,
            IEnumerable<SegmentWeightBand> segmentWeights,
            IEnumerable<ModelAvailabilityRule> modelAvailability,
            int learningFriendlyUntilDay,
            double learningFriendlyShare,
            int opportunityFromDay,
            int minOpportunitiesPerDay,
            double opportunityMaxRejectRatio,
            double jackpotRejectRatioBelow,
            IEnumerable<QuotaBand> jackpotMaxPerDay,
            int trapFromDay,
            int trapMinPerDay,
            IEnumerable<QuotaBand> trapMaxPerDay,
            double trapValueRatio,
            int askingPriceStep,
            IEnumerable<HiddenDefectRule> hiddenDefects,
            GuidedListingSpec guidedListing)
        {
            if (guidedListing == null)
            {
                throw new ArgumentNullException(nameof(guidedListing));
            }

            _listingCounts = Freeze(listingCounts, nameof(listingCounts));
            _segmentWeights = Freeze(segmentWeights, nameof(segmentWeights));
            _modelAvailability = Freeze(modelAvailability, nameof(modelAvailability));
            _jackpotMaxPerDay = Freeze(jackpotMaxPerDay, nameof(jackpotMaxPerDay));
            _trapMaxPerDay = Freeze(trapMaxPerDay, nameof(trapMaxPerDay));
            _hiddenDefects = Freeze(hiddenDefects, nameof(hiddenDefects));
            LifetimeMinDays = lifetimeMinDays;
            LifetimeMaxDays = lifetimeMaxDays;
            LearningFriendlyUntilDay = learningFriendlyUntilDay;
            LearningFriendlyShare = learningFriendlyShare;
            OpportunityFromDay = opportunityFromDay;
            MinOpportunitiesPerDay = minOpportunitiesPerDay;
            OpportunityMaxRejectRatio = opportunityMaxRejectRatio;
            JackpotRejectRatioBelow = jackpotRejectRatioBelow;
            TrapFromDay = trapFromDay;
            TrapMinPerDay = trapMinPerDay;
            TrapValueRatio = trapValueRatio;
            AskingPriceStep = askingPriceStep;
            GuidedListing = guidedListing;
        }

        /// <summary>O gün için geçerli ilan sayısı bandı (en büyük FromDay ≤ gün). Bantlar Gün 1'den başlar.</summary>
        public ListingCountBand CountBandFor(int day)
        {
            ListingCountBand found = _listingCounts[0];
            for (int i = 1; i < _listingCounts.Count; i++)
            {
                if (_listingCounts[i].FromDay <= day)
                {
                    found = _listingCounts[i];
                }
            }

            return found;
        }

        /// <summary>O gün için geçerli segment ağırlıkları (en büyük FromDay ≤ gün).</summary>
        public SegmentWeightBand SegmentBandFor(int day)
        {
            SegmentWeightBand found = _segmentWeights[0];
            for (int i = 1; i < _segmentWeights.Count; i++)
            {
                if (_segmentWeights[i].FromDay <= day)
                {
                    found = _segmentWeights[i];
                }
            }

            return found;
        }

        /// <summary>O gün için jackpot üst sınırı.</summary>
        public int JackpotMax(int day)
        {
            return MaxFor(_jackpotMaxPerDay, day);
        }

        /// <summary>O gün için tuzak üst sınırı; henüz sınır bandı başlamadıysa <see cref="int.MaxValue"/> (sınırsız).</summary>
        public int TrapMax(int day)
        {
            return MaxFor(_trapMaxPerDay, day);
        }

        /// <summary>Model o gün ilanlarda çıkabilir mi? Kuralı olmayan model her zaman çıkabilir.</summary>
        public bool IsModelAvailable(string definitionId, int day)
        {
            for (int i = 0; i < _modelAvailability.Count; i++)
            {
                if (string.Equals(_modelAvailability[i].DefinitionId, definitionId, StringComparison.Ordinal))
                {
                    return day >= _modelAvailability[i].FromDay;
                }
            }

            return true;
        }

        private static int MaxFor(IReadOnlyList<QuotaBand> bands, int day)
        {
            int max = int.MaxValue;
            for (int i = 0; i < bands.Count; i++)
            {
                if (bands[i].FromDay <= day)
                {
                    max = bands[i].Max;
                }
            }

            return max;
        }

        private static ReadOnlyCollection<T> Freeze<T>(IEnumerable<T> items, string name)
        {
            if (items == null)
            {
                throw new ArgumentNullException(name);
            }

            return new ReadOnlyCollection<T>(new List<T>(items));
        }
    }
}
