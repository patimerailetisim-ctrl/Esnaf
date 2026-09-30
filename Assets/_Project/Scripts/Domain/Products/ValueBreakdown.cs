using Esnaf.Core;

namespace Esnaf.Domain.Products
{
    /// <summary>
    /// Bir ürün örneğinin değer hesabının dökümü (değişmez). Oyuncuya gösterilecek "Referans Fiyat" (RF) ve
    /// GİZLİ gerçek değer (V) ile aradaki tüm çarpanlar. GDD v0.2 4.1-4.2.
    /// <c>*Exact</c> alanları yuvarlanmamış iç hesap değerleridir; para olarak kullanılmamalı (Money kullanın).
    /// </summary>
    public sealed class ValueBreakdown
    {
        /// <summary>Herkese açık referans fiyat: baz × hafıza × yaş × talep, 10 TL'ye yuvarlı.</summary>
        public Money ReferencePrice { get; }

        /// <summary>Gizli gerçek değer: RF × pil × ekran × kasa × kamera × paket, 10 TL'ye yuvarlı.</summary>
        public Money TrueValue { get; }

        public double ReferencePriceExact { get; }
        public double TrueValueExact { get; }

        public double StorageMultiplier { get; }
        public double AgeMultiplier { get; }
        public double DemandMultiplier { get; }

        public double BatteryMultiplier { get; }
        public double ScreenMultiplier { get; }
        public double BodyMultiplier { get; }
        public double CameraMultiplier { get; }

        /// <summary>Pil × ekran × kasa × kamera (paket hariç).</summary>
        public double ConditionMultiplier { get; }

        public double PackageMultiplier { get; }

        internal ValueBreakdown(
            double referencePriceExact,
            double trueValueExact,
            double storage,
            double age,
            double demand,
            double battery,
            double screen,
            double body,
            double camera,
            double package)
        {
            ReferencePriceExact = referencePriceExact;
            TrueValueExact = trueValueExact;
            ReferencePrice = Money.FromDoubleRoundedTo10(referencePriceExact);
            TrueValue = Money.FromDoubleRoundedTo10(trueValueExact);
            StorageMultiplier = storage;
            AgeMultiplier = age;
            DemandMultiplier = demand;
            BatteryMultiplier = battery;
            ScreenMultiplier = screen;
            BodyMultiplier = body;
            CameraMultiplier = camera;
            ConditionMultiplier = battery * screen * body * camera;
            PackageMultiplier = package;
        }
    }
}
