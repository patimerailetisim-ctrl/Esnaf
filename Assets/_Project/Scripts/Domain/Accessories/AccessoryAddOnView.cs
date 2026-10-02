using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Esnaf.Core;

namespace Esnaf.Domain.Accessories
{
    /// <summary>Bir aksesuar ek satış seçeneği: sabit satış fiyatı ve stok. Stok yoksa <see cref="IsAvailable"/> false'tur.</summary>
    public sealed class AccessoryAddOnOptionView
    {
        public string AccessoryId { get; }
        public string AccessoryName { get; }

        /// <summary>Sabit satış fiyatı (retailPrice); pazarlık yoktur.</summary>
        public Money RetailPrice { get; }

        public int InStock { get; }

        public bool IsAvailable
        {
            get { return InStock > 0; }
        }

        public AccessoryAddOnOptionView(string accessoryId, string accessoryName, Money retailPrice, int inStock)
        {
            AccessoryId = accessoryId;
            AccessoryName = accessoryName;
            RetailPrice = retailPrice;
            InStock = inStock;
        }
    }

    /// <summary>
    /// Aksesuar ek satış fırsatı (IGameApi.GetAccessoryAddOns): bugünün SON tamamlanmış telefon satışı ve ona eklenebilecek aksesuarlar.
    /// Bugün tamamlanmış telefon satışı yoksa <see cref="HasPhoneSale"/> false'tur ve seçenek yoktur. Telefon satışının kendisi bu görünümle değişmez.
    /// </summary>
    public sealed class AccessoryAddOnView
    {
        public bool HasPhoneSale { get; }

        /// <summary>Ek satışın bağlandığı telefon satış defter satırı; satış yoksa 0.</summary>
        public long PhoneSaleRecordId { get; }

        /// <summary>Telefonu alan müşterinin NPC kimliği; satış yoksa null.</summary>
        public string BuyerNpcId { get; }

        public Money PhoneSalePrice { get; }
        public Money PhoneProfit { get; }

        /// <summary>Bu telefon satışına şimdiye kadar eklenen aksesuar adedi.</summary>
        public int AddOnsSold { get; }

        /// <summary>Bu telefon satışına eklenen aksesuarların toplam satış tutarı (ciro).</summary>
        public Money AccessoryRevenue { get; }

        public Money AccessoryProfit { get; }

        /// <summary>Telefon kârı + şimdiye kadarki aksesuar kârları.</summary>
        public Money TotalProfit { get; }

        public IReadOnlyList<AccessoryAddOnOptionView> Options { get; }

        public AccessoryAddOnView(
            bool hasPhoneSale, long phoneSaleRecordId, string buyerNpcId, Money phoneSalePrice, Money phoneProfit,
            int addOnsSold, Money accessoryRevenue, Money accessoryProfit, Money totalProfit, IEnumerable<AccessoryAddOnOptionView> options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            HasPhoneSale = hasPhoneSale;
            PhoneSaleRecordId = phoneSaleRecordId;
            BuyerNpcId = buyerNpcId;
            PhoneSalePrice = phoneSalePrice;
            PhoneProfit = phoneProfit;
            AddOnsSold = addOnsSold;
            AccessoryRevenue = accessoryRevenue;
            AccessoryProfit = accessoryProfit;
            TotalProfit = totalProfit;
            Options = new ReadOnlyCollection<AccessoryAddOnOptionView>(new List<AccessoryAddOnOptionView>(options));
        }
    }
}
