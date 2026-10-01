using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Esnaf.Domain.Npc
{
    /// <summary>Kişilik arketipleri ve ölçekleri (npc_profiles.json). Bölümler yoksa <see cref="Empty"/>.</summary>
    public sealed class PersonalityCatalog
    {
        private readonly Dictionary<string, PersonalityDefinition> _byId;

        public static readonly PersonalityCatalog Empty = new PersonalityCatalog(null, new PersonalityDefinition[0]);

        /// <summary>Ölçekler; katalog boşsa null.</summary>
        public PersonalityScales Scales { get; }

        public IReadOnlyList<PersonalityDefinition> Definitions { get; }

        public bool IsEmpty
        {
            get { return Definitions.Count == 0; }
        }

        public PersonalityCatalog(PersonalityScales scales, IEnumerable<PersonalityDefinition> definitions)
        {
            var list = new List<PersonalityDefinition>(definitions);
            Scales = scales;
            Definitions = new ReadOnlyCollection<PersonalityDefinition>(list);
            _byId = new Dictionary<string, PersonalityDefinition>(StringComparer.Ordinal);
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Id != null && !_byId.ContainsKey(list[i].Id))
                {
                    _byId.Add(list[i].Id, list[i]);
                }
            }
        }

        public bool TryGet(string id, out PersonalityDefinition definition)
        {
            if (id == null)
            {
                definition = null;
                return false;
            }

            return _byId.TryGetValue(id, out definition);
        }
    }
}
