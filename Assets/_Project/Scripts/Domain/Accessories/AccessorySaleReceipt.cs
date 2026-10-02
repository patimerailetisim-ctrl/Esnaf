using Esnaf.Core;

namespace Esnaf.Domain.Accessories
{
    /// <summary>Başarılı bir aksesuar ek satışının makbuzu (değişmez): satış fiyatı, stoktan çıkan gerçek maliyet ve kâr; bağlı telefon satışının kârı ile birlikte toplam.</summary>
    public sealed class AccessorySaleReceipt
    {
        public string AccessoryId { get; }

        /// <summary>Ek satışın bağlandığı telefon satış defter satırı.</summary>
        public long PhoneSaleRecordId { get; }

        /// <summary>Bu ek satış için yazılan TEK defter satırı.</summary>
        public long LedgerRecordId { get; }

        /// <summary>Satış fiyatı = aksesuarın sabit retailPrice'ı (pazarlık yok).</summary>
        public Money SalePrice { get; }

        /// <summary>Stoktan çıkan 1 birimin gerçek (orantılı) maliyeti.</summary>
        public Money CostBasis { get; }

        /// <summary>Aksesuar kârı = satış fiyatı − gerçek maliyet.</summary>
        public Money Profit
        {
            get { return SalePrice - CostBasis; }
        }

        /// <summary>Bağlı telefon satışının kârı (satış − telefon maliyet tabanı).</summary>
        public Money PhoneProfit { get; }

        /// <summary>İşlemin toplam kârı = telefon kârı + bu aksesuarın kârı.</summary>
        public Money CombinedProfit
        {
            get { return PhoneProfit + Profit; }
        }

        public AccessorySaleReceipt(string accessoryId, long phoneSaleRecordId, long ledgerRecordId, Money salePrice, Money costBasis, Money phoneProfit)
        {
            AccessoryId = accessoryId;
            PhoneSaleRecordId = phoneSaleRecordId;
            LedgerRecordId = ledgerRecordId;
            SalePrice = salePrice;
            CostBasis = costBasis;
            PhoneProfit = phoneProfit;
        }
    }
}
