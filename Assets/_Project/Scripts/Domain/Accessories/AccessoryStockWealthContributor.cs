using System;
using Esnaf.Core;
using Esnaf.Domain.Economy;

namespace Esnaf.Domain.Accessories
{
    /// <summary>Aksesuar stoğu: maliyet tabanıyla servete girer (telefon stoğuyla aynı yaklaşım: <c>StockWealthContributor</c>).</summary>
    public sealed class AccessoryStockWealthContributor : IWealthContributor
    {
        private readonly AccessoryStock _stock;

        public AccessoryStockWealthContributor(AccessoryStock stock)
        {
            if (stock == null)
            {
                throw new ArgumentNullException(nameof(stock));
            }

            _stock = stock;
        }

        public string Key
        {
            get { return WealthKeys.AccessoryStock; }
        }

        public Money GetValue()
        {
            return _stock.StockCost;
        }
    }
}
