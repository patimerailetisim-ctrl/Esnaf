using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Products;

namespace Esnaf.Domain.Content
{
    /// <summary>
    /// Yüklenmiş ve doğrulanmış içeriğin salt okunur deposu. Yalnızca <see cref="Load"/> ile oluşur; geçersiz içerikten
    /// veritabanı ÜRETİLMEZ. Unity'yi bilmez: aynı JSON'ları Unity oyunu ve .NET simülatörü aynı kodla yükler.
    /// </summary>
    public sealed class ContentDatabase
    {
        private readonly ReadOnlyCollection<ProductDefinition> _products;
        private readonly Dictionary<string, ProductDefinition> _productsById;
        private readonly ReadOnlyCollection<ConditionProfile> _conditionProfiles;
        private readonly Dictionary<string, ConditionProfile> _profilesById;

        /// <summary>Dosyadaki sırayla, salt okunur.</summary>
        public IReadOnlyList<ProductDefinition> Products
        {
            get { return _products; }
        }

        /// <summary>Değer formülünün çarpan tabloları (value_tables.json).</summary>
        public ValueTables ValueTables { get; }

        /// <summary>Defter türleri kayıt defteri (transaction_types.json).</summary>
        public TransactionTypes TransactionTypes { get; }

        /// <summary>Ekonomi sabitleri (economy_constants.json).</summary>
        public EconomyConstants EconomyConstants { get; }

        /// <summary>Durum profilleri, dosyadaki sırayla, salt okunur.</summary>
        public IReadOnlyList<ConditionProfile> ConditionProfiles
        {
            get { return _conditionProfiles; }
        }

        private ContentDatabase(
            IEnumerable<ProductDefinition> products,
            ValueTables valueTables,
            IEnumerable<ConditionProfile> conditionProfiles,
            TransactionTypes transactionTypes,
            EconomyConstants economyConstants)
        {
            TransactionTypes = transactionTypes;
            EconomyConstants = economyConstants;
            var list = new List<ProductDefinition>(products);
            _productsById = new Dictionary<string, ProductDefinition>(list.Count, StringComparer.Ordinal);
            for (int i = 0; i < list.Count; i++)
            {
                _productsById.Add(list[i].Id, list[i]);
            }

            _products = new ReadOnlyCollection<ProductDefinition>(list);

            ValueTables = valueTables;
            var profileList = new List<ConditionProfile>(conditionProfiles);
            _profilesById = new Dictionary<string, ConditionProfile>(profileList.Count, StringComparer.Ordinal);
            for (int i = 0; i < profileList.Count; i++)
            {
                _profilesById.Add(profileList[i].Id, profileList[i]);
            }

            _conditionProfiles = new ReadOnlyCollection<ConditionProfile>(profileList);
        }

        public bool TryGetConditionProfile(string id, out ConditionProfile profile)
        {
            if (id == null)
            {
                profile = null;
                return false;
            }

            return _profilesById.TryGetValue(id, out profile);
        }

        public bool TryGetProduct(string id, out ProductDefinition product)
        {
            if (id == null)
            {
                product = null;
                return false;
            }

            return _productsById.TryGetValue(id, out product);
        }

        /// <summary>ID yoksa KeyNotFoundException (programcı hatası; kayıt doğrulaması ayrıca yapılır).</summary>
        public ProductDefinition GetProduct(string id)
        {
            ProductDefinition product;
            if (!TryGetProduct(id, out product))
            {
                throw new KeyNotFoundException("Unknown product definition id '" + id + "'.");
            }

            return product;
        }

        /// <summary>
        /// İçerik dosyalarını kaynaktan okur, ayrıştırır ve doğrular. Hata varsa <see cref="ContentLoadResult.Database"/> null olur.
        /// </summary>
        public static ContentLoadResult Load(IContentSource source, ContentLoadOptions options = null)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            options = options ?? new ContentLoadOptions();
            var issues = new List<ContentIssue>();

            // Ürün modelleri
            IReadOnlyList<ProductDefinition> products = null;
            string text;
            if (!source.TryGetText(ContentFileNames.PhoneModels, out text))
            {
                issues.Add(ContentIssue.Error(ContentIssueCodes.FileMissing, ContentFileNames.PhoneModels, "Content file not found."));
            }
            else
            {
                products = ContentParser.ParseProductModels(ContentFileNames.PhoneModels, text, issues);
                if (products != null)
                {
                    ContentValidator.ValidateProducts(products, options, ContentFileNames.PhoneModels, issues);
                }
            }

