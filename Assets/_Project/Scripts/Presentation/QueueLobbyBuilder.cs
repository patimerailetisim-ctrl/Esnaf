using Esnaf.Domain.Business;
using Esnaf.Domain.Game;
using Esnaf.Domain.Time;

namespace Esnaf.Presentation
{
    /// <summary>
    /// Günlük müşteri akışının görünümünü (Gün 12.5) IGameApi'nin saat, kuyruk ve aktif müşteri görünümlerinden kurar: satış ekranı lobisi ve İlanlar ekranındaki düğme yazısı.
    /// Kural yoktur; yalnızca domain'in söylediğini metne ve düğmelere çevirir.
    /// </summary>
    internal static class QueueLobbyBuilder
    {
        public static QueueLobbyViewModel Build(IGameApi api, ContentPresentation content)
        {
            ClockView clock = api.GetClock();
            CustomerQueueView queue = api.GetCustomerQueue();
            string progress = TurkishTexts.QueueProgress(queue.Served, queue.Total);

            if (!clock.IsOpen)
            {
                return new QueueLobbyViewModel(QueueLobbyState.Closed, clock.Text, progress, TurkishTexts.StoreClosedLine, null, null);
            }

            CustomerView active = api.GetActiveCustomer();
            if (active != null)
            {
                SaleCustomerCardViewModel card = CardOf(api, content, active);
                return active.InstanceId != 0
                    ? new QueueLobbyViewModel(QueueLobbyState.Arrived, clock.Text, progress, null, card, null)
                    : new QueueLobbyViewModel(QueueLobbyState.NoInterest, clock.Text, progress, TurkishTexts.NoInterestLine, card, null);
            }

            if (queue.NextArrivalMinute.HasValue)
            {
                string time = StoreHours.Format(queue.NextArrivalMinute.Value);
                return new QueueLobbyViewModel(QueueLobbyState.Waiting, clock.Text, progress, TurkishTexts.NextCustomerLine(time), null, time);
            }

            return new QueueLobbyViewModel(QueueLobbyState.Done, clock.Text, progress, TurkishTexts.QueueDoneLine, null, null);
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
                    return TurkishTexts.CustomerArrivedButton(lobby.Customer.Name);
                case QueueLobbyState.Waiting:
                    return TurkishTexts.NextCustomerButton(lobby.NextArrivalText);
                case QueueLobbyState.Closed:
                    return TurkishTexts.StoreClosedButton;
                default:
                    return TurkishTexts.QueueDoneButton;
            }
        }

        private static SaleCustomerCardViewModel CardOf(IGameApi api, ContentPresentation content, CustomerView customer)
        {
            string model = string.Empty;
            foreach (Esnaf.Domain.Economy.StockLine line in api.GetInventory())
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
