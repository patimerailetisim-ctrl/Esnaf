using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Esnaf.Domain.Appraisal
{
    /// <summary>
    /// Ekspertizin kontrol ettiği bir kategorik nitelik (GDD v0.2 5.2): ekran "değişmiş" mi, kamera sorunlu mu.
    /// Yanlış alarmda "sanki kusur varmış gibi" alınan değer <see cref="FalseAlarmValue"/>'dur.
    /// </summary>
    public sealed class AppraisalCheck
    {
        private readonly ReadOnlyCollection<string> _defectValues;

        public string Attribute { get; }
        public string FalseAlarmValue { get; }
        public string CleanValue { get; }
        public string WordingKey { get; }

        public IReadOnlyList<string> DefectValues
        {
            get { return _defectValues; }
        }

        public AppraisalCheck(string attribute, IEnumerable<string> defectValues, string falseAlarmValue, string cleanValue, string wordingKey)
        {
            if (defectValues == null)
            {
                throw new ArgumentNullException(nameof(defectValues));
            }

            Attribute = attribute;
            FalseAlarmValue = falseAlarmValue;
            CleanValue = cleanValue;
            WordingKey = wordingKey;
            _defectValues = new ReadOnlyCollection<string>(new List<string>(defectValues));
        }

        public bool IsDefect(string value)
        {
            for (int i = 0; i < _defectValues.Count; i++)
            {
                if (string.Equals(_defectValues[i], value, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
