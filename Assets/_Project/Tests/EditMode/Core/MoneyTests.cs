using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Esnaf.Core;
using NUnit.Framework;

namespace Esnaf.Tests.Core
{
    /// <summary>
    /// NOT: Buradaki senaryolar yalnızca Money aritmetiğini doğrular.
    /// Ekonomi DENGE testi değildir (GDD v0.3 karar 21).
    /// </summary>
    public class MoneyTests
    {
        private static Money Tl(long value)
        {
            return Money.FromTl(value);
        }

        // ---------- Aritmetik senaryolar (GDD 9.3) ----------

        [Test]
        public void Arithmetic_BuyThenSell_250000_Minus27000_Plus35000()
        {
            Money start = Tl(250000);
            Money purchase = Tl(27000);
            Money sale = Tl(35000);

            Money afterBuy = start - purchase;
            Assert.AreEqual(Tl(223000), afterBuy, "Alıştan sonra kalan");

            Money afterSell = afterBuy + sale;
            Assert.AreEqual(Tl(258000), afterSell, "Satıştan sonra son bakiye");

            Money net = afterSell - start;
            Assert.AreEqual(Tl(8000), net, "Net kâr");
            Assert.IsTrue(net.IsPositive);
        }

        [Test]
        public void Arithmetic_AppraisalCapitalizedIntoCostBasis()
        {
            Money start = Tl(250000);
            Money cash = start - Tl(27000) - Tl(1000);
            Money costBasis = Tl(27000) + Tl(1000);

            Assert.AreEqual(Tl(222000), cash);
            Assert.AreEqual(Tl(28000), costBasis);

            cash += Tl(35000);
            Assert.AreEqual(Tl(257000), cash);
            Assert.AreEqual(Tl(7000), Tl(35000) - costBasis);
        }

        [Test]
        public void Arithmetic_WastedAppraisalAndDailyExpense()
        {
            Money cash = Tl(250000) - Tl(300);
            Assert.AreEqual(Tl(249700), cash);

            Money dayThree = Tl(250000) - Tl(500);
            Assert.AreEqual(Tl(249500), dayThree);
        }

        [Test]
        public void Arithmetic_LossIsNegative()
        {
            Money net = Tl(22170) - Tl(27000);
            Assert.AreEqual(Tl(-4830), net);
            Assert.IsTrue(net.IsNegative);
            Assert.AreEqual(Tl(4830), net.Abs());
            Assert.AreEqual(-1, net.Sign);
        }

        [Test]
        public void Zero_And_Sign()
        {
            Assert.IsTrue(Money.Zero.IsZero);
            Assert.AreEqual(0, Money.Zero.Sign);
            Assert.AreEqual(Money.Zero, default(Money));
            Assert.AreEqual(1, Tl(1).Sign);
        }

        [Test]
        public void UnaryMinus_Negates()
        {
            Assert.AreEqual(Tl(-5), -Tl(5));
            Assert.AreEqual(Tl(5), -Tl(-5));
        }

        // ---------- 10 TL yuvarlama ----------

        [TestCase(0L, 0L)]
        [TestCase(4L, 0L)]
        [TestCase(5L, 10L)]
        [TestCase(9L, 10L)]
        [TestCase(10L, 10L)]
        [TestCase(26115L, 26120L)]
        [TestCase(26114L, 26110L)]
        [TestCase(23651L, 23650L)]
        [TestCase(23649L, 23650L)]
        [TestCase(28817L, 28820L)]
        [TestCase(-4L, 0L)]
        [TestCase(-5L, -10L)]
        [TestCase(-26115L, -26120L)]
        [TestCase(-26114L, -26110L)]
        public void RoundTo10_HalfAwayFromZero(long input, long expected)
        {
            Assert.AreEqual(expected, Money.RoundTo10(input));
            Assert.AreEqual(Tl(expected), Money.FromTlRoundedTo10(input));
            Assert.AreEqual(Tl(expected), Tl(input).RoundedTo10());
            Assert.IsTrue(Tl(expected).IsRoundedTo10);
        }

