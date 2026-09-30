using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Esnaf.Domain.Products
{
    /// <summary>
    /// Tüm ürün örneklerinin tek deposu (GDD v0.3 4.3). Konumlar (pazar/envanter/satıldı) örneğin <c>Location</c> alanındadır;
    /// kapsayıcılar yalnızca kimlik tutar. Eklenme sırası korunur.
    /// </summary>
    public sealed class InstanceStore
    {
        private readonly Dictionary<long, ProductInstance> _byId = new Dictionary<long, ProductInstance>();
        private readonly List<ProductInstance> _ordered = new List<ProductInstance>();
        private readonly ReadOnlyCollection<ProductInstance> _view;

        public InstanceStore()
        {
            _view = new ReadOnlyCollection<ProductInstance>(_ordered);
        }

        public int Count
        {
            get { return _ordered.Count; }
        }

        public IReadOnlyList<ProductInstance> All
        {
            get { return _view; }
        }

        public void Add(ProductInstance instance)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            if (instance.InstanceId <= 0)
            {
                throw new ArgumentException("Instance ids start at 1.", nameof(instance));
            }

            if (_byId.ContainsKey(instance.InstanceId))
            {
                throw new ArgumentException("Instance " + instance.InstanceId + " is already stored.", nameof(instance));
            }

            _byId.Add(instance.InstanceId, instance);
            _ordered.Add(instance);
        }

        public bool TryGet(long instanceId, out ProductInstance instance)
        {
            return _byId.TryGetValue(instanceId, out instance);
        }

        public ProductInstance Get(long instanceId)
        {
            ProductInstance instance;
            if (!_byId.TryGetValue(instanceId, out instance))
            {
                throw new KeyNotFoundException("Unknown product instance " + instanceId + ".");
            }

            return instance;
        }
    }
}
