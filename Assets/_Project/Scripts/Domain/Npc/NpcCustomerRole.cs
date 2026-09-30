using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Esnaf.Domain.Products;

namespace Esnaf.Domain.Npc
{
    /// <summary>
    /// Bir NPC'nin MÜŞTERİ rolündeki sayıları (GDD v0.2 6.2). Müşteri üretimi ve satış pazarlığı bu sayıları kullanır.
    /// </summary>
    public sealed class NpcCustomerRole
    {
        /// <summary>Açılış teklifi, müşterinin en çok ödeyeceği tutarın (Max) bu oranıdır.</summary>
        public double OpeningOfferRatio { get; }

        /// <summary>mRatio: müşterinin değer çarpanı.</summary>
        public double ValueRatio { get; }

        public int Patience { get; }

        /// <summary>Müşterinin değeri yanlış görme payı (yalnızca belirtilenlerde; Hatice Teyze %30).</summary>
        public double ValueSigma { get; }

        /// <summary>Paketli (kutu/fatura) üründe ek çarpan (Nermin 1,12); diğerlerinde 1.</summary>
        public double PackageRatio { get; }

        /// <summary>Müşteri olarak ilk gelebildiği gün (v0.2 11.2: Gün 1–2 yalnız Aceleci/Pazarlıkçı, Gün 4'te Şüpheli/Bilgili, Gün 6'da Zengin/Koleksiyoncu).</summary>
        public int AvailableFromDay { get; }

        /// <summary>İlgilendiği segmentler (Cengiz: giriş/orta). Boş liste = hepsi.</summary>
        public IReadOnlyList<ProductSegment> Segments { get; }

        /// <summary>"Rapor göster" güven kazancı; null = pazarlık kurallarındaki varsayılan (Rıza Bey 15).</summary>
        public int? ReportTrustGain { get; }

        public NpcCustomerRole(
            double openingOfferRatio,
            double valueRatio,
            int patience,
            double valueSigma,
            double packageRatio,
            int availableFromDay = 1,
            IEnumerable<ProductSegment> segments = null,
            int? reportTrustGain = null)
        {
            AvailableFromDay = availableFromDay;
            Segments = new ReadOnlyCollection<ProductSegment>(new List<ProductSegment>(segments ?? new ProductSegment[0]));
            ReportTrustGain = reportTrustGain;
            OpeningOfferRatio = openingOfferRatio;
            ValueRatio = valueRatio;
            Patience = patience;
            ValueSigma = valueSigma;
            PackageRatio = packageRatio;
        }

        public bool AcceptsSegment(ProductSegment segment)
        {
            return Segments.Count == 0 || Segments.Contains(segment);
        }
    }
}
