using Esnaf.Core;
using Esnaf.Domain.Accessories;
using NUnit.Framework;
using System;

namespace Esnaf.Tests.Accessories
{
    /// <summary>Aksesuar stoğu: telefon rafından ayrı, birim kapasiteli, atomik stok defteri (para/satış/toptancı bu adımda yok).</summary>
    public class AccessoryStockTests
    {
        private static AccessoryStock Stock(int capacity = 60)
        {
            return new AccessoryStock(capacity);
        }

        [Test]
        public void ANewStock_IsEmpty_WithTheGivenCapacity()
        {
            AccessoryStock s = Stock();

            Assert.AreEqual(60, s.Capacity);
            Assert.AreEqual(0, s.TotalUnits);
            Assert.AreEqual(60, s.FreeUnits);
            Assert.AreEqual(0, s.Quantity("accessory.phone_case"));
            Assert.AreEqual(Money.Zero, s.TotalCost("accessory.phone_case"));
            Assert.AreEqual(0, s.AccessoryIds.Count);
        }

        [Test]
        public void TheCapacity_MustBePositive()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AccessoryStock(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AccessoryStock(-1));
        }

        [Test]
        public void Add_RaisesTheQuantityAndTheCost_AndUsesUnitCapacity()
        {
            AccessoryStock s = Stock();

            Assert.IsTrue(s.Add("accessory.phone_case", 20, Money.FromTl(1400)).IsSuccess);
            Assert.IsTrue(s.Add("accessory.phone_case", 10, Money.FromTl(800)).IsSuccess);
            Assert.IsTrue(s.Add("accessory.powerbank", 5, Money.FromTl(1750)).IsSuccess);

            Assert.AreEqual(30, s.Quantity("accessory.phone_case"));
            Assert.AreEqual(Money.FromTl(2200), s.TotalCost("accessory.phone_case"));
            Assert.AreEqual(35, s.TotalUnits);
            Assert.AreEqual(25, s.FreeUnits);
            CollectionAssert.AreEqual(new[] { "accessory.phone_case", "accessory.powerbank" }, s.AccessoryIds);
        }

        [Test]
        public void Add_BeyondTheCapacity_IsRefused_AndChangesNothing()
        {
            AccessoryStock s = Stock(60);
            s.Add("accessory.phone_case", 50, Money.FromTl(3500));

            var result = s.Add("accessory.charge_cable", 11, Money.FromTl(660));

            Assert.AreEqual("stock.full", result.ErrorCode);
            Assert.AreEqual(50, s.TotalUnits);
            Assert.AreEqual(0, s.Quantity("accessory.charge_cable"));
            Assert.IsTrue(s.Add("accessory.charge_cable", 10, Money.FromTl(600)).IsSuccess, "tam dolu olabilir");
            Assert.AreEqual(0, s.FreeUnits);
        }

        [TestCase(0)]
        [TestCase(-3)]
        public void Add_ANonPositiveQuantity_IsRefused(int quantity)
        {
            AccessoryStock s = Stock();

            Assert.AreEqual("stock.quantity_invalid", s.Add("accessory.phone_case", quantity, Money.FromTl(10)).ErrorCode);
            Assert.AreEqual(0, s.TotalUnits);
        }

        [Test]
        public void Add_ANegativeCost_IsRefused()
        {
            AccessoryStock s = Stock();

            Assert.AreEqual("stock.cost_invalid", s.Add("accessory.phone_case", 1, Money.FromTl(-10)).ErrorCode);
            Assert.AreEqual(0, s.TotalUnits);
        }

        [Test]
        public void Add_NeedsAnId()
        {
            Assert.Throws<ArgumentException>(() => Stock().Add(null, 1, Money.Zero));
            Assert.Throws<ArgumentException>(() => Stock().Add("", 1, Money.Zero));
        }

        [Test]
        public void Remove_ReturnsTheProportionalCostBasis_AndFreesUnits()
        {
            AccessoryStock s = Stock();
            s.Add("accessory.phone_case", 20, Money.FromTl(1400));

            var removed = s.Remove("accessory.phone_case", 5);

            Assert.IsTrue(removed.IsSuccess);
            Assert.AreEqual(Money.FromTl(350), removed.Value);
            Assert.AreEqual(15, s.Quantity("accessory.phone_case"));
            Assert.AreEqual(Money.FromTl(1050), s.TotalCost("accessory.phone_case"));
            Assert.AreEqual(15, s.TotalUnits);
        }

        [Test]
        public void Remove_TheLastUnits_TakesTheWholeRemainingCost_SoNoRoundingResidueStays()
        {
            AccessoryStock s = Stock();
            s.Add("accessory.x", 3, Money.FromTl(100));

            Money first = s.Remove("accessory.x", 1).Value;
            Money second = s.Remove("accessory.x", 1).Value;
            Money third = s.Remove("accessory.x", 1).Value;

            Assert.AreEqual(100, first.Tl + second.Tl + third.Tl);
            Assert.AreEqual(0, s.TotalUnits);
            Assert.AreEqual(Money.Zero, s.TotalCost("accessory.x"));
            Assert.AreEqual(0, s.AccessoryIds.Count, "boşalan kalem listeden düşer");
        }

        [Test]
        public void Remove_MoreThanInStock_IsRefused_AndChangesNothing()
        {
            AccessoryStock s = Stock();
            s.Add("accessory.phone_case", 2, Money.FromTl(140));

            Assert.AreEqual("stock.insufficient", s.Remove("accessory.phone_case", 3).ErrorCode);
            Assert.AreEqual("stock.insufficient", s.Remove("accessory.unknown", 1).ErrorCode);
            Assert.AreEqual("stock.insufficient", s.Remove(null, 1).ErrorCode);
            Assert.AreEqual(2, s.Quantity("accessory.phone_case"));
            Assert.AreEqual(Money.FromTl(140), s.TotalCost("accessory.phone_case"));
        }

        [Test]
        public void Remove_ANonPositiveQuantity_IsRefused()
        {
            AccessoryStock s = Stock();
            s.Add("accessory.phone_case", 2, Money.FromTl(140));

            Assert.AreEqual("stock.quantity_invalid", s.Remove("accessory.phone_case", 0).ErrorCode);
            Assert.AreEqual(2, s.TotalUnits);
        }

        [Test]
        public void TheStock_IsIndependentOfThePhoneShelf()
        {
            var game = Esnaf.Domain.Game.GameSession.NewGame(Esnaf.Tests.Support.MarketHarness.RealContent(), 1UL);
            AccessoryStock s = new AccessoryStock(game.Content.Accessories.ShelfCapacityUnits);
            int phoneShelf = game.Api.GetInventory().Count;

            s.Add("accessory.phone_case", 60, Money.FromTl(4200));

            Assert.AreEqual(phoneShelf, game.Api.GetInventory().Count, "aksesuar telefon rafını tüketmez");
            Assert.AreEqual(60, s.TotalUnits);
        }
    }
}
