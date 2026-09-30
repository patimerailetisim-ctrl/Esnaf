using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Esnaf.Core;

namespace Esnaf.Domain.Appraisal
{
    /// <summary>
    /// UI'ya giden DEĞİŞMEZ ekspertiz görünümü: bulgu, aralıklar, kartlar. Gerçek değer, gerçek nitelikler, yanlış alarm bayrağı ve
    /// tohum burada YOKTUR (bir test üyeleri denetler).
    /// </summary>
    public sealed class AppraisalView
    {
        public long ResultId { get; }
        public long ListingId { get; }
        public long InstanceId { get; }
        public string LevelId { get; }
        public int Day { get; }
        public Money Fee { get; }
        public IReadOnlyList<FindingView> Findings { get; }
        public NumericRange BatteryRange { get; }
        public NumericRange BodyRange { get; }
        public MoneyRange ValueRange { get; }
        public IReadOnlyList<CardView> Cards { get; }

        public AppraisalView(
            long resultId,
            long listingId,
            long instanceId,
            string levelId,
            int day,
            Money fee,
            IEnumerable<FindingView> findings,
            NumericRange batteryRange,
            NumericRange bodyRange,
            MoneyRange valueRange,
            IEnumerable<CardView> cards)
        {
            if (findings == null)
            {
                throw new ArgumentNullException(nameof(findings));
            }

            if (cards == null)
            {
                throw new ArgumentNullException(nameof(cards));
            }

            ResultId = resultId;
            ListingId = listingId;
            InstanceId = instanceId;
            LevelId = levelId;
            Day = day;
            Fee = fee;
            Findings = new ReadOnlyCollection<FindingView>(new List<FindingView>(findings));
            BatteryRange = batteryRange;
            BodyRange = bodyRange;
            ValueRange = valueRange;
            Cards = new ReadOnlyCollection<CardView>(new List<CardView>(cards));
        }
    }
}