        [Test]
        public void IsRoundedTo10_DetectsUnrounded()
        {
            Assert.IsFalse(Tl(26115).IsRoundedTo10);
            Assert.IsTrue(Tl(26120).IsRoundedTo10);
            Assert.IsTrue(Tl(-26120).IsRoundedTo10);
        }

        // ---------- double sınırı (tek temas noktası) ----------

        [TestCase(26115.0, 26120L)]
        [TestCase(23651.3, 23650L)]
        [TestCase(28817.82, 28820L)]
        [TestCase(-26115.0, -26120L)]
        [TestCase(0.0, 0L)]
        [TestCase(4.9, 0L)]
        [TestCase(5.0, 10L)]
        public void FromDoubleRoundedTo10_Rounds(double input, long expected)
        {
            Assert.AreEqual(Tl(expected), Money.FromDoubleRoundedTo10(input));
        }

        [Test]
        public void FromDoubleRoundedTo10_RejectsNaNAndInfinity()
        {
            Assert.Throws<ArgumentException>(() => Money.FromDoubleRoundedTo10(double.NaN));
            Assert.Throws<ArgumentException>(() => Money.FromDoubleRoundedTo10(double.PositiveInfinity));
            Assert.Throws<ArgumentException>(() => Money.FromDoubleRoundedTo10(double.NegativeInfinity));
        }

        [Test]
        public void FromDoubleRoundedTo10_RejectsOutOfRange()
        {
            Assert.Throws<OverflowException>(() => Money.FromDoubleRoundedTo10(1e300));
            Assert.Throws<OverflowException>(() => Money.FromDoubleRoundedTo10(-1e19));
            Assert.Throws<OverflowException>(() => Money.FromDoubleRoundedTo10(9007199254740994.0));
        }

        [Test]
        public void Money_HasNoImplicitOrExplicitConversions()
        {
            // Yanlışlıkla double/long ile karışmasın: dönüşüm operatörü olmamalı.
            MethodInfo[] methods = typeof(Money).GetMethods(BindingFlags.Public | BindingFlags.Static);
            Assert.IsFalse(methods.Any(m => m.Name == "op_Implicit" || m.Name == "op_Explicit"));
        }

        [Test]
        public void Money_HasNoDoubleOrFloatArithmeticOperators()
        {
            MethodInfo[] operators = typeof(Money)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name.StartsWith("op_", StringComparison.Ordinal))
                .ToArray();

            foreach (MethodInfo op in operators)
            {
                foreach (ParameterInfo p in op.GetParameters())
                {
                    Assert.IsTrue(p.ParameterType == typeof(Money), op.Name + " yalnızca Money almalı");
                }
            }
        }

        // ---------- Taşma / kontrollü hata ----------

        [Test]
        public void Add_Overflow_Throws()
        {
            Assert.Throws<OverflowException>(() => { Money _ = Tl(long.MaxValue) + Tl(1); });
            Assert.Throws<OverflowException>(() => { Money _ = Tl(long.MinValue) + Tl(-1); });
        }

        [Test]
        public void Subtract_Overflow_Throws()
        {
            Assert.Throws<OverflowException>(() => { Money _ = Tl(long.MinValue) - Tl(1); });
            Assert.Throws<OverflowException>(() => { Money _ = Tl(long.MaxValue) - Tl(-1); });
        }

        [Test]
        public void Negate_And_Abs_MinValue_Throw()
        {
            Assert.Throws<OverflowException>(() => { Money _ = -Tl(long.MinValue); });
            Assert.Throws<OverflowException>(() => { Money _ = Tl(long.MinValue).Abs(); });
        }

