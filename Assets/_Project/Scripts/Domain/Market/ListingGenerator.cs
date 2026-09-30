using System;
using System.Collections.Generic;
using Esnaf.Core;
using Esnaf.Domain.Content;
using Esnaf.Domain.Npc;
using Esnaf.Domain.Phone;
using Esnaf.Domain.Products;

namespace Esnaf.Domain.Market
{
    /// <summary>
    /// Bir gün için pazar ilanlarını üretir (GDD v0.2 11.1, v0.3 P1–P5). Sayı, satıcı, model, ürün durumu ve fiyatlar
    /// veriden ve verilen <see cref="IRandom"/> akışından gelir (aynı akış durumu + aynı gün = aynı ilanlar).
    ///
    /// Kotalar (Gün 2+ ≥ 2 fırsat, Gün 5+ ≥ 1 tuzak, jackpot ve tuzak üst sınırları) "önce zorunlu yuvalar, sonra dolgu"
    /// yöntemiyle sağlanır: zorunlu yuva için uygun bir aday çıkana kadar (sınırlı sayıda) yeni aday çekilir. Kota
    /// sağlanamazsa <see cref="InvalidOperationException"/> atılır ve HİÇBİR durum değişmez (depo, kimlik sayaçları).
    ///
    /// Rastgele çekim sırası (değişirse altın testler bilinçli olarak güncellenir):
    /// ilan sayısı → [zorunlu tuzaklar] → [zorunlu fırsatlar] → dolgu → karıştırma → ömürler.
    /// Aday çekimi: satıcı → model → ürün örneği → satıcı değerlendirmesi.
    /// </summary>
    public sealed class ListingGenerator
    {
        private const int MaxAttemptsPerSlot = 5000;

        private readonly ContentDatabase _content;
        private readonly MarketConstants _market;
        private readonly InstanceStore _store;
        private readonly IdGenerator _instanceIds;
        private readonly IdGenerator _listingIds;
        private readonly ValueCalculator _calculator;
        private readonly ListingPricer _pricer;
        private readonly InstanceGenerator _candidateFactory;

        public ListingGenerator(ContentDatabase content, InstanceStore store, IdGenerator instanceIds, IdGenerator listingIds)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            if (store == null)
            {
                throw new ArgumentNullException(nameof(store));
            }

            if (instanceIds == null)
            {
                throw new ArgumentNullException(nameof(instanceIds));
            }

            if (listingIds == null)
            {
                throw new ArgumentNullException(nameof(listingIds));
            }

            _content = content;
            _market = content.MarketConstants;
            _store = store;
            _instanceIds = instanceIds;
            _listingIds = listingIds;
            _calculator = new ValueCalculator(content.ValueTables);
            _pricer = new ListingPricer(_calculator, _market);

            // Aday ürünler geçici bir sayaçla üretilir; yalnızca KABUL edilen adaya gerçek kimlik verilir.
            _candidateFactory = new InstanceGenerator(content.ConditionProfiles, new IdGenerator());
        }

        /// <summary>O günün ilanlarını üretir, ürün örneklerini depoya koyar ve ilanları döndürür (pazara eklemek çağıranındır).</summary>
        public IReadOnlyList<MarketListing> Generate(int day, IRandom rng)
        {
            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            RequireDay(day);

            ListingCountBand band = _market.CountBandFor(day);
            int count = rng.NextInt(band.Min, band.Max + 1);

            int requiredTraps = day >= _market.TrapFromDay ? _market.TrapMinPerDay : 0;
            int requiredOpportunities = day >= _market.OpportunityFromDay ? _market.MinOpportunitiesPerDay : 0;

            var picked = new List<Candidate>(count);
            var tally = new Tally();
            if (day == 1)
            {
                Register(picked, tally, BuildGuided());
            }

            if (picked.Count + Math.Max(0, requiredTraps - tally.Traps) + Math.Max(0, requiredOpportunities - tally.Opportunities) > count)
            {
                throw new InvalidOperationException(
                    "Day " + day + " has " + count + " listings, too few for the required " + requiredTraps + " trap(s) and "
                    + requiredOpportunities + " opportunity(ies).");
            }

            int jackpotMax = _market.JackpotMax(day);
            int trapMax = _market.TrapMax(day);

            while (tally.Traps < requiredTraps)
            {
                Register(picked, tally, DrawUntil(day, rng, true, c => c.IsTrap && Fits(c, tally, jackpotMax, trapMax), "a trap"));
            }

            while (tally.Opportunities < requiredOpportunities)
            {
                Register(picked, tally, DrawUntil(day, rng, false, c => c.IsOpportunity && Fits(c, tally, jackpotMax, trapMax), "an opportunity"));
            }

            while (picked.Count < count)
            {
                Register(picked, tally, DrawUntil(day, rng, false, c => Fits(c, tally, jackpotMax, trapMax), "a listing within the quotas"));
            }

            Shuffle(picked, rng);

            var lifetimes = new int[picked.Count];
            for (int i = 0; i < picked.Count; i++)
            {
                lifetimes[i] = rng.NextInt(_market.LifetimeMinDays, _market.LifetimeMaxDays + 1);
            }

            return Commit(picked, lifetimes, day);
        }

