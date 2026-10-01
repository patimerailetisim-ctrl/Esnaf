using System.Collections.Generic;
using System.Collections.ObjectModel;
using Esnaf.Domain.Negotiation;
using Esnaf.Domain.Products;

namespace Esnaf.Domain.Npc
{
    /// <summary>
    /// Bir müşterinin kişilik profili: kişilik arketipi + gerçek mekanik sayılarından okunan düzeyler. Değişmez, saklanmaz, yeniden türetilir.
    /// Gelecekteki doğal Türkçe diyalog bu düzeylerden (ve satış görünümündeki ruh hali/sabırdan) cümle seçer; metin burada yoktur.
    /// </summary>
    public sealed class CustomerProfile
    {
        public string NpcId { get; }
        public string Name { get; }
        public string PersonalityId { get; }
        public string PersonalityName { get; }
        public NegotiationLevel Patience { get; }
        public NegotiationLevel Urgency { get; }
        public NegotiationLevel Knowledge { get; }
        public NegotiationLevel Budget { get; }
        public NegotiationLevel Haggling { get; }

        /// <summary>İlgilendiği segmentler; boş = hepsi.</summary>
        public IReadOnlyList<ProductSegment> PreferredSegments { get; }

        /// <summary>Bu yuvanın başlangıç güven düzeyi (ruh hali); NPC düzeyinde (yuva yok) null.</summary>
        public NegotiationLevel? StartTrust { get; }

        public CustomerProfile(
            string npcId,
            string name,
            string personalityId,
            string personalityName,
            NegotiationLevel patience,
            NegotiationLevel urgency,
            NegotiationLevel knowledge,
            NegotiationLevel budget,
            NegotiationLevel haggling,
            IEnumerable<ProductSegment> preferredSegments,
            NegotiationLevel? startTrust = null)
        {
            NpcId = npcId;
            Name = name;
            PersonalityId = personalityId;
            PersonalityName = personalityName;
            Patience = patience;
            Urgency = urgency;
            Knowledge = knowledge;
            Budget = budget;
            Haggling = haggling;
            PreferredSegments = new ReadOnlyCollection<ProductSegment>(new List<ProductSegment>(preferredSegments));
            StartTrust = startTrust;
        }

        public CustomerProfile WithStartTrust(NegotiationLevel startTrust)
        {
            return new CustomerProfile(NpcId, Name, PersonalityId, PersonalityName, Patience, Urgency, Knowledge, Budget, Haggling, PreferredSegments, startTrust);
        }
    }
}
