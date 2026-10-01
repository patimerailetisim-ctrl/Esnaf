using System;
using System.Collections.Generic;
using Esnaf.Domain.Negotiation;

namespace Esnaf.Domain.Npc
{
    /// <summary>
    /// Bir kişilik arketipi (Pazarlıkçı, Aceleci...). "Beklentiler", arketipin her boyutta kabul ettiği düzeylerdir; NPC'nin GERÇEK sayıları
    /// bunlara uymalıdır (içerik doğrulaması). Beklenti yazılmayan boyut her düzeyi kabul eder.
    /// </summary>
    public sealed class PersonalityDefinition
    {
        private readonly Dictionary<CustomerTrait, HashSet<NegotiationLevel>> _expects;

        public string Id { get; }
        public string Name { get; }

        public PersonalityDefinition(string id, string name, IDictionary<CustomerTrait, IEnumerable<NegotiationLevel>> expects)
        {
            if (expects == null)
            {
                throw new ArgumentNullException(nameof(expects));
            }

            Id = id;
            Name = name;
            _expects = new Dictionary<CustomerTrait, HashSet<NegotiationLevel>>();
            foreach (KeyValuePair<CustomerTrait, IEnumerable<NegotiationLevel>> pair in expects)
            {
                _expects[pair.Key] = new HashSet<NegotiationLevel>(pair.Value);
            }
        }

        public bool Accepts(CustomerTrait trait, NegotiationLevel level)
        {
            HashSet<NegotiationLevel> allowed;
            return !_expects.TryGetValue(trait, out allowed) || allowed.Contains(level);
        }
    }
}
