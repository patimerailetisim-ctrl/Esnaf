using System;
using Esnaf.Core;
using Esnaf.Domain.Npc;
using Esnaf.Domain.Products;

namespace Esnaf.Domain.Market
{
    /// <summary>
    /// Satıcının bir ürünü nasıl değerlendirdiğini ve hangi fiyatları belirlediğini hesaplar (UA5).
    /// <list type="bullet">
    /// <item>Kusuru saklamayan satıcı: inandığı değer = gerçek değer × (1 + sapma + gürültü); toplam hata σ'yı aşmaz.</item>
    /// <item>Kusuru saklayan satıcı (ve ürünün gizli kusuru var): gizli kusurlar yokmuş gibi hesaplanan değere inanır.</item>
    /// <item>İstenen fiyat = inandığı değer × çarpan, ilan fiyatı adımına (yarım yukarı) yuvarlanır; R = R oranı × inandığı değer (10 TL).</item>
    /// </list>
    /// Rastgelelik SABİT tüketilir: her çağrıda tam bir <c>Chance</c> ve bir <c>NextDouble</c> (akış kaymaz).
    /// </summary>
    public sealed class ListingPricer
    {
        private readonly ValueCalculator _calculator;
        private readonly MarketConstants _market;

        public ListingPricer(ValueCalculator calculator, MarketConstants market)
        {
            if (calculator == null)
            {
                throw new ArgumentNullException(nameof(calculator));
            }

            if (market == null)
            {
                throw new ArgumentNullException(nameof(market));
            }

            _calculator = calculator;
            _market = market;
        }

        public SellerValuation Value(ProductInstance instance, ProductDefinition definition, NpcSellerRole seller, IRandom rng)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (seller == null)
            {
                throw new ArgumentNullException(nameof(seller));
            }

            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            bool concealRoll = rng.Chance(seller.ConcealChance);
            double noise = rng.NextDouble();

            ValueBreakdown truth = _calculator.Calculate(instance, definition);
            bool concealed = concealRoll && seller.ConcealChance > 0.0 && HasHiddenDefect(instance);

            double believed;
            if (concealed)
            {
                believed = _calculator.Calculate(WithoutHiddenDefects(instance), definition).TrueValueExact;
            }
            else
            {
                double width = seller.ValueSigma - Math.Abs(seller.ValueBias);
                believed = truth.TrueValueExact * (1.0 + seller.ValueBias + width * (2.0 * noise - 1.0));
            }

            return new SellerValuation(
                truth.TrueValue,
                Money.FromDoubleRoundedTo10(believed),
                AskingPrice(believed, seller.AskMultiplier),
                Money.FromDoubleRoundedTo10(seller.RejectRatio * believed),
                concealed);
        }

        /// <summary>İstenen fiyat: inandığı değer × çarpan, ilan fiyatı adımına yuvarlı.</summary>
        public Money AskingPrice(double believedValueExact, double askMultiplier)
        {
            return Money.FromTl(RoundToStep(believedValueExact * askMultiplier, _market.AskingPriceStep));
        }

        /// <summary>En yakın adıma, yarım yukarı yuvarlar; en az bir adım döner.</summary>
        public static long RoundToStep(double value, long step)
        {
            long steps = (long)Math.Floor(value / step + 0.5);
            if (steps < 1)
            {
                steps = 1;
            }

            return steps * step;
        }

        private bool HasHiddenDefect(ProductInstance instance)
        {
            for (int i = 0; i < _market.HiddenDefects.Count; i++)
            {
                if (IsDefective(instance, _market.HiddenDefects[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private ProductInstance WithoutHiddenDefects(ProductInstance instance)
        {
            var copy = new ProductInstance
            {
                InstanceId = instance.InstanceId,
                DefinitionId = instance.DefinitionId,
                StorageGb = instance.StorageGb,
                AgeMonths = instance.AgeMonths
            };

            foreach (System.Collections.Generic.KeyValuePair<string, AttributeValue> pair in instance.Attributes)
            {
                copy.Attributes[pair.Key] = pair.Value;
            }

            for (int i = 0; i < _market.HiddenDefects.Count; i++)
            {
                HiddenDefectRule rule = _market.HiddenDefects[i];
                if (IsDefective(instance, rule))
                {
                    copy.Attributes[rule.Attribute] = AttributeValue.FromText(rule.CleanValue);
                }
            }

            return copy;
        }

        private static bool IsDefective(ProductInstance instance, HiddenDefectRule rule)
        {
            AttributeValue value;
            return instance.Attributes.TryGetValue(rule.Attribute, out value)
                   && value.Kind == AttributeKind.Text
                   && rule.IsHidden(value.Text);
        }
    }
}