            // Değer tabloları
            ValueTables valueTables = null;
            if (!source.TryGetText(ContentFileNames.ValueTables, out text))
            {
                issues.Add(ContentIssue.Error(ContentIssueCodes.FileMissing, ContentFileNames.ValueTables, "Content file not found."));
            }
            else
            {
                valueTables = ContentParser.ParseValueTables(ContentFileNames.ValueTables, text, issues);
                if (valueTables != null)
                {
                    ContentValidator.ValidateValueTables(valueTables, ContentFileNames.ValueTables, issues);
                }
            }

            // Durum profilleri (ekran/kamera değerleri tablolarla çapraz denetlenir; tablo yüklenemediyse çapraz denetim atlanır)
            IReadOnlyList<ConditionProfile> conditionProfiles = null;
            if (!source.TryGetText(ContentFileNames.ConditionProfiles, out text))
            {
                issues.Add(ContentIssue.Error(ContentIssueCodes.FileMissing, ContentFileNames.ConditionProfiles, "Content file not found."));
            }
            else
            {
                conditionProfiles = ContentParser.ParseConditionProfiles(ContentFileNames.ConditionProfiles, text, issues);
                if (conditionProfiles != null && valueTables != null)
                {
                    ContentValidator.ValidateConditionProfiles(conditionProfiles, valueTables, ContentFileNames.ConditionProfiles, issues);
                }
            }

            // Defter türleri
            IReadOnlyList<TransactionType> transactionTypes = null;
            if (!source.TryGetText(ContentFileNames.TransactionTypes, out text))
            {
                issues.Add(ContentIssue.Error(ContentIssueCodes.FileMissing, ContentFileNames.TransactionTypes, "Content file not found."));
            }
            else
            {
                transactionTypes = ContentParser.ParseTransactionTypes(ContentFileNames.TransactionTypes, text, issues);
                if (transactionTypes != null)
                {
                    ContentValidator.ValidateTransactionTypes(transactionTypes, ContentFileNames.TransactionTypes, issues);
                }
            }

            // Ekonomi sabitleri
            EconomyConstants economyConstants = null;
            if (!source.TryGetText(ContentFileNames.EconomyConstants, out text))
            {
                issues.Add(ContentIssue.Error(ContentIssueCodes.FileMissing, ContentFileNames.EconomyConstants, "Content file not found."));
            }
            else
            {
                economyConstants = ContentParser.ParseEconomyConstants(ContentFileNames.EconomyConstants, text, issues);
                if (economyConstants != null)
                {
                    ContentValidator.ValidateEconomyConstants(economyConstants, ContentFileNames.EconomyConstants, issues);
                }
            }

            // ID manifesti
            IReadOnlyList<string> manifestIds = null;
            if (source.TryGetText(ContentFileNames.IdManifest, out text))
            {
                manifestIds = ContentParser.ParseIdManifest(ContentFileNames.IdManifest, text, issues);
            }
            else if (options.RequireManifest)
            {
                issues.Add(ContentIssue.Error(ContentIssueCodes.FileMissing, ContentFileNames.IdManifest, "Content file not found."));
            }

            // Manifest karşılaştırması yalnızca önceki adımlar temizse yapılır (art arda gelen gürültüyü önler).
            if (products != null && manifestIds != null && !HasError(issues))
            {
                var contentIds = new List<string>(products.Count);
                for (int i = 0; i < products.Count; i++)
                {
                    contentIds.Add(products[i].Id);
                }

                ContentValidator.ValidateManifest(manifestIds, contentIds, ContentFileNames.IdManifest, issues);
            }

            if (HasError(issues)
                || products == null
                || valueTables == null
                || conditionProfiles == null
                || transactionTypes == null
                || economyConstants == null)
            {
                return new ContentLoadResult(null, issues);
            }

            return new ContentLoadResult(
                new ContentDatabase(products, valueTables, conditionProfiles, new TransactionTypes(transactionTypes), economyConstants), issues);
        }

        private static bool HasError(IList<ContentIssue> issues)
        {
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity == ContentIssueSeverity.Error)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
