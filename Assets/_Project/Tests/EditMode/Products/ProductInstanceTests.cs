using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Esnaf.Core;
using Esnaf.Domain.Products;
using NUnit.Framework;

namespace Esnaf.Tests.Products
{
    public class ProductInstanceTests
    {
        /// <summary>GDD v0.3 Bölüm 4.4 örneği: oyuncunun envanterindeki ekranı değişmiş Elma E13 Pro.</summary>
        private static ProductInstance GddExample()
        {
            var instance = new ProductInstance
            {
                InstanceId = 1042,
                DefinitionId = "phone.elma_e13_pro",
                StorageGb = 128,
                AgeMonths = 18,
                SellerNpcId = "npc.kemal",
                ListingId = 311,
                AcquiredDay = 4,
                PurchasePrice = Money.FromTl(27000),
                CostBasis = Money.FromTl(27000),
                Location = ProductLocation.Inventory
            };
            instance.Attributes["battery"] = AttributeValue.FromNumber(78);
            instance.Attributes["screen"] = AttributeValue.FromText("replaced_aftermarket");
            instance.Attributes["body"] = AttributeValue.FromNumber(82);
            instance.Attributes["camera"] = AttributeValue.FromText("ok");
            instance.Attributes["box"] = AttributeValue.FromFlag(false);
            instance.Attributes["invoice"] = AttributeValue.FromFlag(false);
            return instance;
        }

        [Test]
        public void GddExample_HoldsConcreteState()
        {
            ProductInstance instance = GddExample();

            Assert.AreEqual(1042L, instance.InstanceId);
            Assert.AreEqual("phone.elma_e13_pro", instance.DefinitionId);
            Assert.AreEqual(78L, instance.GetNumber("battery"));
            Assert.AreEqual("replaced_aftermarket", instance.GetText("screen"));
            Assert.AreEqual(82L, instance.GetNumber("body"));
            Assert.AreEqual("ok", instance.GetText("camera"));
            Assert.IsFalse(instance.GetFlag("box"));
            Assert.IsFalse(instance.GetFlag("invoice"));
            Assert.AreEqual(Money.FromTl(27000), instance.PurchasePrice);
            Assert.AreEqual(Money.FromTl(27000), instance.CostBasis);
            Assert.AreEqual(4, instance.AcquiredDay);
            Assert.AreEqual(ProductLocation.Inventory, instance.Location);
        }

        [Test]
        public void NewInstance_HasSafeDefaults()
        {
            var instance = new ProductInstance();

            Assert.AreEqual(ProductLocation.Market, instance.Location);
            Assert.IsNull(instance.AcquiredDay);
            Assert.AreEqual(Money.Zero, instance.PurchasePrice);
            Assert.AreEqual(Money.Zero, instance.CostBasis);
            Assert.AreEqual(0, instance.Attributes.Count);
        }

        [Test]
        public void MissingAttribute_ThrowsKeyNotFound()
        {
            ProductInstance instance = GddExample();

            Assert.Throws<KeyNotFoundException>(() => instance.GetNumber("water_damage"));
            Assert.Throws<KeyNotFoundException>(() => instance.GetNumber(null));
        }

        [Test]
        public void WrongAttributeKind_ThrowsInvalidOperation()
        {
            ProductInstance instance = GddExample();

            Assert.Throws<InvalidOperationException>(() => instance.GetText("battery"));
            Assert.Throws<InvalidOperationException>(() => instance.GetNumber("screen"));
            Assert.Throws<InvalidOperationException>(() => instance.GetFlag("battery"));
        }

        [Test]
        public void AttributeKeys_AreCaseSensitive()
        {
            ProductInstance instance = GddExample();

            Assert.Throws<KeyNotFoundException>(() => instance.GetNumber("Battery"));
        }

        [Test]
        public void Attributes_CanBeChanged_ForExampleAfterARepair()
        {
            ProductInstance instance = GddExample();

            instance.Attributes["battery"] = AttributeValue.FromNumber(100);

            Assert.AreEqual(100L, instance.GetNumber("battery"));
        }

