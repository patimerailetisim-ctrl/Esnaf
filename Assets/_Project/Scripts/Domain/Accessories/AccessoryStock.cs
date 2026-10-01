using System;
using System.Collections.Generic;
using Esnaf.Core;

namespace Esnaf.Domain.Accessories
{
    /// <summary>
    /// Aksesuar stoğu (durum): aksesuar başına ADET ve toplam maliyet. Telefon rafından (<c>InventoryState</c>) tamamen AYRIDIR ve onun yuvalarını
    /// tüketmez; kapasite BİRİM cinsindendir (başlangıç 60, accessories.json). Her işlem ya tamamen uygulanır ya da HİÇ uygulanmaz.
    /// Bu sınıf yalnızca stok defteridir: para hareketi, toptancı satın alma ve satış bu adımda yoktur (sonraki adımlar).
    /// Maliyet kalem başına TOPLAM olarak tutulur; birim çıkarken orantılı pay (aşağı yuvarlı, son birimde kalan tamamı) düşülür.
    /// </summary>
    public sealed class AccessoryStock
    {
        private sealed class Line
        {
            public int Quantity;
            public long TotalCost;
        }

        private readonly Dictionary<string, Line> _lines = new Dictionary<string, Line>(StringComparer.Ordinal);
        private int _units;

        public int Capacity { get; }

        public AccessoryStock(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Accessory shelf capacity must be positive.");
            }

            Capacity = capacity;
        }

        /// <summary>Raftaki toplam birim.</summary>
        public int TotalUnits
        {
            get { return _units; }
        }

        public int FreeUnits
        {
            get { return Capacity - _units; }
        }

        public int Quantity(string accessoryId)
        {
            Line line;
            return accessoryId != null && _lines.TryGetValue(accessoryId, out line) ? line.Quantity : 0;
        }

        /// <summary>Bir kalemin stoktaki toplam maliyet tabanı (TL).</summary>
        public Money TotalCost(string accessoryId)
        {
            Line line;
            return accessoryId != null && _lines.TryGetValue(accessoryId, out line) ? Money.FromTl(line.TotalCost) : Money.Zero;
        }

        /// <summary>Stokta adedi 0'dan büyük kalemlerin kimlikleri (kimliğe göre sıralı: belirlenimci).</summary>
        public IReadOnlyList<string> AccessoryIds
        {
            get
            {
                var ids = new List<string>(_lines.Keys);
                ids.Sort(StringComparer.Ordinal);
                return ids;
            }
        }

        /// <summary>Stoğa <paramref name="quantity"/> adet ekler; <paramref name="totalCost"/> bu partinin toplam maliyetidir. Hatalar: stock.quantity_invalid, stock.cost_invalid, stock.full.</summary>
        public Result Add(string accessoryId, int quantity, Money totalCost)
        {
            if (string.IsNullOrEmpty(accessoryId))
            {
                throw new ArgumentException("An accessory id is required.", nameof(accessoryId));
            }

            if (quantity <= 0)
            {
                return Result.Fail("stock.quantity_invalid", "The quantity must be positive.");
            }

            if (totalCost.Tl < 0)
            {
                return Result.Fail("stock.cost_invalid", "The cost must not be negative.");
            }

            if (quantity > FreeUnits)
            {
                return Result.Fail("stock.full", "The accessory shelf holds " + Capacity + " units; " + FreeUnits + " are free.");
            }

            Line line;
            if (!_lines.TryGetValue(accessoryId, out line))
            {
                line = new Line();
                _lines.Add(accessoryId, line);
            }

            line.Quantity += quantity;
            line.TotalCost += totalCost.Tl;
            _units += quantity;
            return Result.Ok();
        }

        /// <summary>Stoktan <paramref name="quantity"/> adet çıkarır ve çıkan birimlerin maliyet tabanını döndürür. Hatalar: stock.quantity_invalid, stock.insufficient.</summary>
        public Result<Money> Remove(string accessoryId, int quantity)
        {
            if (quantity <= 0)
            {
                return Result<Money>.Fail("stock.quantity_invalid", "The quantity must be positive.");
            }

            Line line;
            if (accessoryId == null || !_lines.TryGetValue(accessoryId, out line) || line.Quantity < quantity)
            {
                return Result<Money>.Fail("stock.insufficient", "Not enough '" + accessoryId + "' in stock.");
            }

            long cost = quantity == line.Quantity ? line.TotalCost : line.TotalCost * quantity / line.Quantity;
            line.Quantity -= quantity;
            line.TotalCost -= cost;
            _units -= quantity;
            if (line.Quantity == 0)
            {
                _lines.Remove(accessoryId);
            }

            return Result<Money>.Ok(Money.FromTl(cost));
        }
    }
}
