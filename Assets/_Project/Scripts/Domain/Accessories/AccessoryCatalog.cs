using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Esnaf.Domain.Accessories
{
    /// <summary>Aksesuar tanımları ve aksesuar rafının birim kapasitesi (accessories.json). Dosya yoksa <see cref="Empty"/>.</summary>
    public sealed class AccessoryCatalog
    {
        private readonly Dictionary<string, AccessoryDefinition> _byId;

        public static readonly AccessoryCatalog Empty = new AccessoryCatalog(0, new AccessoryDefinition[0]);

        /// <summary>Aksesuar rafının başlangıç kapasitesi (BİRİM cinsinden); telefon rafından tamamen ayrıdır.</summary>
        public int ShelfCapacityUnits { get; }

        public IReadOnlyList<AccessoryDefinition> Definitions { get; }

        public bool IsEmpty
        {
            get { return Definitions.Count == 0; }
        }

        public AccessoryCatalog(int shelfCapacityUnits, IEnumerable<AccessoryDefinition> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            var list = new List<AccessoryDefinition>(definitions);
            ShelfCapacityUnits = shelfCapacityUnits;
            Definitions = new ReadOnlyCollection<AccessoryDefinition>(list);
            _byId = new Dictionary<string, AccessoryDefinition>(StringComparer.Ordinal);
            foreach (AccessoryDefinition definition in list)
            {
                if (definition.Id != null && !_byId.ContainsKey(definition.Id))
                {
                    _byId.Add(definition.Id, definition);
                }
            }
        }

        public bool TryGet(string id, out AccessoryDefinition definition)
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
