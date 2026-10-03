using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Esnaf.Core;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Products;

namespace Esnaf.Domain.Inventory
{
    /// <summary>
    /// Envanter sistemi: raf, maliyet tabanı ve ürün konumu. Para hareketlerini <see cref="EconomyService"/> ile deftere yazar
    /// (GDD bağımlılık matrisi: Envanter → Ekonomi). Bu sürümde fiyatlar dışarıdan SABİT verilir; pazarlık Trade sisteminin işidir.
    /// Her işlem ya tamamen uygulanır ya da HİÇ uygulanmaz.
    ///
    /// Maliyet tabanı = alış fiyatı + bekleyen ekspertiz ücretleri + tamir (GDD v0.2 9.2).
    /// </summary>
    public sealed class InventoryService
    {
        private readonly InventoryState _state;
        private readonly InstanceStore _store;
        private readonly EconomyService _economy;
        private readonly IEventBus _events;

        public InventoryService(InventoryState state, InstanceStore store, EconomyService economy, IEventBus events)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (store == null)
            {
                throw new ArgumentNullException(nameof(store));
            }

            if (economy == null)
            {
                throw new ArgumentNullException(nameof(economy));
            }

            _state = state;
            _store = store;
            _economy = economy;
            _events = events;
        }

        /// <summary>Pazardaki ürünü verilen fiyata satın alıp rafa koyar.</summary>
        public Result Acquire(long instanceId, Money price, int day)
        {
            ProductInstance instance;
            if (!_store.TryGet(instanceId, out instance))
            {
                return Result.Fail("instance.unknown", "Unknown product instance " + instanceId + ".");
            }

            if (instance.Location != ProductLocation.Market)
            {
                return Result.Fail("instance.not_on_market", "Instance " + instanceId + " is not on the market.");
            }

            if (!IsPositiveRounded(price))
            {
                return Result.Fail("price.invalid", "Price must be positive and a multiple of 10 TL.");
            }

            if (_state.Count >= _state.Capacity)
            {
                return Result.Fail("inventory.full", "The shelf is full (" + _state.Capacity + ").");
            }

            Result<TransactionRecord> payment = _economy.RecordPurchase(instanceId, instance.DefinitionId, instance.SellerNpcId, price, day);
            if (payment.IsFailure)
            {
                return Result.Fail(payment.ErrorCode, payment.Message);
            }

            Money appraisalCost = _economy.CapitalizePendingAppraisals(instanceId);
            instance.PurchasePrice = price;
            instance.CostBasis = price + appraisalCost;
            instance.AcquiredDay = day;
            instance.Location = ProductLocation.Inventory;
            _state.Add(instanceId);

            if (_events != null)
            {
                _events.Publish(new ItemAddedToShelf(instanceId));
            }

            return Result.Ok();
        }

        /// <summary>Raftaki ürüne etiket fiyatı koyar/değiştirir (GDD v0.3 3.5 ItemPriced). Etiketsiz ürüne müşteri ilgilenmez.</summary>
        public Result SetPrice(long instanceId, Money price)
        {
            ProductInstance instance;
            if (!_store.TryGet(instanceId, out instance))
            {
                return Result.Fail("instance.unknown", "Unknown product instance " + instanceId + ".");
            }

            if (instance.Location != ProductLocation.Inventory || !_state.Contains(instanceId))
            {
                return Result.Fail("instance.not_in_inventory", "Instance " + instanceId + " is not on the shelf.");
            }

            if (!IsPositiveRounded(price))
            {
                return Result.Fail("price.invalid", "Price must be positive and a multiple of 10 TL.");
            }

            instance.ListPrice = price;
            if (_events != null)
            {
                _events.Publish(new ItemPriced(instanceId, price));
            }

            return Result.Ok();
        }

        /// <summary>
        /// Raftaki ürünü satıştan çıkarır (Gün 13.2): etiket fiyatı 0'a döner (mevcut "satış dışı" durumu; yeni durum yoktur). Ürün YOK EDİLMEZ, stoktan düşmez, maliyet tabanı ve
        /// mülkiyet değişmez, yalnızca satılabilir stok olmaktan çıkar (müşteri talep havuzuna girmez). Fiyat girilerek yeniden satışa alınabilir. Zaten fiyatsızsa etkisizdir (başarılı).
        /// Hatalar: instance.unknown, instance.not_in_inventory.
        /// </summary>
        public Result ClearPrice(long instanceId)
        {
            ProductInstance instance;
            if (!_store.TryGet(instanceId, out instance))
            {
                return Result.Fail("instance.unknown", "Unknown product instance " + instanceId + ".");
            }

            if (instance.Location != ProductLocation.Inventory || !_state.Contains(instanceId))
            {
                return Result.Fail("instance.not_in_inventory", "Instance " + instanceId + " is not on the shelf.");
            }

            if (!instance.ListPrice.IsPositive)
            {
                return Result.Ok();
            }

            instance.ListPrice = Money.Zero;
            if (_events != null)
            {
                _events.Publish(new ItemPriced(instanceId, Money.Zero));
            }

            return Result.Ok();
        }

