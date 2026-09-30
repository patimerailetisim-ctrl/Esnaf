using Esnaf.Core;

namespace Esnaf.Domain.Market
{
    /// <summary>Bir satıcının bir ürün için değer yargısı ve fiyatları (UA5).</summary>
    public sealed class SellerValuation
    {
        /// <summary>Ürünün gerçek değeri (ValueCalculator, 10 TL'ye yuvarlı).</summary>
        public Money TrueValue { get; }

        /// <summary>Satıcının ürünü değer sandığı tutar (10 TL'ye yuvarlı).</summary>
        public Money BelievedValue { get; }

        /// <summary>İstenen (ilan) fiyat; ilan fiyatı adımına (50 TL) yuvarlı.</summary>
        public Money AskingPrice { get; }

        /// <summary>Gizli ret fiyatı R (10 TL'ye yuvarlı).</summary>
        public Money RejectPrice { get; }

        /// <summary>Satıcı gizli kusurları gerçekten saklıyor mu (kusur var ve saklama zarı tuttu).</summary>
        public bool Concealed { get; }

        public SellerValuation(Money trueValue, Money believedValue, Money askingPrice, Money rejectPrice, bool concealed)
        {
            TrueValue = trueValue;
            BelievedValue = believedValue;
            AskingPrice = askingPrice;
            RejectPrice = rejectPrice;
            Concealed = concealed;
        }
    }
}
