using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Esnaf.Domain.Economy
{
    /// <summary>Defter türleri kayıt defteri (dosyadaki sırayla, salt okunur).</summary>
    public sealed class TransactionTypes
    {
        private readonly ReadOnlyCollection<TransactionType> _types;
        private readonly Dictionary<string, TransactionType> _byId;

        public IReadOnlyList<TransactionType> Types
        {
            get { return _types; }
        }

        /// <exception cref="ArgumentException">Kimlik boş veya yinelenmişse (doğrulayıcı bunu önceden yakalar).</exception>
        public TransactionTypes(IEnumerable<TransactionType> types)
        {
            if (types == null)
            {
                throw new ArgumentNullException(nameof(types));
            }

            var list = new List<TransactionType>(types);
            _byId = new Dictionary<string, TransactionType>(list.Count, StringComparer.Ordinal);
            foreach (TransactionType type in list)
            {
                if (type == null || string.IsNullOrEmpty(type.Id))
                {
                    throw new ArgumentException("Transaction types need a non-empty id.", nameof(types));
                }

                if (_byId.ContainsKey(type.Id))
                {
                    throw new ArgumentException("Duplicate transaction type id '" + type.Id + "'.", nameof(types));
                }

                _byId.Add(type.Id, type);
            }

            _types = new ReadOnlyCollection<TransactionType>(list);
        }

        public bool TryGet(string id, out TransactionType type)
        {
            if (id == null)
            {
                type = null;
                return false;
            }

            return _byId.TryGetValue(id, out type);
        }

        public TransactionType Get(string id)
        {
            TransactionType type;
            if (!TryGet(id, out type))
            {
                throw new KeyNotFoundException("Unknown transaction type '" + id + "'.");
            }

            return type;
        }
    }
}
