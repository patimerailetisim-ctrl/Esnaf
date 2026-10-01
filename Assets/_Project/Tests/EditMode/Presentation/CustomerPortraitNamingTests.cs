using System.Linq;
using Esnaf.Presentation;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Presentation
{
    public class CustomerPortraitNamingTests
    {
        [Test]
        public void TheTwentyEightExpectedPortraits_HaveDistinctKeys_NoneEmpty()
        {
            Assert.AreEqual(28, CustomerPortraitNaming.ExpectedNames.Count);
            string[] keys = CustomerPortraitNaming.ExpectedNames.Select(CustomerPortraitNaming.Key).ToArray();

            Assert.IsTrue(keys.All(k => k.Length > 0));
            Assert.AreEqual(28, keys.Distinct().Count(), "iki portre aynı ada çözülüyor");
        }

        [TestCase("Ahmet", "ahmet")]
        [TestCase("Ahmet.png", "ahmet")]
        [TestCase("ahmet", "ahmet")]
        [TestCase("Dr. Murat", "murat")]
        [TestCase("Kemal Abi", "kemal")]
        [TestCase("Hatice Teyze", "hatice")]
        [TestCase("Rıza Bey", "riza")]
        [TestCase("Ayşe Hanım", "ayse")]
        [TestCase("Oğuz", "oguz")]
        [TestCase("Oğuz.png", "oguz")]
        [TestCase("Yiğit", "yigit")]
        [TestCase("İrem", "irem")]
        [TestCase("IREM", "irem")]
        [TestCase("  Selin  ", "selin")]
        [TestCase("", "")]
        [TestCase(null, "")]
        [TestCase("Dr.", "")]
        public void TheKey_IgnoresCase_TurkishLetters_AndTitles(string input, string expected)
        {
            Assert.AreEqual(expected, CustomerPortraitNaming.Key(input));
        }

        [Test]
        public void TheKeyOfAPortraitFile_EqualsTheKeyOfTheCustomersName()
        {
            foreach (string name in CustomerPortraitNaming.ExpectedNames)
            {
                Assert.AreEqual(CustomerPortraitNaming.Key(name + ".png"), CustomerPortraitNaming.Key(name), name);
            }
        }
    }
}
