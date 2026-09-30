using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Esnaf.Domain.Products
{
    /// <summary>
    /// Değer formülünün çarpan tabloları ve katsayıları (value_tables.json, GDD v0.2 4.2). Yüklendikten sonra salt okunur.
    /// Bu sınıf doğrulama yapmaz; kuralları <c>ContentValidator.ValidateValueTables</c> uygular.
    /// </summary>
    public sealed class ValueTables
    {
        private readonly ReadOnlyCollection<AgeBand> _ageBands;
        private readonly ReadOnlyCollection<IdMultiplier> _screen;
        private readonly ReadOnlyCollection<IdMultiplier> _camera;

        /// <summary>Artan sırada yaş bantları (ilki 0. aydan başlar).</summary>
        public IReadOnlyList<AgeBand> AgeBands
        {
            get { return _ageBands; }
        }

        /// <summary>Bu değer ve üstündeki pil sağlığında pil çarpanı 1,00'dır.</summary>
        public int BatteryFullAtOrAbove { get; }

        /// <summary>Tam değerin altındaki her pil puanı için çarpandan düşülen miktar.</summary>
        public double BatteryPenaltyPerPoint { get; }

        public double BodyBase { get; }
        public double BodySpan { get; }

        public IReadOnlyList<IdMultiplier> ScreenMultipliers
        {
            get { return _screen; }
        }

        public IReadOnlyList<IdMultiplier> CameraMultipliers
        {
            get { return _camera; }
        }

        public double BoxBonus { get; }
        public double InvoiceBonus { get; }

        public ValueTables(
            IEnumerable<AgeBand> ageBands,
            int batteryFullAtOrAbove,
            double batteryPenaltyPerPoint,
            double bodyBase,
            double bodySpan,
            IEnumerable<IdMultiplier> screenMultipliers,
            IEnumerable<IdMultiplier> cameraMultipliers,
            double boxBonus,
            double invoiceBonus)
        {
            if (ageBands == null)
            {
                throw new ArgumentNullException(nameof(ageBands));
            }

            if (screenMultipliers == null)
            {
                throw new ArgumentNullException(nameof(screenMultipliers));
            }

            if (cameraMultipliers == null)
            {
                throw new ArgumentNullException(nameof(cameraMultipliers));
            }

            _ageBands = new ReadOnlyCollection<AgeBand>(new List<AgeBand>(ageBands));
            BatteryFullAtOrAbove = batteryFullAtOrAbove;
            BatteryPenaltyPerPoint = batteryPenaltyPerPoint;
            BodyBase = bodyBase;
            BodySpan = bodySpan;
            _screen = new ReadOnlyCollection<IdMultiplier>(new List<IdMultiplier>(screenMultipliers));
            _camera = new ReadOnlyCollection<IdMultiplier>(new List<IdMultiplier>(cameraMultipliers));
            BoxBonus = boxBonus;
            InvoiceBonus = invoiceBonus;
        }

        public bool TryGetScreenMultiplier(string id, out double multiplier)
        {
            return TryFind(_screen, id, out multiplier);
        }

        public bool TryGetCameraMultiplier(string id, out double multiplier)
        {
            return TryFind(_camera, id, out multiplier);
        }

        private static bool TryFind(IReadOnlyList<IdMultiplier> list, string id, out double multiplier)
        {
            if (id != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (string.Equals(list[i].Id, id, StringComparison.Ordinal))
                    {
                        multiplier = list[i].Multiplier;
                        return true;
                    }
                }
            }

            multiplier = 0.0;
            return false;
        }
    }
}
