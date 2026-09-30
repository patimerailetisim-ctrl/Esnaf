using System;
using System.Collections.Generic;
using Esnaf.Core;
using Esnaf.Domain.Products;

namespace Esnaf.Domain.Appraisal
{
    /// <summary>
    /// Bir ekspertiz seviyesinin (S0–S3) kuralları (GDD v0.2 5.1–5.3). Değişmez tanım verisidir; içerik dosyasından gelir.
    /// </summary>
    public sealed class AppraisalLevel
    {
        private readonly Dictionary<string, double> _detect;

        public string Id { get; }
        public string Name { get; }
        public int UnlockDay { get; }

        /// <summary>Gereken ekipman (S3: "test_device"); gerekmiyorsa null.</summary>
        public string RequiredEquipment { get; }

        public AppraisalConfidence Confidence { get; }

        /// <summary>Kanıt gücü (0,2–1,0): koz kartının gücü ve gözlenen değerdeki kusur üssü.</summary>
        public double EvidencePower { get; }

        public double FalseAlarmChance { get; }

        /// <summary>Pil/kasa aralığının yarı genişliği (puan); aralık verilmiyorsa null (S0: pil beyan).</summary>
        public int? BatteryHalfWidth { get; }
        public int? BodyHalfWidth { get; }

        /// <summary>Değer aralığının yarı genişliği (oran); değer aralığı verilmiyorsa null (S0).</summary>
        public double? ValueHalfWidth { get; }

        /// <summary>Aralık merkezinin, yarı genişliğin bu oranına kadar rastgele kayması (UA10).</summary>
        public double CenterShift { get; }

        /// <summary>Gözlenen değere ±oran rastgele gürültü (UA10).</summary>
        public double ValueNoise { get; }

        /// <summary>Gerçek değerin aralıkta olma tahmini (risk kartındaki "aralık dışı kalma olasılığı" = 1 − bu).</summary>
        public double? CoverageEstimate { get; }

        private readonly Money _feeEntry;
        private readonly Money _feeMid;
        private readonly Money _feeUpper;

        public AppraisalLevel(
            string id,
            string name,
            int unlockDay,
            string requiredEquipment,
            Money feeEntry,
            Money feeMid,
            Money feeUpper,
            AppraisalConfidence confidence,
            double evidencePower,
            IDictionary<string, double> detect,
            double falseAlarmChance,
            int? batteryHalfWidth,
            int? bodyHalfWidth,
            double? valueHalfWidth,
            double centerShift,
            double valueNoise,
            double? coverageEstimate)
        {
            if (detect == null)
            {
                throw new ArgumentNullException(nameof(detect));
            }

            Id = id;
            Name = name;
            UnlockDay = unlockDay;
            RequiredEquipment = requiredEquipment;
            _feeEntry = feeEntry;
            _feeMid = feeMid;
            _feeUpper = feeUpper;
            Confidence = confidence;
            EvidencePower = evidencePower;
            _detect = new Dictionary<string, double>(detect, StringComparer.Ordinal);
            FalseAlarmChance = falseAlarmChance;
            BatteryHalfWidth = batteryHalfWidth;
            BodyHalfWidth = bodyHalfWidth;
            ValueHalfWidth = valueHalfWidth;
            CenterShift = centerShift;
            ValueNoise = valueNoise;
            CoverageEstimate = coverageEstimate;
        }

        /// <summary>Ürünün segmentine göre ekspertiz ücreti.</summary>
        public Money FeeFor(ProductSegment segment)
        {
            switch (segment)
            {
                case ProductSegment.Entry:
                    return _feeEntry;
                case ProductSegment.Mid:
                    return _feeMid;
                default:
                    return _feeUpper;
            }
        }

        /// <summary>Gerçek bir kusuru yakalama olasılığı; kontrol edilmeyen nitelik için 0.</summary>
        public double DetectChance(string attribute)
        {
            double chance;
            return attribute != null && _detect.TryGetValue(attribute, out chance) ? chance : 0.0;
        }

        /// <summary>Doğrulayıcı için: bu seviyenin tespit olasılığı tanımladığı nitelikler.</summary>
        public IEnumerable<string> DetectAttributes
        {
            get { return _detect.Keys; }
        }
    }
}
