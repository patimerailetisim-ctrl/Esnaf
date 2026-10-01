using System.Linq;
using Esnaf.Presentation;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Presentation
{
    public class PhoneAssetNamingTests
    {
        private static readonly string[] FileBases =
        {
            "NovaN1Lite", "YildizY5", "SamsunVegaA3", "NovaN3Pro", "ZirveZ5", "YildizY8Plus", "ElmaE11", "SamsunVegaS21", "ElmaE13Pro", "ElmaE14ProMax"
        };

        [Test]
        public void TheTenModelIds_AreExactlyThePhonesOfTheRealContent()
        {
            var content = MarketHarness.RealContent();

            CollectionAssert.AreEquivalent(
                PhoneModelIds.All.ToArray(),
                content.Products.Where(p => p.Id.StartsWith("phone.")).Select(p => p.Id).ToArray());
            Assert.AreEqual(10, PhoneModelIds.All.Count);
        }

        [Test]
        public void EveryModelId_HasADistinctKey_ThatItsFileBaseNameProduces()
        {
            Assert.AreEqual(FileBases.Length, PhoneModelIds.All.Count);
            for (int i = 0; i < FileBases.Length; i++)
            {
                string key;
                PhoneAssetNaming.Parse(FileBases[i] + "_Front.png", "ignored", out key);
                Assert.AreEqual(PhoneAssetNaming.ModelKey(PhoneModelIds.All[i]), key, PhoneModelIds.All[i]);
            }

            Assert.AreEqual(10, PhoneModelIds.All.Select(PhoneAssetNaming.ModelKey).Distinct().Count());
        }

        [TestCase("NovaN1Lite_Front.png", PhoneAssetKind.Front)]
        [TestCase("NovaN1Lite_Back.png", PhoneAssetKind.Back)]
        [TestCase("NovaN1Lite_Side.png", PhoneAssetKind.Side)]
        [TestCase("NovaN1Lite_Camera.png", PhoneAssetKind.Camera)]
        [TestCase("NovaN1Lite_AllViews.png", PhoneAssetKind.AllViews)]
        [TestCase("NovaN1Lite_Top.png", PhoneAssetKind.Unknown)]
        [TestCase("", PhoneAssetKind.Unknown)]
        public void TheSuffix_DecidesTheKind_AndAllViewsIsNeverAnAngle(string file, PhoneAssetKind expected)
        {
            string key;

            Assert.AreEqual(expected, PhoneAssetNaming.Parse(file, "x", out key));
        }

        [Test]
        public void AFileWithoutAModelPrefix_UsesItsFolderName()
        {
            string key;

            Assert.AreEqual(PhoneAssetKind.Back, PhoneAssetNaming.Parse("Back.png", "Elma_E13_Pro", out key));
            Assert.AreEqual(PhoneAssetNaming.ModelKey("phone.elma_e13_pro"), key);
        }

        [Test]
        public void TheModelKey_IgnoresCaseUnderscoresAndTheKindPrefix()
        {
            Assert.AreEqual("novan1lite", PhoneAssetNaming.ModelKey("phone.nova_n1_lite"));
            Assert.AreEqual("elmae13pro", PhoneAssetNaming.ModelKey("phone.elma_e13_pro"));
            Assert.AreEqual(string.Empty, PhoneAssetNaming.ModelKey(null));
        }
    }
}
