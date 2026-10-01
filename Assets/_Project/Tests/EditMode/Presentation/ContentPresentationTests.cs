using System;
using System.Linq;
using Esnaf.Core;
using Esnaf.Presentation;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Presentation
{
    public class ContentPresentationTests
    {
        private static ContentPresentation Real()
        {
            return new ContentPresentation(MarketHarness.RealContent());
        }

        [Test]
        public void ModelName_ComesFromTheContentFile_AndFallsBackToTheId()
        {
            Assert.AreEqual("Nova N1 Lite", Real().ModelName("phone.nova_n1_lite"));
            Assert.AreEqual("Yıldız Y5", Real().ModelName("phone.yildiz_y5"));
            Assert.AreEqual("phone.unknown", Real().ModelName("phone.unknown"));
            Assert.AreEqual(string.Empty, Real().ModelName(null));
        }

        [Test]
        public void NpcName_ComesFromTheContentFile_AndFallsBackToTheId()
        {
            Assert.AreEqual("Kemal Abi", Real().NpcName("npc.kemal"));
            Assert.AreEqual("Selin", Real().NpcName("npc.selin"));
            Assert.AreEqual("npc.nobody", Real().NpcName("npc.nobody"));
            Assert.AreEqual(string.Empty, Real().NpcName(null));
        }

        [Test]
        public void ShelfCapacity_IsTheContentValue()
        {
            Assert.AreEqual(6, Real().ShelfCapacity);
        }

        [Test]
        public void AppraisalLevels_ListTheContentLevelsInOrder()
        {
            var levels = Real().AppraisalLevels;

            CollectionAssert.AreEqual(new[] { "s0", "s1", "s2", "s3" }, levels.Select(l => l.Id).ToArray());
            CollectionAssert.AreEqual(new[] { "Göz muayenesi", "Temel kontrol", "Ayrıntılı kontrol", "Profesyonel ekspertiz" }, levels.Select(l => l.Name).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 3, 5, 6 }, levels.Select(l => l.UnlockDay).ToArray());
            CollectionAssert.AreEqual(new[] { false, false, false, true }, levels.Select(l => l.RequiresEquipment).ToArray());
        }

        [TestCase("s0", "phone.nova_n1_lite", 0L)]
        [TestCase("s1", "phone.nova_n1_lite", 100L)]
        [TestCase("s1", "phone.samsun_vega_a3", 200L)]
        [TestCase("s1", "phone.samsun_vega_s21", 300L)]
        [TestCase("s2", "phone.nova_n1_lite", 350L)]
        [TestCase("s3", "phone.samsun_vega_s21", 2000L)]
        public void TryGetFee_UsesTheSegmentOfTheProduct(string levelId, string definitionId, long expected)
        {
            Money fee;

            Assert.IsTrue(Real().TryGetFee(levelId, definitionId, out fee));
            Assert.AreEqual(Money.FromTl(expected), fee);
        }

        [TestCase("s9", "phone.nova_n1_lite")]
        [TestCase("s1", "phone.unknown")]
        [TestCase(null, "phone.nova_n1_lite")]
        [TestCase("s1", null)]
        public void TryGetFee_UnknownLevelOrProduct_IsFalse(string levelId, string definitionId)
        {
            Money fee;

            Assert.IsFalse(Real().TryGetFee(levelId, definitionId, out fee));
            Assert.AreEqual(Money.Zero, fee);
        }

        [Test]
        public void TheConstructor_ChecksItsArgument()
        {
            Assert.Throws<ArgumentNullException>(() => new ContentPresentation(null));
        }
    }
}
