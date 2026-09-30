using Esnaf.Core;

namespace Esnaf.Domain.Economy
{
    /// <summary>Bildirim: deftere bir satır yazıldı (durum değişikliğinden SONRA yayınlanır; K5).</summary>
    public sealed class TransactionRecorded
    {
        public TransactionRecord Record { get; }

        public TransactionRecorded(TransactionRecord record)
        {
            Record = record;
        }
    }

    /// <summary>Bildirim: nakit değişti (yalnızca gerçekten değiştiyse yayınlanır).</summary>
    public sealed class CashChanged
    {
        public Money OldCash { get; }
        public Money NewCash { get; }

        public CashChanged(Money oldCash, Money newCash)
        {
            OldCash = oldCash;
            NewCash = newCash;
        }
    }
}
