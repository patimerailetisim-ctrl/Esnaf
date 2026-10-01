using System;
using Esnaf.Domain.Negotiation;

namespace Esnaf.Domain.Npc
{
    /// <summary>NPC'nin gerçek mekanik sayılarından kişilik düzeylerini ve profilini okur. Rastgelelik kullanmaz, formül eklemez.</summary>
    public static class CustomerProfiler
    {
        public static NegotiationLevel LevelOf(CustomerTrait trait, NpcDefinition npc, PersonalityScales scales, NegotiationRules rules)
        {
            switch (trait)
            {
                case CustomerTrait.Patience:
                    return NegotiationLevels.PatienceOf(rules, npc.Customer.Patience);
                case CustomerTrait.Urgency:
                    return scales.Urgency.LevelOf(npc.Seller.Urgency);
                case CustomerTrait.Knowledge:
                    return scales.Knowledge.LevelOf(npc.Seller.ValueSigma);
                case CustomerTrait.Budget:
                    return scales.Budget.LevelOf(npc.Customer.ValueRatio);
                case CustomerTrait.Haggling:
                    return scales.Haggling.LevelOf(npc.Customer.OpeningOfferRatio);
                default:
                    throw new ArgumentOutOfRangeException(nameof(trait));
            }
        }

        /// <returns>Kişilik kataloğu boşsa veya NPC'nin kişiliği katalogda yoksa null.</returns>
        public static CustomerProfile Build(NpcDefinition npc, PersonalityCatalog catalog, NegotiationRules rules)
        {
            if (npc == null)
            {
                throw new ArgumentNullException(nameof(npc));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            PersonalityDefinition definition;
            if (catalog.IsEmpty || !catalog.TryGet(npc.PersonalityId, out definition))
            {
                return null;
            }

            PersonalityScales scales = catalog.Scales;
            return new CustomerProfile(
                npc.Id,
                npc.Name,
                definition.Id,
                definition.Name,
                LevelOf(CustomerTrait.Patience, npc, scales, rules),
                LevelOf(CustomerTrait.Urgency, npc, scales, rules),
                LevelOf(CustomerTrait.Knowledge, npc, scales, rules),
                LevelOf(CustomerTrait.Budget, npc, scales, rules),
                LevelOf(CustomerTrait.Haggling, npc, scales, rules),
                npc.Customer.Segments);
        }
    }
}
