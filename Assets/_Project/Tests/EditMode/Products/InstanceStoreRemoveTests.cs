using System;
using System.Linq;
using Esnaf.Domain.Products;
using NUnit.Framework;

namespace Esnaf.Tests.Products
{
    public class InstanceStoreRemoveTests
    {
        private static InstanceStore Store(params long[] ids)
        {
            var store = new InstanceStore();
            foreach (long id in ids)
            {
                store.Add(new ProductInstance { InstanceId = id, DefinitionId = "phone.x" });
            }

            return store;
        }

        [Test]
        public void Remove_DeletesTheInstance_AndKeepsTheRestInOrder()
        {
            InstanceStore store = Store(1, 2, 3, 4);

            Assert.IsTrue(store.Remove(2));

            CollectionAssert.AreEqual(new long[] { 1, 3, 4 }, store.All.Select(i => i.InstanceId).ToArray());
            Assert.AreEqual(3, store.Count);
            ProductInstance found;
            Assert.IsFalse(store.TryGet(2, out found));
            Assert.IsTrue(store.TryGet(3, out found));
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => store.Get(2));
        }

        [Test]
        public void Remove_UnknownId_ReturnsFalse_AndChangesNothing()
        {
            InstanceStore store = Store(1, 2);

            Assert.IsFalse(store.Remove(9));
            Assert.IsFalse(store.Remove(0));

            Assert.AreEqual(2, store.Count);
        }

        [Test]
        public void Remove_ThenAddingTheSameIdAgain_IsAllowed()
        {
            InstanceStore store = Store(1);
            store.Remove(1);

            Assert.DoesNotThrow(() => store.Add(new ProductInstance { InstanceId = 1, DefinitionId = "phone.y" }));
            Assert.AreEqual("phone.y", store.Get(1).DefinitionId);
        }
    }
}
