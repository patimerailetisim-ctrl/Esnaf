using System.Collections.Generic;
using Esnaf.Core;
using Esnaf.Domain.Accessories;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Game;

namespace Esnaf.Presentation
{
    /// <summary>
    /// Dükkan ana ekranını (Gün 13.4) MEVCUT API'lerden kurar: GetInventory (raf, satış fiyatı), GetAccessoryStock, kuyruk lobisi (aktif müşteri). Yeni kural/ekonomi/müşteri mantığı yoktur;
    /// yalnızca gösterir.
    /// </summary>
    internal static class ShopScreenBuilder
    {
        public static ShopScreenViewModel Build(IGameApi api, ContentPresentation content)
        {
            IReadOnlyList<StockLine> stock = api.GetInventory();
            var sellable = new List<ShopPhoneViewModel>();
            var offSale = new List<ShopPhoneViewModel>();
            foreach (StockLine line in stock)
            {
                bool priced = line.ListPrice.IsPositive;
                var tile = new ShopPhoneViewModel(
                    line.InstanceId,
                    line.DefinitionId,
                    content.ModelName(line.DefinitionId),
                    priced ? MoneyFormatter.Format(line.ListPrice) : TurkishTexts.ShopOffSale,
                    priced);
                (priced ? sellable : offSale).Add(tile);
            }

            AccessoryStockView accessories = api.GetAccessoryStock();
            var accessoryRows = new List<ShopAccessoryViewModel>();
            foreach (AccessoryStockLineView line in accessories.Lines)
            {
                accessoryRows.Add(new ShopAccessoryViewModel(line.AccessoryId, line.AccessoryName, line.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }

            QueueLobbyViewModel lobby = QueueLobbyBuilder.Build(api, content);
            ShopCustomerViewModel customer = null;
            string note = TurkishTexts.ShopNoCustomer;
            if (lobby.State == QueueLobbyState.Closed)
            {
                note = TurkishTexts.StoreClosedLine;
            }
            else if (lobby.Customer != null)
            {
                bool canGo = lobby.State == QueueLobbyState.Arrived;
                customer = new ShopCustomerViewModel(
                    lobby.Customer.CustomerId,
                    lobby.Customer.NpcId,
                    lobby.Customer.Name,
                    TurkishTexts.ShopCustomerTitle(lobby.Customer.Name),
                    lobby.Customer.InterestLine,
                    lobby.Waiting.Count > 0 ? TurkishTexts.ShopWaiting(lobby.Waiting.Count) : null,
                    canGo,
                    TurkishTexts.GoToCustomerButton);
            }

            return new ShopScreenViewModel(
                TurkishTexts.ShopShelfHeader(stock.Count, content.ShelfCapacity),
                sellable,
                offSale,
                TurkishTexts.ShopOffSaleHeader,
                stock.Count == 0 ? TurkishTexts.ShopShelfEmpty : null,
                TurkishTexts.ShopAccessoryHeader(accessories.TotalUnits, accessories.Capacity),
                accessoryRows,
                accessoryRows.Count == 0 ? TurkishTexts.ShopAccessoriesEmpty : null,
                TurkishTexts.ShopCustomerHeader,
                customer,
                note,
                TurkishTexts.EndDayButton);
        }
    }
}
