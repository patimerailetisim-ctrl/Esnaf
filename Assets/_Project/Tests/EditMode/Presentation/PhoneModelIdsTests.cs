using Esnaf.Presentation;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Presentation
{
    public class PhoneModelIdsTests
    {
        [Test]
        public void TheElmaE13ProId_ExistsInTheRealContent_AndIsNamedLikeTheAssets()
        {
            var content = MarketHarness.RealContent();

            Assert.AreEqual("phone.elma_e13_pro", PhoneModelIds.ElmaE13Pro);
            Assert.AreEqual("Elma E13 Pro", new ContentPresentation(content).ModelName(PhoneModelIds.ElmaE13Pro));
        }
    }
}
