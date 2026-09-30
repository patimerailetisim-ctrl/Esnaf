using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Esnaf.Domain.Market
{
    /// <summary>
    /// Kusuru saklayan satıcının "görmezden geldiği" bir nitelik: nitelik bu değerlerden birindeyse satıcı onu
    /// <see cref="CleanValue"/>'daymış gibi değerlendirir (v0.2 Senaryo 4: ekran "değişmiş", kamera arızalı).
    /// </summary>
    public sealed class HiddenDefectRule
    {
        private readonly ReadOnlyCollection<string> _hiddenValues;

        public string Attribute { get; }
        public string CleanValue { get; }

        public IReadOnlyList<string> HiddenValues
        {
            get { return _hiddenValues; }
        }

        public HiddenDefectRule(string attribute, IEnumerable<string> hiddenValues, string cleanValue)
        {
            if (hiddenValues == null)
            {
                throw new ArgumentNullException(nameof(hiddenValues));
            }

            Attribute = attribute;
            CleanValue = cleanValue;
            _hiddenValues = new ReadOnlyCollection<string>(new List<string>(hiddenValues));
        }

        public bool IsHidden(string value)
        {
            for (int i = 0; i < _hiddenValues.Count; i++)
            {
                if (string.Equals(_hiddenValues[i], value, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
