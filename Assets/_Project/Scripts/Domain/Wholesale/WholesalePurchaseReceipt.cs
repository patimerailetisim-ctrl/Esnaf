using Esnaf.Core;

namespace Esnaf.Domain.Wholesale
{
    /// <summary>Başarılı bir toptan paket alışının makbuzu (değişmez).</summary>
    public sealed class WholesalePurchaseReceipt
    {
        public string SupplierId { get; }
        public string AccessoryId { get; }
        public int Quantity { get; }
        public Money UnitCost { get; }
        public Money TotalCost { get; }

        /// <summary>Bu alış için yazılan TEK defter satırının kimliği.</summary>
        public long LedgerRecordId { get; }

        public WholesalePurchaseReceipt(string supplierId, string accessoryId, int quantity, Money unitCost, Money totalCost, long ledgerRecordId)
        {
            SupplierId = supplierId;
            AccessoryId = accessoryId;
            Quantity = quantity;
            UnitCost = unitCost;
            TotalCost = totalCost;
            LedgerRecordId = ledgerRecordId;
        }
    }
}
