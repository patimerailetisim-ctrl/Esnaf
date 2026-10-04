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

    /// <summary>
    /// Toptancı ekranındaki bir TELEFON teklifi satırı (Gün 14): hazır Türkçe satırlar. Tüm telefon teklifleri aynı yapıdan gelir; modele özel alan yoktur.
    /// <see cref="ProductId"/> aynı zamanda telefon görselinin (PhoneImageCatalog) anahtarıdır.
    /// </summary>
    public sealed class PhoneOfferRowViewModel
    {
        public string SupplierId { get; }
        public string ProductId { get; }
        public string Name { get; }

        /// <summary>"Sıfır" (toptancıdan gelen telefonlar yenidir).</summary>
        public string ConditionLine { get; }

        /// <summary>"14.000 ₺ / adet".</summary>
        public string UnitCostLine { get; }

        /// <summary>"5 adet".</summary>
        public string QuantityLine { get; }

        /// <summary>"Paket: 70.000 ₺".</summary>
        public string PackPriceLine { get; }

        /// <summary>"Önerilen satış: 15.000 ₺" (içerikte tanımlıysa; toptan fiyattan bağımsız); yoksa null.</summary>
        public string RetailLine { get; }

        public bool IsLocked { get; }

        /// <summary>"5 ADET AL" ya da kilitliyse "Gün 3'te açılır".</summary>
        public string ButtonText { get; }

        public bool IsButtonEnabled
        {
            get { return !IsLocked; }
        }

        public string LockNote { get; }

        public PhoneOfferRowViewModel(
            string supplierId, string productId, string name, string conditionLine, string unitCostLine, string quantityLine, string packPriceLine, string retailLine,
            bool isLocked, string buttonText, string lockNote)
        {
            SupplierId = supplierId;
            ProductId = productId;
            Name = name;
            ConditionLine = conditionLine;
            UnitCostLine = unitCostLine;
            QuantityLine = quantityLine;
            PackPriceLine = packPriceLine;
            RetailLine = retailLine;
            IsLocked = isLocked;
            ButtonText = buttonText;
            LockNote = lockNote;
        }
    }

    /// <summary>Toptancı ekranı: başlık (toptancı adı), nakit, TELEFON teklifleri (Gün 14), aksesuar teklifleri ve aksesuar stok özeti.</summary>
    public sealed class WholesaleScreenViewModel
    {
        public string Title { get; }
        public string SupplierName { get; }
        public string CashLine { get; }

        /// <summary>"Stok: 20 / 60 adet" (aksesuar kapasitesi).</summary>
        public string StockLine { get; }

        public IReadOnlyList<WholesaleOfferRowViewModel> Offers { get; }
        public string EmptyNote { get; }

        /// <summary>"Telefonlar" başlığı (telefon teklifi varsa); yoksa null.</summary>
        public string PhonesHeader { get; }

        /// <summary>İçerikteki tüm telefon teklifleri (otomatik keşfedilir).</summary>
        public IReadOnlyList<PhoneOfferRowViewModel> PhoneOffers { get; }

        public WholesaleScreenViewModel(
            string title, string supplierName, string cashLine, string stockLine, IEnumerable<WholesaleOfferRowViewModel> offers, string emptyNote,
            string phonesHeader = null, IEnumerable<PhoneOfferRowViewModel> phoneOffers = null)
        {
            PhonesHeader = phonesHeader;
            PhoneOffers = new ReadOnlyCollection<PhoneOfferRowViewModel>(new List<PhoneOfferRowViewModel>(phoneOffers ?? new PhoneOfferRowViewModel[0]));
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
