using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Esnaf.Domain.Products;

namespace Esnaf.Domain.Content
{
    /// <summary>
    /// Yüklenmiş tanımların ANLAMSAL kurallarını denetler (biçim, aralık, tutarlılık, ID manifesti).
    /// Sınıflar doğrulama yapmadığı için doğrudan kodla üretilmiş tanımlar da denetlenebilir (testler).
    /// Tüm sorunlar toplanır; ilk hatada durmaz.
    /// </summary>
    public static class ContentValidator
    {
        private const int MinReleaseYear = 2000;
        private const int MaxReleaseYear = 2100;
        private const double MultiplierTolerance = 1e-9;

        // "sektor.ad" (küçük harf, rakam, alt çizgi). Örn: phone.elma_e13_pro
        private static readonly Regex IdFormat = new Regex("^[a-z][a-z0-9_]*\\.[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant);

        public static void ValidateProducts(
            IReadOnlyList<ProductDefinition> products,
            ContentLoadOptions options,
            string fileName,
            ICollection<ContentIssue> issues)
        {
            if (products.Count == 0)
            {
                issues.Add(ContentIssue.Error(ContentIssueCodes.ProductsEmpty, fileName, "The file contains no product models."));
                return;
            }

            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < products.Count; i++)
            {
                ValidateProduct(products[i], options, fileName, seenIds, issues);
            }
        }

        /// <param name="manifestIds">content_id_manifest.json içindeki ID'ler.</param>
        /// <param name="contentIds">İçerikte gerçekten bulunan ID'ler.</param>
        public static void ValidateManifest(
            IReadOnlyList<string> manifestIds,
            IReadOnlyCollection<string> contentIds,
            string fileName,
            ICollection<ContentIssue> issues)
        {
            var manifestSet = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < manifestIds.Count; i++)
            {
                if (!manifestSet.Add(manifestIds[i]))
                {
                    issues.Add(ContentIssue.Error(
                        ContentIssueCodes.ManifestDuplicate, fileName, "ID '" + manifestIds[i] + "' is listed more than once."));
                }
            }

            var contentSet = new HashSet<string>(contentIds, StringComparer.Ordinal);

            for (int i = 0; i < manifestIds.Count; i++)
            {
                string id = manifestIds[i];
                if (!contentSet.Contains(id))
                {
                    issues.Add(ContentIssue.Error(
                        ContentIssueCodes.ManifestIdMissingInContent,
                        fileName,
                        "ID '" + id + "' existed before but is gone from the content. IDs must never be deleted or renamed; mark it \"deprecated\": true instead."));
                }
            }

            foreach (string id in contentIds)
            {
                if (!manifestSet.Contains(id))
                {
                    issues.Add(ContentIssue.Error(
                        ContentIssueCodes.ManifestIdNotRegistered,
                        fileName,
                        "New ID '" + id + "' is not registered. Add \"" + id + "\" to the 'ids' list of " + ContentFileNames.IdManifest + "."));
                }
            }
        }

