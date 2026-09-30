using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Esnaf.Domain.Economy
{
    /// <summary>Bir satışın talep kaydı: hangi modelden, hangi gün (satış baskısı için).</summary>
    public sealed class DemandSale
    {
        public string ModelId { get; }
        public int Day { get; }

        public DemandSale(string modelId, int day)
        {
            ModelId = modelId;
            Day = day;
        }
    }

    /// <summary>
    /// Talep durumu (kayda girer; GDD v0.3 4.3 EconomyState: "model başına talep endeksi, model başına son 5 gün satış sayacı").
    /// Kayıtlı olmayan modelin endeksi 1,00'dır.
    /// </summary>
    public sealed class DemandState
    {
        private readonly Dictionary<string, double> _index = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly List<DemandSale> _sales = new List<DemandSale>();
        private readonly ReadOnlyCollection<DemandSale> _salesView;

        public DemandState()
        {
            _salesView = new ReadOnlyCollection<DemandSale>(_sales);
        }

        /// <summary>Endeksi kaydedilmiş modeller, kimliğe göre sıralı.</summary>
        public IReadOnlyList<KeyValuePair<string, double>> Indexes
        {
            get
            {
                var list = new List<KeyValuePair<string, double>>(_index);
                list.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
                return new ReadOnlyCollection<KeyValuePair<string, double>>(list);
            }
        }

        /// <summary>Kayıtlı satışlar, kayıt sırasıyla.</summary>
        public IReadOnlyList<DemandSale> Sales
        {
            get { return _salesView; }
        }

        public double GetIndex(string modelId)
        {
            double value;
            return _index.TryGetValue(modelId, out value) ? value : 1.0;
        }

        /// <summary>Bir modelin talep endeksini yazar (talep güncellemesi ve kayıt yükleme için).</summary>
        public void SetIndex(string modelId, double value)
        {
            _index[modelId] = value;
        }

        internal void AddSale(string modelId, int day)
        {
            _sales.Add(new DemandSale(modelId, day));
        }

        internal void RemoveSalesOlderThanOrEqual(int day)
        {
            _sales.RemoveAll(s => s.Day <= day);
        }
    }
}
