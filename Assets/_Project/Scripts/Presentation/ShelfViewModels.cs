using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Esnaf.Presentation
{
    /// <summary>Raftaki bir ürünün satırı (StockLine'dan): model adı ve maliyet tabanı.</summary>
    public sealed class ShelfItemRowViewModel
    {
        public string Title { get; }
        public string CostLine { get; }

        public ShelfItemRowViewModel(string title, string costLine)
        {
            Title = title;
            CostLine = costLine;
        }
    }

    /// <summary>Salt okunur raf ekranı: doluluk ve ürünler (IGameApi.GetInventory). Fiyat etiketi ve satış bu ekranda yoktur.</summary>
    public sealed class ShelfScreenViewModel
    {
        public string Title { get; }
        public string CapacityLine { get; }
        public IReadOnlyList<ShelfItemRowViewModel> Items { get; }
        public string EmptyNote { get; }

        public ShelfScreenViewModel(string title, string capacityLine, IEnumerable<ShelfItemRowViewModel> items, string emptyNote)
        {
            Title = title;
            CapacityLine = capacityLine;
            Items = new ReadOnlyCollection<ShelfItemRowViewModel>(new List<ShelfItemRowViewModel>(items));
            EmptyNote = emptyNote;
        }
    }
}
