using System;
using Esnaf.Core;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Products;

namespace Esnaf.Domain.Inventory
{
    /// <summary>Stok: rafta duran ürünlerin MALİYET tabanı toplamı (MVP'de tahmini piyasa değeri değil, GDD v0.2 9.2).</summary>
    public sealed class StockWealthContributor : IWealthContributor
    {
        private readonly InventoryState _inventory;
        private readonly InstanceStore _store;

        public StockWealthContributor(InventoryState inventory, InstanceStore store)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            if (store == null)
            {
                throw new ArgumentNullException(nameof(store));
            }

            _inventory = inventory;
            _store = store;
        }

        public string Key
        {
            get { return WealthKeys.Stock; }
        }

        public Money GetValue()
        {
            Money total = Money.Zero;
            foreach (long id in _inventory.ItemIds)
            {
                total += _store.Get(id).CostBasis;
            }

            return total;
        }
    }
}
