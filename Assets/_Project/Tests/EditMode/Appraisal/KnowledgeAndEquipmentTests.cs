using System;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Appraisal;
using NUnit.Framework;

namespace Esnaf.Tests.Appraisal
{
    public class KnowledgeAndEquipmentTests
    {
        private static AppraisalResult Result(long id, long instanceId, string level)
        {
            return new AppraisalResult(
                id, instanceId, "phone.x", level, 3, Money.FromTl(200), 99UL,
                new[] { new AttributeFinding("screen", "w", true, AppraisalConfidence.Low, 0.4, false) },
                new NumericRange(1, 2), new NumericRange(3, 4), new MoneyRange(Money.FromTl(100), Money.FromTl(200)), new TrumpCard[0]);
        }

        [Test]
        public void EmptyKnowledge_KnowsNothing()
        {
            var k = new KnowledgeState();
            AppraisalResult found;

            Assert.AreEqual(0, k.All.Count);
            Assert.IsFalse(k.TryGet(1, "s1", out found));
            Assert.IsFalse(k.TryGetById(1, out found));
            Assert.AreEqual(0, k.ForInstance(1).Count);
        }

        [Test]
        public void Add_StoresByInstanceAndLevel_AndById()
        {
            var k = new KnowledgeState();
            AppraisalResult a = Result(1, 10, "s1");
            AppraisalResult b = Result(2, 10, "s2");
            AppraisalResult c = Result(3, 11, "s1");
            k.Add(a);
            k.Add(b);
            k.Add(c);
            AppraisalResult found;

            Assert.IsTrue(k.TryGet(10, "s1", out found));
            Assert.AreSame(a, found);
            Assert.IsTrue(k.TryGet(10, "s2", out found));
            Assert.AreSame(b, found);
            Assert.IsTrue(k.TryGet(11, "s1", out found));
            Assert.AreSame(c, found);
            Assert.IsFalse(k.TryGet(11, "s2", out found));
            Assert.IsTrue(k.TryGetById(2, out found));
            Assert.AreSame(b, found);
            Assert.IsFalse(k.TryGetById(9, out found));
            CollectionAssert.AreEqual(new long[] { 1, 2, 3 }, k.All.Select(r => r.ResultId).ToArray());
            CollectionAssert.AreEqual(new long[] { 1, 2 }, k.ForInstance(10).Select(r => r.ResultId).ToArray());
            CollectionAssert.AreEqual(new long[] { 3 }, k.ForInstance(11).Select(r => r.ResultId).ToArray());
        }

        [Test]
        public void Add_SameInstanceAndLevelTwice_Throws_BecauseResultsAreLocked()
        {
            var k = new KnowledgeState();
            k.Add(Result(1, 10, "s1"));

            Assert.Throws<ArgumentException>(() => k.Add(Result(2, 10, "s1")));
            Assert.AreEqual(1, k.All.Count);
        }

        [Test]
        public void Add_DuplicateResultId_Throws()
        {
            var k = new KnowledgeState();
            k.Add(Result(1, 10, "s1"));

            Assert.Throws<ArgumentException>(() => k.Add(Result(1, 11, "s1")));
            Assert.Throws<ArgumentNullException>(() => k.Add(null));
        }

        [Test]
        public void Views_AreReadOnlyCopies()
        {
            var k = new KnowledgeState();
            k.Add(Result(1, 10, "s1"));

            Assert.IsFalse(k.All is System.Collections.Generic.List<AppraisalResult>);
            Assert.IsFalse(k.ForInstance(10) is System.Collections.Generic.List<AppraisalResult>);
            Assert.AreNotSame(k.ForInstance(10), k.ForInstance(10));
        }

        [Test]
        public void Equipment_GrantAndOwn()
        {
            var e = new EquipmentState();

            Assert.IsFalse(e.Owns("test_device"));
            e.Grant("test_device");
            e.Grant("test_device");

            Assert.IsTrue(e.Owns("test_device"));
            Assert.IsFalse(e.Owns("other"));
            Assert.IsFalse(e.Owns(null));
            CollectionAssert.AreEqual(new[] { "test_device" }, e.All.ToArray());
        }

        [Test]
        public void Equipment_AllIsSortedAndReadOnly()
        {
            var e = new EquipmentState();
            e.Grant("zeta");
            e.Grant("alpha");

            CollectionAssert.AreEqual(new[] { "alpha", "zeta" }, e.All.ToArray());
            Assert.IsFalse(e.All is System.Collections.Generic.List<string>);
            Assert.Throws<ArgumentException>(() => e.Grant(" "));
            Assert.Throws<ArgumentException>(() => e.Grant(null));
        }
    }
}
