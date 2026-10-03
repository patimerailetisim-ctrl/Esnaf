using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace Esnaf.Presentation
{
    /// <summary>Dükkan rafındaki bir telefonun karosu (Gün 13.4): görsel için model kimliği, model adı, satış fiyatı ya da "satışta değil".</summary>
    public sealed class ShopPhoneViewModel
    {
        public long InstanceId { get; }
        public string DefinitionId { get; }
        public string Model { get; }
        public string PriceText { get; }
        public bool IsSellable { get; }

        public ShopPhoneViewModel(long instanceId, string definitionId, string model, string priceText, bool isSellable)
        {
            InstanceId = instanceId;
            DefinitionId = definitionId;
            Model = model;
            PriceText = priceText;
            IsSellable = isSellable;
        }
    }

    /// <summary>Aksesuar rafındaki satır: ürün adı + miktar.</summary>
    public sealed class ShopAccessoryViewModel
    {
        public string AccessoryId { get; }
        public string Name { get; }
        public string QuantityText { get; }

        public ShopAccessoryViewModel(string accessoryId, string name, string quantityText)
        {
            AccessoryId = accessoryId;
            Name = name;
            QuantityText = quantityText;
        }
    }

    /// <summary>Mağazadaki aktif müşteri kartı (mevcut kuyruk lobisi verisinden; yeni müşteri mantığı yok).</summary>
    public sealed class ShopCustomerViewModel
    {
        public long CustomerId { get; }
        public string NpcId { get; }
        public string Name { get; }

        /// <summary>"Müşteri mağazada: Kemal Abi".</summary>
        public string Title { get; }

        /// <summary>İlgilendiği ürün satırı ya da "ilgilendiği ürün yok".</summary>
        public string InterestLine { get; }

        /// <summary>"Sırada 2 müşteri daha var" (yoksa null).</summary>
        public string WaitingLine { get; }

        /// <summary>İlgilendiği ürün varsa "Müşteriye Git" açıktır (mevcut satış akışı).</summary>
        public bool CanGo { get; }

        public string GoButtonText { get; }

        public ShopCustomerViewModel(long customerId, string npcId, string name, string title, string interestLine, string waitingLine, bool canGo, string goButtonText)
        {
            CustomerId = customerId;
            NpcId = npcId;
            Name = name;
            Title = title;
            InterestLine = interestLine;
            WaitingLine = waitingLine;
            CanGo = canGo;
            GoButtonText = goButtonText;
        }
    }

    /// <summary>
    /// Dükkan ana ekranı (Gün 13.4): telefon rafı (satışta / satış dışı), aksesuar rafı, aktif müşteri. Satış ekranı DEĞİLDİR; telefona dokunmak mevcut Raf fiyat panelini,
    /// "Müşteriye Git" mevcut satış akışını açar. Oyun durumu içermez.
    /// </summary>
    public sealed class ShopScreenViewModel
    {
        public string ShelfHeader { get; }
        public IReadOnlyList<ShopPhoneViewModel> SellablePhones { get; }
        public IReadOnlyList<ShopPhoneViewModel> OffSalePhones { get; }
        public string OffSaleHeader { get; }

        /// <summary>Raf boşsa nazik not; değilse null.</summary>
        public string ShelfEmptyNote { get; }

        public string AccessoryHeader { get; }
        public IReadOnlyList<ShopAccessoryViewModel> Accessories { get; }

        /// <summary>Aksesuar stoğu boşsa nazik not; değilse null.</summary>
        public string AccessoriesEmptyNote { get; }

        public string CustomerHeader { get; }

        /// <summary>Aktif müşteri; yoksa null (o zaman <see cref="CustomerNote"/> gösterilir).</summary>
        public ShopCustomerViewModel Customer { get; }

        public string CustomerNote { get; }
        public string EndDayButtonText { get; }

        public ShopScreenViewModel(
            string shelfHeader,
            IEnumerable<ShopPhoneViewModel> sellable,
            IEnumerable<ShopPhoneViewModel> offSale,
            string offSaleHeader,
            string shelfEmptyNote,
            string accessoryHeader,
            IEnumerable<ShopAccessoryViewModel> accessories,
            string accessoriesEmptyNote,
            string customerHeader,
            ShopCustomerViewModel customer,
            string customerNote,
            string endDayButtonText)
        {
            ShelfHeader = shelfHeader;
            SellablePhones = new ReadOnlyCollection<ShopPhoneViewModel>(new List<ShopPhoneViewModel>(sellable));
            OffSalePhones = new ReadOnlyCollection<ShopPhoneViewModel>(new List<ShopPhoneViewModel>(offSale));
            OffSaleHeader = offSaleHeader;
            ShelfEmptyNote = shelfEmptyNote;
            AccessoryHeader = accessoryHeader;
            Accessories = new ReadOnlyCollection<ShopAccessoryViewModel>(new List<ShopAccessoryViewModel>(accessories));
            AccessoriesEmptyNote = accessoriesEmptyNote;
            CustomerHeader = customerHeader;
            Customer = customer;
            CustomerNote = customerNote;
            EndDayButtonText = endDayButtonText;
        }

        /// <summary>Ekranın yeniden kurulması gerekip gerekmediğini anlamak için içerik imzası.</summary>
        public string Signature()
        {
            var sb = new StringBuilder();
            sb.Append(ShelfHeader).Append('|').Append(AccessoryHeader).Append('|');
            foreach (ShopPhoneViewModel p in SellablePhones)
            {
                sb.Append(p.InstanceId).Append(':').Append(p.PriceText).Append(',');
            }

            sb.Append('|');
            foreach (ShopPhoneViewModel p in OffSalePhones)
            {
                sb.Append(p.InstanceId).Append(',');
            }

            sb.Append('|');
            foreach (ShopAccessoryViewModel a in Accessories)
            {
                sb.Append(a.AccessoryId).Append(':').Append(a.QuantityText).Append(',');
            }

            sb.Append('|').Append(Customer == null ? 0L : Customer.CustomerId).Append(Customer != null && Customer.CanGo ? "g" : "n");
            sb.Append('|').Append(Customer == null ? CustomerNote : Customer.WaitingLine);
            return sb.ToString();
        }
    }

    /// <summary>Kalıcı alt navigasyon sekmeleri (Gün 13.4).</summary>
    public enum NavTab
    {
        Shop = 0,
        Wholesale = 1,
        Listings = 2,
        Profile = 3
    }

    public sealed class NavItemViewModel
    {
        public NavTab Tab { get; }
        public string Label { get; }
        public bool IsActive { get; }

        public NavItemViewModel(NavTab tab, string label, bool isActive)
        {
            Tab = tab;
            Label = label;
            IsActive = isActive;
        }
    }

    /// <summary>Kalıcı alt navigasyon: Dükkan | Toptancı | İlanlar | Profil; hangi sekmenin aktif olduğu şu anki ekrandan türer.</summary>
    public sealed class NavBarViewModel
    {
        public IReadOnlyList<NavItemViewModel> Items { get; }
        public NavTab Active { get; }

        public NavBarViewModel(NavTab active)
        {
            Active = active;
            var items = new List<NavItemViewModel>
            {
                new NavItemViewModel(NavTab.Shop, TurkishTexts.NavShop, active == NavTab.Shop),
                new NavItemViewModel(NavTab.Wholesale, TurkishTexts.NavWholesale, active == NavTab.Wholesale),
                new NavItemViewModel(NavTab.Listings, TurkishTexts.NavListings, active == NavTab.Listings),
                new NavItemViewModel(NavTab.Profile, TurkishTexts.NavProfile, active == NavTab.Profile)
            };
            Items = new ReadOnlyCollection<NavItemViewModel>(items);
        }
    }
}
