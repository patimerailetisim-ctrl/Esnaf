using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

        /// <summary>Dosyadaki sırayla, salt okunur.</summary>
        public IReadOnlyList<ProductDefinition> Products
        {
            get { return _products; }
        }

        private ContentDatabase(IEnumerable<ProductDefinition> products)
        {
            var list = new List<ProductDefinition>(products);
            _productsById = new Dictionary<string, ProductDefinition>(list.Count, StringComparer.Ordinal);
            for (int i = 0; i < list.Count; i++)
            {
                _productsById.Add(list[i].Id, list[i]);
            }

            _products = new ReadOnlyCollection<ProductDefinition>(list);
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

            if (HasError(issues) || products == null)
            {
                return new ContentLoadResult(null, issues);
            }

            return new ContentLoadResult(new ContentDatabase(products), issues);
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
