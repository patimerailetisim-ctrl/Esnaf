using System.Collections.Generic;
using Esnaf.Domain.Business;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Game;
using Esnaf.Domain.Time;

namespace Esnaf.Presentation
{
    /// <summary>
    /// Günlük müşteri akışının görünümünü (Gün 12.5, 12.6) IGameApi'nin saat, kuyruk ve aktif müşteri görünümlerinden kurar: satış ekranı lobisi ve İlanlar ekranındaki düğme yazısı.
    /// Kural yoktur; yalnızca domain'in söylediğini metne ve düğmelere çevirir. Toplam/günlük müşteri sayısı ya da sıradaki geliş saati ASLA gösterilmez; yalnızca şu an
    /// mağazada olanlar (aktif müşteri ve sıradakiler) görünür.
    /// </summary>
    internal static class QueueLobbyBuilder
    {
        public static QueueLobbyViewModel Build(IGameApi api, ContentPresentation content)
        {
            ClockView clock = api.GetClock();
            CustomerQueueView queue = api.GetCustomerQueue();
            var waiting = new List<QueueWaitingRowViewModel>();
            foreach (QueuedCustomer c in queue.Line)
            {
                waiting.Add(new QueueWaitingRowViewModel(
                    c.CustomerId, c.NpcId, content.CustomerName(c.CustomerId, c.NpcId), TurkishTexts.ArrivedAt(c.ArrivalText)));
            }

            if (!clock.IsOpen)
            {
                return new QueueLobbyViewModel(QueueLobbyState.Closed, TurkishTexts.StoreClosedLine, null, new QueueWaitingRowViewModel[0]);
            }

            CustomerView active = api.GetActiveCustomer();
            if (active != null)
            {
                SaleCustomerCardViewModel card = CardOf(api, content, active);
                return active.InstanceId != 0
                    ? new QueueLobbyViewModel(QueueLobbyState.Arrived, null, card, waiting)
                    : new QueueLobbyViewModel(QueueLobbyState.NoInterest, TurkishTexts.NoInterestLine, card, waiting);
            }

            return new QueueLobbyViewModel(QueueLobbyState.Empty, TurkishTexts.NoCustomersLine, null, new QueueWaitingRowViewModel[0]);
        }

        /// <summary>İlanlar ekranındaki Müşteriler düğmesinin yazısı: durum özeti.</summary>
        public static string ButtonText(IGameApi api, ContentPresentation content)
        {
            if (api.GetSale() != null)
            {
                return TurkishTexts.SaleInProgressButton;
            }

            QueueLobbyViewModel lobby = Build(api, content);
            switch (lobby.State)
            {
                case QueueLobbyState.Arrived:
                case QueueLobbyState.NoInterest:
                    return TurkishTexts.CustomerArrivedButton(lobby.Customer.Name, lobby.Waiting.Count);
                case QueueLobbyState.Closed:
                    return TurkishTexts.StoreClosedButton;
                default:
                    return TurkishTexts.NoCustomersButton;
            }
        }

        private static SaleCustomerCardViewModel CardOf(IGameApi api, ContentPresentation content, CustomerView customer)
        {
            string model = string.Empty;
            foreach (StockLine line in api.GetInventory())
            {
                if (line.InstanceId == customer.InstanceId)
                {
                    model = content.ModelName(line.DefinitionId);
                }
            }

            return new SaleCustomerCardViewModel(
                customer.CustomerId,
                customer.NpcId,
                content.CustomerName(customer.CustomerId, customer.NpcId),
                customer.Profile == null ? string.Empty : customer.Profile.PersonalityName,
                customer.InstanceId == 0 ? TurkishTexts.NoInterestLine : TurkishTexts.CustomerInterest(model));
        }
    }
}
