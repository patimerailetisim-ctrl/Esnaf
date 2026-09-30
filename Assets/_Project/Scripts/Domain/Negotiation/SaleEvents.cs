using Esnaf.Core;

namespace Esnaf.Domain.Negotiation
{
    // Yalnızca BİLDİRİM (K5): durum önce değişir, olay sonra yayınlanır. Olaylar gizli pazarlık durumunu taşımaz.

    public sealed class CustomerNegotiationStarted
    {
        public long CustomerId { get; }
        public string NpcId { get; }
        public long InstanceId { get; }

        public CustomerNegotiationStarted(long customerId, string npcId, long instanceId)
        {
            CustomerId = customerId;
            NpcId = npcId;
            InstanceId = instanceId;
        }
    }

    public sealed class CustomerOfferMade
    {
        public long CustomerId { get; }
        public int Round { get; }
        public Money Ask { get; }
        public Money ShownPrice { get; }
        public NegotiationPhase Phase { get; }
        public bool TooExpensive { get; }

        public CustomerOfferMade(long customerId, int round, Money ask, Money shownPrice, NegotiationPhase phase, bool tooExpensive)
        {
            CustomerId = customerId;
            Round = round;
            Ask = ask;
            ShownPrice = shownPrice;
            Phase = phase;
            TooExpensive = tooExpensive;
        }
    }

    public sealed class ItemSold
    {
        public long InstanceId { get; }
        public string BuyerNpcId { get; }
        public Money Price { get; }

        public ItemSold(long instanceId, string buyerNpcId, Money price)
        {
            InstanceId = instanceId;
            BuyerNpcId = buyerNpcId;
            Price = price;
        }
    }

    public sealed class CustomerNegotiationEnded
    {
        public long CustomerId { get; }
        public long InstanceId { get; }

        /// <summary>Deal ya da Failed.</summary>
        public NegotiationPhase Phase { get; }

        public Money DealPrice { get; }

        public CustomerNegotiationEnded(long customerId, long instanceId, NegotiationPhase phase, Money dealPrice)
        {
            CustomerId = customerId;
            InstanceId = instanceId;
            Phase = phase;
            DealPrice = dealPrice;
        }
    }
}
