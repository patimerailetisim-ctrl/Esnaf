using System;

namespace Esnaf.Core
{
    /// <summary>
    /// Ürün örneği gibi çalışma zamanı nesneleri için artan tamsayı kimlikleri (1, 2, 3...).
    /// Sayaç kayda girer; yüklenince kaldığı yerden devam eder.
    /// </summary>
    public sealed class IdGenerator
    {
        public long LastIssued { get; private set; }

        public IdGenerator(long lastIssued = 0)
        {
            if (lastIssued < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(lastIssued), "lastIssued cannot be negative.");
            }

            LastIssued = lastIssued;
        }

        /// <summary>Sıradaki kimlik. long taşarsa OverflowException.</summary>
        public long Next()
        {
            LastIssued = checked(LastIssued + 1);
            return LastIssued;
        }

        public void Restore(long lastIssued)
        {
            if (lastIssued < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(lastIssued), "lastIssued cannot be negative.");
            }

            LastIssued = lastIssued;
        }
    }
}
