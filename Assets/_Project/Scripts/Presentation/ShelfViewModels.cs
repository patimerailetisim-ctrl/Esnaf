using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Esnaf.Presentation
{
    /// <summary>Raftaki bir ürünün satırı (StockLine'dan): model adı ve maliyet tabanı.</summary>
    public sealed class ShelfItemRowViewModel
    {
        public string Title { get; }
        public string CostLine { get; }

        /// <summary>Ürünün oyundaki kimliği (Gün 12.7: fiyat belirlemek için seçilir); eski çağrılarda 0.</summary>
        public long InstanceId { get; }

        /// <summary>"Satış fiyatı: 5.900 ₺" ya da fiyat yoksa uyarı satırı.</summary>
        public string PriceLine { get; }

        /// <summary>Fiyatı girilmiş (satılabilir) mi? Fiyatsız ürün müşteri talep havuzuna girmez.</summary>
        public bool IsSellable { get; }

        public bool IsSelected { get; }

        public ShelfItemRowViewModel(string title, string costLine, long instanceId = 0, string priceLine = null, bool isSellable = false, bool isSelected = false)
        {
            Title = title;
            CostLine = costLine;
            InstanceId = instanceId;
            PriceLine = priceLine;
            IsSellable = isSellable;
            IsSelected = isSelected;
        }
    }

    /// <summary>Raf ekranında seçili ürünün fiyat belirleme paneli (Gün 12.7): maliyet, seçili satış fiyatı, tahmini kâr ve marj. Kaydetme IGameApi.SetPrice ile yapılır.</summary>
    public sealed class ShelfPriceEditorViewModel
    {
        public long InstanceId { get; }
        public string Title { get; }
        public string CostLine { get; }

        /// <summary>Şu an kayıtlı etiket fiyatı satırı ("Kayıtlı fiyat: …" ya da "Kayıtlı fiyat yok").</summary>
        public string SavedLine { get; }

        /// <summary>Seçicideki (henüz kaydedilmemiş) fiyat.</summary>
        public Esnaf.Core.Money Price { get; }

        public string PriceLine { get; }
        public string ProfitLine { get; }
        public string MarginLine { get; }

        /// <summary>Fiyat geçerliyse (sıfırdan büyük, 10 ₺'nin katı) kaydedilebilir.</summary>
        public bool CanSave { get; }

        public string SaveButtonText { get; }

        /// <summary>"Müşteri tavanı: 34.300 ₺" (mevcut müşteri hesabından türeyen bilgi); tavan bilinmiyorsa null.</summary>
        public string CeilingLine { get; }

        /// <summary>Seçili fiyat tavanın üstündeyse "Bu fiyatın üzerinde müşteriler bu telefonu pahalı bulabilir."; aksi halde null.</summary>
        public string Warning { get; }

        /// <summary>Modelin tanımı (telefon görseli için).</summary>
        public string DefinitionId { get; }

        /// <summary>"Stok: 2 adet": rafta bu modelden kaç telefon var (Gün 13.2).</summary>
        public string StockLine { get; }

        /// <summary>"Satıştan Çıkar" yalnızca şu an satışta (fiyatı girilmiş) ürün için vardır.</summary>
        public bool CanRemoveFromSale { get; }

        public string RemoveButtonText { get; }

        public ShelfPriceEditorViewModel(
            long instanceId, string title, string costLine, string savedLine, Esnaf.Core.Money price, string priceLine, string profitLine, string marginLine, bool canSave, string saveButtonText,
            string ceilingLine = null, string warning = null, string definitionId = null, string stockLine = null, bool canRemoveFromSale = false, string removeButtonText = null)
        {
            CeilingLine = ceilingLine;
            Warning = warning;
            DefinitionId = definitionId;
            StockLine = stockLine;
            CanRemoveFromSale = canRemoveFromSale;
            RemoveButtonText = removeButtonText;
            InstanceId = instanceId;
            Title = title;
            CostLine = costLine;
            SavedLine = savedLine;
            Price = price;
            PriceLine = priceLine;
            ProfitLine = profitLine;
            MarginLine = marginLine;
            CanSave = canSave;
            SaveButtonText = saveButtonText;
        }
    }

    /// <summary>Raf ekranı: doluluk, ürünler (IGameApi.GetInventory) ve seçili ürünün fiyat paneli.</summary>
    public sealed class ShelfScreenViewModel
    {
        public string Title { get; }
        public string CapacityLine { get; }
        public IReadOnlyList<ShelfItemRowViewModel> Items { get; }
        public string EmptyNote { get; }

        /// <summary>Seçili ürün varsa fiyat belirleme paneli; yoksa null.</summary>
        public ShelfPriceEditorViewModel Editor { get; }

        public ShelfScreenViewModel(string title, string capacityLine, IEnumerable<ShelfItemRowViewModel> items, string emptyNote, ShelfPriceEditorViewModel editor = null)
        {
            Title = title;
            CapacityLine = capacityLine;
            Items = new ReadOnlyCollection<ShelfItemRowViewModel>(new List<ShelfItemRowViewModel>(items));
            EmptyNote = emptyNote;
            Editor = editor;
        }
    }
}
