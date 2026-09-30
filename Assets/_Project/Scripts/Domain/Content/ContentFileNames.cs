namespace Esnaf.Domain.Content
{
    /// <summary>MVP içerik dosyalarının adları ve desteklenen şema sürümü. (Diğer dosyalar sonraki günlerde eklenir.)</summary>
    public static class ContentFileNames
    {
        public const string PhoneModels = "phone_models.json";
        public const string IdManifest = "content_id_manifest.json";
        public const string ValueTables = "value_tables.json";
        public const string ConditionProfiles = "condition_profiles.json";
        public const string TransactionTypes = "transaction_types.json";
        public const string EconomyConstants = "economy_constants.json";
        public const string NpcProfiles = "npc_profiles.json";
        public const string AppraisalLevels = "appraisal_levels.json";

        /// <summary>Her içerik dosyasının en üstündeki "schemaVersion" değeri bu olmalı.</summary>
        public const int SupportedSchemaVersion = 1;
    }
}
