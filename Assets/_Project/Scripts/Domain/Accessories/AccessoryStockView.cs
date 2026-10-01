using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Esnaf.Core;

namespace Esnaf.Domain.Accessories
{
    /// <summary>Aksesuar stoğundaki bir kalem (IGameApi.GetAccessoryStock): adet ve toplam maliyet tabanı. Birim/ortalama maliyeti gösterim katmanı hesaplar.</summary>
    public sealed class AccessoryStockLineView
    {
        public string AccessoryId { get; }
        public string AccessoryName { get; }
        public int Quantity { get; }
        public Money TotalCost { get; }

        public AccessoryStockLineView(string accessoryId, string accessoryName, int quantity, Money totalCost)
        {
            AccessoryId = accessoryId;
            AccessoryName = accessoryName;
            Quantity = quantity;
            TotalCost = totalCost;
        }
    }

    /// <summary>
    /// Aksesuar stoğunun DEĞİŞMEZ görünümü: kapasite ve doluluk BİRİM cinsindendir ve telefon rafından (GetInventory) tamamen ayrıdır.
    /// Kalemler kimliğe göre sıralıdır.
    /// </summary>
    public sealed class AccessoryStockView
    {
        public int Capacity { get; }
        public int TotalUnits { get; }

        /// <summary>Stoktaki tüm kalemlerin toplam maliyet tabanı.</summary>
        public Money TotalCost { get; }

        public IReadOnlyList<AccessoryStockLineView> Lines { get; }

        public AccessoryStockView(int capacity, int totalUnits, Money totalCost, IEnumerable<AccessoryStockLineView> lines)
        {
            if (lines == null)
            {
                throw new ArgumentNullException(nameof(lines));
            }

            Capacity = capacity;
            TotalUnits = totalUnits;
            TotalCost = totalCost;
            Lines = new ReadOnlyCollection<AccessoryStockLineView>(new List<AccessoryStockLineView>(lines));
        }
    }
}
