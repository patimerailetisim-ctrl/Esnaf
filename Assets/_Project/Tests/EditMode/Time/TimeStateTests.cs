using System;
using Esnaf.Domain.Time;
using NUnit.Framework;

namespace Esnaf.Tests.Time
{
    public class TimeStateTests
    {
        [Test]
        public void NewState_KeepsDayAndSeed()
        {
            var time = new TimeState(1, 42UL);

            Assert.AreEqual(1, time.Day);
            Assert.AreEqual(42UL, time.MasterSeed);
        }

        [Test]
        public void Advance_IncrementsByOne_AndReturnsTheNewDay()
        {
            var time = new TimeState(3, 1UL);

            Assert.AreEqual(4, time.Advance());
            Assert.AreEqual(5, time.Advance());
            Assert.AreEqual(5, time.Day);
        }

        [Test]
        public void Constructor_RejectsDayBelowOne()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TimeState(0, 1UL));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TimeState(-2, 1UL));
            Assert.DoesNotThrow(() => new TimeState(1, 1UL));
        }

        [Test]
        public void Advance_AtIntMax_Throws()
        {
            var time = new TimeState(int.MaxValue, 1UL);

            Assert.Throws<OverflowException>(() => time.Advance());
            Assert.AreEqual(int.MaxValue, time.Day, "başarısız ilerleme günü değiştirmemeli");
        }

        [Test]
        public void DayEndContext_KeepsDay_AndStartsEmpty()
        {
            var ctx = new DayEndContext(4);

            Assert.AreEqual(4, ctx.Day);
            Assert.AreEqual(0, ctx.NewDay);
            Assert.AreEqual(Esnaf.Core.Money.Zero, ctx.ExpenseCharged);
            Assert.AreEqual(0, ctx.ExpiredListingIds.Count);
            Assert.AreEqual(0, ctx.NewListingIds.Count);
            Assert.AreEqual(0, ctx.ExecutedStepIds.Count);
        }

        [Test]
        public void DayEndContext_RejectsDayBelowOne()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DayEndContext(0));
        }

        [Test]
        public void DayEndOrder_MatchesTheFixedGddTable()
        {
            // GDD v0.3 3.4: 1 kaçan müşteriler · 2 günlük gider · 3 stok kaybı · 4 ilan ömrü · 5 talep · 6 XP/görev · 7 yeni gün · 8 otomatik kayıt
            Assert.AreEqual(1, DayEndOrder.MissedCustomers);
            Assert.AreEqual(2, DayEndOrder.DailyExpense);
            Assert.AreEqual(3, DayEndOrder.StockHoldingLoss);
            Assert.AreEqual(4, DayEndOrder.ListingExpiry);
            Assert.AreEqual(5, DayEndOrder.DemandUpdate);
            Assert.AreEqual(6, DayEndOrder.Progression);
            Assert.AreEqual(7, DayEndOrder.NewDay);
            Assert.AreEqual(8, DayEndOrder.AutoSave);
        }
    }
}
