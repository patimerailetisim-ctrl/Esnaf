using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Esnaf.Domain.Appraisal;
using Esnaf.Domain.Business;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Market;
using Esnaf.Domain.Negotiation;
using Esnaf.Domain.Npc;
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
        private readonly ReadOnlyCollection<NpcDefinition> _npcs;
        private readonly Dictionary<string, NpcDefinition> _npcsById;

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

        /// <summary>Ekspertiz kuralları (appraisal_levels.json).</summary>
        public AppraisalConfig Appraisal { get; }

        /// <summary>Alış pazarlığı kuralları (negotiation_rules.json).</summary>
        public NegotiationRules Negotiation { get; }

        /// <summary>Müşteri üretimi sabitleri (economy_constants.json "customers").</summary>
        public CustomerConstants Customers { get; }

        /// <summary>Talep modeli sabitleri (economy_constants.json "demand").</summary>
        public DemandConstants Demand { get; }

        /// <summary>Pazar (ilan üretimi) sabitleri (economy_constants.json "market" bölümü).</summary>
        public MarketConstants MarketConstants { get; }

        /// <summary>NPC tanımları (npc_profiles.json), dosyadaki sırayla, salt okunur.</summary>
        public IReadOnlyList<NpcDefinition> Npcs
        {
            get { return _npcs; }
        }

        /// <summary>Müşteri kişilik arketipleri ve ölçekleri (npc_profiles.json); bölümler yoksa boş katalog.</summary>
        public PersonalityCatalog Personalities { get; }

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
            EconomyConstants economyConstants,
            MarketConstants marketConstants,
            IEnumerable<NpcDefinition> npcs,
            AppraisalConfig appraisal,
            NegotiationRules negotiation,
            CustomerConstants customers,
            DemandConstants demand,
            PersonalityCatalog personalities)
        {
            Personalities = personalities;
            Customers = customers;
            Demand = demand;
            Appraisal = appraisal;
            Negotiation = negotiation;
            TransactionTypes = transactionTypes;
            EconomyConstants = economyConstants;
            MarketConstants = marketConstants;
            var npcList = new List<NpcDefinition>(npcs);
            _npcsById = new Dictionary<string, NpcDefinition>(npcList.Count, StringComparer.Ordinal);
            for (int i = 0; i < npcList.Count; i++)
            {
                _npcsById.Add(npcList[i].Id, npcList[i]);
            }

            _npcs = new ReadOnlyCollection<NpcDefinition>(npcList);
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

        public bool TryGetNpc(string id, out NpcDefinition npc)
        {
            if (id == null)
            {
                npc = null;
                return false;
            }

            return _npcsById.TryGetValue(id, out npc);
        }

        /// <summary>ID yoksa KeyNotFoundException (programcı hatası).</summary>
        public NpcDefinition GetNpc(string id)
        {
            NpcDefinition npc;
            if (!TryGetNpc(id, out npc))
            {
                throw new KeyNotFoundException("Unknown NPC id '" + id + "'.");
            }

            return npc;
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

            // Ekonomi sabitleri (+ pazar bölümü)
            EconomyConstants economyConstants = null;
            MarketConstants marketConstants = null;
            CustomerConstants customerConstants = null;
            DemandConstants demandConstants = null;
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

                marketConstants = ContentParser.ParseMarketConstants(ContentFileNames.EconomyConstants, text, issues);
                customerConstants = ContentParser.ParseCustomerConstants(ContentFileNames.EconomyConstants, text, issues);
                if (customerConstants != null)
                {
                    ContentValidator.ValidateCustomers(customerConstants, ContentFileNames.EconomyConstants, issues);
                }

                demandConstants = ContentParser.ParseDemandConstants(ContentFileNames.EconomyConstants, text, issues);
                if (demandConstants != null)
                {
                    ContentValidator.ValidateDemand(demandConstants, ContentFileNames.EconomyConstants, issues);
                }
            }

            // NPC profilleri
            IReadOnlyList<NpcDefinition> npcs = null;
            PersonalityCatalog personalities = null;
            if (!source.TryGetText(ContentFileNames.NpcProfiles, out text))
            {
                issues.Add(ContentIssue.Error(ContentIssueCodes.FileMissing, ContentFileNames.NpcProfiles, "Content file not found."));
            }
            else
            {
                npcs = ContentParser.ParseNpcs(ContentFileNames.NpcProfiles, text, issues);
                if (npcs != null)
                {
                    ContentValidator.ValidateNpcs(npcs, ContentFileNames.NpcProfiles, issues);
                    personalities = ContentParser.ParsePersonalities(ContentFileNames.NpcProfiles, text, issues);
                }
            }

            // Ekspertiz kuralları
            AppraisalConfig appraisal = null;
            if (!source.TryGetText(ContentFileNames.AppraisalLevels, out text))
            {
                issues.Add(ContentIssue.Error(ContentIssueCodes.FileMissing, ContentFileNames.AppraisalLevels, "Content file not found."));
            }
            else
            {
                appraisal = ContentParser.ParseAppraisal(ContentFileNames.AppraisalLevels, text, issues);
                if (appraisal != null && valueTables != null)
                {
                    ContentValidator.ValidateAppraisal(appraisal, valueTables, ContentFileNames.AppraisalLevels, issues);
                }
            }

            if (customerConstants != null && npcs != null)
            {
                ContentValidator.ValidateCustomerLinks(customerConstants, npcs, ContentFileNames.EconomyConstants, issues);
            }

            // Pazarlık kuralları (rapor seviyesi ekspertiz seviyeleriyle çapraz denetlenir)
            NegotiationRules negotiation = null;
            if (!source.TryGetText(ContentFileNames.NegotiationRules, out text))
            {
                issues.Add(ContentIssue.Error(ContentIssueCodes.FileMissing, ContentFileNames.NegotiationRules, "Content file not found."));
            }
            else
            {
                negotiation = ContentParser.ParseNegotiation(ContentFileNames.NegotiationRules, text, issues);
                if (negotiation != null)
                {
                    ContentValidator.ValidateNegotiation(negotiation, ContentFileNames.NegotiationRules, issues);
                    if (appraisal != null)
                    {
                        ContentValidator.ValidateNegotiationLinks(negotiation, appraisal, ContentFileNames.NegotiationRules, issues);
                    }
                }
            }

            if (personalities != null && npcs != null && negotiation != null)
            {
                ContentValidator.ValidatePersonalities(personalities, npcs, negotiation, ContentFileNames.NpcProfiles, issues);
            }

            // Pazar kuralları ürünlere, NPC'lere ve değer tablolarına başvurur; önceki dosyalar temizse çapraz denetlenir
            // (bozuk bir dosyanın ardından art arda gelen başvuru hatası gürültüsünü önler).
            if (marketConstants != null && npcs != null && products != null && valueTables != null && !HasError(issues))
            {
                ContentValidator.ValidateMarket(marketConstants, npcs, products, valueTables, ContentFileNames.EconomyConstants, issues);
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
            if (products != null && npcs != null && manifestIds != null && !HasError(issues))
            {
                var contentIds = new List<string>(products.Count + npcs.Count);
                for (int i = 0; i < products.Count; i++)
                {
                    contentIds.Add(products[i].Id);
                }

                for (int i = 0; i < npcs.Count; i++)
                {
                    contentIds.Add(npcs[i].Id);
                }

                ContentValidator.ValidateManifest(manifestIds, contentIds, ContentFileNames.IdManifest, issues);
            }

            if (HasError(issues)
                || products == null
                || valueTables == null
                || conditionProfiles == null
                || transactionTypes == null
                || economyConstants == null
                || marketConstants == null
                || npcs == null
                || appraisal == null
                || negotiation == null
                || customerConstants == null
                || demandConstants == null
                || personalities == null)
            {
                return new ContentLoadResult(null, issues);
            }

            return new ContentLoadResult(
                new ContentDatabase(
                    products, valueTables, conditionProfiles, new TransactionTypes(transactionTypes), economyConstants, marketConstants, npcs, appraisal, negotiation, customerConstants, demandConstants, personalities),
                issues);
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