        [Test]
        public void RoundTo10_NearLimits()
        {
            Assert.Throws<OverflowException>(() => Money.RoundTo10(long.MaxValue));      // ...807 -> ...810 taşar
            Assert.Throws<OverflowException>(() => Money.RoundTo10(long.MinValue));      // ...808 -> ...810 taşar
            Assert.AreEqual(long.MaxValue - 7, Money.RoundTo10(long.MaxValue - 3));      // ...804 -> ...800
        }

        [Test]
        public void TryAdd_TrySubtract_ReportOverflowWithoutThrowing()
        {
            Money result;

            Assert.IsFalse(Money.TryAdd(Tl(long.MaxValue), Tl(1), out result));
            Assert.AreEqual(Money.Zero, result);

            Assert.IsTrue(Money.TryAdd(Tl(250000), Tl(-27000), out result));
            Assert.AreEqual(Tl(223000), result);

            Assert.IsFalse(Money.TrySubtract(Tl(long.MinValue), Tl(1), out result));
            Assert.AreEqual(Money.Zero, result);

            Assert.IsTrue(Money.TrySubtract(Tl(258000), Tl(250000), out result));
            Assert.AreEqual(Tl(8000), result);
        }

        // ---------- Karşılaştırma ----------

        [Test]
        public void Equality_Hash_And_Comparison()
        {
            Assert.IsTrue(Tl(100) == Tl(100));
            Assert.IsTrue(Tl(100) != Tl(110));
            Assert.IsTrue(Tl(100) < Tl(110));
            Assert.IsTrue(Tl(110) > Tl(100));
            Assert.IsTrue(Tl(100) <= Tl(100));
            Assert.IsTrue(Tl(100) >= Tl(100));
            Assert.AreEqual(Tl(100).GetHashCode(), Tl(100).GetHashCode());
            Assert.IsTrue(Tl(100).Equals((object)Tl(100)));
            Assert.IsFalse(Tl(100).Equals((object)100L));
            Assert.AreEqual(-1, Math.Sign(Tl(1).CompareTo(Tl(2))));
        }

        [Test]
        public void MinMax()
        {
            Assert.AreEqual(Tl(5), Money.Min(Tl(5), Tl(9)));
            Assert.AreEqual(Tl(9), Money.Max(Tl(5), Tl(9)));
        }

        [Test]
        public void Sorting_Works()
        {
            Money[] values = { Tl(30), Tl(-5), Tl(10) };
            Array.Sort(values);
            Assert.AreEqual(new[] { Tl(-5), Tl(10), Tl(30) }, values);
        }

        // ---------- Gösterim ----------

        [TestCase(0L, "0 TL")]
        [TestCase(999L, "999 TL")]
        [TestCase(1000L, "1.000 TL")]
        [TestCase(27000L, "27.000 TL")]
        [TestCase(250000L, "250.000 TL")]
        [TestCase(1234567L, "1.234.567 TL")]
        [TestCase(-27000L, "-27.000 TL")]
        [TestCase(-1234567L, "-1.234.567 TL")]
        [TestCase(-5L, "-5 TL")]
        public void ToString_UsesDotGrouping(long value, string expected)
        {
            Assert.AreEqual(expected, Tl(value).ToString());
        }

        [Test]
        public void ToString_IsCultureIndependent()
        {
            CultureInfo original = CultureInfo.CurrentCulture;
            try
            {
                var custom = (CultureInfo)CultureInfo.InvariantCulture.Clone();
                custom.NumberFormat.NumberGroupSeparator = ",";
                custom.NumberFormat.NegativeSign = "~";
                CultureInfo.CurrentCulture = custom;

                Assert.AreEqual("-1.234.567 TL", Tl(-1234567).ToString());
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
        }

        [Test]
        public void ToString_MinAndMaxValue_DoNotThrow()
        {
            Assert.AreEqual("9.223.372.036.854.775.807 TL", Tl(long.MaxValue).ToString());
            Assert.AreEqual("-9.223.372.036.854.775.808 TL", Tl(long.MinValue).ToString());
        }
    }
}
