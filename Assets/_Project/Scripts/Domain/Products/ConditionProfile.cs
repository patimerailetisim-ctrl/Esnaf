using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Esnaf.Domain.Products
{
    /// <summary>
    /// Durum profili (GDD v0.2 4.3: Temiz, Kullanılmış, Yıpranmış, Tamirli, Sorunlu): bir ilan üretilirken profil seçilir,
    /// sonra nitelikler bu profilin aralıklarından çekilir. Tanım verisidir (condition_profiles.json), salt okunurdur.
    /// Bu sınıf doğrulama yapmaz; kuralları <c>ContentValidator.ValidateConditionProfiles</c> uygular.
    /// </summary>
    public sealed class ConditionProfile
    {
        private readonly ReadOnlyCollection<WeightedValue> _screenChoices;
        private readonly ReadOnlyCollection<WeightedValue> _cameraChoices;

        public string Id { get; }
        public string Name { get; }

        /// <summary>Profil seçilme ağırlığı (kullanılabilir profiller arasında orantılı).</summary>
        public int Weight { get; }

        /// <summary>Profilin ilk kullanılabildiği oyun günü (örn. Sorunlu profil için 5).</summary>
        public int AvailableFromDay { get; }

        public int BatteryMin { get; }
        public int BatteryMax { get; }
        public int BodyMin { get; }
        public int BodyMax { get; }

        public IReadOnlyList<WeightedValue> ScreenChoices
        {
            get { return _screenChoices; }
        }

        public IReadOnlyList<WeightedValue> CameraChoices
        {
            get { return _cameraChoices; }
        }

        /// <summary>Kutu bulunma olasılığı [0,1].</summary>
        public double BoxChance { get; }

        /// <summary>Fatura bulunma olasılığı [0,1].</summary>
        public double InvoiceChance { get; }

        public ConditionProfile(
            string id,
            string name,
            int weight,
            int availableFromDay,
            int batteryMin,
            int batteryMax,
            int bodyMin,
            int bodyMax,
            IEnumerable<WeightedValue> screenChoices,
            IEnumerable<WeightedValue> cameraChoices,
            double boxChance,
            double invoiceChance)
        {
            if (screenChoices == null)
            {
                throw new ArgumentNullException(nameof(screenChoices));
            }

            if (cameraChoices == null)
            {
                throw new ArgumentNullException(nameof(cameraChoices));
            }

            Id = id;
            Name = name;
            Weight = weight;
            AvailableFromDay = availableFromDay;
            BatteryMin = batteryMin;
            BatteryMax = batteryMax;
            BodyMin = bodyMin;
            BodyMax = bodyMax;
            _screenChoices = new ReadOnlyCollection<WeightedValue>(new List<WeightedValue>(screenChoices));
            _cameraChoices = new ReadOnlyCollection<WeightedValue>(new List<WeightedValue>(cameraChoices));
            BoxChance = boxChance;
            InvoiceChance = invoiceChance;
        }
    }
}
