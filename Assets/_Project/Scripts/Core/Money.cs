using System;

namespace Esnaf.Core
{
    /// <summary>
    /// Oyunun para tipi: tam sayı TL (kuruş yok). Toplama/çıkarma yalnızca long ile yapılır,
    /// taşma durumunda <see cref="OverflowException"/> fırlatılır (sessiz taşma yok).
    /// Fiyatlar 10 TL'ye yuvarlanır (yarım değerler sıfırdan uzağa: 26.115 -> 26.120).
    /// double ile tek temas noktası: <see cref="FromDoubleRoundedTo10"/> (çarpan hesabı sonucu -> para).
    /// </summary>
    public readonly struct Money : IEquatable<Money>, IComparable<Money>
    {
        public const long RoundingStep = 10;

        // 2^53: double'ın tam sayıları kayıpsız gösterebildiği son sınır.
        private const double MaxExactDouble = 9007199254740992.0;

        public static readonly Money Zero = new Money(0L);

        public long Tl { get; }

        private Money(long tl)
        {
            Tl = tl;
        }

        // ---- Oluşturma ----

        public static Money FromTl(long tl)
        {
            return new Money(tl);
        }

        public static Money FromTlRoundedTo10(long tl)
        {
            return new Money(RoundTo10(tl));
        }

        /// <summary>
        /// Çarpanlı (double) bir hesabın sonucunu paraya çevirir ve 10 TL'ye yuvarlar.
        /// NaN/Sonsuz için <see cref="ArgumentException"/>, ±2^53 dışı için <see cref="OverflowException"/>.
        /// </summary>
        public static Money FromDoubleRoundedTo10(double tl)
        {
            if (double.IsNaN(tl) || double.IsInfinity(tl))
            {
                throw new ArgumentException("Money cannot be created from NaN or Infinity.", nameof(tl));
            }
            if (Math.Abs(tl) > MaxExactDouble)
            {
                throw new OverflowException("Value is outside the exactly representable range.");
            }

            double tens = Math.Round(tl / 10.0, MidpointRounding.AwayFromZero);
            return new Money(checked((long)tens * RoundingStep));
        }

        // ---- Yuvarlama ----

        /// <summary>En yakın 10 TL'ye yuvarlar; tam ortada sıfırdan uzağa gider. Taşarsa OverflowException.</summary>
        public static long RoundTo10(long value)
        {
            long remainder = value % RoundingStep; // işareti value ile aynı, -9..9
            if (remainder == 0)
            {
                return value;
            }

            long towardZero = value - remainder;
            long distance = remainder < 0 ? -remainder : remainder;
            if (distance < 5)
            {
                return towardZero;
            }

            return checked(towardZero + (value > 0 ? RoundingStep : -RoundingStep));
        }

        public Money RoundedTo10()
        {
            return new Money(RoundTo10(Tl));
        }

        public bool IsRoundedTo10
        {
            get { return Tl % RoundingStep == 0; }
        }

        // ---- Durum ----

        public bool IsZero
        {
            get { return Tl == 0; }
        }

        public bool IsNegative
        {
            get { return Tl < 0; }
        }

        public bool IsPositive
        {
            get { return Tl > 0; }
        }

        public int Sign
        {
            get { return Tl > 0 ? 1 : (Tl < 0 ? -1 : 0); }
        }

        // ---- Aritmetik (taşma kontrollü) ----

        public static Money operator +(Money a, Money b)
        {
            return new Money(checked(a.Tl + b.Tl));
        }

        public static Money operator -(Money a, Money b)
        {
            return new Money(checked(a.Tl - b.Tl));
        }

        public static Money operator -(Money a)
        {
            return new Money(checked(-a.Tl));
        }

        public Money Abs()
        {
            return Tl < 0 ? new Money(checked(-Tl)) : this;
        }

        public static bool TryAdd(Money a, Money b, out Money result)
        {
            long sum;
            try
            {
                sum = checked(a.Tl + b.Tl);
            }
            catch (OverflowException)
            {
                result = Zero;
                return false;
            }

            result = new Money(sum);
            return true;
        }

        public static bool TrySubtract(Money a, Money b, out Money result)
        {
            long difference;
            try
            {
                difference = checked(a.Tl - b.Tl);
            }
            catch (OverflowException)
            {
                result = Zero;
                return false;
            }

            result = new Money(difference);
            return true;
        }

        public static Money Min(Money a, Money b)
        {
            return a.Tl <= b.Tl ? a : b;
        }

        public static Money Max(Money a, Money b)
        {
            return a.Tl >= b.Tl ? a : b;
        }

        // ---- Karşılaştırma ----

        public static bool operator ==(Money a, Money b)
        {
            return a.Tl == b.Tl;
        }

        public static bool operator !=(Money a, Money b)
        {
            return a.Tl != b.Tl;
        }

        public static bool operator <(Money a, Money b)
        {
            return a.Tl < b.Tl;
        }

        public static bool operator >(Money a, Money b)
        {
            return a.Tl > b.Tl;
        }

        public static bool operator <=(Money a, Money b)
        {
            return a.Tl <= b.Tl;
        }

        public static bool operator >=(Money a, Money b)
        {
            return a.Tl >= b.Tl;
        }

        public bool Equals(Money other)
        {
            return Tl == other.Tl;
        }

        public override bool Equals(object obj)
        {
            return obj is Money other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Tl.GetHashCode();
        }

        public int CompareTo(Money other)
        {
            return Tl.CompareTo(other.Tl);
        }

        // ---- Gösterim ----

        /// <summary>"27.000 TL", "-1.234.567 TL". Sistem kültüründen bağımsızdır.</summary>
        public override string ToString()
        {
            // long.MinValue için de güvenli büyüklük hesabı.
            ulong magnitude = Tl < 0 ? (ulong)(-(Tl + 1)) + 1UL : (ulong)Tl;
            string digits = magnitude.ToString(System.Globalization.CultureInfo.InvariantCulture);

            var builder = new System.Text.StringBuilder(digits.Length + digits.Length / 3 + 5);
            if (Tl < 0)
            {
                builder.Append('-');
            }

            int firstGroup = digits.Length % 3;
            if (firstGroup == 0)
            {
                firstGroup = 3;
            }

            builder.Append(digits, 0, firstGroup);
            for (int i = firstGroup; i < digits.Length; i += 3)
            {
                builder.Append('.');
                builder.Append(digits, i, 3);
            }

            builder.Append(" TL");
            return builder.ToString();
        }
    }
}