        private static void ValidateProduct(
            ProductDefinition product,
            ContentLoadOptions options,
            string fileName,
            HashSet<string> seenIds,
            ICollection<ContentIssue> issues)
        {
            string label = string.IsNullOrEmpty(product.Id) ? "(product without id)" : product.Id;

            // ID
            bool idValid = !string.IsNullOrEmpty(product.Id) && IdFormat.IsMatch(product.Id);
            if (!idValid)
            {
                issues.Add(ContentIssue.Error(
                    ContentIssueCodes.ProductIdFormat,
                    fileName,
                    label + ": id must look like 'sector.name' using lowercase letters, digits and underscores (e.g. phone.elma_e13_pro)."));
            }
            else if (!seenIds.Add(product.Id))
            {
                issues.Add(ContentIssue.Error(ContentIssueCodes.ProductIdDuplicate, fileName, label + ": duplicate id."));
            }

            // Sektör
            if (!Contains(options.AllowedSectors, product.Sector))
            {
                issues.Add(ContentIssue.Error(
                    ContentIssueCodes.ProductSectorUnknown, fileName, label + ": sector '" + product.Sector + "' is not allowed."));
            }
            else if (idValid && !product.Id.StartsWith(product.Sector + ".", StringComparison.Ordinal))
            {
                issues.Add(ContentIssue.Error(
                    ContentIssueCodes.ProductIdSectorMismatch,
                    fileName,
                    label + ": id must start with its sector '" + product.Sector + ".'."));
            }

            // Metinler
            if (string.IsNullOrWhiteSpace(product.Name))
            {
                issues.Add(ContentIssue.Error(ContentIssueCodes.ProductNameEmpty, fileName, label + ": name is empty."));
            }

            if (string.IsNullOrWhiteSpace(product.Brand))
            {
                issues.Add(ContentIssue.Error(ContentIssueCodes.ProductBrandEmpty, fileName, label + ": brand is empty."));
            }

            // Çıkış yılı
            if (product.ReleaseYear < MinReleaseYear || product.ReleaseYear > MaxReleaseYear)
            {
                issues.Add(ContentIssue.Error(
                    ContentIssueCodes.ProductReleaseYearRange,
                    fileName,
                    label + ": releaseYear " + product.ReleaseYear + " is outside " + MinReleaseYear + ".." + MaxReleaseYear + "."));
            }

            // Baz fiyat
            if (!product.BasePrice.IsPositive)
            {
                issues.Add(ContentIssue.Error(
                    ContentIssueCodes.ProductBasePriceNotPositive, fileName, label + ": basePrice must be greater than 0."));
            }
            else if (!product.BasePrice.IsRoundedTo10)
            {
                issues.Add(ContentIssue.Error(
                    ContentIssueCodes.ProductBasePriceNotRounded,
                    fileName,
                    label + ": basePrice " + product.BasePrice.Tl + " must be a multiple of 10 TL."));
            }

            ValidateStorage(product, label, fileName, issues);

            // Yaş aralığı
            if (product.MinAgeMonths < 0 || product.MaxAgeMonths < 0)
            {
                issues.Add(ContentIssue.Error(ContentIssueCodes.ProductAgeNegative, fileName, label + ": ageMonths cannot be negative."));
            }
            else if (product.MinAgeMonths > product.MaxAgeMonths)
            {
                issues.Add(ContentIssue.Error(
                    ContentIssueCodes.ProductAgeMinGreaterThanMax,
                    fileName,
                    label + ": ageMonths.min (" + product.MinAgeMonths + ") is greater than max (" + product.MaxAgeMonths + ")."));
            }
        }

        private static void ValidateStorage(ProductDefinition product, string label, string fileName, ICollection<ContentIssue> issues)
        {
            IReadOnlyList<StorageOption> storage = product.StorageOptions;
            if (storage.Count == 0)
            {
                issues.Add(ContentIssue.Error(ContentIssueCodes.ProductStorageEmpty, fileName, label + ": storageOptions is empty."));
                return;
            }

            var seenGb = new HashSet<int>();
            StorageOption baseOption = null;
            for (int i = 0; i < storage.Count; i++)
            {
                StorageOption option = storage[i];

                if (option.Gb <= 0)
                {
                    issues.Add(ContentIssue.Error(
                        ContentIssueCodes.ProductStorageGbNotPositive, fileName, label + ": storage option " + option.Gb + " GB must be positive."));
                }
                else if (!seenGb.Add(option.Gb))
                {
                    issues.Add(ContentIssue.Error(
                        ContentIssueCodes.ProductStorageDuplicate, fileName, label + ": storage option " + option.Gb + " GB is listed more than once."));
                }

                if (!(option.Multiplier > 0.0))
                {
                    issues.Add(ContentIssue.Error(
                        ContentIssueCodes.ProductStorageMultNotPositive,
                        fileName,
                        label + ": storage option " + option.Gb + " GB has a non-positive multiplier."));
                }

                if (option.Gb == product.BaseStorageGb && baseOption == null)
                {
                    baseOption = option;
                }
            }

            if (baseOption == null)
            {
                issues.Add(ContentIssue.Error(
                    ContentIssueCodes.ProductStorageBaseMissing,
                    fileName,
                    label + ": baseStorageGb " + product.BaseStorageGb + " is not one of the storage options."));
            }
            else if (Math.Abs(baseOption.Multiplier - 1.0) > MultiplierTolerance)
            {
                issues.Add(ContentIssue.Error(
                    ContentIssueCodes.ProductStorageBaseMultNotOne,
                    fileName,
                    label + ": the base storage option (" + baseOption.Gb + " GB) must have multiplier 1.0."));
            }
        }

        private static bool Contains(ICollection<string> values, string value)
        {
            if (value == null)
            {
                return false;
            }

            foreach (string candidate in values)
            {
                if (string.Equals(candidate, value, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
