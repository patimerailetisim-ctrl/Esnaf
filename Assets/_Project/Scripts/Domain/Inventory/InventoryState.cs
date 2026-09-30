using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Esnaf.Domain.Inventory
{
    /// <summary>
    /// Raf durumu (kayda girecek): kapasite ve rafta duran ürün KİMLİKLERİ (edinme sırasıyla). Ürünün kendisi <c>InstanceStore</c>'dadır.
    /// Etiket fiyatları Gün 8'de eklenecek. Değişiklikler yalnızca <see cref="InventoryService"/> ile yapılır (I3: raf ≤ kapasite).
    /// </summary>
    public sealed class InventoryState
    {
        private readonly List<long> _ids = new List<long>();
        private readonly ReadOnlyCollection<long> _view;

        public int Capacity { get; internal set; }

        public InventoryState(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Shelf capacity must be positive.");
            }

            Capacity = capacity;
            _view = new ReadOnlyCollection<long>(_ids);
        }

        public int Count
        {
            get { return _ids.Count; }
        }

        public IReadOnlyList<long> ItemIds
        {
            get { return _view; }
        }

        public bool Contains(long instanceId)
        {
            return _ids.Contains(instanceId);
        }

        internal void Add(long instanceId)
        {
            _ids.Add(instanceId);
        }

        internal bool Remove(long instanceId)
        {
            return _ids.Remove(instanceId);
        }
    }
}