        /// <summary>Raftaki ürünü verilen fiyata satar. Kâr/zarar = satış − maliyet tabanı.</summary>
        public Result<SaleReceipt> Sell(long instanceId, Money price, int day, string buyerNpcId = null)
        {
            ProductInstance instance;
            if (!_store.TryGet(instanceId, out instance))
            {
                return Result<SaleReceipt>.Fail("instance.unknown", "Unknown product instance " + instanceId + ".");
            }

            if (instance.Location != ProductLocation.Inventory || !_state.Contains(instanceId))
            {
                return Result<SaleReceipt>.Fail("instance.not_in_inventory", "Instance " + instanceId + " is not on the shelf.");
            }

            if (!IsPositiveRounded(price))
            {
                return Result<SaleReceipt>.Fail("price.invalid", "Price must be positive and a multiple of 10 TL.");
            }

            Money costBasis = instance.CostBasis;
            Result<TransactionRecord> sale = _economy.RecordSale(instanceId, instance.DefinitionId, buyerNpcId, price, costBasis, day);
            if (sale.IsFailure)
            {
                return Result<SaleReceipt>.Fail(sale.ErrorCode, sale.Message);
            }

            instance.Location = ProductLocation.Sold;
            _state.Remove(instanceId);
            return Result<SaleReceipt>.Ok(new SaleReceipt(sale.Value.Id, price, costBasis));
        }

        /// <summary>Tamir MALİYETİNİ yazar: nakit düşer, ürünün maliyet tabanı artar. (Tamirin ürün durumuna etkisi tamir sisteminin işidir.)</summary>
        public Result AddRepairCost(long instanceId, Money cost, int day)
        {
            ProductInstance instance;
            if (!_store.TryGet(instanceId, out instance))
            {
                return Result.Fail("instance.unknown", "Unknown product instance " + instanceId + ".");
            }

            if (instance.Location != ProductLocation.Inventory || !_state.Contains(instanceId))
            {
                return Result.Fail("instance.not_in_inventory", "Instance " + instanceId + " is not on the shelf.");
            }

            if (!IsPositiveRounded(cost))
            {
                return Result.Fail("price.invalid", "Cost must be positive and a multiple of 10 TL.");
            }

            Result<TransactionRecord> payment = _economy.RecordRepair(instanceId, instance.DefinitionId, cost, day);
            if (payment.IsFailure)
            {
                return Result.Fail(payment.ErrorCode, payment.Message);
            }

            instance.CostBasis += cost;
            return Result.Ok();
        }

        /// <summary>Raf kapasitesini artırır; bedel yatırımdır (gider değil, işletme varlığı).</summary>
        public Result UpgradeCapacity(int newCapacity, Money cost, int day)
        {
            if (newCapacity <= _state.Capacity)
            {
                return Result.Fail("capacity.not_larger", "New capacity " + newCapacity + " must exceed " + _state.Capacity + ".");
            }

            if (!IsPositiveRounded(cost))
            {
                return Result.Fail("price.invalid", "Cost must be positive and a multiple of 10 TL.");
            }

            Result<TransactionRecord> payment = _economy.RecordInvestment(cost, day, "ledger.memo.shelf_upgrade");
            if (payment.IsFailure)
            {
                return Result.Fail(payment.ErrorCode, payment.Message);
            }

            _state.Capacity = newCapacity;
            return Result.Ok();
        }

        /// <summary>Raftaki her ürün için maliyet satırı (gün sonu özetinin "Stok" bölümü).</summary>
        public IReadOnlyList<StockLine> GetStockLines()
        {
            var lines = new List<StockLine>(_state.Count);
            foreach (long id in _state.ItemIds)
            {
                ProductInstance instance = _store.Get(id);
                lines.Add(new StockLine(id, instance.DefinitionId, instance.CostBasis, instance.ListPrice));
            }

            return new ReadOnlyCollection<StockLine>(lines);
        }

        private static bool IsPositiveRounded(Money amount)
        {
            return amount.IsPositive && amount.IsRoundedTo10;
        }
    }
}
