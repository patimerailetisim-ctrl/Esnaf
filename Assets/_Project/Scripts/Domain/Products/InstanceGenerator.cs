using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Esnaf.Core;
using Esnaf.Domain.Phone;

namespace Esnaf.Domain.Products
{
    /// <summary>
    /// Bir model tanımından, bir durum profili seçerek SOMUT telefon örneği üretir (GDD v0.2 4.3).
    /// Yalnızca verilen <see cref="IRandom"/> akışını kullanır (aynı akış durumu + aynı girdi = aynı ürün).
    /// Üretilen örnek Pazar'da (Market) başlar; satıcı, ilan ve fiyat bilgisini ilan üreticisi sonra doldurur.
    ///
    /// Rastgele çekim SIRASI (değişirse altın testler bilinçli olarak güncellenir):
    /// profil → hafıza → yaş → pil → kasa → ekran → kamera → kutu → fatura.
    /// </summary>
    public sealed class InstanceGenerator
    {
        private readonly ReadOnlyCollection<ConditionProfile> _profiles;
        private readonly IdGenerator _ids;

        public InstanceGenerator(IReadOnlyList<ConditionProfile> profiles, IdGenerator ids)
        {
            if (profiles == null)
            {
                throw new ArgumentNullException(nameof(profiles));
            }

            if (ids == null)
            {
                throw new ArgumentNullException(nameof(ids));
            }

            if (profiles.Count == 0)
            {
                throw new ArgumentException("At least one condition profile is required.", nameof(profiles));
            }

            _profiles = new ReadOnlyCollection<ConditionProfile>(new List<ConditionProfile>(profiles));
            _ids = ids;
        }

        public ProductInstance Generate(ProductDefinition definition, int day, IRandom rng)
        {
            ConditionProfile ignored;
            return Generate(definition, day, rng, out ignored);
        }

        /// <param name="day">Oyun günü (&gt;= 1). Henüz kullanılabilir olmayan profiller (ör. Sorunlu, Gün 5) seçilmez.</param>
        /// <param name="profile">Ürünün üretildiği profil (oyuncuya gösterilmez; test/simülasyon içindir).</param>
        public ProductInstance Generate(ProductDefinition definition, int day, IRandom rng, out ConditionProfile profile)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            if (day < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(day), "Day must be at least 1.");
            }

            if (definition.StorageOptions.Count == 0)
            {
                throw new ArgumentException(definition.Id + " has no storage options.", nameof(definition));
            }

            if (definition.MinAgeMonths < 0 || definition.MinAgeMonths > definition.MaxAgeMonths)
            {
                throw new ArgumentException(definition.Id + " has an invalid age range.", nameof(definition));
            }

            ConditionProfile chosen = PickProfile(day, rng);

            StorageOption storage = definition.StorageOptions[rng.NextInt(definition.StorageOptions.Count)];
            int age = rng.NextInt(definition.MinAgeMonths, definition.MaxAgeMonths + 1);
            int battery = rng.NextInt(chosen.BatteryMin, chosen.BatteryMax + 1);
            int body = rng.NextInt(chosen.BodyMin, chosen.BodyMax + 1);
            string screen = PickWeighted(chosen.ScreenChoices, rng);
            string camera = PickWeighted(chosen.CameraChoices, rng);
            bool box = rng.Chance(chosen.BoxChance);
            bool invoice = rng.Chance(chosen.InvoiceChance);

            var instance = new ProductInstance
            {
                InstanceId = _ids.Next(),
                DefinitionId = definition.Id,
                StorageGb = storage.Gb,
                AgeMonths = age,
                Location = ProductLocation.Market
            };
            instance.Attributes[PhoneAttributes.Battery] = AttributeValue.FromNumber(battery);
            instance.Attributes[PhoneAttributes.Screen] = AttributeValue.FromText(screen);
            instance.Attributes[PhoneAttributes.Body] = AttributeValue.FromNumber(body);
            instance.Attributes[PhoneAttributes.Camera] = AttributeValue.FromText(camera);
            instance.Attributes[PhoneAttributes.Box] = AttributeValue.FromFlag(box);
            instance.Attributes[PhoneAttributes.Invoice] = AttributeValue.FromFlag(invoice);

            profile = chosen;
            return instance;
        }

        private ConditionProfile PickProfile(int day, IRandom rng)
        {
            int total = 0;
            for (int i = 0; i < _profiles.Count; i++)
            {
                if (_profiles[i].AvailableFromDay <= day)
                {
                    total += _profiles[i].Weight;
                }
            }

            if (total <= 0)
            {
                throw new InvalidOperationException("No condition profile is available on day " + day + ".");
            }

            int roll = rng.NextInt(total);
            int cumulative = 0;
            for (int i = 0; i < _profiles.Count; i++)
            {
                if (_profiles[i].AvailableFromDay > day)
                {
                    continue;
                }

                cumulative += _profiles[i].Weight;
                if (roll < cumulative)
                {
                    return _profiles[i];
                }
            }

            throw new InvalidOperationException("Weighted profile selection failed."); // ulaşılamaz: roll < total
        }

        private static string PickWeighted(IReadOnlyList<WeightedValue> choices, IRandom rng)
        {
            int total = 0;
            for (int i = 0; i < choices.Count; i++)
            {
                total += choices[i].Weight;
            }

            int roll = rng.NextInt(total);
            int cumulative = 0;
            for (int i = 0; i < choices.Count; i++)
            {
                cumulative += choices[i].Weight;
                if (roll < cumulative)
                {
                    return choices[i].Value;
                }
            }

            throw new InvalidOperationException("Weighted selection failed."); // ulaşılamaz: roll < total
        }
    }
}
