using System;
using System.Collections.Generic;
using Esnaf.Core;
using Esnaf.Domain.Phone;
using Esnaf.Domain.Products;

namespace Esnaf.Domain.Appraisal
{
    /// <summary>
    /// Ekspertiz hesabı (SAF; rastgelelik enjekte edilir). GDD v0.2 5.2–5.3, 7.4, UA9–UA11.
    /// <list type="number">
    /// <item>Her kontrol için bir bulgu: gerçek kusur varsa tespit olasılığıyla, yoksa yanlış alarm olasılığıyla "bulundu".</item>
    /// <item>Pil ve kasa: aralık = merkez ± yarı genişlik; merkez gerçekten, yarı genişliğin <c>centerShift</c> oranına kadar kayar.</item>
    /// <item>Değer aralığı: gözlenen durumla değer (gizli kusurlar "yokmuş gibi"; bulunan kusur çarpanı ^ kanıt gücü kadar geri katılır),
    /// gürültüyle çarpılır, ± yarı genişlik ve 10 TL yuvarlama.</item>
    /// <item>Bulunan her bulgu bir koz kartıdır: sorunun TL değeri = kusursuz değer × (1 − kusur çarpanı).</item>
    /// </list>
    /// Rastgele çekim sırası SABİTTİR: her kontrol için bir <c>Chance</c> (veri sırasıyla), sonra pil, kasa, değer için birer <c>NextDouble</c>.
    /// Ürün örneğini DEĞİŞTİRMEZ.
    /// </summary>
    public sealed class AppraisalCalculator
    {
        private const int MaxPercent = 100;

        private readonly ValueCalculator _values;
        private readonly AppraisalConfig _config;

        public AppraisalCalculator(ValueCalculator values, AppraisalConfig config)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            _values = values;
            _config = config;
        }

        public AppraisalEvaluation Evaluate(ProductInstance instance, ProductDefinition definition, AppraisalLevel level, IRandom rng)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            IReadOnlyList<AppraisalCheck> checks = _config.Checks;
            var actual = new string[checks.Count];
            var isDefect = new bool[checks.Count];
            var found = new bool[checks.Count];
            for (int i = 0; i < checks.Count; i++)
            {
                actual[i] = TextOf(instance, checks[i].Attribute);
                isDefect[i] = actual[i] != null && checks[i].IsDefect(actual[i]);
                double chance = isDefect[i] ? level.DetectChance(checks[i].Attribute) : level.FalseAlarmChance;
                found[i] = rng.Chance(chance);
            }

            double batteryU = rng.NextDouble();
            double bodyU = rng.NextDouble();
            double valueU = rng.NextDouble();

            var findings = new List<AttributeFinding>(checks.Count);
            for (int i = 0; i < checks.Count; i++)
            {
                findings.Add(new AttributeFinding(
                    checks[i].Attribute, checks[i].WordingKey, found[i], level.Confidence, level.EvidencePower, found[i] && !isDefect[i]));
            }

            int batteryCenter = Center((int)instance.GetNumber(PhoneAttributes.Battery), level.BatteryHalfWidth, level.CenterShift, batteryU);
            int bodyCenter = Center((int)instance.GetNumber(PhoneAttributes.Body), level.BodyHalfWidth, level.CenterShift, bodyU);
            NumericRange batteryRange = RangeOf(batteryCenter, level.BatteryHalfWidth);
            NumericRange bodyRange = RangeOf(bodyCenter, level.BodyHalfWidth);

            MoneyRange valueRange = null;
            if (level.ValueHalfWidth.HasValue)
            {
                valueRange = ValueRangeOf(instance, definition, level, checks, actual, isDefect, found, batteryCenter, bodyCenter, valueU);
            }

