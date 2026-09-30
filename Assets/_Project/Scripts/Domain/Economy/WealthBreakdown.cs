using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Esnaf.Core;

namespace Esnaf.Domain.Economy
{
    public sealed class WealthLine
    {
        public string Key { get; }
        public Money Amount { get; }

        public WealthLine(string key, Money amount)
        {
            Key = key;
            Amount = amount;
        }
    }

    /// <summary>Toplam servet ve kalem kalem dökümü (değişmez).</summary>
    public sealed class WealthBreakdown
    {
        public IReadOnlyList<WealthLine> Lines { get; }
        public Money Total { get; }

        public WealthBreakdown(IEnumerable<WealthLine> lines)
        {
            if (lines == null)
            {
                throw new ArgumentNullException(nameof(lines));
            }

            var list = new List<WealthLine>(lines);
            Money total = Money.Zero;
            foreach (WealthLine line in list)
            {
                total += line.Amount;
            }

            Lines = new ReadOnlyCollection<WealthLine>(list);
            Total = total;
        }

        /// <summary>Anahtarı olmayan kalem için 0.</summary>
        public Money GetAmount(string key)
        {
            foreach (WealthLine line in Lines)
            {
                if (string.Equals(line.Key, key, StringComparison.Ordinal))
                {
                    return line.Amount;
                }
            }

            return Money.Zero;
        }
    }
}
