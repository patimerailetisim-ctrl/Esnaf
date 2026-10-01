using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Esnaf.Presentation
{
    /// <summary>Toptancı ekranındaki bir teklif satırı: hazır Türkçe satırlar ve düğmenin durumu. Düğme yalnızca GÜN KİLİDİNDE devre dışıdır (API'nin IsAvailableToday bilgisi); para/kapasite hatasını oyun söyler.</summary>
    public sealed class WholesaleOfferRowViewModel
    {
        public string SupplierId { get; }
        public string AccessoryId { get; }
        public string Name { get; }
        public string PackLine { get; }
        public string UnitCostLine { get; }
        public string PackPriceLine { get; }
        public bool IsLocked { get; }

        /// <summary>"Satın Al" ya da kilitliyse "Gün 3'te açılır".</summary>
        public string ButtonText { get; }

        public bool IsButtonEnabled
        {
            get { return !IsLocked; }
        }

        /// <summary>Kilitliyse "Bu ürün Gün 3'te açılacak."; değilse null.</summary>
        public string LockNote { get; }

        public WholesaleOfferRowViewModel(
            string supplierId, string accessoryId, string name, string packLine, string unitCostLine, string packPriceLine,
            bool isLocked, string buttonText, string lockNote)
        {
            SupplierId = supplierId;
            AccessoryId = accessoryId;
            Name = name;
            PackLine = packLine;
            UnitCostLine = unitCostLine;
            PackPriceLine = packPriceLine;
            IsLocked = isLocked;
            ButtonText = buttonText;
            LockNote = lockNote;
        }
    }

    /// <summary>Toptancı ekranı: başlık (toptancı adı), nakit, teklifler ve aksesuar stok özeti. Telefon rafıyla ilgisi yoktur.</summary>
    public sealed class WholesaleScreenViewModel
    {
        public string Title { get; }
        public string SupplierName { get; }
        public string CashLine { get; }

        /// <summary>"Stok: 20 / 60 adet" (aksesuar kapasitesi).</summary>
        public string StockLine { get; }

        public IReadOnlyList<WholesaleOfferRowViewModel> Offers { get; }
        public string EmptyNote { get; }

        public WholesaleScreenViewModel(
            string title, string supplierName, string cashLine, string stockLine, IEnumerable<WholesaleOfferRowViewModel> offers, string emptyNote)
        {
            Title = title;
            SupplierName = supplierName;
            CashLine = cashLine;
            StockLine = stockLine;
            Offers = new ReadOnlyCollection<WholesaleOfferRowViewModel>(new List<WholesaleOfferRowViewModel>(offers));
            EmptyNote = emptyNote;
        }
    }

    /// <summary>Aksesuar stoğundaki bir ürün: ad, adet, ortalama birim maliyet, toplam maliyet ve kapasite payı.</summary>
    public sealed class AccessoryStockRowViewModel
    {
        public string AccessoryId { get; }
        public string Name { get; }
        public string QuantityLine { get; }
        public string AverageCostLine { get; }
        public string TotalCostLine { get; }
        public string ShareLine { get; }

        public AccessoryStockRowViewModel(string accessoryId, string name, string quantityLine, string averageCostLine, string totalCostLine, string shareLine)
        {
            AccessoryId = accessoryId;
            Name = name;
            QuantityLine = quantityLine;
            AverageCostLine = averageCostLine;
            TotalCostLine = totalCostLine;
            ShareLine = shareLine;
        }
    }

    /// <summary>Aksesuar Stoğu ekranı: "Stok: X / 60 adet", stok maliyeti ve ürünler. Telefon rafı (Raf ekranı) ayrıdır.</summary>
    public sealed class AccessoryStockScreenViewModel
    {
        public string Title { get; }
        public string CapacityLine { get; }
        public string TotalCostLine { get; }
        public IReadOnlyList<AccessoryStockRowViewModel> Items { get; }
        public string EmptyNote { get; }

        public AccessoryStockScreenViewModel(string title, string capacityLine, string totalCostLine, IEnumerable<AccessoryStockRowViewModel> items, string emptyNote)
        {
            Title = title;
            CapacityLine = capacityLine;
            TotalCostLine = totalCostLine;
            Items = new ReadOnlyCollection<AccessoryStockRowViewModel>(new List<AccessoryStockRowViewModel>(items));
            EmptyNote = emptyNote;
        }
    }
}
