using System.Globalization;
using System.Threading;
using Esnaf.Core;
using Esnaf.Presentation;
using NUnit.Framework;

namespace Esnaf.Tests.Presentation
{
    public class MoneyFormatterTests
    {
        [TestCase(0L, "0 ₺")]
        [TestCase(5L, "5 ₺")]
        [TestCase(10L, "10 ₺")]
        [TestCase(99L, "99 ₺")]
        [TestCase(100L, "100 ₺")]
        [TestCase(999L, "999 ₺")]
        [TestCase(1000L, "1.000 ₺")]
        [TestCase(1010L, "1.010 ₺")]
        [TestCase(9999L, "9.999 ₺")]
        [TestCase(12500L, "12.500 ₺")]
        [TestCase(100000L, "100.000 ₺")]
        [TestCase(250000L, "250.000 ₺")]
        [TestCase(1000000L, "1.000.000 ₺")]
        [TestCase(1234567L, "1.234.567 ₺")]
        [TestCase(-10L, "-10 ₺")]
        [TestCase(-999L, "-999 ₺")]
        [TestCase(-1000L, "-1.000 ₺")]
        [TestCase(-1500L, "-1.500 ₺")]
        [TestCase(-1234567L, "-1.234.567 ₺")]
        [TestCase(long.MaxValue, "9.223.372.036.854.775.807 ₺")]
        [TestCase(long.MinValue, "-9.223.372.036.854.775.808 ₺")]
        public void Format_UsesTurkishGrouping_AndTheLiraSign(long tl, string expected)
        {
            Assert.AreEqual(expected, MoneyFormatter.Format(Money.FromTl(tl)));
        }

        [Test]
        public void Format_DoesNotDependOnTheCurrentCulture()
        {
            CultureInfo saved = Thread.CurrentThread.CurrentCulture;
            try
            {
                foreach (string name in new[] { "tr-TR", "en-US", "de-DE", "fr-FR" })
                {
                    Thread.CurrentThread.CurrentCulture = new CultureInfo(name);
                    Assert.AreEqual("-1.234.567 ₺", MoneyFormatter.Format(Money.FromTl(-1234567L)), name);
                }
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = saved;
            }
        }

        [Test]
        public void TheLiraSign_IsTheTurkishLiraCodePoint()
        {
            Assert.AreEqual('₺', MoneyFormatter.Symbol);
        }
    }
}
