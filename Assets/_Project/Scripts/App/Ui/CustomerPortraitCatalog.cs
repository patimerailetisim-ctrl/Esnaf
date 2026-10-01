using System;
using System.Collections.Generic;
using System.Text;
using Esnaf.Presentation;
using UnityEngine;

namespace Esnaf.App.Ui
{
    /// <summary>
    /// Müşteri adı → gerçek portre (Sprite). Yalnızca Unity varlık bağlama kataloğudur (oyun verisi değildir); girdiler
    /// Esnaf > Setup Customer Portraits ile Art/Customers klasörü TARANARAK kurulur (GUID elle yazılmaz). Eşleşme adın anahtarıyladır
    /// (<see cref="CustomerPortraitNaming.Key"/>): "Dr. Murat" ve "Murat" aynı portreyi bulur. Bulunamazsa null döner; ekran silüete düşer.
    /// </summary>
    [CreateAssetMenu(fileName = "CustomerPortraitCatalog", menuName = "Esnaf/Customer Portrait Catalog")]
    public sealed class CustomerPortraitCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string _name;
            [SerializeField] private Sprite _sprite;

            /// <summary>Dosya adı (uzantısız), ör. "Oğuz".</summary>
            public string Name
            {
                get { return _name; }
            }

            public Sprite Sprite
            {
                get { return _sprite; }
            }
        }

        [SerializeField]
        private Entry[] _entries = new Entry[0];

        private Dictionary<string, Sprite> _byKey;

        public int Count
        {
            get { return _entries == null ? 0 : _entries.Length; }
        }

        public Sprite GetSprite(string customerName)
        {
            string key = CustomerPortraitNaming.Key(customerName);
            if (key.Length == 0)
            {
                return null;
            }

            if (_byKey == null)
            {
                _byKey = BuildIndex();
            }

            Sprite sprite;
            return _byKey.TryGetValue(key, out sprite) ? sprite : null;
        }

        /// <summary>Tanılama: katalogdaki portre adları ve Sprite'ı boş olanlar.</summary>
        public string Describe()
        {
            var sb = new StringBuilder();
            int empty = 0;
            if (_entries != null)
            {
                foreach (Entry entry in _entries)
                {
                    if (entry == null || entry.Sprite == null)
                    {
                        empty++;
                    }
                    else
                    {
                        sb.Append(entry.Name).Append(' ');
                    }
                }
            }

            return Count + " girdi (" + empty + " boş): " + sb;
        }

        private void OnValidate()
        {
            _byKey = null;
        }

        private Dictionary<string, Sprite> BuildIndex()
        {
            var index = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            if (_entries != null)
            {
                foreach (Entry entry in _entries)
                {
                    if (entry == null || entry.Sprite == null)
                    {
                        continue;
                    }

                    string key = CustomerPortraitNaming.Key(entry.Name);
                    if (key.Length > 0 && !index.ContainsKey(key))
                    {
                        index.Add(key, entry.Sprite);
                    }
                }
            }

            return index;
        }
    }
}
