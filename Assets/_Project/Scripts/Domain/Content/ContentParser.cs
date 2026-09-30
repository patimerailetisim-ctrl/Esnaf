using System.Collections.Generic;
using Esnaf.Core;
using Esnaf.Domain.Appraisal;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Market;
using Esnaf.Domain.Npc;
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

        /// <returns>Yapısal veya eksik-alan hatası varsa null; aksi halde çarpan tabloları.</returns>
        public static ValueTables ParseValueTables(string fileName, string json, ICollection<ContentIssue> issues)
        {
            ValueTablesFileDto file = Deserialize<ValueTablesFileDto>(fileName, json, issues);
            if (file == null || !CheckSchemaVersion(fileName, file.SchemaVersion, issues))
            {
                return null;
            }

            const string root = "value tables";
            bool ok = true;

            var ageBands = new List<AgeBand>();
            if (Require(fileName, root, "age", file.Age != null, issues))
            {
                for (int i = 0; i < file.Age.Count; i++)
                {
                    AgeBandDto band = file.Age[i];
                    string field = "age[" + i + "]";
                    if (!Require(fileName, root, field, band != null, issues))
                    {
                        ok = false;
                        continue;
                    }

                    bool bandOk = Require(fileName, root, field + ".fromMonths", band.FromMonths.HasValue, issues);
                    bandOk &= Require(fileName, root, field + ".mult", band.Mult.HasValue, issues);
                    if (bandOk)
                    {
                        ageBands.Add(new AgeBand(band.FromMonths.Value, band.Mult.Value));
                    }

                    ok &= bandOk;
                }
            }
            else
            {
                ok = false;
            }

            int batteryFull = 0;
            double batteryPenalty = 0.0;
            if (Require(fileName, root, "battery", file.Battery != null, issues))
            {
                bool batteryOk = Require(fileName, root, "battery.fullAtOrAbove", file.Battery.FullAtOrAbove.HasValue, issues);
                batteryOk &= Require(fileName, root, "battery.penaltyPerPoint", file.Battery.PenaltyPerPoint.HasValue, issues);
                if (batteryOk)
                {
                    batteryFull = file.Battery.FullAtOrAbove.Value;
                    batteryPenalty = file.Battery.PenaltyPerPoint.Value;
                }

                ok &= batteryOk;
            }
            else
            {
                ok = false;
            }

            double bodyBase = 0.0;
            double bodySpan = 0.0;
            if (Require(fileName, root, "body", file.Body != null, issues))
            {
                bool bodyOk = Require(fileName, root, "body.base", file.Body.Base.HasValue, issues);
                bodyOk &= Require(fileName, root, "body.span", file.Body.Span.HasValue, issues);
                if (bodyOk)
                {
                    bodyBase = file.Body.Base.Value;
                    bodySpan = file.Body.Span.Value;
                }

                ok &= bodyOk;
            }
            else
            {
                ok = false;
            }

            List<IdMultiplier> screen = MapIdMultipliers(fileName, root, "screen", file.Screen, issues, ref ok);
            List<IdMultiplier> camera = MapIdMultipliers(fileName, root, "camera", file.Camera, issues, ref ok);

            double boxBonus = 0.0;
            double invoiceBonus = 0.0;
            if (Require(fileName, root, "package", file.Package != null, issues))
            {
                bool packageOk = Require(fileName, root, "package.boxBonus", file.Package.BoxBonus.HasValue, issues);
                packageOk &= Require(fileName, root, "package.invoiceBonus", file.Package.InvoiceBonus.HasValue, issues);
                if (packageOk)
                {
                    boxBonus = file.Package.BoxBonus.Value;
                    invoiceBonus = file.Package.InvoiceBonus.Value;
                }

                ok &= packageOk;
            }
            else
            {
                ok = false;
            }

            if (!ok)
            {
                return null;
            }

            return new ValueTables(ageBands, batteryFull, batteryPenalty, bodyBase, bodySpan, screen, camera, boxBonus, invoiceBonus);
        }

        /// <returns>Yapısal hata varsa null; aksi halde (kısmi de olabilir) profil listesi.</returns>
        public static IReadOnlyList<ConditionProfile> ParseConditionProfiles(string fileName, string json, ICollection<ContentIssue> issues)
        {
            ConditionProfilesFileDto file = Deserialize<ConditionProfilesFileDto>(fileName, json, issues);
            if (file == null || !CheckSchemaVersion(fileName, file.SchemaVersion, issues))
            {
                return null;
            }

            if (file.Profiles == null)
            {
                issues.Add(FieldMissing(fileName, "profiles"));
                return null;
            }

            var profiles = new List<ConditionProfile>(file.Profiles.Count);
            for (int i = 0; i < file.Profiles.Count; i++)
            {
                ConditionProfile profile = MapProfile(fileName, i, file.Profiles[i], issues);
                if (profile != null)
                {
                    profiles.Add(profile);
                }
            }

            return profiles;
        }

        /// <returns>Yapısal hata varsa null; aksi halde (kısmi de olabilir) defter türleri listesi.</returns>
        public static IReadOnlyList<TransactionType> ParseTransactionTypes(string fileName, string json, ICollection<ContentIssue> issues)
        {
            TransactionTypesFileDto file = Deserialize<TransactionTypesFileDto>(fileName, json, issues);
            if (file == null || !CheckSchemaVersion(fileName, file.SchemaVersion, issues))
            {
                return null;
            }

            if (file.Types == null)
            {
                issues.Add(FieldMissing(fileName, "types"));
                return null;
            }

            var types = new List<TransactionType>(file.Types.Count);
            for (int i = 0; i < file.Types.Count; i++)
            {
                TransactionType type = MapTransactionType(fileName, i, file.Types[i], issues);
                if (type != null)
                {
                    types.Add(type);
                }
            }

            return types;
        }

        /// <returns>Yapısal veya eksik-alan hatası varsa null; aksi halde ekonomi sabitleri.</returns>
        public static EconomyConstants ParseEconomyConstants(string fileName, string json, ICollection<ContentIssue> issues)
        {
            EconomyConstantsFileDto file = Deserialize<EconomyConstantsFileDto>(fileName, json, issues);
            if (file == null || !CheckSchemaVersion(fileName, file.SchemaVersion, issues))
            {
                return null;
            }

            const string root = "economy constants";
            bool ok = Require(fileName, root, "openingCapital", file.OpeningCapital.HasValue, issues);
            ok &= Require(fileName, root, "initialShelfCapacity", file.InitialShelfCapacity.HasValue, issues);

            bool expenseOk = Require(fileName, root, "dailyExpense", file.DailyExpense != null, issues);
            if (expenseOk)
            {
                expenseOk &= Require(fileName, root, "dailyExpense.fromDay", file.DailyExpense.FromDay.HasValue, issues);
                expenseOk &= Require(fileName, root, "dailyExpense.amount", file.DailyExpense.Amount.HasValue, issues);
            }

            if (!ok || !expenseOk)
            {
                return null;
            }

            return new EconomyConstants(
                Money.FromTl(file.OpeningCapital.Value),
                file.DailyExpense.FromDay.Value,
                Money.FromTl(file.DailyExpense.Amount.Value),
                file.InitialShelfCapacity.Value);
        }

        /// <returns>Yapısal veya eksik-alan hatası varsa null; aksi halde NPC tanımları (dosyadaki sırayla).</returns>
        public static IReadOnlyList<NpcDefinition> ParseNpcs(string fileName, string json, ICollection<ContentIssue> issues)
        {
            NpcProfilesFileDto file = Deserialize<NpcProfilesFileDto>(fileName, json, issues);
            if (file == null || !CheckSchemaVersion(fileName, file.SchemaVersion, issues))
            {
                return null;
            }

            if (file.Npcs == null)
            {
                issues.Add(FieldMissing(fileName, "npcs"));
                return null;
            }

            var npcs = new List<NpcDefinition>(file.Npcs.Count);
            bool allOk = true;
            for (int i = 0; i < file.Npcs.Count; i++)
            {
                NpcDefinition npc = MapNpc(fileName, i, file.Npcs[i], issues);
                if (npc != null)
                {
                    npcs.Add(npc);
                }
                else
                {
                    allOk = false;
                }
            }

            return allOk ? npcs : null;
        }

        /// <returns>Yapısal veya eksik-alan hatası varsa null; aksi halde economy_constants.json içindeki "market" bölümü.</returns>
        public static MarketConstants ParseMarketConstants(string fileName, string json, ICollection<ContentIssue> issues)
        {
            EconomyConstantsFileDto file = Deserialize<EconomyConstantsFileDto>(fileName, json, issues);
            if (file == null || !CheckSchemaVersion(fileName, file.SchemaVersion, issues))
            {
                return null;
            }

            if (!Require(fileName, "economy constants", "market", file.Market != null, issues))
            {
                return null;
            }

            return MapMarket(fileName, file.Market, issues);
        }

        /// <returns>Yapısal veya eksik-alan hatası varsa null; aksi halde ekspertiz kuralları.</returns>
        public static AppraisalConfig ParseAppraisal(string fileName, string json, ICollection<ContentIssue> issues)
        {
            AppraisalFileDto file = Deserialize<AppraisalFileDto>(fileName, json, issues);
            if (file == null || !CheckSchemaVersion(fileName, file.SchemaVersion, issues))
            {
                return null;
            }

            const string root = "appraisal";
            bool ok = true;

            var levels = new List<AppraisalLevel>();
            if (Require(fileName, root, "levels", file.Levels != null, issues))
            {
                for (int i = 0; i < file.Levels.Count; i++)
                {
                    AppraisalLevel level = MapAppraisalLevel(fileName, root, i, file.Levels[i], issues);
                    if (level != null)
                    {
                        levels.Add(level);
                    }
                    else
                    {
                        ok = false;
                    }
                }
            }
            else
            {
                ok = false;
            }

            var checks = new List<AppraisalCheck>();
            if (Require(fileName, root, "checks", file.Checks != null, issues))
            {
                for (int i = 0; i < file.Checks.Count; i++)
                {
                    AppraisalCheckDto dto = file.Checks[i];
                    string f = "checks[" + i + "]";
                    bool entryOk = Require(fileName, root, f, dto != null, issues);
                    if (entryOk)
                    {
                        entryOk &= Require(fileName, root, f + ".attribute", dto.Attribute != null, issues);
                        entryOk &= Require(fileName, root, f + ".defectValues", dto.DefectValues != null, issues);
                        entryOk &= Require(fileName, root, f + ".falseAlarmValue", dto.FalseAlarmValue != null, issues);
                        entryOk &= Require(fileName, root, f + ".cleanValue", dto.CleanValue != null, issues);
                        entryOk &= Require(fileName, root, f + ".wordingKey", dto.WordingKey != null, issues);
                    }

                    if (entryOk)
                    {
                        checks.Add(new AppraisalCheck(dto.Attribute, dto.DefectValues, dto.FalseAlarmValue, dto.CleanValue, dto.WordingKey));
                    }

                    ok &= entryOk;
                }
            }
            else
            {
                ok = false;
            }

            bool riskOk = Require(fileName, root, "riskCard", file.RiskCard != null, issues);
            if (riskOk)
            {
                riskOk &= Require(fileName, root, "riskCard.expectedSaleFactor", file.RiskCard.ExpectedSaleFactor.HasValue, issues);
            }

            if (!ok || !riskOk)
            {
                return null;
            }

            return new AppraisalConfig(levels, checks, file.RiskCard.ExpectedSaleFactor.Value);
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

        private static List<IdMultiplier> MapIdMultipliers(
            string fileName, string label, string field, List<IdMultiplierDto> dtos, ICollection<ContentIssue> issues, ref bool ok)
        {
            var result = new List<IdMultiplier>();
            if (!Require(fileName, label, field, dtos != null, issues))
            {
                ok = false;
                return result;
            }

            for (int i = 0; i < dtos.Count; i++)
            {
                IdMultiplierDto dto = dtos[i];
                string entry = field + "[" + i + "]";
                if (!Require(fileName, label, entry, dto != null, issues))
                {
                    ok = false;
                    continue;
                }

                bool entryOk = Require(fileName, label, entry + ".id", dto.Id != null, issues);
                entryOk &= Require(fileName, label, entry + ".mult", dto.Mult.HasValue, issues);
                if (entryOk)
                {
                    result.Add(new IdMultiplier(dto.Id, dto.Mult.Value));
                }

                ok &= entryOk;
            }

            return result;
        }

        private static ConditionProfile MapProfile(string fileName, int index, ConditionProfileDto dto, ICollection<ContentIssue> issues)
        {
            string label = "profiles[" + index + "]";
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
            ok &= Require(fileName, label, "name", dto.Name != null, issues);
            ok &= Require(fileName, label, "weight", dto.Weight.HasValue, issues);
            ok &= Require(fileName, label, "availableFromDay", dto.AvailableFromDay.HasValue, issues);
            ok &= Require(fileName, label, "boxChance", dto.BoxChance.HasValue, issues);
            ok &= Require(fileName, label, "invoiceChance", dto.InvoiceChance.HasValue, issues);

            int batteryMin = 0;
            int batteryMax = 0;
            ok &= MapRange(fileName, label, "battery", dto.Battery, issues, out batteryMin, out batteryMax);
            int bodyMin = 0;
            int bodyMax = 0;
            ok &= MapRange(fileName, label, "body", dto.Body, issues, out bodyMin, out bodyMax);

            List<WeightedValue> screen = MapChoices(fileName, label, "screen", dto.Screen, issues, ref ok);
            List<WeightedValue> camera = MapChoices(fileName, label, "camera", dto.Camera, issues, ref ok);

            if (!ok)
            {
                return null;
            }

            return new ConditionProfile(
                dto.Id,
                dto.Name,
                dto.Weight.Value,
                dto.AvailableFromDay.Value,
                batteryMin,
                batteryMax,
                bodyMin,
                bodyMax,
                screen,
                camera,
                dto.BoxChance.Value,
                dto.InvoiceChance.Value);
        }

        private static bool MapRange(
            string fileName, string label, string field, IntRangeDto range, ICollection<ContentIssue> issues, out int min, out int max)
        {
            min = 0;
            max = 0;
            if (!Require(fileName, label, field, range != null, issues))
            {
                return false;
            }

            bool ok = Require(fileName, label, field + ".min", range.Min.HasValue, issues);
            ok &= Require(fileName, label, field + ".max", range.Max.HasValue, issues);
            if (ok)
            {
                min = range.Min.Value;
                max = range.Max.Value;
            }

            return ok;
        }

        private static List<WeightedValue> MapChoices(
            string fileName, string label, string field, List<WeightedValueDto> dtos, ICollection<ContentIssue> issues, ref bool ok)
        {
            var result = new List<WeightedValue>();
            if (!Require(fileName, label, field, dtos != null, issues))
            {
                ok = false;
                return result;
            }

            for (int i = 0; i < dtos.Count; i++)
            {
                WeightedValueDto dto = dtos[i];
                string entry = field + "[" + i + "]";
                if (!Require(fileName, label, entry, dto != null, issues))
                {
                    ok = false;
                    continue;
                }

                bool entryOk = Require(fileName, label, entry + ".value", dto.Value != null, issues);
                entryOk &= Require(fileName, label, entry + ".weight", dto.Weight.HasValue, issues);
                if (entryOk)
                {
                    result.Add(new WeightedValue(dto.Value, dto.Weight.Value));
                }

                ok &= entryOk;
            }

            return result;
        }

        private static TransactionType MapTransactionType(string fileName, int index, TransactionTypeDto dto, ICollection<ContentIssue> issues)
        {
            string label = "types[" + index + "]";
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
            ok &= Require(fileName, label, "displayKey", dto.DisplayKey != null, issues);
            ok &= Require(fileName, label, "category", dto.Category != null, issues);
            ok &= Require(fileName, label, "direction", dto.Direction != null, issues);
            ok &= Require(fileName, label, "profitEffect", dto.ProfitEffect != null, issues);

            TransactionCategory category = TransactionCategory.Capital;
            if (dto.Category != null && !TransactionTypeEnums.TryParseCategory(dto.Category, out category))
            {
                issues.Add(ContentIssue.Error(
                    ContentIssueCodes.TransactionTypeCategoryInvalid,
                    fileName,
                    label + ": invalid category '" + dto.Category + "' (allowed: capital, trade, expense, investment)."));
                ok = false;
            }

            TransactionDirection direction = TransactionDirection.Inflow;
            if (dto.Direction != null && !TransactionTypeEnums.TryParseDirection(dto.Direction, out direction))
            {
                issues.Add(ContentIssue.Error(
                    ContentIssueCodes.TransactionTypeDirectionInvalid,
                    fileName,
                    label + ": invalid direction '" + dto.Direction + "' (allowed: inflow, outflow, neutral)."));
                ok = false;
            }

            ProfitEffect effect = ProfitEffect.None;
            if (dto.ProfitEffect != null && !TransactionTypeEnums.TryParseProfitEffect(dto.ProfitEffect, out effect))
            {
                issues.Add(ContentIssue.Error(
                    ContentIssueCodes.TransactionTypeProfitEffectInvalid,
                    fileName,
                    label + ": invalid profitEffect '" + dto.ProfitEffect + "' (allowed: none, expense, sale, write_off)."));
                ok = false;
            }

            if (!ok)
            {
                return null;
            }

            return new TransactionType(dto.Id, dto.DisplayKey, category, direction, effect);
        }

        private static NpcDefinition MapNpc(string fileName, int index, NpcDto dto, ICollection<ContentIssue> issues)
        {
            string label = "npcs[" + index + "]";
            if (dto == null)
            {
                issues.Add(FieldMissing(fileName, label));
                return null;
            }

            if (dto.Id != null)
            {
                label += " (" + dto.Id + ")";
            }

            bool ok = Require(fileName, label, "id", dto.Id != null, issues);
            ok &= Require(fileName, label, "name", dto.Name != null, issues);
            ok &= Require(fileName, label, "personality", dto.Personality != null, issues);

            NpcSellerRole seller = null;
            if (Require(fileName, label, "seller", dto.Seller != null, issues))
            {
                seller = MapSeller(fileName, label, dto.Seller, issues);
            }

            NpcCustomerRole customer = null;
            if (Require(fileName, label, "customer", dto.Customer != null, issues))
            {
                customer = MapCustomer(fileName, label, dto.Customer, issues);
            }

            if (!ok || seller == null || customer == null)
            {
                return null;
            }

            return new NpcDefinition(dto.Id, dto.Name, dto.Personality, seller, customer);
        }

        private static NpcSellerRole MapSeller(string fileName, string label, NpcSellerDto dto, ICollection<ContentIssue> issues)
        {
            bool ok = Require(fileName, label, "seller.availableFromDay", dto.AvailableFromDay.HasValue, issues);
            ok &= Require(fileName, label, "seller.askMultiplier", dto.AskMultiplier.HasValue, issues);
            ok &= Require(fileName, label, "seller.rejectRatio", dto.RejectRatio.HasValue, issues);
            ok &= Require(fileName, label, "seller.patience", dto.Patience.HasValue, issues);
            ok &= Require(fileName, label, "seller.valueSigma", dto.ValueSigma.HasValue, issues);
            ok &= Require(fileName, label, "seller.urgency", dto.Urgency.HasValue, issues);
            ok &= Require(fileName, label, "seller.persuasion", dto.Persuasion.HasValue, issues);
            if (!ok)
            {
                return null;
            }

            return new NpcSellerRole(
                dto.AvailableFromDay.Value,
                dto.AskMultiplier.Value,
                dto.RejectRatio.Value,
                dto.Patience.Value,
                dto.ValueSigma.Value,
                dto.ValueBias ?? 0.0,
                dto.Urgency.Value,
                dto.Persuasion.Value,
                dto.ConcealChance ?? 0.0,
                dto.LearningFriendly ?? false,
                dto.UrgentLabelFromDay);
        }

        private static NpcCustomerRole MapCustomer(string fileName, string label, NpcCustomerDto dto, ICollection<ContentIssue> issues)
        {
            bool ok = Require(fileName, label, "customer.openingOfferRatio", dto.OpeningOfferRatio.HasValue, issues);
            ok &= Require(fileName, label, "customer.valueRatio", dto.ValueRatio.HasValue, issues);
            ok &= Require(fileName, label, "customer.patience", dto.Patience.HasValue, issues);
            if (!ok)
            {
                return null;
            }

            return new NpcCustomerRole(
                dto.OpeningOfferRatio.Value,
                dto.ValueRatio.Value,
                dto.Patience.Value,
                dto.ValueSigma ?? 0.0,
                dto.PackageRatio ?? 1.0);
        }

        private static MarketConstants MapMarket(string fileName, MarketDto dto, ICollection<ContentIssue> issues)
        {
            const string root = "market";
            bool ok = true;

            var counts = new List<ListingCountBand>();
            if (Require(fileName, root, "listingCounts", dto.ListingCounts != null, issues))
            {
                for (int i = 0; i < dto.ListingCounts.Count; i++)
                {
                    ListingCountDto e = dto.ListingCounts[i];
                    string f = "listingCounts[" + i + "]";
                    bool entryOk = Require(fileName, root, f, e != null, issues);
                    if (entryOk)
                    {
                        entryOk &= Require(fileName, root, f + ".fromDay", e.FromDay.HasValue, issues);
                        entryOk &= Require(fileName, root, f + ".min", e.Min.HasValue, issues);
                        entryOk &= Require(fileName, root, f + ".max", e.Max.HasValue, issues);
                    }

                    if (entryOk)
                    {
                        counts.Add(new ListingCountBand(e.FromDay.Value, e.Min.Value, e.Max.Value));
                    }

                    ok &= entryOk;
                }
            }
            else
            {
                ok = false;
            }

            bool lifetimeOk = Require(fileName, root, "listingLifetimeDays", dto.ListingLifetimeDays != null, issues);
            if (lifetimeOk)
            {
                lifetimeOk &= Require(fileName, root, "listingLifetimeDays.min", dto.ListingLifetimeDays.Min.HasValue, issues);
                lifetimeOk &= Require(fileName, root, "listingLifetimeDays.max", dto.ListingLifetimeDays.Max.HasValue, issues);
            }

            ok &= lifetimeOk;

            var weights = new List<SegmentWeightBand>();
            if (Require(fileName, root, "segmentWeights", dto.SegmentWeights != null, issues))
            {
                for (int i = 0; i < dto.SegmentWeights.Count; i++)
                {
                    SegmentWeightDto e = dto.SegmentWeights[i];
                    string f = "segmentWeights[" + i + "]";
                    bool entryOk = Require(fileName, root, f, e != null, issues);
                    if (entryOk)
                    {
                        entryOk &= Require(fileName, root, f + ".fromDay", e.FromDay.HasValue, issues);
                        entryOk &= Require(fileName, root, f + ".entry", e.Entry.HasValue, issues);
                        entryOk &= Require(fileName, root, f + ".mid", e.Mid.HasValue, issues);
                        entryOk &= Require(fileName, root, f + ".upper", e.Upper.HasValue, issues);
                    }

                    if (entryOk)
                    {
                        weights.Add(new SegmentWeightBand(e.FromDay.Value, e.Entry.Value, e.Mid.Value, e.Upper.Value));
                    }

                    ok &= entryOk;
                }
            }
            else
            {
                ok = false;
            }

            var availability = new List<ModelAvailabilityRule>();
            if (Require(fileName, root, "modelAvailability", dto.ModelAvailability != null, issues))
            {
                for (int i = 0; i < dto.ModelAvailability.Count; i++)
                {
                    ModelAvailabilityDto e = dto.ModelAvailability[i];
                    string f = "modelAvailability[" + i + "]";
                    bool entryOk = Require(fileName, root, f, e != null, issues);
                    if (entryOk)
                    {
                        entryOk &= Require(fileName, root, f + ".id", e.Id != null, issues);
                        entryOk &= Require(fileName, root, f + ".fromDay", e.FromDay.HasValue, issues);
                    }

                    if (entryOk)
                    {
                        availability.Add(new ModelAvailabilityRule(e.Id, e.FromDay.Value));
                    }

                    ok &= entryOk;
                }
            }
            else
            {
                ok = false;
            }

            bool friendlyOk = Require(fileName, root, "learningFriendlySellers", dto.LearningFriendlySellers != null, issues);
            if (friendlyOk)
            {
                friendlyOk &= Require(fileName, root, "learningFriendlySellers.untilDay", dto.LearningFriendlySellers.UntilDay.HasValue, issues);
                friendlyOk &= Require(fileName, root, "learningFriendlySellers.share", dto.LearningFriendlySellers.Share.HasValue, issues);
            }

            ok &= friendlyOk;

            bool opportunityOk = Require(fileName, root, "opportunity", dto.Opportunity != null, issues);
            if (opportunityOk)
            {
                opportunityOk &= Require(fileName, root, "opportunity.fromDay", dto.Opportunity.FromDay.HasValue, issues);
                opportunityOk &= Require(fileName, root, "opportunity.minPerDay", dto.Opportunity.MinPerDay.HasValue, issues);
                opportunityOk &= Require(fileName, root, "opportunity.maxRejectRatio", dto.Opportunity.MaxRejectRatio.HasValue, issues);
            }

            ok &= opportunityOk;

            var jackpotBands = new List<QuotaBand>();
            bool jackpotOk = Require(fileName, root, "jackpot", dto.Jackpot != null, issues);
            if (jackpotOk)
            {
                jackpotOk &= Require(fileName, root, "jackpot.rejectRatioBelow", dto.Jackpot.RejectRatioBelow.HasValue, issues);
                jackpotOk &= MapQuotaBands(fileName, root, "jackpot.maxPerDay", dto.Jackpot.MaxPerDay, jackpotBands, issues);
            }

            ok &= jackpotOk;

            var trapBands = new List<QuotaBand>();
            bool trapOk = Require(fileName, root, "trap", dto.Trap != null, issues);
            if (trapOk)
            {
                trapOk &= Require(fileName, root, "trap.fromDay", dto.Trap.FromDay.HasValue, issues);
                trapOk &= Require(fileName, root, "trap.minPerDay", dto.Trap.MinPerDay.HasValue, issues);
                trapOk &= MapQuotaBands(fileName, root, "trap.maxPerDay", dto.Trap.MaxPerDay, trapBands, issues);
                trapOk &= Require(fileName, root, "trap.valueRatio", dto.Trap.ValueRatio.HasValue, issues);
            }

            ok &= trapOk;

            ok &= Require(fileName, root, "askingPriceStep", dto.AskingPriceStep.HasValue, issues);

            var hidden = new List<HiddenDefectRule>();
            if (Require(fileName, root, "hiddenDefects", dto.HiddenDefects != null, issues))
            {
                for (int i = 0; i < dto.HiddenDefects.Count; i++)
                {
                    HiddenDefectDto e = dto.HiddenDefects[i];
                    string f = "hiddenDefects[" + i + "]";
                    bool entryOk = Require(fileName, root, f, e != null, issues);
                    if (entryOk)
                    {
                        entryOk &= Require(fileName, root, f + ".attribute", e.Attribute != null, issues);
                        entryOk &= Require(fileName, root, f + ".hiddenValues", e.HiddenValues != null, issues);
                        entryOk &= Require(fileName, root, f + ".cleanValue", e.CleanValue != null, issues);
                    }

                    if (entryOk)
                    {
                        hidden.Add(new HiddenDefectRule(e.Attribute, e.HiddenValues, e.CleanValue));
                    }

                    ok &= entryOk;
                }
            }
            else
            {
                ok = false;
            }

            GuidedListingSpec guided = null;
            if (Require(fileName, root, "guidedListing", dto.GuidedListing != null, issues))
            {
                guided = MapGuided(fileName, root, dto.GuidedListing, issues);
            }

            if (!ok || guided == null)
            {
                return null;
            }

            return new MarketConstants(
                counts,
                dto.ListingLifetimeDays.Min.Value,
                dto.ListingLifetimeDays.Max.Value,
                weights,
                availability,
                dto.LearningFriendlySellers.UntilDay.Value,
                dto.LearningFriendlySellers.Share.Value,
                dto.Opportunity.FromDay.Value,
                dto.Opportunity.MinPerDay.Value,
                dto.Opportunity.MaxRejectRatio.Value,
                dto.Jackpot.RejectRatioBelow.Value,
                jackpotBands,
                dto.Trap.FromDay.Value,
                dto.Trap.MinPerDay.Value,
                trapBands,
                dto.Trap.ValueRatio.Value,
                dto.AskingPriceStep.Value,
                hidden,
                guided);
        }

        private static bool MapQuotaBands(
            string fileName,
            string root,
            string field,
            List<QuotaDto> dtos,
            List<QuotaBand> result,
            ICollection<ContentIssue> issues)
        {
            if (!Require(fileName, root, field, dtos != null, issues))
            {
                return false;
            }

            bool ok = true;
            for (int i = 0; i < dtos.Count; i++)
            {
                QuotaDto e = dtos[i];
                string f = field + "[" + i + "]";
                bool entryOk = Require(fileName, root, f, e != null, issues);
                if (entryOk)
                {
                    entryOk &= Require(fileName, root, f + ".fromDay", e.FromDay.HasValue, issues);
                    entryOk &= Require(fileName, root, f + ".max", e.Max.HasValue, issues);
                }

                if (entryOk)
                {
                    result.Add(new QuotaBand(e.FromDay.Value, e.Max.Value));
                }

                ok &= entryOk;
            }

            return ok;
        }

        private static GuidedListingSpec MapGuided(string fileName, string root, GuidedListingDto dto, ICollection<ContentIssue> issues)
        {
            bool ok = Require(fileName, root, "guidedListing.sellerNpcId", dto.SellerNpcId != null, issues);
            ok &= Require(fileName, root, "guidedListing.definitionId", dto.DefinitionId != null, issues);
            ok &= Require(fileName, root, "guidedListing.storageGb", dto.StorageGb.HasValue, issues);
            ok &= Require(fileName, root, "guidedListing.ageMonths", dto.AgeMonths.HasValue, issues);
            ok &= Require(fileName, root, "guidedListing.battery", dto.Battery.HasValue, issues);
            ok &= Require(fileName, root, "guidedListing.body", dto.Body.HasValue, issues);
            ok &= Require(fileName, root, "guidedListing.screen", dto.Screen != null, issues);
            ok &= Require(fileName, root, "guidedListing.camera", dto.Camera != null, issues);
            ok &= Require(fileName, root, "guidedListing.box", dto.Box.HasValue, issues);
            ok &= Require(fileName, root, "guidedListing.invoice", dto.Invoice.HasValue, issues);
            ok &= Require(fileName, root, "guidedListing.rejectPrice", dto.RejectPrice.HasValue, issues);
            if (!ok)
            {
                return null;
            }

            return new GuidedListingSpec(
                dto.SellerNpcId,
                dto.DefinitionId,
                dto.StorageGb.Value,
                dto.AgeMonths.Value,
                dto.Battery.Value,
                dto.Body.Value,
                dto.Screen,
                dto.Camera,
                dto.Box.Value,
                dto.Invoice.Value,
                Money.FromTl(dto.RejectPrice.Value));
        }

        private static AppraisalLevel MapAppraisalLevel(string fileName, string root, int index, AppraisalLevelDto dto, ICollection<ContentIssue> issues)
        {
            string f = "levels[" + index + "]";
            if (!Require(fileName, root, f, dto != null, issues))
            {
                return null;
            }

            bool ok = Require(fileName, root, f + ".id", dto.Id != null, issues);
            ok &= Require(fileName, root, f + ".name", dto.Name != null, issues);
            ok &= Require(fileName, root, f + ".unlockDay", dto.UnlockDay.HasValue, issues);

            bool feesOk = Require(fileName, root, f + ".fees", dto.Fees != null, issues);
            if (feesOk)
            {
                feesOk &= Require(fileName, root, f + ".fees.entry", dto.Fees.Entry.HasValue, issues);
                feesOk &= Require(fileName, root, f + ".fees.mid", dto.Fees.Mid.HasValue, issues);
                feesOk &= Require(fileName, root, f + ".fees.upper", dto.Fees.Upper.HasValue, issues);
            }

            ok &= feesOk;

            bool confidenceOk = Require(fileName, root, f + ".confidence", dto.Confidence != null, issues);
            AppraisalConfidence confidence = AppraisalConfidence.Hint;
            if (confidenceOk && !AppraisalConfidences.TryParse(dto.Confidence, out confidence))
            {
                issues.Add(ContentIssue.Error(
                    ContentIssueCodes.AppraisalConfidenceInvalid,
                    fileName,
                    f + ": invalid confidence '" + dto.Confidence + "' (allowed: hint, low, medium, certain)."));
                confidenceOk = false;
            }

            ok &= confidenceOk;
            ok &= Require(fileName, root, f + ".evidencePower", dto.EvidencePower.HasValue, issues);
            ok &= Require(fileName, root, f + ".detect", dto.Detect != null, issues);
            ok &= Require(fileName, root, f + ".falseAlarm", dto.FalseAlarm.HasValue, issues);
            ok &= Require(fileName, root, f + ".centerShift", dto.CenterShift.HasValue, issues);
            ok &= Require(fileName, root, f + ".valueNoise", dto.ValueNoise.HasValue, issues);
            if (!ok)
            {
                return null;
            }

            return new AppraisalLevel(
                dto.Id,
                dto.Name,
                dto.UnlockDay.Value,
                dto.RequiredEquipment,
                Money.FromTl(dto.Fees.Entry.Value),
                Money.FromTl(dto.Fees.Mid.Value),
                Money.FromTl(dto.Fees.Upper.Value),
                confidence,
                dto.EvidencePower.Value,
                dto.Detect,
                dto.FalseAlarm.Value,
                dto.BatteryHalfWidth,
                dto.BodyHalfWidth,
                dto.ValueHalfWidth,
                dto.CenterShift.Value,
                dto.ValueNoise.Value,
                dto.CoverageEstimate);
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
