using System.Collections.Generic;
using Esnaf.Core;
using Esnaf.Domain.Content;
using Esnaf.Domain.Market;
using Esnaf.Domain.Products;
using NUnit.Framework;

namespace Esnaf.Tests.Support
{
    /// <summary>
    /// Pazar testleri için hazır düzenek: içerik, depo, kimlik üreteçleri, rastgelelik akışları ve ilan üretici.
    /// Varsayılan içerik GERÇEK içerik klasörüdür (GDD sayıları); fixture içeriği de verilebilir.
    /// </summary>
    public sealed class MarketHarness
    {
        private static ContentDatabase _real;

        public ContentDatabase Content { get; }
        public InstanceStore Store { get; }
        public IdGenerator InstanceIds { get; }
        public IdGenerator ListingIds { get; }
        public RngStreams Rng { get; }
        public ListingGenerator Generator { get; }
        public ValueCalculator Calculator { get; }

        /// <summary>Gerçek içerik (bir kez yüklenir, salt okunurdur).</summary>
        public static ContentDatabase RealContent()
        {
            if (_real == null)
            {
                ContentLoadResult result = ContentDatabase.Load(new DirectoryContentSource(TestPaths.ContentDataDirectory()));
                Assert.IsNotNull(result.Database, result.FormatIssues());
                _real = result.Database;
            }

            return _real;
        }

        public MarketHarness(ulong seed = 1UL, ContentDatabase content = null)
        {
            Content = content ?? RealContent();
            Store = new InstanceStore();
            InstanceIds = new IdGenerator();
            ListingIds = new IdGenerator();
            Rng = new RngStreams(seed);
            Generator = new ListingGenerator(Content, Store, InstanceIds, ListingIds);
            Calculator = new ValueCalculator(Content.ValueTables);
        }

        /// <summary>O gün için ilanları "market" akışıyla üretir.</summary>
        public IReadOnlyList<MarketListing> Generate(int day)
        {
            return Generator.Generate(day, Rng.Get("market"));
        }

        public ProductInstance InstanceOf(MarketListing listing)
        {
            return Store.Get(listing.InstanceId);
        }

        public ProductDefinition DefinitionOf(MarketListing listing)
        {
            return Content.GetProduct(InstanceOf(listing).DefinitionId);
        }

        public Money TrueValueOf(MarketListing listing)
        {
            return Calculator.TrueValue(InstanceOf(listing), DefinitionOf(listing));
        }

        /// <summary>Fixture içeriğinden (2 test modeli, 3 test NPC'si) değiştirilmiş pazar bölümüyle içerik yükler.</summary>
        public static ContentDatabase FixtureContent(string economyConstantsJson = null)
        {
            DictionaryContentSource source = ContentFixtures.ValidSource();
            if (economyConstantsJson != null)
            {
                source.Add(ContentFileNames.EconomyConstants, economyConstantsJson);
            }

            ContentLoadResult result = ContentDatabase.Load(source);
            Assert.IsNotNull(result.Database, result.FormatIssues());
            return result.Database;
        }
    }
}