            return new AppraisalEvaluation(findings, batteryRange, bodyRange, valueRange, CardsOf(instance, definition, level, checks, actual, isDefect, found));
        }

        private MoneyRange ValueRangeOf(
            ProductInstance instance,
            ProductDefinition definition,
            AppraisalLevel level,
            IReadOnlyList<AppraisalCheck> checks,
            string[] actual,
            bool[] isDefect,
            bool[] found,
            int batteryCenter,
            int bodyCenter,
            double valueU)
        {
            ProductInstance observed = Copy(instance);
            observed.Attributes[PhoneAttributes.Battery] = AttributeValue.FromNumber(batteryCenter);
            observed.Attributes[PhoneAttributes.Body] = AttributeValue.FromNumber(bodyCenter);
            for (int i = 0; i < checks.Count; i++)
            {
                if (isDefect[i])
                {
                    observed.Attributes[checks[i].Attribute] = AttributeValue.FromText(checks[i].CleanValue);
                }
            }

            double value = _values.Calculate(observed, definition).TrueValueExact;
            for (int i = 0; i < checks.Count; i++)
            {
                if (found[i])
                {
                    value *= Math.Pow(DefectMultiplier(checks[i], isDefect[i] ? actual[i] : checks[i].FalseAlarmValue), level.EvidencePower);
                }
            }

            value *= 1.0 + level.ValueNoise * (2.0 * valueU - 1.0);
            double half = level.ValueHalfWidth.Value;
            return new MoneyRange(
                Money.FromDoubleRoundedTo10(value * (1.0 - half)),
                Money.FromDoubleRoundedTo10(value * (1.0 + half)));
        }

        private List<TrumpCard> CardsOf(
            ProductInstance instance,
            ProductDefinition definition,
            AppraisalLevel level,
            IReadOnlyList<AppraisalCheck> checks,
            string[] actual,
            bool[] isDefect,
            bool[] found)
        {
            var cards = new List<TrumpCard>();
            for (int i = 0; i < checks.Count; i++)
            {
                if (!found[i])
                {
                    continue;
                }

                ProductInstance withoutDefect = Copy(instance);
                if (isDefect[i])
                {
                    withoutDefect.Attributes[checks[i].Attribute] = AttributeValue.FromText(checks[i].CleanValue);
                }

                double clean = _values.Calculate(withoutDefect, definition).TrueValueExact;
                double multiplier = DefectMultiplier(checks[i], isDefect[i] ? actual[i] : checks[i].FalseAlarmValue);
                cards.Add(new TrumpCard(
                    checks[i].Attribute,
                    checks[i].WordingKey,
                    level.Confidence,
                    level.EvidencePower,
                    Money.FromDoubleRoundedTo10(clean * (1.0 - multiplier)),
                    !isDefect[i]));
            }

            return cards;
        }

        private double DefectMultiplier(AppraisalCheck check, string value)
        {
            double multiplier;
            bool known = check.Attribute == PhoneAttributes.Screen
                ? _values.Tables.TryGetScreenMultiplier(value, out multiplier)
                : _values.Tables.TryGetCameraMultiplier(value, out multiplier);
            if (!known)
            {
                throw new InvalidOperationException("No value multiplier for " + check.Attribute + " = '" + value + "'.");
            }

            return multiplier;
        }

        private static int Center(int trueValue, int? halfWidth, double centerShift, double u)
        {
            if (!halfWidth.HasValue)
            {
                return trueValue;
            }

            int shift = (int)Math.Round(halfWidth.Value * centerShift * (2.0 * u - 1.0), MidpointRounding.AwayFromZero);
            return Math.Max(0, Math.Min(MaxPercent, trueValue + shift));
        }

        private static NumericRange RangeOf(int center, int? halfWidth)
        {
            if (!halfWidth.HasValue)
            {
                return null;
            }

            return new NumericRange(Math.Max(0, center - halfWidth.Value), Math.Min(MaxPercent, center + halfWidth.Value));
        }

        private static string TextOf(ProductInstance instance, string attribute)
        {
            AttributeValue value;
            if (instance.Attributes.TryGetValue(attribute, out value) && value.Kind == AttributeKind.Text)
            {
                return value.Text;
            }

            return null;
        }

        private static ProductInstance Copy(ProductInstance instance)
        {
            var copy = new ProductInstance
            {
                InstanceId = instance.InstanceId,
                DefinitionId = instance.DefinitionId,
                StorageGb = instance.StorageGb,
                AgeMonths = instance.AgeMonths
            };
            foreach (KeyValuePair<string, AttributeValue> pair in instance.Attributes)
            {
                copy.Attributes[pair.Key] = pair.Value;
            }

            return copy;
        }
    }
}
