using System.Collections.Generic;
using System.Collections.ObjectModel;
using Esnaf.Core;

namespace Esnaf.Domain.Wholesale
{
    /// <summary>Başarılı bir telefon paketi alışının makbuzu (değişmez): kaç adet, hangi ProductInstance kimlikleri, toplam maliyet ve TEK defter satırı.</summary>
    public sealed class PhonePackReceipt
    {
        public string SupplierId { get; }
        public string ProductId { get; }
        public int Quantity { get; }
        public Money UnitCost { get; }
        public Money TotalCost { get; }
        public long LedgerRecordId { get; }

        /// <summary>Pakette gelen ayrı ProductInstance'ların kimlikleri (adet kadar).</summary>
        public IReadOnlyList<long> InstanceIds { get; }

        public PhonePackReceipt(string supplierId, string productId, int quantity, Money unitCost, Money totalCost, long ledgerRecordId, IEnumerable<long> instanceIds)
        {
            SupplierId = supplierId;
            ProductId = productId;
            Quantity = quantity;
            UnitCost = unitCost;
            TotalCost = totalCost;
            LedgerRecordId = ledgerRecordId;
            InstanceIds = new ReadOnlyCollection<long>(new List<long>(instanceIds));
        }
    }
}
