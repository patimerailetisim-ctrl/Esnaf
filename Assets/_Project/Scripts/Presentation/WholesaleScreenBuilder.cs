using System;
using System.Collections.Generic;
using Esnaf.Core;
using Esnaf.Domain.Accessories;
using Esnaf.Domain.Game;
using Esnaf.Domain.Wholesale;

namespace Esnaf.Presentation
{
    /// <summary>Toptancı ve aksesuar stoğu ekranlarının görünüm modellerini IGameApi görünümlerinden kurar. Kural yoktur: yalnızca metne çevirir.</summary>
    internal static class WholesaleScreenBuilder
    {
        public static WholesaleScreenViewModel BuildWholesale(IGameApi api)
        {
            var rows = new List<WholesaleOfferRowViewModel>();
            string supplier = string.Empty;
            foreach (WholesaleOfferView offer in api.GetWholesaleOffers())
            {
                if (supplier.Length == 0)
                {
                    supplier = offer.SupplierName;
                }

                bool locked = !offer.IsAvailableToday;
                rows.Add(new WholesaleOfferRowViewModel(
                    offer.SupplierId,
                    offer.AccessoryId,
                    offer.AccessoryName,
                    TurkishTexts.PackLine(offer.PackSize),
                    TurkishTexts.UnitCostLine(offer.UnitCost),
                    TurkishTexts.PackPriceLine(offer.PackCost),
                    locked,
                    locked ? TurkishTexts.OpensOnButton(offer.AvailableFromDay) : TurkishTexts.BuyPackButton,
                    locked ? TurkishTexts.OpensOnNote(offer.AvailableFromDay) : null));
            }

            AccessoryStockView stock = api.GetAccessoryStock();
            return new WholesaleScreenViewModel(
                TurkishTexts.WholesaleTitle,
                supplier,
                TurkishTexts.Cash(api.GetCash()),
                TurkishTexts.StockCapacityLine(stock.TotalUnits, stock.Capacity),
                rows,
                rows.Count == 0 ? TurkishTexts.WholesaleNoOffers : null);
        }

        public static AccessoryStockScreenViewModel BuildStock(IGameApi api)
        {
            AccessoryStockView stock = api.GetAccessoryStock();
            var rows = new List<AccessoryStockRowViewModel>();
            foreach (AccessoryStockLineView line in stock.Lines)
            {
                rows.Add(new AccessoryStockRowViewModel(
                    line.AccessoryId,
                    line.AccessoryName,
                    TurkishTexts.StockQuantityLine(line.Quantity),
                    TurkishTexts.StockAverageCostLine(AverageCost(line)),
                    TurkishTexts.StockLineTotalLine(line.TotalCost),
                    TurkishTexts.StockShareLine(Percent(line.Quantity, stock.Capacity))));
            }

            return new AccessoryStockScreenViewModel(
                TurkishTexts.AccessoryStockTitle,
                TurkishTexts.StockCapacityLine(stock.TotalUnits, stock.Capacity),
                TurkishTexts.StockTotalCostLine(stock.TotalCost),
                rows,
                rows.Count == 0 ? TurkishTexts.AccessoryStockEmpty : null);
        }

        /// <summary>Ortalama birim maliyet = toplam maliyet / adet (en yakın liraya yuvarlı). Yalnızca gösterimdir.</summary>
        internal static Money AverageCost(AccessoryStockLineView line)
        {
            if (line.Quantity <= 0)
            {
                return Money.Zero;
            }

            return Money.FromTl((line.TotalCost.Tl * 2 + line.Quantity) / (2L * line.Quantity));
        }

        /// <summary>Kapasitenin yüzdesi (en yakın tamsayı).</summary>
        internal static int Percent(int units, int capacity)
        {
            return capacity <= 0 ? 0 : (int)Math.Round(units * 100.0 / capacity, MidpointRounding.AwayFromZero);
        }
    }
}
