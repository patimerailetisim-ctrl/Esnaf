using System;
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
                    return entry.For(angle);
                }
            }

            return null;
        }
    }
}
