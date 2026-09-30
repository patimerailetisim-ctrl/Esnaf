using Esnaf.Core;

namespace Esnaf.Domain.Market
{
    /// <summary>Gün 1'in rehberli ilanının tam tarifi (v0.2 Gün 1): ürün nitelikleri, satıcı ve kolay mod ret fiyatı.</summary>
    public sealed class GuidedListingSpec
    {
        public string SellerNpcId { get; }
        public string DefinitionId { get; }
        public int StorageGb { get; }
        public int AgeMonths { get; }
        public int Battery { get; }
        public int Body { get; }
        public string Screen { get; }
        public string Camera { get; }
        public bool Box { get; }
        public bool Invoice { get; }
        public Money RejectPrice { get; }

        public GuidedListingSpec(
            string sellerNpcId,
            string definitionId,
            int storageGb,
            int ageMonths,
            int battery,
            int body,
            string screen,
            string camera,
            bool box,
            bool invoice,
            Money rejectPrice)
        {
            SellerNpcId = sellerNpcId;
            DefinitionId = definitionId;
            StorageGb = storageGb;
            AgeMonths = ageMonths;
            Battery = battery;
            Body = body;
            Screen = screen;
            Camera = camera;
            Box = box;
            Invoice = invoice;
            RejectPrice = rejectPrice;
        }
    }
}
