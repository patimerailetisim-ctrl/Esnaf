using System;
using Esnaf.Core;
using Esnaf.Domain.Phone;

namespace Esnaf.Domain.Products
{
    /// <summary>
    /// MVP telefon değer formülü (GDD v0.2 4.1-4.2):
    /// <code>
    /// Referans Fiyat (RF) = Baz Fiyat × Hafıza × Yaş × Talep
    /// Gerçek Değer   (V)  = RF × Pil × Ekran × Kasa × Kamera × Paket
    /// </code>
    /// Tüm çarpanlar value_tables.json'dan gelir; koda gömülü sayı yoktur. Sonuçlar 10 TL'ye yuvarlanır.
    /// Bu sınıf durum tutmaz ve ürün örneğini/tanımı DEĞİŞTİRMEZ. Gerçek değer kayda yazılmaz; her seferinde hesaplanır.
    /// </summary>
    public sealed class ValueCalculator
    {
        private const long MaxPercent = 100;

        private readonly ValueTables _tables;

        public ValueCalculator(ValueTables tables)
        {
            if (tables == null)
            {
                throw new ArgumentNullException(nameof(tables));
            }

            _tables = tables;
        }

        /// <param name="demandMultiplier">Model talep çarpanı (Gün 5'e kadar 1,0). Pozitif ve sonlu olmalı.</param>
        public ValueBreakdown Calculate(ProductInstance instance, ProductDefinition definition, double demandMultiplier = 1.0)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (!(demandMultiplier > 0.0) || double.IsInfinity(demandMultiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(demandMultiplier), "Demand multiplier must be positive and finite.");
            }

            if (!string.Equals(instance.DefinitionId, definition.Id, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Instance " + instance.InstanceId + " belongs to '" + instance.DefinitionId + "', not '" + definition.Id + "'.",
                    nameof(definition));
            }

            if (instance.AgeMonths < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(instance), "Age cannot be negative.");
            }

            double storageMultiplier;
            if (!definition.TryGetStorageMultiplier(instance.StorageGb, out storageMultiplier))
            {
                throw new ArgumentException(
                    definition.Id + " is not sold with " + instance.StorageGb + " GB.", nameof(instance));
            }

            long battery = RequirePercent(instance.GetNumber(PhoneAttributes.Battery), PhoneAttributes.Battery);
            long body = RequirePercent(instance.GetNumber(PhoneAttributes.Body), PhoneAttributes.Body);
            string screenId = instance.GetText(PhoneAttributes.Screen);
            string cameraId = instance.GetText(PhoneAttributes.Camera);
            bool box = instance.GetFlag(PhoneAttributes.Box);
            bool invoice = instance.GetFlag(PhoneAttributes.Invoice);

            double screenMultiplier;
            if (!_tables.TryGetScreenMultiplier(screenId, out screenMultiplier))
            {
                throw new ArgumentException("Unknown screen state '" + screenId + "'.", nameof(instance));
            }

            double cameraMultiplier;
            if (!_tables.TryGetCameraMultiplier(cameraId, out cameraMultiplier))
            {
                throw new ArgumentException("Unknown camera state '" + cameraId + "'.", nameof(instance));
            }

            double ageMultiplier = AgeMultiplier(instance.AgeMonths);
            double batteryMultiplier = 1.0 - _tables.BatteryPenaltyPerPoint * Math.Max(0, _tables.BatteryFullAtOrAbove - (int)battery);
            double bodyMultiplier = _tables.BodyBase + _tables.BodySpan * (body / 100.0);
            double packageMultiplier = 1.0 + (box ? _tables.BoxBonus : 0.0) + (invoice ? _tables.InvoiceBonus : 0.0);

            double referenceExact = definition.BasePrice.Tl * storageMultiplier * ageMultiplier * demandMultiplier;
            double valueExact = referenceExact * batteryMultiplier * screenMultiplier * bodyMultiplier * cameraMultiplier * packageMultiplier;

            return new ValueBreakdown(
                referenceExact,
                valueExact,
                storageMultiplier,
                ageMultiplier,
                demandMultiplier,
                batteryMultiplier,
                screenMultiplier,
                bodyMultiplier,
                cameraMultiplier,
                packageMultiplier);
        }

        public Money ReferencePrice(ProductInstance instance, ProductDefinition definition, double demandMultiplier = 1.0)
        {
            return Calculate(instance, definition, demandMultiplier).ReferencePrice;
        }

        public Money TrueValue(ProductInstance instance, ProductDefinition definition, double demandMultiplier = 1.0)
        {
            return Calculate(instance, definition, demandMultiplier).TrueValue;
        }

        // Yaşı fromMonths'u aşmayan son bant geçerlidir.
        private double AgeMultiplier(int ageMonths)
        {
            double multiplier = 0.0;
            bool found = false;
            for (int i = 0; i < _tables.AgeBands.Count; i++)
            {
                if (ageMonths >= _tables.AgeBands[i].FromMonths)
                {
                    multiplier = _tables.AgeBands[i].Multiplier;
                    found = true;
                }
            }

            if (!found)
            {
                throw new InvalidOperationException("No age band covers " + ageMonths + " months; value tables are invalid.");
            }

            return multiplier;
        }

        private static long RequirePercent(long value, string name)
        {
            if (value < 0 || value > MaxPercent)
            {
                throw new ArgumentOutOfRangeException(name, "Percentage must be between 0 and 100 but was " + value + ".");
            }

            return value;
        }
    }
}
