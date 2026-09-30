using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Esnaf.Domain.Phone;
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

        /// <summary>Çarpan tablolarının kuralları (GDD v0.2 4.2). Tüm sorunlar toplanır.</summary>
        public static void ValidateValueTables(ValueTables tables, string fileName, ICollection<ContentIssue> issues)
        {
            // Yaş bantları
            IReadOnlyList<AgeBand> bands = tables.AgeBands;
            if (bands.Count == 0)
            {
                issues.Add(ContentIssue.Error(ContentIssueCodes.ValueTablesAgeEmpty, fileName, "age: at least one band is required."));
            }
            else
            {
                if (bands[0].FromMonths != 0)
                {
                    issues.Add(ContentIssue.Error(
                        ContentIssueCodes.ValueTablesAgeFirstNotZero,
                        fileName,
                        "age: the first band must start at 0 months (found " + bands[0].FromMonths + ")."));
                }

                for (int i = 0; i < bands.Count; i++)
                {
                    if (i > 0 && bands[i].FromMonths <= bands[i - 1].FromMonths)
                    {
                        issues.Add(ContentIssue.Error(
                            ContentIssueCodes.ValueTablesAgeNotIncreasing,
                            fileName,
                            "age: band " + i + " (fromMonths " + bands[i].FromMonths + ") must start after the previous band (" + bands[i - 1].FromMonths + ")."));
                    }

                    if (!(bands[i].Multiplier > 0.0))
                    {
                        issues.Add(ContentIssue.Error(
                            ContentIssueCodes.ValueTablesAgeMultNotPositive, fileName, "age: band " + i + " multiplier must be greater than 0."));
                    }
                }
            }

            // Pil: 0..100 ve %0 pilde bile çarpan pozitif kalmalı
            if (tables.BatteryFullAtOrAbove < 0
                || tables.BatteryFullAtOrAbove > 100
                || !(tables.BatteryPenaltyPerPoint > 0.0)
                || !(1.0 - tables.BatteryPenaltyPerPoint * tables.BatteryFullAtOrAbove > 0.0))
            {
                issues.Add(ContentIssue.Error(
                    ContentIssueCodes.ValueTablesBatteryInvalid,
                    fileName,
                    "battery: fullAtOrAbove must be 0..100, penaltyPerPoint > 0, and the multiplier at 0% must stay positive."));
            }

            // Kasa
            if (!(tables.BodyBase > 0.0) || !(tables.BodySpan >= 0.0))
            {
                issues.Add(ContentIssue.Error(
                    ContentIssueCodes.ValueTablesBodyInvalid, fileName, "body: base must be > 0 and span must be >= 0."));
            }

            ValidateIdMultipliers(
                tables.ScreenMultipliers,
                PhoneAttributes.RequiredScreenIds,
                "screen",
                ContentIssueCodes.ValueTablesScreenMissingId,
                ContentIssueCodes.ValueTablesScreenDuplicate,
                ContentIssueCodes.ValueTablesScreenMultNotPositive,
                fileName,
                issues);
            ValidateIdMultipliers(
                tables.CameraMultipliers,
                PhoneAttributes.RequiredCameraIds,
                "camera",
                ContentIssueCodes.ValueTablesCameraMissingId,
                ContentIssueCodes.ValueTablesCameraDuplicate,
                ContentIssueCodes.ValueTablesCameraMultNotPositive,
                fileName,
                issues);

            // Paket
            if (!(tables.BoxBonus >= 0.0) || !(tables.InvoiceBonus >= 0.0))
            {
                issues.Add(ContentIssue.Error(
                    ContentIssueCodes.ValueTablesPackageNegative, fileName, "package: boxBonus and invoiceBonus cannot be negative."));
            }
        }

        /// <summary>
        /// Durum profillerinin kuralları (GDD v0.2 4.3). Ekran/kamera değerleri çarpan tablolarıyla ÇAPRAZ denetlenir.
        /// </summary>
        public static void ValidateConditionProfiles(
            IReadOnlyList<ConditionProfile> profiles,
            ValueTables tables,
            string fileName,
            ICollection<ContentIssue> issues)
        {
            if (profiles.Count == 0)
            {
                issues.Add(ContentIssue.Error(ContentIssueCodes.ProfilesEmpty, fileName, "The file contains no condition profiles."));
                return;
            }

            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            bool anyOnDayOne = false;
            for (int i = 0; i < profiles.Count; i++)
            {
                ConditionProfile profile = profiles[i];
                string label = string.IsNullOrWhiteSpace(profile.Id) ? "profiles[" + i + "]" : profile.Id;

                if (string.IsNullOrWhiteSpace(profile.Id))
                {
                    issues.Add(ContentIssue.Error(ContentIssueCodes.ProfileIdEmpty, fileName, label + ": id is empty."));
                }
                else if (!seenIds.Add(profile.Id))
                {
                    issues.Add(ContentIssue.Error(ContentIssueCodes.ProfileIdDuplicate, fileName, label + ": duplicate id."));
                }

                if (profile.Weight <= 0)
                {
                    issues.Add(ContentIssue.Error(
                        ContentIssueCodes.ProfileWeightNotPositive, fileName, label + ": weight must be greater than 0."));
                }

                if (profile.AvailableFromDay < 1)
                {
                    issues.Add(ContentIssue.Error(
                        ContentIssueCodes.ProfileDayInvalid, fileName, label + ": availableFromDay must be at least 1."));
                }
                else if (profile.AvailableFromDay == 1)
                {
                    anyOnDayOne = true;
                }

                ValidateRange(profile.BatteryMin, profile.BatteryMax, label, "battery", fileName, issues);
                ValidateRange(profile.BodyMin, profile.BodyMax, label, "body", fileName, issues);

                ValidateChoices(profile.ScreenChoices, "screen", label, tables.TryGetScreenMultiplier, fileName, issues);
                ValidateChoices(profile.CameraChoices, "camera", label, tables.TryGetCameraMultiplier, fileName, issues);

                ValidateChance(profile.BoxChance, label, "boxChance", fileName, issues);
                ValidateChance(profile.InvoiceChance, label, "invoiceChance", fileName, issues);
            }

            if (!anyOnDayOne)
            {
                issues.Add(ContentIssue.Error(
                    ContentIssueCodes.ProfilesNoDayOne, fileName, "At least one profile must be available on day 1."));
            }
        }

        private static void ValidateIdMultipliers(
            IReadOnlyList<IdMultiplier> entries,
            IReadOnlyList<string> requiredIds,
            string section,
            string missingCode,
            string duplicateCode,
            string multiplierCode,
            string fileName,
            ICollection<ContentIssue> issues)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < entries.Count; i++)
            {
                IdMultiplier entry = entries[i];
                if (entry.Id == null || !seen.Add(entry.Id))
                {
                    issues.Add(ContentIssue.Error(duplicateCode, fileName, section + ": id '" + entry.Id + "' is empty or listed more than once."));
                }

                if (!(entry.Multiplier > 0.0))
                {
                    issues.Add(ContentIssue.Error(
                        multiplierCode, fileName, section + ": '" + entry.Id + "' multiplier must be greater than 0."));
                }
            }

            for (int i = 0; i < requiredIds.Count; i++)
            {
                if (!seen.Contains(requiredIds[i]))
                {
                    issues.Add(ContentIssue.Error(
                        missingCode, fileName, section + ": required id '" + requiredIds[i] + "' is missing."));
                }
            }
        }

        private delegate bool TryGetMultiplier(string id, out double multiplier);

        private static void ValidateRange(int min, int max, string label, string field, string fileName, ICollection<ContentIssue> issues)
        {
            if (min < 0 || max > 100 || min > max)
            {
                issues.Add(ContentIssue.Error(
                    ContentIssueCodes.ProfileRangeInvalid,
                    fileName,
                    label + ": " + field + " range " + min + ".." + max + " must satisfy 0 <= min <= max <= 100."));
            }
        }

        private static void ValidateChoices(
            IReadOnlyList<WeightedValue> choices,
            string field,
            string label,
            TryGetMultiplier lookup,
            string fileName,
            ICollection<ContentIssue> issues)
        {
            if (choices.Count == 0)
            {
                issues.Add(ContentIssue.Error(ContentIssueCodes.ProfileChoicesEmpty, fileName, label + ": " + field + " has no choices."));
                return;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < choices.Count; i++)
            {
                WeightedValue choice = choices[i];

                if (choice.Weight <= 0)
                {
                    issues.Add(ContentIssue.Error(
                        ContentIssueCodes.ProfileChoiceWeightNotPositive,
                        fileName,
                        label + ": " + field + " choice '" + choice.Value + "' needs a weight greater than 0."));
                }

                if (choice.Value == null || !seen.Add(choice.Value))
                {
                    issues.Add(ContentIssue.Error(
                        ContentIssueCodes.ProfileChoiceDuplicate,
                        fileName,
                        label + ": " + field + " choice '" + choice.Value + "' is empty or listed more than once."));
                }

                double ignored;
                if (!lookup(choice.Value, out ignored))
                {
                    issues.Add(ContentIssue.Error(
                        ContentIssueCodes.ProfileChoiceUnknownValue,
                        fileName,
                        label + ": " + field + " value '" + choice.Value + "' does not exist in value_tables.json."));
                }
            }
        }

        private static void ValidateChance(double chance, string label, string field, string fileName, ICollection<ContentIssue> issues)
        {
            if (!(chance >= 0.0 && chance <= 1.0))
            {
                issues.Add(ContentIssue.Error(
                    ContentIssueCodes.ProfileChanceRange, fileName, label + ": " + field + " must be between 0 and 1."));
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
