using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Esnaf.Core;
using Esnaf.Domain.Appraisal;
using Esnaf.Domain.Business;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Market;
using Esnaf.Domain.Negotiation;
using Esnaf.Domain.Npc;
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

        private const int MaxHalfWidthPoints = 50;

        /// <summary>v0.2 5.5: tahmini satış = değer × çarpan; makul üst sınır.</summary>
        private const double MaxExpectedSaleFactor = 1.25;

        /// <summary>v0.2 10.4: müşteri Max'ı ≤ V × 1,25 (aşırı prim yok).</summary>
        private const double MaxCustomerValueRatio = 1.25;

        // "sektor.ad" (küçük harf, rakam, alt çizgi). Örn: phone.elma_e13_pro
        private static readonly Regex IdFormat = new Regex("^[a-z][a-z0-9_]*\\.[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant);

        // NPC kimliği: "npc.kemal" (küçük harf, rakam, alt çizgi; harfle başlar).
        private static readonly Regex NpcIdFormat = new Regex("^npc\\.[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant);

        // Ekspertiz seviye kimliği: "s1" (küçük harf, rakam, alt çizgi; harfle başlar).
        private static readonly Regex AppraisalLevelIdFormat = new Regex("^[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant);

        // Defter türü kimliği: küçük harf, rakam, alt çizgi (örn. daily_expense).
        private static readonly Regex TransactionTypeIdFormat = new Regex("^[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant);

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

        /// <summary>
        /// Defter türlerinin kuralları. Çekirdek 8 tür zorunludur ve beklenen davranışta olmalıdır (ekonomi kodu bunlara dayanır);
        /// başka türler (ör. ileride kredi) serbestçe eklenebilir.
        /// </summary>
        public static void ValidateTransactionTypes(IReadOnlyList<TransactionType> types, string fileName, ICollection<ContentIssue> issues)
        {
            if (types.Count == 0)
            {
                issues.Add(ContentIssue.Error(ContentIssueCodes.TransactionTypesEmpty, fileName, "The file contains no transaction types."));
                return;
            }

            var byId = new Dictionary<string, TransactionType>(StringComparer.Ordinal);
            for (int i = 0; i < types.Count; i++)
            {
                TransactionType type = types[i];
                string label = string.IsNullOrEmpty(type.Id) ? "types[" + i + "]" : type.Id;

                if (string.IsNullOrEmpty(type.Id) || !TransactionTypeIdFormat.IsMatch(type.Id))
                {
                    issues.Add(ContentIssue.Error(
                        ContentIssueCodes.TransactionTypeIdFormat,
                        fileName,
                        label + ": id must use lowercase letters, digits and underscores (e.g. daily_expense)."));
                }
                else if (byId.ContainsKey(type.Id))
                {
                    issues.Add(ContentIssue.Error(ContentIssueCodes.TransactionTypeIdDuplicate, fileName, label + ": duplicate id."));
                }
                else
                {
                    byId.Add(type.Id, type);
                }

                if (string.IsNullOrWhiteSpace(type.DisplayKey))
                {
                    issues.Add(ContentIssue.Error(ContentIssueCodes.TransactionTypeDisplayKeyEmpty, fileName, label + ": displayKey is empty."));
                }

                if (!EffectMatchesDirection(type.ProfitEffect, type.Direction))
                {
                    issues.Add(ContentIssue.Error(
                        ContentIssueCodes.TransactionTypeEffectDirectionMismatch,
                        fileName,
                        label + ": profitEffect " + type.ProfitEffect + " cannot be used with direction " + type.Direction + "."));
                }
            }

            ValidateRequiredType(byId, TransactionTypeIds.OpeningCapital, TransactionCategory.Capital, TransactionDirection.Inflow, ProfitEffect.None, fileName, issues);
            ValidateRequiredType(byId, TransactionTypeIds.Purchase, TransactionCategory.Trade, TransactionDirection.Outflow, ProfitEffect.None, fileName, issues);
            ValidateRequiredType(byId, TransactionTypeIds.Sale, TransactionCategory.Trade, TransactionDirection.Inflow, ProfitEffect.Sale, fileName, issues);
            ValidateRequiredType(byId, TransactionTypeIds.Appraisal, TransactionCategory.Trade, TransactionDirection.Outflow, ProfitEffect.None, fileName, issues);
            ValidateRequiredType(byId, TransactionTypeIds.WastedAppraisal, TransactionCategory.Expense, TransactionDirection.Neutral, ProfitEffect.WriteOff, fileName, issues);
            ValidateRequiredType(byId, TransactionTypeIds.Repair, TransactionCategory.Trade, TransactionDirection.Outflow, ProfitEffect.None, fileName, issues);
            ValidateRequiredType(byId, TransactionTypeIds.DailyExpense, TransactionCategory.Expense, TransactionDirection.Outflow, ProfitEffect.Expense, fileName, issues);
            ValidateRequiredType(byId, TransactionTypeIds.Investment, TransactionCategory.Investment, TransactionDirection.Outflow, ProfitEffect.None, fileName, issues);
        }

        /// <summary>Ekonomi sabitlerinin kuralları: tutarlar 10 TL'ye yuvarlı, gün ve kapasite en az 1.</summary>
        public static void ValidateEconomyConstants(EconomyConstants constants, string fileName, ICollection<ContentIssue> issues)
        {
            if (!constants.OpeningCapital.IsPositive || !constants.OpeningCapital.IsRoundedTo10)
            {
                issues.Add(ContentIssue.Error(
                    ContentIssueCodes.EconomyOpeningCapitalInvalid,
                    fileName,
                    "openingCapital " + constants.OpeningCapital.Tl + " must be positive and a multiple of 10 TL."));
            }

            if (constants.DailyExpenseFromDay < 1
                || constants.DailyExpenseAmount.IsNegative
                || !constants.DailyExpenseAmount.IsRoundedTo10)
            {
                issues.Add(ContentIssue.Error(
                    ContentIssueCodes.EconomyDailyExpenseInvalid,
                    fileName,
                    "dailyExpense needs fromDay >= 1 and a non-negative amount that is a multiple of 10 TL."));
            }

            if (constants.InitialShelfCapacity < 1)
            {
                issues.Add(ContentIssue.Error(
                    ContentIssueCodes.EconomyShelfCapacityInvalid, fileName, "initialShelfCapacity must be at least 1."));
            }
        }

        private static bool EffectMatchesDirection(ProfitEffect effect, TransactionDirection direction)
        {
            switch (effect)
            {
                case ProfitEffect.Sale:
                    return direction == TransactionDirection.Inflow;
                case ProfitEffect.Expense:
                    return direction == TransactionDirection.Outflow;
                case ProfitEffect.WriteOff:
                    return direction == TransactionDirection.Neutral;
                default:
                    return true;
            }
        }

        private static void ValidateRequiredType(
            Dictionary<string, TransactionType> byId,
            string id,
            TransactionCategory category,
            TransactionDirection direction,
            ProfitEffect effect,
            string fileName,
            ICollection<ContentIssue> issues)
        {
            TransactionType type;
            if (!byId.TryGetValue(id, out type))
            {
                issues.Add(ContentIssue.Error(
                    ContentIssueCodes.TransactionTypeRequiredMissing, fileName, "Required core transaction type '" + id + "' is missing."));
                return;
            }

            if (type.Category != category || type.Direction != direction || type.ProfitEffect != effect)
            {
                issues.Add(ContentIssue.Error(
                    ContentIssueCodes.TransactionTypeRequiredMismatch,
                    fileName,
                    "Core transaction type '" + id + "' must be category " + category + ", direction " + direction + ", profitEffect " + effect + "."));
            }
        }

        // ---------- NPC profilleri (npc_profiles.json) ----------

        public static void ValidateNpcs(IReadOnlyList<NpcDefinition> npcs, string fileName, ICollection<ContentIssue> issues)
        {
            if (npcs.Count == 0)
            {
                issues.Add(ContentIssue.Error(ContentIssueCodes.NpcListEmpty, fileName, "The file contains no NPCs."));
                return;
            }

            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < npcs.Count; i++)
            {
                NpcDefinition npc = npcs[i];
                string label = string.IsNullOrWhiteSpace(npc.Id) ? "npcs[" + i + "]" : npc.Id;

                if (string.IsNullOrEmpty(npc.Id) || !NpcIdFormat.IsMatch(npc.Id))
                {
                    issues.Add(ContentIssue.Error(
                        ContentIssueCodes.NpcIdFormat, fileName, label + ": id '" + npc.Id + "' must look like npc.kemal (lowercase letters, digits, underscore)."));
                }
                else if (!seenIds.Add(npc.Id))
                {
                    issues.Add(ContentIssue.Error(ContentIssueCodes.NpcIdDuplicate, fileName, label + ": duplicate id."));
                }

                if (string.IsNullOrWhiteSpace(npc.Name))
                {
                    NpcField(issues, fileName, label, "name", "must not be empty.");
                }

                if (string.IsNullOrWhiteSpace(npc.Personality))
                {
                    NpcField(issues, fileName, label, "personality", "must not be empty.");
                }

                if (npc.Gender != null && npc.Gender != "male" && npc.Gender != "female")
                {
                    NpcField(issues, fileName, label, "gender", "must be 'male' or 'female'.");
                }

                ValidateSeller(npc.Seller, label, fileName, issues);
                ValidateCustomer(npc.Customer, label, fileName, issues);
            }
        }

        private static readonly Regex PersonalityIdFormat = new Regex("^[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant);

        /// <summary>
        /// Kişilik kataloğunu ve NPC bağlarını denetler. Katalog varsa her NPC'nin kişiliği olmalı ve NPC'nin GERÇEK sayıları
        /// (sabır, aciliyet, bilgi, bütçe, pazarlık) arketipin beklentisine uymalıdır: kişilik etiket değil, mekaniğin özetidir.
        /// </summary>
        public static void ValidatePersonalities(
            PersonalityCatalog catalog, IReadOnlyList<NpcDefinition> npcs, NegotiationRules rules, string fileName, ICollection<ContentIssue> issues)
        {
            int before = issues.Count;
            if (catalog.IsEmpty)
            {
                for (int i = 0; i < npcs.Count; i++)
                {
                    if (npcs[i].PersonalityId != null)
                    {
                        PersonalityIssue(issues, ContentIssueCodes.PersonalityUnknown, fileName, npcs[i].Id + ": personalityId '" + npcs[i].PersonalityId + "' has no personalities catalog.");
                    }
                }

                return;
            }

            ValidateScale(catalog.Scales.Urgency, "urgency", fileName, issues);
            ValidateScale(catalog.Scales.Knowledge, "knowledge", fileName, issues);
            ValidateScale(catalog.Scales.Budget, "budget", fileName, issues);
            ValidateScale(catalog.Scales.Haggling, "haggling", fileName, issues);

            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.Definitions.Count; i++)
            {
                PersonalityDefinition d = catalog.Definitions[i];
                string label = string.IsNullOrEmpty(d.Id) ? "personalities[" + i + "]" : d.Id;
                if (string.IsNullOrEmpty(d.Id) || !PersonalityIdFormat.IsMatch(d.Id))
                {
                    PersonalityIssue(issues, ContentIssueCodes.PersonalityFieldInvalid, fileName, label + ": id '" + d.Id + "' must be lowercase letters, digits, underscore.");
                }
                else if (!seenIds.Add(d.Id))
                {
                    PersonalityIssue(issues, ContentIssueCodes.PersonalityIdDuplicate, fileName, label + ": duplicate personality id '" + d.Id + "'.");
                }

                if (string.IsNullOrWhiteSpace(d.Name))
                {
                    PersonalityIssue(issues, ContentIssueCodes.PersonalityFieldInvalid, fileName, label + ": name must not be empty.");
                }
            }

            if (issues.Count > before)
            {
                return; // katalog bozukken NPC bağları gürültü üretir
            }

            for (int i = 0; i < npcs.Count; i++)
            {
                NpcDefinition npc = npcs[i];
                if (npc.PersonalityId == null)
                {
                    PersonalityIssue(issues, ContentIssueCodes.PersonalityMissing, fileName, npc.Id + ": personalityId is required when personalities are defined.");
                    continue;
                }

                PersonalityDefinition definition;
                if (!catalog.TryGet(npc.PersonalityId, out definition))
                {
                    PersonalityIssue(issues, ContentIssueCodes.PersonalityUnknown, fileName, npc.Id + ": personalityId '" + npc.PersonalityId + "' is not defined.");
                    continue;
                }

                foreach (CustomerTrait trait in new[] { CustomerTrait.Patience, CustomerTrait.Urgency, CustomerTrait.Knowledge, CustomerTrait.Budget, CustomerTrait.Haggling })
                {
                    NegotiationLevel level = CustomerProfiler.LevelOf(trait, npc, catalog.Scales, rules);
                    if (!definition.Accepts(trait, level))
                    {
                        PersonalityIssue(
                            issues, ContentIssueCodes.PersonalityInconsistent, fileName,
                            npc.Id + ": " + trait.ToString().ToLowerInvariant() + " is " + level.ToString().ToLowerInvariant() + ", which contradicts personality '" + definition.Id + "'.");
                    }
                }
            }
        }

        private static void ValidateScale(PersonalityScale scale, string name, string fileName, ICollection<ContentIssue> issues)
        {
            bool ordered = scale.Direction == ScaleDirection.Up ? scale.High > scale.Medium : scale.High < scale.Medium;
            if (!ordered)
            {
                PersonalityIssue(
                    issues, ContentIssueCodes.PersonalityFieldInvalid, fileName,
                    "personalityScale." + name + ": high must lie beyond medium in the '" + (scale.Direction == ScaleDirection.Up ? "up" : "down") + "' direction.");
            }
        }

        private static void PersonalityIssue(ICollection<ContentIssue> issues, string code, string fileName, string message)
        {
            issues.Add(ContentIssue.Error(code, fileName, message));
        }

        private static void ValidateSeller(NpcSellerRole s, string label, string fileName, ICollection<ContentIssue> issues)
        {
            if (s.AvailableFromDay < 1)
            {
                NpcField(issues, fileName, label, "seller.availableFromDay", "must be at least 1.");
            }

            if (s.AskMultiplier <= 0.0)
            {
                NpcField(issues, fileName, label, "seller.askMultiplier", "must be greater than 0.");
            }

            if (s.RejectRatio <= 0.0 || s.RejectRatio > 1.0 || (s.AskMultiplier > 0.0 && s.RejectRatio >= s.AskMultiplier))
            {
                NpcField(issues, fileName, label, "seller.rejectRatio", "must be in (0, 1] and below askMultiplier.");
            }

            if (s.Patience < 1)
            {
                NpcField(issues, fileName, label, "seller.patience", "must be at least 1.");
            }

            bool sigmaOk = s.ValueSigma >= 0.0 && s.ValueSigma < 1.0;
            if (!sigmaOk)
            {
                NpcField(issues, fileName, label, "seller.valueSigma", "must be in [0, 1).");
            }
            else if (Math.Abs(s.ValueBias) > s.ValueSigma)
            {
                NpcField(issues, fileName, label, "seller.valueBias", "must not exceed valueSigma in size.");
            }

            if (s.Urgency < 0.0 || s.Urgency > 1.0)
            {
                NpcField(issues, fileName, label, "seller.urgency", "must be in [0, 1].");
            }

            if (s.Persuasion < 0.0 || s.Persuasion > 1.0)
            {
                NpcField(issues, fileName, label, "seller.persuasion", "must be in [0, 1].");
            }

            if (s.ConcealChance < 0.0 || s.ConcealChance > 1.0)
            {
                NpcField(issues, fileName, label, "seller.concealChance", "must be in [0, 1].");
            }

            if (s.UrgentLabelFromDay.HasValue && s.UrgentLabelFromDay.Value < 1)
            {
                NpcField(issues, fileName, label, "seller.urgentLabelFromDay", "must be at least 1.");
            }

            if (s.WrongCardPenaltyMultiplier < 1.0 || s.WrongCardPenaltyMultiplier > MaxWrongCardPenaltyMultiplier)
            {
                NpcField(issues, fileName, label, "seller.wrongCardPenaltyMultiplier", "must be in [1, " + MaxWrongCardPenaltyMultiplier + "].");
            }
        }

        private static void ValidateCustomer(NpcCustomerRole c, string label, string fileName, ICollection<ContentIssue> issues)
        {
            if (c.OpeningOfferRatio <= 0.0 || c.OpeningOfferRatio > 1.0)
            {
                NpcField(issues, fileName, label, "customer.openingOfferRatio", "must be in (0, 1].");
            }

            if (c.ValueRatio <= 0.0 || c.ValueRatio > MaxCustomerValueRatio)
            {
                NpcField(issues, fileName, label, "customer.valueRatio", "must be in (0, " + MaxCustomerValueRatio + "].");
            }

            if (c.Patience < 1)
            {
                NpcField(issues, fileName, label, "customer.patience", "must be at least 1.");
            }

            if (c.ValueSigma < 0.0 || c.ValueSigma >= 1.0)
            {
                NpcField(issues, fileName, label, "customer.valueSigma", "must be in [0, 1).");
            }

            if (c.PackageRatio < 1.0)
            {
                NpcField(issues, fileName, label, "customer.packageRatio", "must be at least 1.");
            }

            if (c.AvailableFromDay < 1)
            {
                NpcField(issues, fileName, label, "customer.availableFromDay", "must be at least 1.");
            }

            if (c.ReportTrustGain.HasValue && (c.ReportTrustGain.Value < 0 || c.ReportTrustGain.Value > 100))
            {
                NpcField(issues, fileName, label, "customer.reportTrustGain", "must be in [0, 100].");
            }

            var seenSegments = new HashSet<ProductSegment>();
            foreach (ProductSegment segment in c.Segments)
            {
                if (!seenSegments.Add(segment))
                {
                    NpcField(issues, fileName, label, "customer.segments", "lists '" + segment + "' more than once.");
                    break;
                }
            }
        }

        private static void NpcField(ICollection<ContentIssue> issues, string fileName, string label, string field, string rule)
        {
            issues.Add(ContentIssue.Error(ContentIssueCodes.NpcFieldInvalid, fileName, label + ": " + field + " " + rule));
        }

        // ---------- pazar sabitleri (economy_constants.json "market" bölümü) ----------

        public static void ValidateMarket(
            MarketConstants market,
            IReadOnlyList<NpcDefinition> npcs,
            IReadOnlyList<ProductDefinition> products,
            ValueTables tables,
            string fileName,
            ICollection<ContentIssue> issues)
        {
            ValidateListingCounts(market, fileName, issues);

            if (market.LifetimeMinDays < 1)
            {
                MarketField(issues, fileName, "listingLifetimeDays.min", "must be at least 1.");
            }

            if (market.LifetimeMaxDays < market.LifetimeMinDays)
            {
                MarketField(issues, fileName, "listingLifetimeDays.max", "must not be below min.");
            }

            ValidateSegmentWeights(market, fileName, issues);
            ValidateModelAvailability(market, products, fileName, issues);

            if (market.LearningFriendlyUntilDay < 1)
            {
                MarketField(issues, fileName, "learningFriendlySellers.untilDay", "must be at least 1.");
            }

            if (market.LearningFriendlyShare < 0.0 || market.LearningFriendlyShare > 1.0)
            {
                MarketField(issues, fileName, "learningFriendlySellers.share", "must be in [0, 1].");
            }

            if (market.OpportunityFromDay < 1)
            {
                MarketField(issues, fileName, "opportunity.fromDay", "must be at least 1.");
            }

            if (market.MinOpportunitiesPerDay < 0)
            {
                MarketField(issues, fileName, "opportunity.minPerDay", "must not be negative.");
            }

            if (market.OpportunityMaxRejectRatio <= 0.0 || market.OpportunityMaxRejectRatio > 1.0)
            {
                MarketField(issues, fileName, "opportunity.maxRejectRatio", "must be in (0, 1].");
            }

            if (market.JackpotRejectRatioBelow <= 0.0 || market.JackpotRejectRatioBelow > 1.0)
            {
                MarketField(issues, fileName, "jackpot.rejectRatioBelow", "must be in (0, 1].");
            }

            ValidateQuotaBands(market.JackpotMaxPerDay, "jackpot.maxPerDay", true, 0, fileName, issues);

            if (market.TrapFromDay < 1)
            {
                MarketField(issues, fileName, "trap.fromDay", "must be at least 1.");
            }

            if (market.TrapMinPerDay < 0)
            {
                MarketField(issues, fileName, "trap.minPerDay", "must not be negative.");
            }

            if (market.TrapValueRatio <= 1.0)
            {
                MarketField(issues, fileName, "trap.valueRatio", "must be greater than 1.");
            }

            ValidateQuotaBands(market.TrapMaxPerDay, "trap.maxPerDay", false, Math.Max(market.TrapMinPerDay, 0), fileName, issues);

            if (market.AskingPriceStep < 10 || market.AskingPriceStep % 10 != 0)
            {
                MarketField(issues, fileName, "askingPriceStep", "must be a positive multiple of 10 TL.");
            }

            ValidateHiddenDefects(market, tables, fileName, issues);
            ValidateGuidedListing(market, npcs, products, tables, fileName, issues);
            ValidateTrapSeller(market, npcs, fileName, issues);
        }

        private static void ValidateListingCounts(MarketConstants market, string fileName, ICollection<ContentIssue> issues)
        {
            IReadOnlyList<ListingCountBand> bands = market.ListingCounts;
            if (bands.Count == 0)
            {
                MarketField(issues, fileName, "listingCounts", "needs at least one band.");
                return;
            }

            for (int i = 0; i < bands.Count; i++)
            {
                string f = "listingCounts[" + i + "]";
                if (i == 0 ? bands[i].FromDay != 1 : bands[i].FromDay <= bands[i - 1].FromDay)
                {
                    MarketField(issues, fileName, f + ".fromDay", i == 0 ? "of the first band must be 1." : "must be greater than the previous band's fromDay.");
                }

                if (bands[i].Min < 1)
                {
                    MarketField(issues, fileName, f + ".min", "must be at least 1.");
                }

                if (bands[i].Max < bands[i].Min)
                {
                    MarketField(issues, fileName, f + ".max", "must not be below min.");
                }
            }
        }

        private static void ValidateSegmentWeights(MarketConstants market, string fileName, ICollection<ContentIssue> issues)
        {
            IReadOnlyList<SegmentWeightBand> bands = market.SegmentWeights;
            if (bands.Count == 0)
            {
                MarketField(issues, fileName, "segmentWeights", "needs at least one band.");
                return;
            }

            for (int i = 0; i < bands.Count; i++)
            {
                string f = "segmentWeights[" + i + "]";
                if (i == 0 ? bands[i].FromDay != 1 : bands[i].FromDay <= bands[i - 1].FromDay)
                {
                    MarketField(issues, fileName, f + ".fromDay", i == 0 ? "of the first band must be 1." : "must be greater than the previous band's fromDay.");
                }

                if (bands[i].Entry < 0)
                {
                    MarketField(issues, fileName, f + ".entry", "must not be negative.");
                }

                if (bands[i].Mid < 0)
                {
                    MarketField(issues, fileName, f + ".mid", "must not be negative.");
                }

                if (bands[i].Upper < 0)
                {
                    MarketField(issues, fileName, f + ".upper", "must not be negative.");
                }

                if (bands[i].Entry + bands[i].Mid + bands[i].Upper <= 0)
                {
                    MarketField(issues, fileName, f, "needs a total weight greater than 0.");
                }
            }
        }

        private static void ValidateModelAvailability(
            MarketConstants market,
            IReadOnlyList<ProductDefinition> products,
            string fileName,
            ICollection<ContentIssue> issues)
        {
            var knownIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < products.Count; i++)
            {
                knownIds.Add(products[i].Id);
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < market.ModelAvailability.Count; i++)
            {
                ModelAvailabilityRule rule = market.ModelAvailability[i];
                string f = "modelAvailability[" + i + "]";
                if (!knownIds.Contains(rule.DefinitionId))
                {
                    MarketReference(issues, fileName, f + ".id", rule.DefinitionId);
                }
                else if (!seen.Add(rule.DefinitionId))
                {
                    MarketField(issues, fileName, f + ".id", "'" + rule.DefinitionId + "' is listed more than once.");
                }

                if (rule.FromDay < 1)
                {
                    MarketField(issues, fileName, f + ".fromDay", "must be at least 1.");
                }
            }
        }

        private static void ValidateQuotaBands(
            IReadOnlyList<QuotaBand> bands,
            string field,
            bool mustStartAtDayOne,
            int minMax,
            string fileName,
            ICollection<ContentIssue> issues)
        {
            if (bands.Count == 0 && mustStartAtDayOne)
            {
                MarketField(issues, fileName, field, "needs at least one band.");
                return;
            }

            for (int i = 0; i < bands.Count; i++)
            {
                string f = field + "[" + i + "]";
                if (i == 0 ? (mustStartAtDayOne ? bands[i].FromDay != 1 : bands[i].FromDay < 1) : bands[i].FromDay <= bands[i - 1].FromDay)
                {
                    MarketField(issues, fileName, f + ".fromDay", i == 0 ? "of the first band is invalid." : "must be greater than the previous band's fromDay.");
                }

                if (bands[i].Max < minMax)
                {
                    MarketField(issues, fileName, f + ".max", "must be at least " + minMax + ".");
                }
            }
        }

        private static void ValidateHiddenDefects(MarketConstants market, ValueTables tables, string fileName, ICollection<ContentIssue> issues)
        {
            if (market.HiddenDefects.Count == 0)
            {
                MarketField(issues, fileName, "hiddenDefects", "needs at least one rule.");
                return;
            }

            for (int i = 0; i < market.HiddenDefects.Count; i++)
            {
                HiddenDefectRule rule = market.HiddenDefects[i];
                string f = "hiddenDefects[" + i + "]";

                TryGetMultiplier lookup;
                if (rule.Attribute == PhoneAttributes.Screen)
                {
                    lookup = tables.TryGetScreenMultiplier;
                }
                else if (rule.Attribute == PhoneAttributes.Camera)
                {
                    lookup = tables.TryGetCameraMultiplier;
                }
                else
                {
                    MarketField(issues, fileName, f + ".attribute", "must be 'screen' or 'camera'.");
                    continue;
                }

                if (rule.HiddenValues.Count == 0)
                {
                    MarketField(issues, fileName, f + ".hiddenValues", "needs at least one value.");
                }

                double ignored;
                for (int v = 0; v < rule.HiddenValues.Count; v++)
                {
                    if (!lookup(rule.HiddenValues[v], out ignored))
                    {
                        MarketField(issues, fileName, f + ".hiddenValues", "'" + rule.HiddenValues[v] + "' is not a known " + rule.Attribute + " id.");
                    }
                }

                if (!lookup(rule.CleanValue, out ignored))
                {
                    MarketField(issues, fileName, f + ".cleanValue", "'" + rule.CleanValue + "' is not a known " + rule.Attribute + " id.");
                }
                else if (rule.IsHidden(rule.CleanValue))
                {
                    MarketField(issues, fileName, f + ".cleanValue", "must not be one of the hidden values.");
                }
            }
        }

        private static void ValidateGuidedListing(
            MarketConstants market,
            IReadOnlyList<NpcDefinition> npcs,
            IReadOnlyList<ProductDefinition> products,
            ValueTables tables,
            string fileName,
            ICollection<ContentIssue> issues)
        {
            GuidedListingSpec g = market.GuidedListing;

            NpcDefinition seller = null;
            for (int i = 0; i < npcs.Count; i++)
            {
                if (npcs[i].Id == g.SellerNpcId)
                {
                    seller = npcs[i];
                }
            }

            if (seller == null)
            {
                MarketReference(issues, fileName, "guidedListing.sellerNpcId", g.SellerNpcId);
            }
            else if (seller.Seller.AvailableFromDay > 1)
            {
                MarketField(issues, fileName, "guidedListing.sellerNpcId", "'" + g.SellerNpcId + "' is not available as a seller on day 1.");
            }

            ProductDefinition product = null;
            for (int i = 0; i < products.Count; i++)
            {
                if (products[i].Id == g.DefinitionId)
                {
                    product = products[i];
                }
            }

            if (product == null)
            {
                MarketReference(issues, fileName, "guidedListing.definitionId", g.DefinitionId);
            }
            else
            {
                if (!market.IsModelAvailable(product.Id, 1))
                {
                    MarketField(issues, fileName, "guidedListing.definitionId", "'" + product.Id + "' is not available on day 1.");
                }

                double ignoredMultiplier;
                if (!product.TryGetStorageMultiplier(g.StorageGb, out ignoredMultiplier))
                {
                    MarketField(issues, fileName, "guidedListing.storageGb", g.StorageGb + " GB is not offered by " + product.Id + ".");
                }

                if (g.AgeMonths < product.MinAgeMonths || g.AgeMonths > product.MaxAgeMonths)
                {
                    MarketField(issues, fileName, "guidedListing.ageMonths", "must be within the model's age range " + product.MinAgeMonths + "-" + product.MaxAgeMonths + ".");
                }
            }

            if (g.Battery < 0 || g.Battery > 100)
            {
                MarketField(issues, fileName, "guidedListing.battery", "must be in [0, 100].");
            }

            if (g.Body < 0 || g.Body > 100)
            {
                MarketField(issues, fileName, "guidedListing.body", "must be in [0, 100].");
            }

            double ignored;
            if (!tables.TryGetScreenMultiplier(g.Screen, out ignored))
            {
                MarketField(issues, fileName, "guidedListing.screen", "'" + g.Screen + "' is not a known screen id.");
            }

            if (!tables.TryGetCameraMultiplier(g.Camera, out ignored))
            {
                MarketField(issues, fileName, "guidedListing.camera", "'" + g.Camera + "' is not a known camera id.");
            }

            if (!g.RejectPrice.IsPositive || !g.RejectPrice.IsRoundedTo10)
            {
                MarketField(issues, fileName, "guidedListing.rejectPrice", "must be positive and a multiple of 10 TL.");
            }
        }

        private static void ValidateTrapSeller(MarketConstants market, IReadOnlyList<NpcDefinition> npcs, string fileName, ICollection<ContentIssue> issues)
        {
            if (market.TrapMinPerDay <= 0 || market.TrapFromDay < 1)
            {
                return; // zorunlu tuzak yok ya da trap.fromDay zaten geçersiz olarak raporlandı
            }

            for (int i = 0; i < npcs.Count; i++)
            {
                if (npcs[i].Seller.ConcealChance > 0.0 && npcs[i].Seller.AvailableFromDay <= market.TrapFromDay)
                {
                    return;
                }
            }

            issues.Add(ContentIssue.Error(
                ContentIssueCodes.MarketTrapSellerMissing,
                fileName,
                "Traps are required from day " + market.TrapFromDay + " but no seller with concealChance > 0 is available by then."));
        }

        private static void MarketField(ICollection<ContentIssue> issues, string fileName, string field, string rule)
        {
            issues.Add(ContentIssue.Error(ContentIssueCodes.MarketFieldInvalid, fileName, "market." + field + " " + rule));
        }

        private static void MarketReference(ICollection<ContentIssue> issues, string fileName, string field, string id)
        {
            issues.Add(ContentIssue.Error(
                ContentIssueCodes.MarketReferenceMissing, fileName, "market." + field + " refers to unknown id '" + id + "'."));
        }

        // ---------- ekspertiz kuralları (appraisal_levels.json) ----------

        public static void ValidateAppraisal(AppraisalConfig config, ValueTables tables, string fileName, ICollection<ContentIssue> issues)
        {
            var checkAttributes = new HashSet<string>(StringComparer.Ordinal);
            ValidateAppraisalChecks(config, tables, fileName, issues, checkAttributes);

            if (config.Levels.Count == 0)
            {
                issues.Add(ContentIssue.Error(ContentIssueCodes.AppraisalLevelsEmpty, fileName, "The file contains no appraisal levels."));
            }

            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < config.Levels.Count; i++)
            {
                AppraisalLevel level = config.Levels[i];
                string label = string.IsNullOrWhiteSpace(level.Id) ? "levels[" + i + "]" : level.Id;
                if (string.IsNullOrEmpty(level.Id) || !AppraisalLevelIdFormat.IsMatch(level.Id))
                {
                    issues.Add(ContentIssue.Error(
                        ContentIssueCodes.AppraisalLevelIdFormat, fileName, label + ": id '" + level.Id + "' must be lowercase letters, digits, underscore (e.g. s1)."));
                }
                else if (!seenIds.Add(level.Id))
                {
                    issues.Add(ContentIssue.Error(ContentIssueCodes.AppraisalLevelIdDuplicate, fileName, label + ": duplicate id."));
                }

                ValidateAppraisalLevel(level, label, checkAttributes, fileName, issues);
            }

            if (config.ExpectedSaleFactor <= 0.0 || config.ExpectedSaleFactor > MaxExpectedSaleFactor)
            {
                AppraisalField(issues, fileName, "riskCard", "expectedSaleFactor must be in (0, " + MaxExpectedSaleFactor + "].");
            }
        }

        private static void ValidateAppraisalChecks(
            AppraisalConfig config,
            ValueTables tables,
            string fileName,
            ICollection<ContentIssue> issues,
            ISet<string> attributes)
        {
            if (config.Checks.Count == 0)
            {
                AppraisalField(issues, fileName, "checks", "needs at least one checked attribute.");
                return;
            }

            for (int i = 0; i < config.Checks.Count; i++)
            {
                AppraisalCheck check = config.Checks[i];
                string f = "checks[" + i + "]";

                TryGetMultiplier lookup;
                if (check.Attribute == PhoneAttributes.Screen)
                {
                    lookup = tables.TryGetScreenMultiplier;
                }
                else if (check.Attribute == PhoneAttributes.Camera)
                {
                    lookup = tables.TryGetCameraMultiplier;
                }
                else
                {
                    AppraisalField(issues, fileName, f + ".attribute", "must be 'screen' or 'camera'.");
                    continue;
                }

                if (!attributes.Add(check.Attribute))
                {
                    AppraisalField(issues, fileName, f + ".attribute", "'" + check.Attribute + "' is checked more than once.");
                }

                double ignored;
                bool defectsOk = check.DefectValues.Count > 0;
                if (!defectsOk)
                {
                    AppraisalField(issues, fileName, f + ".defectValues", "needs at least one value.");
                }

                for (int v = 0; v < check.DefectValues.Count; v++)
                {
                    if (!lookup(check.DefectValues[v], out ignored))
                    {
                        AppraisalField(issues, fileName, f + ".defectValues", "'" + check.DefectValues[v] + "' is not a known " + check.Attribute + " id.");
                        defectsOk = false;
                    }
                }

                if (defectsOk && !check.IsDefect(check.FalseAlarmValue))
                {
                    AppraisalField(issues, fileName, f + ".falseAlarmValue", "must be one of the defect values.");
                }

                if (!lookup(check.CleanValue, out ignored))
                {
                    AppraisalField(issues, fileName, f + ".cleanValue", "'" + check.CleanValue + "' is not a known " + check.Attribute + " id.");
                }
                else if (check.IsDefect(check.CleanValue))
                {
                    AppraisalField(issues, fileName, f + ".cleanValue", "must not be one of the defect values.");
                }

                if (string.IsNullOrWhiteSpace(check.WordingKey))
                {
                    AppraisalField(issues, fileName, f + ".wordingKey", "must not be empty.");
                }
            }
        }

        private static void ValidateAppraisalLevel(
            AppraisalLevel level,
            string label,
            ISet<string> checkAttributes,
            string fileName,
            ICollection<ContentIssue> issues)
        {
            if (string.IsNullOrWhiteSpace(level.Name))
            {
                AppraisalField(issues, fileName, label, "name must not be empty.");
            }

            if (level.UnlockDay < 1)
            {
                AppraisalField(issues, fileName, label, "unlockDay must be at least 1.");
            }

            if (level.RequiredEquipment != null && string.IsNullOrWhiteSpace(level.RequiredEquipment))
            {
                AppraisalField(issues, fileName, label, "requiredEquipment must not be empty when given.");
            }

            ValidateFee(level.FeeFor(ProductSegment.Entry), "fees.entry", label, fileName, issues);
            ValidateFee(level.FeeFor(ProductSegment.Mid), "fees.mid", label, fileName, issues);
            ValidateFee(level.FeeFor(ProductSegment.Upper), "fees.upper", label, fileName, issues);

            if (level.EvidencePower <= 0.0 || level.EvidencePower > 1.0)
            {
                AppraisalField(issues, fileName, label, "evidencePower must be in (0, 1].");
            }

            if (level.FalseAlarmChance < 0.0 || level.FalseAlarmChance > 1.0)
            {
                AppraisalField(issues, fileName, label, "falseAlarm must be in [0, 1].");
            }

            foreach (string attribute in checkAttributes)
            {
                if (!ContainsKey(level, attribute))
                {
                    AppraisalField(issues, fileName, label, "detect." + attribute + " is missing.");
                }
            }

            foreach (string attribute in level.DetectAttributes)
            {
                if (!checkAttributes.Contains(attribute))
                {
                    AppraisalField(issues, fileName, label, "detect." + attribute + " is not a checked attribute.");
                }
                else if (level.DetectChance(attribute) < 0.0 || level.DetectChance(attribute) > 1.0)
                {
                    AppraisalField(issues, fileName, label, "detect." + attribute + " must be in [0, 1].");
                }
            }

            if (level.BatteryHalfWidth.HasValue && (level.BatteryHalfWidth.Value < 1 || level.BatteryHalfWidth.Value > MaxHalfWidthPoints))
            {
                AppraisalField(issues, fileName, label, "batteryHalfWidth must be in [1, " + MaxHalfWidthPoints + "].");
            }

            if (level.BodyHalfWidth.HasValue && (level.BodyHalfWidth.Value < 1 || level.BodyHalfWidth.Value > MaxHalfWidthPoints))
            {
                AppraisalField(issues, fileName, label, "bodyHalfWidth must be in [1, " + MaxHalfWidthPoints + "].");
            }

            if (level.ValueHalfWidth.HasValue)
            {
                if (level.ValueHalfWidth.Value <= 0.0 || level.ValueHalfWidth.Value >= 0.5)
                {
                    AppraisalField(issues, fileName, label, "valueHalfWidth must be in (0, 0.5).");
                }
                else if (!level.BatteryHalfWidth.HasValue || !level.BodyHalfWidth.HasValue)
                {
                    AppraisalField(issues, fileName, label, "valueHalfWidth needs batteryHalfWidth and bodyHalfWidth (the observed value uses their centers).");
                }
            }

            if (level.CenterShift < 0.0 || level.CenterShift >= 1.0)
            {
                AppraisalField(issues, fileName, label, "centerShift must be in [0, 1).");
            }

            if (level.ValueNoise < 0.0 || level.ValueNoise >= 0.5)
            {
                AppraisalField(issues, fileName, label, "valueNoise must be in [0, 0.5).");
            }

            if (level.CoverageEstimate.HasValue && (level.CoverageEstimate.Value <= 0.0 || level.CoverageEstimate.Value > 1.0))
            {
                AppraisalField(issues, fileName, label, "coverageEstimate must be in (0, 1].");
            }
        }

        private static bool ContainsKey(AppraisalLevel level, string attribute)
        {
            foreach (string key in level.DetectAttributes)
            {
                if (key == attribute)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ValidateFee(Money fee, string field, string label, string fileName, ICollection<ContentIssue> issues)
        {
            if (fee.IsNegative || !fee.IsRoundedTo10)
            {
                AppraisalField(issues, fileName, label, field + " must be non-negative and a multiple of 10 TL.");
            }
        }

        private const double MaxShopPremiumCap = 0.5;
        private const double MaxRatioCeiling = 3.0;
        private const double MaxDemandNoise = 0.5;

        /// <summary>economy_constants.json "customers" bölümü: değer aralıkları (NPC başvuruları ayrıca <see cref="ValidateCustomerLinks"/>'te).</summary>
        public static void ValidateCustomers(CustomerConstants c, string fileName, ICollection<ContentIssue> issues)
        {
            if (c.ShopPremiumCap < 0.0 || c.ShopPremiumCap > MaxShopPremiumCap)
            {
                CustomerField(issues, fileName, "customers.shopPremiumCap", "must be in [0, " + MaxShopPremiumCap + "].");
            }

            if (c.ShopPremium < 0.0 || c.ShopPremium > c.ShopPremiumCap)
            {
                CustomerField(issues, fileName, "customers.shopPremium", "must be in [0, shopPremiumCap].");
            }

            if (c.MaxRatioToTrueValue < 1.0 || c.MaxRatioToTrueValue > MaxRatioCeiling)
            {
                CustomerField(issues, fileName, "customers.maxRatioToTrueValue", "must be in [1, " + MaxRatioCeiling + "].");
            }

            if (c.CountMax < 1)
            {
                CustomerField(issues, fileName, "customers.count.max", "must be at least 1.");
            }

            if (c.CountBase < 0 || (c.CountMax >= 1 && c.CountBase > c.CountMax))
            {
                CustomerField(issues, fileName, "customers.count.base", "must be in [0, max].");
            }

            if (c.CountPerShelfItem < 0.0)
            {
                CustomerField(issues, fileName, "customers.count.perShelfItem", "must not be negative.");
            }

            if (c.RichMaxPerDay < 0)
            {
                CustomerField(issues, fileName, "customers.richQuota.maxPerDay", "must not be negative.");
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in c.RichNpcIds)
            {
                if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
                {
                    CustomerField(issues, fileName, "customers.richQuota.npcIds", "ids must be non-empty and unique.");
                    break;
                }
            }
        }

        /// <summary>Zengin müşteri kotasındaki kimlikler ve Gün 1 müşteri havuzu NPC'lerle çapraz denetlenir.</summary>
        public static void ValidateCustomerLinks(CustomerConstants c, IReadOnlyList<NpcDefinition> npcs, string fileName, ICollection<ContentIssue> issues)
        {
            var known = new HashSet<string>(StringComparer.Ordinal);
            bool dayOne = false;
            foreach (NpcDefinition npc in npcs)
            {
                known.Add(npc.Id);
                dayOne |= npc.Customer.AvailableFromDay <= 1;
            }

            foreach (string id in c.RichNpcIds)
            {
                if (!known.Contains(id))
                {
                    CustomerField(issues, fileName, "customers.richQuota.npcIds", "unknown NPC '" + id + "'.");
                }
            }

            if (!dayOne)
            {
                CustomerField(issues, fileName, "customers", "at least one NPC must be available as a customer on day 1.");
            }
        }

        /// <summary>economy_constants.json "demand" bölümü.</summary>
        public static void ValidateDemand(DemandConstants d, string fileName, ICollection<ContentIssue> issues)
        {
            if (d.LiveFromDay < 1)
            {
                DemandField(issues, fileName, "demand.liveFromDay", "must be at least 1.");
            }

            if (d.DailyNoise < 0.0 || d.DailyNoise >= MaxDemandNoise)
            {
                DemandField(issues, fileName, "demand.dailyNoise", "must be in [0, " + MaxDemandNoise + ").");
            }

            if (d.MeanReversion < 0.0 || d.MeanReversion > 1.0)
            {
                DemandField(issues, fileName, "demand.meanReversion", "must be in [0, 1].");
            }

            if (d.Min <= 0.0 || d.Min > 1.0)
            {
                DemandField(issues, fileName, "demand.min", "must be in (0, 1].");
            }

            if (d.Max < 1.0)
            {
                DemandField(issues, fileName, "demand.max", "must be at least 1.");
            }

            if (d.PressurePerSale <= 0.0 || d.PressurePerSale > 1.0)
            {
                DemandField(issues, fileName, "demand.salesPressure.perSale", "must be in (0, 1].");
            }

            if (d.PressureWindowDays < 1)
            {
                DemandField(issues, fileName, "demand.salesPressure.windowDays", "must be at least 1.");
            }

            if (d.PressureFloor <= 0.0 || d.PressureFloor > 1.0)
            {
                DemandField(issues, fileName, "demand.salesPressure.floor", "must be in (0, 1].");
            }
        }

        private static void CustomerField(ICollection<ContentIssue> issues, string fileName, string label, string rule)
        {
            issues.Add(ContentIssue.Error(ContentIssueCodes.CustomerFieldInvalid, fileName, label + ": " + rule));
        }

        private static void DemandField(ICollection<ContentIssue> issues, string fileName, string label, string rule)
        {
            issues.Add(ContentIssue.Error(ContentIssueCodes.DemandFieldInvalid, fileName, label + ": " + rule));
        }

        private const double MaxWrongCardPenaltyMultiplier = 5.0;
        private const double MaxMoodSwing = 0.2;

        /// <summary>negotiation_rules.json değer aralıkları (kendi içinde tutarlılık).</summary>
        public static void ValidateNegotiation(NegotiationRules r, string fileName, ICollection<ContentIssue> issues)
        {
            if (r.PriceBaseShare < 0.0 || r.PriceBaseShare > 1.0)
            {
                NegotiationField(issues, fileName, "price.baseShare", "must be in [0, 1].");
            }

            if (r.PriceTrustShare < 0.0)
            {
                NegotiationField(issues, fileName, "price.trustShare", "must not be negative.");
            }

            if (r.PriceUrgencyShare < 0.0)
            {
                NegotiationField(issues, fileName, "price.urgencyShare", "must not be negative.");
            }

            if (r.PriceBaseShare >= 0.0 && r.PriceTrustShare >= 0.0 && r.PriceUrgencyShare >= 0.0
                && r.PriceBaseShare + r.PriceTrustShare + r.PriceUrgencyShare > 1.0)
            {
                NegotiationField(issues, fileName, "price", "the three shares must add up to at most 1 (the seller cannot give more than the whole gap).");
            }

            bool insultRatioOk = r.InsultRatio > 0.0 && r.InsultRatio < 1.0;
            if (!insultRatioOk)
            {
                NegotiationField(issues, fileName, "insult.ratio", "must be in (0, 1).");
            }

            if (r.InsultTrustLoss < 0 || r.InsultTrustLoss > 100)
            {
                NegotiationField(issues, fileName, "insult.trustLoss", "must be in [0, 100].");
            }

            if (r.InsultExtraPatienceLoss < 0)
            {
                NegotiationField(issues, fileName, "insult.extraPatienceLoss", "must not be negative.");
            }

            if (r.InsultPenaltyFromDay < 1)
            {
                NegotiationField(issues, fileName, "insult.penaltyFromDay", "must be at least 1.");
            }

            if (r.NearOfferRatio > 1.0 || (insultRatioOk && r.NearOfferRatio <= r.InsultRatio))
            {
                NegotiationField(issues, fileName, "nearOffer.ratio", "must be above insult.ratio and at most 1.");
            }

            if (r.NearOfferTrustGain < 0 || r.NearOfferTrustGain > 100)
            {
                NegotiationField(issues, fileName, "nearOffer.trustGain", "must be in [0, 100].");
            }

            if (r.CardCorrectTrustGain < 0 || r.CardCorrectTrustGain > 100)
            {
                NegotiationField(issues, fileName, "card.correctTrustGain", "must be in [0, 100].");
            }

            if (r.CardWrongTrustLoss < 0 || r.CardWrongTrustLoss > 100)
            {
                NegotiationField(issues, fileName, "card.wrongTrustLoss", "must be in [0, 100].");
            }

            if (r.CardWrongPatienceLoss < 0)
            {
                NegotiationField(issues, fileName, "card.wrongPatienceLoss", "must not be negative.");
            }

            if (string.IsNullOrWhiteSpace(r.ReportLevelId))
            {
                NegotiationField(issues, fileName, "card.reportLevelId", "must not be empty.");
            }

            if (r.ReportPersuasionBonus < 0.0 || r.ReportPersuasionBonus > 1.0)
            {
                NegotiationField(issues, fileName, "card.reportPersuasionBonus", "must be in [0, 1].");
            }

            if (r.RejectFloorRatio <= 0.0 || r.RejectFloorRatio > 1.0)
            {
                NegotiationField(issues, fileName, "card.rejectFloorRatio", "must be in (0, 1].");
            }

            if (r.SellTooExpensiveRatio <= 1.0 || r.SellTooExpensiveRatio > 2.0)
            {
                NegotiationField(issues, fileName, "sell.tooExpensiveRatio", "must be in (1, 2].");
            }

            if (r.SellReportSigmaFactor < 0.0 || r.SellReportSigmaFactor > 1.0)
            {
                NegotiationField(issues, fileName, "sell.reportSigmaFactor", "must be in [0, 1].");
            }

            if (r.SellReportTrustGain < 0 || r.SellReportTrustGain > 100)
            {
                NegotiationField(issues, fileName, "sell.reportTrustGain", "must be in [0, 100].");
            }

            var reportIds = new HashSet<string>(StringComparer.Ordinal);
            if (r.SellReportLevelIds.Count == 0)
            {
                NegotiationField(issues, fileName, "sell.reportLevelIds", "needs at least one level.");
            }

            foreach (string id in r.SellReportLevelIds)
            {
                if (string.IsNullOrWhiteSpace(id) || !reportIds.Add(id))
                {
                    NegotiationField(issues, fileName, "sell.reportLevelIds", "ids must be non-empty and unique.");
                    break;
                }
            }

            if (r.StartTrustSpread < 0)
            {
                NegotiationField(issues, fileName, "start.trustSpread", "must not be negative.");
            }

            if (r.StartTrust - r.StartTrustSpread < 0 || r.StartTrust + r.StartTrustSpread > 100)
            {
                NegotiationField(issues, fileName, "start.trust", "trust ± trustSpread must stay within [0, 100].");
            }

            if (r.RejectMoodSwing < 0.0 || r.RejectMoodSwing > MaxMoodSwing)
            {
                NegotiationField(issues, fileName, "start.rejectMoodSwing", "must be in [0, " + MaxMoodSwing + "].");
            }

            if (r.MoodLowBelow < 0 || r.MoodHighFrom > 100)
            {
                if (r.MoodLowBelow < 0)
                {
                    NegotiationField(issues, fileName, "view.moodLowBelow", "must not be negative.");
                }

                if (r.MoodHighFrom > 100)
                {
                    NegotiationField(issues, fileName, "view.moodHighFrom", "must be at most 100.");
                }
            }
            else if (r.MoodLowBelow >= r.MoodHighFrom)
            {
                NegotiationField(issues, fileName, "view.mood", "moodLowBelow must be below moodHighFrom.");
            }

            if (r.PatienceLowAtMost < 0)
            {
                NegotiationField(issues, fileName, "view.patienceLowAtMost", "must not be negative.");
            }
            else if (r.PatienceMediumAtMost <= r.PatienceLowAtMost)
            {
                NegotiationField(issues, fileName, "view.patience", "patienceMediumAtMost must be above patienceLowAtMost.");
            }
        }

        /// <summary>Rapor seviyesi, ekspertiz seviyeleri arasında olmalı (çapraz denetim).</summary>
        public static void ValidateNegotiationLinks(NegotiationRules r, AppraisalConfig appraisal, string fileName, ICollection<ContentIssue> issues)
        {
            AppraisalLevel level;
            if (!string.IsNullOrWhiteSpace(r.ReportLevelId) && !appraisal.TryGetLevel(r.ReportLevelId, out level))
            {
                NegotiationField(issues, fileName, "card.reportLevelId", "unknown appraisal level '" + r.ReportLevelId + "'.");
            }

            foreach (string id in r.SellReportLevelIds)
            {
                if (!appraisal.TryGetLevel(id, out level))
                {
                    NegotiationField(issues, fileName, "sell.reportLevelIds", "unknown appraisal level '" + id + "'.");
                }
            }
        }

        private static void NegotiationField(ICollection<ContentIssue> issues, string fileName, string label, string rule)
        {
            issues.Add(ContentIssue.Error(ContentIssueCodes.NegotiationFieldInvalid, fileName, label + ": " + rule));
        }

        private static void AppraisalField(ICollection<ContentIssue> issues, string fileName, string label, string rule)
        {
            issues.Add(ContentIssue.Error(ContentIssueCodes.AppraisalFieldInvalid, fileName, label + ": " + rule));
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
