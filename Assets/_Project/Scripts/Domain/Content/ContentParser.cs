using System.Collections.Generic;
using Esnaf.Core;
using Esnaf.Domain.Products;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Esnaf.Domain.Content
{
    /// <summary>
    /// JSON metnini tanım nesnelerine çevirir. Unity'yi ve dosya sistemini bilmez.
    /// Yapısal bozukluklar (geçersiz JSON, bilinmeyen alan, yanlış tip, yanlış schemaVersion) dosyanın tamamını reddeder ve null döner.
    /// Anlamsal eksikler (eksik alan, geçersiz segment) model bazında raporlanır; hatalı modeller listeye alınmaz.
    /// Değer kurallarını (fiyat, ID biçimi...) <see cref="ContentValidator"/> denetler.
    /// </summary>
    public static class ContentParser
    {
        /// <returns>Yapısal hata varsa null; aksi halde (kısmi de olabilir) tanım listesi. Sorunlar <paramref name="issues"/>'a eklenir.</returns>
        public static IReadOnlyList<ProductDefinition> ParseProductModels(string fileName, string json, ICollection<ContentIssue> issues)
        {
            ProductModelsFileDto file = Deserialize<ProductModelsFileDto>(fileName, json, issues);
            if (file == null || !CheckSchemaVersion(fileName, file.SchemaVersion, issues))
            {
                return null;
            }

            if (file.Models == null)
            {
                issues.Add(FieldMissing(fileName, "models"));
                return null;
            }

            var products = new List<ProductDefinition>(file.Models.Count);
            for (int i = 0; i < file.Models.Count; i++)
            {
                ProductDefinition product = MapProduct(fileName, i, file.Models[i], issues);
                if (product != null)
                {
                    products.Add(product);
                }
            }

            return products;
        }

        /// <returns>Yapısal hata varsa null; aksi halde ID listesi (dosyadaki sırayla).</returns>
        public static IReadOnlyList<string> ParseIdManifest(string fileName, string json, ICollection<ContentIssue> issues)
        {
            IdManifestFileDto file = Deserialize<IdManifestFileDto>(fileName, json, issues);
            if (file == null || !CheckSchemaVersion(fileName, file.SchemaVersion, issues))
            {
                return null;
            }

            if (file.Ids == null)
            {
                issues.Add(FieldMissing(fileName, "ids"));
                return null;
            }

            var ids = new List<string>(file.Ids.Count);
            bool valid = true;
            for (int i = 0; i < file.Ids.Count; i++)
            {
                if (file.Ids[i] == null)
                {
                    issues.Add(FieldMissing(fileName, "ids[" + i + "]"));
                    valid = false;
                    continue;
                }

                ids.Add(file.Ids[i]);
            }

            return valid ? ids : null;
        }

        // ---- ortak ----

        private static T Deserialize<T>(string fileName, string json, ICollection<ContentIssue> issues) where T : class
        {
            if (json != null)
            {
                json = json.TrimStart('﻿'); // Windows editörlerinin eklediği BOM
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                issues.Add(ContentIssue.Error(ContentIssueCodes.FileEmpty, fileName, "File is empty."));
                return null;
            }

            var settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                MissingMemberHandling = MissingMemberHandling.Error, // yazım hatalı alan adı = hata
                CheckAdditionalContent = true,                        // kapanıştan sonra fazladan içerik = hata
                DateParseHandling = DateParseHandling.None
            };
            settings.Converters.Add(new StrictScalarConverter()); // 10000.5 / "10000" gibi sessiz dönüşümleri engeller

            T result;
            try
            {
                result = JsonConvert.DeserializeObject<T>(json, settings);
            }
            catch (JsonException ex)
            {
                issues.Add(ContentIssue.Error(ContentIssueCodes.FileSyntax, fileName, ex.Message));
                return null;
            }

            if (result == null)
            {
                issues.Add(ContentIssue.Error(ContentIssueCodes.FileSyntax, fileName, "Root must be a JSON object."));
                return null;
            }

            return result;
        }

        private static bool CheckSchemaVersion(string fileName, int? version, ICollection<ContentIssue> issues)
        {
            if (!version.HasValue)
            {
                issues.Add(ContentIssue.Error(
                    ContentIssueCodes.SchemaVersionMissing, fileName, "Missing 'schemaVersion' (expected " + ContentFileNames.SupportedSchemaVersion + ")."));
                return false;
            }

            if (version.Value != ContentFileNames.SupportedSchemaVersion)
            {
                issues.Add(ContentIssue.Error(
                    ContentIssueCodes.SchemaVersionUnsupported,
                    fileName,
                    "Unsupported schemaVersion " + version.Value + " (supported: " + ContentFileNames.SupportedSchemaVersion + ")."));
                return false;
            }

            return true;
        }

        private static ContentIssue FieldMissing(string fileName, string field)
        {
            return ContentIssue.Error(ContentIssueCodes.FieldMissing, fileName, "Missing required field '" + field + "'.");
        }

        // ---- ürün modeli ----

        private static ProductDefinition MapProduct(string fileName, int index, ProductModelDto dto, ICollection<ContentIssue> issues)
        {
            string label = "models[" + index + "]";
            if (dto == null)
            {
                issues.Add(FieldMissing(fileName, label));
                return null;
            }

            if (dto.Id != null)
            {
                label += " (" + dto.Id + ")";
            }

            bool ok = true;
            ok &= Require(fileName, label, "id", dto.Id != null, issues);
            ok &= Require(fileName, label, "sector", dto.Sector != null, issues);
            ok &= Require(fileName, label, "name", dto.Name != null, issues);
            ok &= Require(fileName, label, "brand", dto.Brand != null, issues);
            ok &= Require(fileName, label, "segment", dto.Segment != null, issues);
            ok &= Require(fileName, label, "releaseYear", dto.ReleaseYear.HasValue, issues);
            ok &= Require(fileName, label, "basePrice", dto.BasePrice.HasValue, issues);
            ok &= Require(fileName, label, "baseStorageGb", dto.BaseStorageGb.HasValue, issues);
            ok &= Require(fileName, label, "storageOptions", dto.StorageOptions != null, issues);
            ok &= Require(fileName, label, "ageMonths", dto.AgeMonths != null, issues);

            ProductSegment segment = ProductSegment.Entry;
            if (dto.Segment != null && !ProductSegments.TryParse(dto.Segment, out segment))
            {
                issues.Add(ContentIssue.Error(
                    ContentIssueCodes.ProductSegmentInvalid,
                    fileName,
                    label + ": invalid segment '" + dto.Segment + "' (allowed: entry, mid, upper)."));
                ok = false;
            }

            var options = new List<StorageOption>();
            if (dto.StorageOptions != null)
            {
                for (int j = 0; j < dto.StorageOptions.Count; j++)
                {
                    StorageOptionDto option = dto.StorageOptions[j];
                    string optionLabel = "storageOptions[" + j + "]";
                    if (option == null)
                    {
                        ok &= Require(fileName, label, optionLabel, false, issues);
                        continue;
                    }

                    bool optionOk = Require(fileName, label, optionLabel + ".gb", option.Gb.HasValue, issues);
                    optionOk &= Require(fileName, label, optionLabel + ".mult", option.Mult.HasValue, issues);
                    if (optionOk)
                    {
                        options.Add(new StorageOption(option.Gb.Value, option.Mult.Value));
                    }

                    ok &= optionOk;
                }
            }

            if (dto.AgeMonths != null)
            {
                ok &= Require(fileName, label, "ageMonths.min", dto.AgeMonths.Min.HasValue, issues);
                ok &= Require(fileName, label, "ageMonths.max", dto.AgeMonths.Max.HasValue, issues);
            }

            if (!ok)
            {
                return null;
            }

            return new ProductDefinition(
                dto.Id,
                dto.Sector,
                dto.Name,
                dto.Brand,
                segment,
                dto.ReleaseYear.Value,
                Money.FromTl(dto.BasePrice.Value),
                dto.BaseStorageGb.Value,
                options,
                dto.AgeMonths.Min.Value,
                dto.AgeMonths.Max.Value,
                dto.IconKey ?? dto.Id,
                dto.Deprecated ?? false);
        }

        private static bool Require(string fileName, string label, string field, bool present, ICollection<ContentIssue> issues)
        {
            if (!present)
            {
                issues.Add(ContentIssue.Error(
                    ContentIssueCodes.FieldMissing, fileName, label + ": missing required field '" + field + "'."));
            }

            return present;
        }
    }
}
