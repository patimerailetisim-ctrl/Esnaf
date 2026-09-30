using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Market;
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

        /// <summary>v0.2 10.4: müşteri Max'ı ≤ V × 1,25 (aşırı prim yok).</summary>
        private const double MaxCustomerValueRatio = 1.25;

        // "sektor.ad" (küçük harf, rakam, alt çizgi). Örn: phone.elma_e13_pro
        private static readonly Regex IdFormat = new Regex("^[a-z][a-z0-9_]*\\.[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant);

        // NPC kimliği: "npc.kemal" (küçük harf, rakam, alt çizgi; harfle başlar).
        private static readonly Regex NpcIdFormat = new Regex("^npc\\.[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant);

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

                ValidateSeller(npc.Seller, label, fileName, issues);
                ValidateCustomer(npc.Customer, label, fileName, issues);
            }
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
