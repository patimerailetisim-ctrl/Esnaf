using Esnaf.Core;

namespace Esnaf.Domain.Negotiation
{
    /// <summary>
    /// Süren satış pazarlığının durumu (kayda girer). Müşterinin Max'ı, güveni ve sabrı GİZLİDİR; UI'ya giden görünümler taşımaz.
    /// Değişiklik yalnızca <see cref="SaleEngine"/> içinden yapılır.
    /// </summary>
    public sealed class SaleState
    {
        public SaleSetup Setup { get; }
        public NegotiationPhase Phase { get; internal set; }
        public int Round { get; internal set; }
        public int Patience { get; internal set; }
        public int Trust { get; internal set; }

        /// <summary>Gizli: müşterinin şu anki Max'ı (rapor gösterilince değişebilir).</summary>
        public double Max { get; internal set; }

        /// <summary>Müşterinin yuvarlanmamış teklifi (kesin değer).</summary>
        public double Offer { get; internal set; }

        /// <summary>Müşterinin şu anki, oyuncuya gösterilen teklifi.</summary>
        public Money ShownPrice { get; internal set; }

        public Money DealPrice { get; internal set; }

        /// <summary>"Rapor göster" bu pazarlıkta kullanıldı.</summary>
        public bool ReportShown { get; internal set; }

        public bool IsOpen
        {
            get { return Phase == NegotiationPhase.Active || Phase == NegotiationPhase.FinalOffer; }
        }

        internal SaleState(SaleSetup setup)
        {
            Setup = setup;
            Phase = NegotiationPhase.Active;
            Patience = setup.Patience;
            Trust = setup.Trust;
            Max = setup.Max;
            Offer = (double)setup.Opening.Tl;
            ShownPrice = setup.Opening;
            DealPrice = Money.Zero;
        }

        internal SaleState Clone()
        {
            return new SaleState(Setup)
            {
                Phase = Phase,
                Round = Round,
                Patience = Patience,
                Trust = Trust,
                Max = Max,
                Offer = Offer,
                ShownPrice = ShownPrice,
                DealPrice = DealPrice,
                ReportShown = ReportShown
            };
        }
    }
}
