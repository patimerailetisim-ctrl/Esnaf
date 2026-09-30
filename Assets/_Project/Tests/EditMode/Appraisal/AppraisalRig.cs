using Esnaf.Core;
using Esnaf.Domain.Appraisal;
using Esnaf.Domain.Content;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Products;
using Esnaf.Tests.Support;

namespace Esnaf.Tests.Appraisal
{
    /// <summary>
    /// Ekspertiz servisi için hazır düzenek: fixture içeriği (sıfır sapma/gürültü), gerçek servisler ve olay yolu.
    /// Fixture ürünü phone.test_one: Orta segment, baz 10.000 TL.
    /// </summary>
    public sealed class AppraisalRig
    {
        public ContentDatabase Content { get; }
        public EconomyHarness Economy { get; }
        public KnowledgeState Knowledge { get; }
        public EquipmentState Equipment { get; }
        public IdGenerator ResultIds { get; }
        public AppraisalService Service { get; }
        public EventBus Bus { get; }

        public AppraisalRig(ulong masterSeed = 42UL, ContentDatabase content = null)
        {
            Content = content ?? MarketHarness.FixtureContent();
            Economy = new EconomyHarness();
            Bus = Economy.Bus;
            Knowledge = new KnowledgeState();
            Equipment = new EquipmentState();
            ResultIds = new IdGenerator();
            Service = new AppraisalService(
                Content, Knowledge, Equipment, Economy.Economy, Economy.Store, ResultIds, Bus, masterSeed);
        }

        /// <summary>Pazarda duran, gizli ekranı değişmiş test telefonu (v0.2 5.4 örneğinin fixture karşılığı).</summary>
        public ProductInstance Listing(string definitionId = "phone.test_one", string screen = "replaced_aftermarket", string camera = "ok")
        {
            ProductInstance instance = Economy.NewMarketInstance(definitionId);
            instance.AgeMonths = 12;
            instance.StorageGb = 128;
            instance.Attributes["battery"] = AttributeValue.FromNumber(78);
            instance.Attributes["screen"] = AttributeValue.FromText(screen);
            instance.Attributes["body"] = AttributeValue.FromNumber(85);
            instance.Attributes["camera"] = AttributeValue.FromText(camera);
            instance.Attributes["box"] = AttributeValue.FromFlag(false);
            instance.Attributes["invoice"] = AttributeValue.FromFlag(false);
            return instance;
        }
    }
}
