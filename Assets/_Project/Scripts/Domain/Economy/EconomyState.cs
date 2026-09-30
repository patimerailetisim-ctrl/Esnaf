using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Esnaf.Core;

namespace Esnaf.Domain.Economy
{
    /// <summary>
    /// Ekonomi durumu (kayda girecek): defter, işletme varlıkları ve bekleyen ekspertiz ücretleri.
    /// Nakit ayrıca saklanmaz; defter bakiyesidir (I1 yapı gereği tutar). Değişiklikler yalnızca <see cref="EconomyService"/> ile yapılır.
    /// Talep endeksleri gibi alanlar sonraki günlerde eklenecek.
    /// </summary>
    public sealed class EconomyState
    {
        private readonly Dictionary<long, List<long>> _pendingAppraisals = new Dictionary<long, List<long>>();

        public Ledger Ledger { get; }

        public Money Cash
        {
            get { return Ledger.Balance; }
        }

        /// <summary>Yatırımların toplamı (raf yükseltme, ekipman). Servete varlık olarak girer, gider değildir.</summary>
        public Money BusinessAssets { get; internal set; }

        public EconomyState(TransactionTypes types)
        {
            if (types == null)
            {
                throw new ArgumentNullException(nameof(types));
            }

            Ledger = new Ledger(types);
        }

        /// <summary>Bu ürün için ödenmiş ama henüz ürüne eklenmemiş/yazılmamış ekspertiz satırlarının kimlikleri.</summary>
        public IReadOnlyList<long> GetPendingAppraisalRecordIds(long instanceId)
        {
            List<long> ids;
            if (_pendingAppraisals.TryGetValue(instanceId, out ids))
            {
                return new ReadOnlyCollection<long>(new List<long>(ids));
            }

            return new ReadOnlyCollection<long>(new List<long>());
        }

        /// <summary>Tüm bekleyen ekspertiz ücretlerinin toplamı.</summary>
        public Money PendingAppraisalTotal()
        {
            Money total = Money.Zero;
            foreach (KeyValuePair<long, List<long>> pair in _pendingAppraisals)
            {
                foreach (long recordId in pair.Value)
                {
                    TransactionRecord record;
                    if (Ledger.TryGetById(recordId, out record))
                    {
                        total += record.Amount.Abs();
                    }
                }
            }

            return total;
        }

        internal void AddPendingAppraisal(long instanceId, long recordId)
        {
            List<long> ids;
            if (!_pendingAppraisals.TryGetValue(instanceId, out ids))
            {
                ids = new List<long>();
                _pendingAppraisals.Add(instanceId, ids);
            }

            ids.Add(recordId);
        }

        /// <summary>Bekleyenleri döndürür ve listeden çıkarır.</summary>
        internal IReadOnlyList<long> TakePendingAppraisals(long instanceId)
        {
            List<long> ids;
            if (!_pendingAppraisals.TryGetValue(instanceId, out ids))
            {
                return new ReadOnlyCollection<long>(new List<long>());
            }

            _pendingAppraisals.Remove(instanceId);
            return new ReadOnlyCollection<long>(ids);
        }
    }
}
