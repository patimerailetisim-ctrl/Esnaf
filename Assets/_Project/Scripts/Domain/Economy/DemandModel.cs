using System;
using System.Collections.Generic;
using Esnaf.Core;

namespace Esnaf.Domain.Economy
{
    /// <summary>
    /// Talep modeli (GDD v0.2 10.2). Gün 1–4 tüm modeller 1,00; canlı günden itibaren her gün
    /// <c>talep = talep + rastgele(±gürültü) + ortalamaya çekim × (1 − talep)</c>, sınır [min, max].
    /// Oyuncu etkisi: penceredeki her satış talebi <c>×0,985</c> çarpar (en fazla −%10). Etkin çarpan = endeks × baskı.
    /// Sayıların hepsi <see cref="DemandConstants"/>'tandır; rastgelelik dışarıdan verilir (her model için tek çekim, verilen sırayla).
    /// </summary>
    public sealed class DemandModel
    {
        private readonly DemandConstants _constants;
        private readonly DemandState _state;

        public DemandModel(DemandConstants constants, DemandState state)
        {
            if (constants == null)
            {
                throw new ArgumentNullException(nameof(constants));
            }

            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            _constants = constants;
            _state = state;
        }

        public double Index(string modelId)
        {
            return _state.GetIndex(modelId);
        }

        /// <summary>Son penceredeki satışlardan gelen baskı çarpanı (en düşük: taban).</summary>
        public double Pressure(string modelId, int day)
        {
            int count = 0;
            for (int i = 0; i < _state.Sales.Count; i++)
            {
                DemandSale sale = _state.Sales[i];
                if (string.Equals(sale.ModelId, modelId, StringComparison.Ordinal)
                    && sale.Day > day - _constants.PressureWindowDays
                    && sale.Day <= day)
                {
                    count++;
                }
            }

            return Math.Max(_constants.PressureFloor, Math.Pow(_constants.PressurePerSale, count));
        }

        /// <summary>Değer formülüne giren etkin talep çarpanı.</summary>
        public double Multiplier(string modelId, int day)
        {
            return Index(modelId) * Pressure(modelId, day);
        }

        public void RecordSale(string modelId, int day)
        {
            _state.AddSale(modelId, day);
        }

        /// <summary>Yeni güne girerken endeksleri günceller (gün sonu adım 5). Canlı günden önce hiçbir şey yapmaz, rastgelelik tüketmez.</summary>
        public void UpdateForNewDay(int newDay, IRandom rng, IEnumerable<string> modelIds)
        {
            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            if (modelIds == null)
            {
                throw new ArgumentNullException(nameof(modelIds));
            }

            if (newDay < _constants.LiveFromDay)
            {
                return;
            }

            foreach (string id in modelIds)
            {
                double current = _state.GetIndex(id);
                double noise = (2.0 * rng.NextDouble() - 1.0) * _constants.DailyNoise;
                double next = current + noise + _constants.MeanReversion * (1.0 - current);
                _state.SetIndex(id, Math.Max(_constants.Min, Math.Min(_constants.Max, next)));
            }

            // Bu güne ve sonrasına hiçbir zaman sayılmayacak satışlar atılır.
            _state.RemoveSalesOlderThanOrEqual(newDay - _constants.PressureWindowDays);
        }
    }
}
