using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Esnaf.Presentation
{
    /// <summary>Gerçek ürün görseli bağlanan telefon modellerinin içerik kimlikleri (phone_models.json ile birebir; bir test denetler).</summary>
    public static class PhoneModelIds
    {
        public const string ElmaE13Pro = "phone.elma_e13_pro";

        public static readonly IReadOnlyList<string> All = new ReadOnlyCollection<string>(new[]
        {
            "phone.nova_n1_lite",
            "phone.yildiz_y5",
            "phone.samsun_vega_a3",
            "phone.nova_n3_pro",
            "phone.zirve_z5",
            "phone.yildiz_y8_plus",
            "phone.elma_e11",
            "phone.samsun_vega_s21",
            ElmaE13Pro,
            "phone.elma_e14_pro_max"
        });
    }
}