        /// <summary>
        /// Satıcı seçimi (UA4/UA8): o gün açık satıcılardan; ilk günlerde (P2) öğrenme dostu satıcılar payını alır,
        /// grup içinde eşit olasılıkla.
        /// </summary>
        public NpcDefinition PickSeller(int day, IRandom rng)
        {
            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            RequireDay(day);

            var all = new List<NpcDefinition>();
            var friendly = new List<NpcDefinition>();
            var others = new List<NpcDefinition>();
            for (int i = 0; i < _content.Npcs.Count; i++)
            {
                NpcDefinition npc = _content.Npcs[i];
                if (npc.Seller.AvailableFromDay > day)
                {
                    continue;
                }

                all.Add(npc);
                (npc.Seller.LearningFriendly ? friendly : others).Add(npc);
            }

            if (all.Count == 0)
            {
                throw new InvalidOperationException("No seller is available on day " + day + ".");
            }

            List<NpcDefinition> pool = all;
            if (day <= _market.LearningFriendlyUntilDay && friendly.Count > 0 && others.Count > 0)
            {
                pool = rng.Chance(_market.LearningFriendlyShare) ? friendly : others;
            }

            return pool[rng.NextInt(pool.Count)];
        }

        /// <summary>Model seçimi: önce segment (o günün ağırlıklarıyla), sonra segmentteki açık modellerden eşit olasılıkla.</summary>
        public ProductDefinition PickModel(int day, IRandom rng)
        {
            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            RequireDay(day);

            SegmentWeightBand band = _market.SegmentBandFor(day);
            var bySegment = new List<ProductDefinition>[3];
            for (int s = 0; s < bySegment.Length; s++)
            {
                bySegment[s] = new List<ProductDefinition>();
            }

            for (int i = 0; i < _content.Products.Count; i++)
            {
                ProductDefinition product = _content.Products[i];
                if (!product.IsDeprecated && _market.IsModelAvailable(product.Id, day))
                {
                    bySegment[(int)product.Segment].Add(product);
                }
            }

            int total = 0;
            var weights = new int[3];
            for (int s = 0; s < weights.Length; s++)
            {
                weights[s] = bySegment[s].Count > 0 ? band.WeightFor((ProductSegment)s) : 0;
                total += weights[s];
            }

            if (total <= 0)
            {
                throw new InvalidOperationException("No product model is available on day " + day + ".");
            }

            int roll = rng.NextInt(total);
            for (int s = 0; s < weights.Length; s++)
            {
                if (roll < weights[s])
                {
                    return bySegment[s][rng.NextInt(bySegment[s].Count)];
                }

                roll -= weights[s];
            }

            throw new InvalidOperationException("Weighted segment selection failed."); // ulaşılamaz: roll < total
        }

        // ---------- aday üretimi ----------

        private sealed class Candidate
        {
            public ProductInstance Instance;
            public ProductDefinition Definition;
            public NpcDefinition Seller;
            public SellerValuation Valuation;
            public bool IsOpportunity;
            public bool IsTrap;
            public bool IsJackpot;
            public bool IsGuided;
        }

        private sealed class Tally
        {
            public int Opportunities;
            public int Traps;
            public int Jackpots;
        }

        private static bool Fits(Candidate c, Tally tally, int jackpotMax, int trapMax)
        {
            return (!c.IsJackpot || tally.Jackpots < jackpotMax) && (!c.IsTrap || tally.Traps < trapMax);
        }

        private static void Register(List<Candidate> picked, Tally tally, Candidate c)
        {
            picked.Add(c);
            tally.Opportunities += c.IsOpportunity ? 1 : 0;
            tally.Traps += c.IsTrap ? 1 : 0;
            tally.Jackpots += c.IsJackpot ? 1 : 0;
        }

        private Candidate DrawUntil(int day, IRandom rng, bool concealingSellerOnly, Func<Candidate, bool> accept, string what)
        {
            for (int attempt = 0; attempt < MaxAttemptsPerSlot; attempt++)
            {
                Candidate c = Draw(day, rng, concealingSellerOnly);
                if (accept(c))
                {
                    return c;
                }
            }

            throw new InvalidOperationException("Could not generate " + what + " for day " + day + " in " + MaxAttemptsPerSlot + " attempts.");
        }

        private Candidate Draw(int day, IRandom rng, bool concealingSellerOnly)
        {
            NpcDefinition seller = concealingSellerOnly ? PickConcealingSeller(day, rng) : PickSeller(day, rng);
            ProductDefinition definition = PickModel(day, rng);
            ProductInstance instance = _candidateFactory.Generate(definition, day, rng);
            SellerValuation valuation = _pricer.Value(instance, definition, seller.Seller, rng);
            return Classify(instance, definition, seller, valuation, false);
        }

