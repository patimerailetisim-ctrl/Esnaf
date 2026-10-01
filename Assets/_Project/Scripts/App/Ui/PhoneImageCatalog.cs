using System;
using System.Collections.Generic;
using System.Text;
using Esnaf.Presentation;
using UnityEngine;

namespace Esnaf.App.Ui
{
    /// <summary>
    /// Telefon modeli + açı → gerçek ürün görseli (Sprite) eşlemesi. Yalnızca Unity varlık bağlama kataloğudur (oyun verisi değildir);
    /// Sprite'lar Inspector'dan (veya Esnaf > Setup Day 10) atanır. <see cref="GameBootstrap"/> bunu <see cref="PhoneImages.Provider"/>'a bağlar.
    /// Eksik model/açı/Sprite için null döner; ekran mock çizime düşer, hata vermez.
    /// </summary>
    [CreateAssetMenu(fileName = "PhoneImageCatalog", menuName = "Esnaf/Phone Image Catalog")]
    public sealed class PhoneImageCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string _definitionId;
            [SerializeField] private Sprite _front;
            [SerializeField] private Sprite _back;
            [SerializeField] private Sprite _side;
            [SerializeField] private Sprite _camera;
            [SerializeField] private Sprite _allViews;

            public string DefinitionId
            {
                get { return _definitionId; }
            }

            /// <summary>Tüm görünümlerin tek görseli (şimdilik ekranda kullanılmaz; ileride kapak/ön izleme için).</summary>
            public Sprite AllViews
            {
                get { return _allViews; }
            }

            public Sprite For(PhoneAngle angle)
            {
                switch (angle)
                {
                    case PhoneAngle.Front:
                        return _front;
                    case PhoneAngle.Back:
                        return _back;
                    case PhoneAngle.Side:
                        return _side;
                    case PhoneAngle.CameraClose:
                        return _camera;
                    default:
                        return null; // Alt/Üst için gerçek görsel yoktur
                }
            }
        }

        private static readonly HashSet<string> Reported = new HashSet<string>();

        [SerializeField]
        private Entry[] _entries = new Entry[0];

        public Sprite GetSprite(string definitionId, PhoneAngle angle)
        {
            if (definitionId == null || _entries == null)
            {
                return null;
            }

            foreach (Entry entry in _entries)
            {
                if (entry != null && entry.DefinitionId == definitionId)
                {
                    Sprite sprite = entry.For(angle);
                    if (sprite == null && angle != PhoneAngle.TopBottom)
                    {
                        ReportOnce(definitionId, angle, "katalog girdisi var ama bu a\u00E7\u0131n\u0131n Sprite alan\u0131 bo\u015F (Sprite atanmam\u0131\u015F / PNG Sprite olarak i\u00E7e aktar\u0131lmam\u0131\u015F).");
                    }

                    return sprite;
                }
            }

            ReportOnce(definitionId, angle, "katalogda bu model kimli\u011Fi yok. Katalogdakiler: " + Describe());
            return null;
        }

        /// <summary>Tan\u0131lama: girdiler ve hangi a\u00E7\u0131lar\u0131n Sprite'\u0131 dolu. \u00D6rn. "phone.elma_e13_pro[Front,Back,Side,CameraClose]".</summary>
        public string Describe()
        {
            if (_entries == null || _entries.Length == 0)
            {
                return "(girdi yok)";
            }

            var sb = new StringBuilder();
            foreach (Entry entry in _entries)
            {
                if (entry == null)
                {
                    continue;
                }

                sb.Append('\'').Append(entry.DefinitionId).Append("'[");
                bool first = true;
                foreach (PhoneAngle angle in new[] { PhoneAngle.Front, PhoneAngle.Back, PhoneAngle.Side, PhoneAngle.CameraClose })
                {
                    if (entry.For(angle) != null)
                    {
                        sb.Append(first ? string.Empty : ",").Append(angle);
                        first = false;
                    }
                }

                sb.Append("] ");
            }

            return sb.ToString();
        }

        private static void ReportOnce(string definitionId, PhoneAngle angle, string reason)
        {
            if (Reported.Add(definitionId + "|" + angle))
            {
                Debug.LogWarning("[PhoneImages] '" + definitionId + "' + " + angle + " i\u00E7in Sprite yok -> mock: " + reason);
            }
        }
    }
}
