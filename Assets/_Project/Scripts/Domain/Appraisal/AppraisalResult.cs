using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Esnaf.Core;

namespace Esnaf.Domain.Appraisal
{
    /// <summary>
    /// EKSPERTİZ SONUCU (durum, kayda girer; GDD v0.3 4.3): oyuncunun "bildikleri". Gerçek değeri ve gerçek nitelikleri içermez.
    /// Aynı ürün + aynı seviye için tek sonuç vardır ve kilitlidir (I5).
    /// </summary>
    public sealed class AppraisalResult
    {
        public long ResultId { get; }
        public long InstanceId { get; }
        public string DefinitionId { get; }
        public string LevelId { get; }
        public int Day { get; }
        public Money Fee { get; }

        /// <summary>Sonucun üretildiği tohum: hash(masterSeed, instanceId, level).</summary>
        public ulong Seed { get; }

        public IReadOnlyList<AttributeFinding> Findings { get; }
        public NumericRange BatteryRange { get; }
        public NumericRange BodyRange { get; }
        public MoneyRange ValueRange { get; }
        public IReadOnlyList<TrumpCard> Cards { get; }

        public AppraisalResult(
            long resultId,
            long instanceId,
            string definitionId,
            string levelId,
            int day,
            Money fee,
            ulong seed,
            IEnumerable<AttributeFinding> findings,
            NumericRange batteryRange,
            NumericRange bodyRange,
            MoneyRange valueRange,
            IEnumerable<TrumpCard> cards)
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
            InstanceId = instanceId;
            DefinitionId = definitionId;
            LevelId = levelId;
            Day = day;
            Fee = fee;
            Seed = seed;
            Findings = new ReadOnlyCollection<AttributeFinding>(new List<AttributeFinding>(findings));
            BatteryRange = batteryRange;
            BodyRange = bodyRange;
            ValueRange = valueRange;
            Cards = new ReadOnlyCollection<TrumpCard>(new List<TrumpCard>(cards));
        }
    }
}