        /// <summary>Tuzak yuvası için: o gün açık ve kusur saklama olasılığı olan satıcılardan eşit olasılıkla.</summary>
        public NpcDefinition PickConcealingSeller(int day, IRandom rng)
        {
            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            RequireDay(day);

            var pool = new List<NpcDefinition>();
            for (int i = 0; i < _content.Npcs.Count; i++)
            {
                NpcDefinition npc = _content.Npcs[i];
                if (npc.Seller.AvailableFromDay <= day && npc.Seller.ConcealChance > 0.0)
                {
                    pool.Add(npc);
                }
            }

            if (pool.Count == 0)
            {
                throw new InvalidOperationException("No concealing seller is available on day " + day + ".");
            }

            return pool[rng.NextInt(pool.Count)];
        }

        private Candidate Classify(ProductInstance instance, ProductDefinition definition, NpcDefinition seller, SellerValuation valuation, bool guided)
        {
            decimal value = valuation.TrueValue.Tl;
            bool trap = valuation.Concealed && valuation.BelievedValue.Tl >= (decimal)_market.TrapValueRatio * value;
            bool opportunity = !trap && valuation.RejectPrice.Tl <= (decimal)_market.OpportunityMaxRejectRatio * value;

            return new Candidate
            {
                Instance = instance,
                Definition = definition,
                Seller = seller,
                Valuation = valuation,
                IsOpportunity = opportunity,
                IsTrap = trap,
                IsJackpot = seller.Seller.RejectRatio < _market.JackpotRejectRatioBelow,
                IsGuided = guided
            };
        }

        /// <summary>Gün 1'in rehberli ilanı: veri dosyasındaki tarife göre sabit ürün; gürültüsüz, sabit R.</summary>
        private Candidate BuildGuided()
        {
            GuidedListingSpec spec = _market.GuidedListing;
            NpcDefinition seller = _content.GetNpc(spec.SellerNpcId);
            ProductDefinition definition = _content.GetProduct(spec.DefinitionId);

            var instance = new ProductInstance
            {
                DefinitionId = definition.Id,
                StorageGb = spec.StorageGb,
                AgeMonths = spec.AgeMonths,
                Location = ProductLocation.Market
            };
            instance.Attributes[PhoneAttributes.Battery] = AttributeValue.FromNumber(spec.Battery);
            instance.Attributes[PhoneAttributes.Screen] = AttributeValue.FromText(spec.Screen);
            instance.Attributes[PhoneAttributes.Body] = AttributeValue.FromNumber(spec.Body);
            instance.Attributes[PhoneAttributes.Camera] = AttributeValue.FromText(spec.Camera);
            instance.Attributes[PhoneAttributes.Box] = AttributeValue.FromFlag(spec.Box);
            instance.Attributes[PhoneAttributes.Invoice] = AttributeValue.FromFlag(spec.Invoice);

            ValueBreakdown truth = _calculator.Calculate(instance, definition);
            var valuation = new SellerValuation(
                truth.TrueValue,
                Money.FromDoubleRoundedTo10(truth.TrueValueExact),
                _pricer.AskingPrice(truth.TrueValueExact, seller.Seller.AskMultiplier),
                spec.RejectPrice,
                false);

            return Classify(instance, definition, seller, valuation, true);
        }

        private static void Shuffle(List<Candidate> items, IRandom rng)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = rng.NextInt(i + 1);
                Candidate swap = items[i];
                items[i] = items[j];
                items[j] = swap;
            }
        }

        // ---------- kayıt ----------

        private IReadOnlyList<MarketListing> Commit(List<Candidate> picked, int[] lifetimes, int day)
        {
            var listings = new List<MarketListing>(picked.Count);
            for (int i = 0; i < picked.Count; i++)
            {
                Candidate c = picked[i];
                long listingId = _listingIds.Next();

                c.Instance.InstanceId = _instanceIds.Next();
                c.Instance.ListingId = listingId;
                c.Instance.SellerNpcId = c.Seller.Id;
                c.Instance.Location = ProductLocation.Market;
                _store.Add(c.Instance);

                listings.Add(new MarketListing(
                    listingId,
                    c.Instance.InstanceId,
                    c.Seller.Id,
                    c.Valuation.AskingPrice,
                    TagsFor(c, day),
                    day,
                    lifetimes[i],
                    c.Valuation.RejectPrice,
                    c.Valuation.BelievedValue,
                    c.IsOpportunity,
                    c.IsTrap,
                    c.IsJackpot,
                    c.IsGuided));
            }

            return listings;
        }

        private static List<string> TagsFor(Candidate c, int day)
        {
            var tags = new List<string>();
            if (c.Instance.GetFlag(PhoneAttributes.Box))
            {
                tags.Add(ListingTags.Box);
            }

            if (c.Instance.GetFlag(PhoneAttributes.Invoice))
            {
                tags.Add(ListingTags.Invoice);
            }

            int? urgentFrom = c.Seller.Seller.UrgentLabelFromDay;
            if (urgentFrom.HasValue && day >= urgentFrom.Value)
            {
                tags.Add(ListingTags.UrgentSale);
            }

            return tags;
        }

        private static void RequireDay(int day)
        {
            if (day < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(day), "Day must be at least 1.");
            }
        }
    }
}
