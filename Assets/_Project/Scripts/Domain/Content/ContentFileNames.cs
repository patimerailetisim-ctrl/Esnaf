namespace Esnaf.Domain.Content
{
    /// <summary>MVP içerik dosyalarının adları ve desteklenen şema sürümü. (Diğer dosyalar sonraki günlerde eklenir.)</summary>
    public static class ContentFileNames
    {
        public const string PhoneModels = "phone_models.json";
        public const string IdManifest = "content_id_manifest.json";
        public const string ValueTables = "value_tables.json";
        public const string ConditionProfiles = "condition_profiles.json";

        /// <summary>Her içerik dosyasının en üstündeki "schemaVersion" değeri bu olmalı.</summary>
        public const int SupportedSchemaVersion = 1;
    }
}