        // ---------- Tanım ve örnek KESİN ayrı ----------

        [Test]
        public void Instance_DoesNotHoldADefinition_OnlyItsId()
        {
            PropertyInfo[] properties = typeof(ProductInstance).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            Assert.IsFalse(properties.Any(p => p.PropertyType == typeof(ProductDefinition)), "Örnek, tanım nesnesi tutmamalı");
            Assert.IsFalse(properties.Any(p => p.PropertyType == typeof(StorageOption)));
            Assert.IsTrue(properties.Any(p => p.Name == nameof(ProductInstance.DefinitionId) && p.PropertyType == typeof(string)));
        }

        [Test]
        public void Instance_DoesNotDuplicateDefinitionData()
        {
            string[] definitionFields = { "BasePrice", "Name", "Brand", "Segment", "ReleaseYear", "StorageOptions", "IconKey", "Sector" };
            PropertyInfo[] properties = typeof(ProductInstance).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (string field in definitionFields)
            {
                Assert.IsFalse(properties.Any(p => p.Name == field), "Örnekte tanım alanı olmamalı: " + field);
            }
        }

        [Test]
        public void Definition_DoesNotKnowAboutInstances()
        {
            PropertyInfo[] properties = typeof(ProductDefinition).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            Assert.IsFalse(properties.Any(p => p.PropertyType == typeof(ProductInstance)));
            Assert.IsFalse(properties.Any(p => p.Name == "InstanceId"));
        }
    }

    public class AttributeValueTests
    {
        [Test]
        public void Number_Text_Flag_RoundTrip()
        {
            Assert.AreEqual(AttributeKind.Number, AttributeValue.FromNumber(78).Kind);
            Assert.AreEqual(78L, AttributeValue.FromNumber(78).Number);
            Assert.AreEqual(AttributeKind.Text, AttributeValue.FromText("ok").Kind);
            Assert.AreEqual("ok", AttributeValue.FromText("ok").Text);
            Assert.AreEqual(AttributeKind.Flag, AttributeValue.FromFlag(true).Kind);
            Assert.IsTrue(AttributeValue.FromFlag(true).Flag);
        }

        [Test]
        public void ReadingWrongKind_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => { long _ = AttributeValue.FromText("x").Number; });
            Assert.Throws<InvalidOperationException>(() => { string _ = AttributeValue.FromNumber(1).Text; });
            Assert.Throws<InvalidOperationException>(() => { bool _ = AttributeValue.FromNumber(1).Flag; });
        }

        [Test]
        public void FromText_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => AttributeValue.FromText(null));
        }

        [Test]
        public void Equality_ComparesKindAndValue()
        {
            Assert.AreEqual(AttributeValue.FromNumber(5), AttributeValue.FromNumber(5));
            Assert.AreNotEqual(AttributeValue.FromNumber(5), AttributeValue.FromNumber(6));
            Assert.AreNotEqual(AttributeValue.FromNumber(0), AttributeValue.FromFlag(false), "Farklı tür eşit sayılmamalı");
            Assert.AreNotEqual(AttributeValue.FromText("a"), AttributeValue.FromText("A"));
            Assert.IsTrue(AttributeValue.FromText("a") == AttributeValue.FromText("a"));
            Assert.IsTrue(AttributeValue.FromFlag(true) != AttributeValue.FromFlag(false));
            Assert.AreEqual(AttributeValue.FromText("a").GetHashCode(), AttributeValue.FromText("a").GetHashCode());
        }

        [Test]
        public void ToString_IsCultureIndependent()
        {
            Assert.AreEqual("1234567", AttributeValue.FromNumber(1234567).ToString());
            Assert.AreEqual("ok", AttributeValue.FromText("ok").ToString());
            Assert.AreEqual("true", AttributeValue.FromFlag(true).ToString());
            Assert.AreEqual("false", AttributeValue.FromFlag(false).ToString());
        }
    }
}
