namespace Esnaf.Domain.Content
{
    public enum ContentIssueSeverity
    {
        Warning = 0,
        Error = 1
    }

    /// <summary>İçerik yüklerken bulunan bir sorun. Hata (Error) varsa içerik yüklenmez.</summary>
    public sealed class ContentIssue
    {
        public ContentIssueSeverity Severity { get; }

        /// <summary>Kararlı makine kodu (bkz. <see cref="ContentIssueCodes"/>).</summary>
        public string Code { get; }

        public string File { get; }
        public string Message { get; }

        public ContentIssue(ContentIssueSeverity severity, string code, string file, string message)
        {
            Severity = severity;
            Code = code;
            File = file;
            Message = message;
        }

        public static ContentIssue Error(string code, string file, string message)
        {
            return new ContentIssue(ContentIssueSeverity.Error, code, file, message);
        }

        public static ContentIssue Warning(string code, string file, string message)
        {
            return new ContentIssue(ContentIssueSeverity.Warning, code, file, message);
        }

        public override string ToString()
        {
            return "[" + Severity + "] " + File + ": " + Code + ": " + Message;
        }
    }

    public static class ContentIssueCodes
    {
        // Dosya düzeyi
        public const string FileMissing = "file.missing";
        public const string FileEmpty = "file.empty";
        public const string FileSyntax = "file.syntax";
        public const string SchemaVersionMissing = "file.schema_version_missing";
        public const string SchemaVersionUnsupported = "file.schema_version_unsupported";
        public const string FieldMissing = "file.field_missing";

        // Ürün modeli kuralları
        public const string ProductsEmpty = "products.empty";
        public const string ProductIdFormat = "product.id.format";
        public const string ProductIdDuplicate = "product.id.duplicate";
        public const string ProductIdSectorMismatch = "product.id.sector_mismatch";
        public const string ProductSectorUnknown = "product.sector.unknown";
        public const string ProductNameEmpty = "product.name.empty";
        public const string ProductBrandEmpty = "product.brand.empty";
        public const string ProductSegmentInvalid = "product.segment.invalid";
        public const string ProductReleaseYearRange = "product.release_year.range";
        public const string ProductBasePriceNotPositive = "product.base_price.not_positive";
        public const string ProductBasePriceNotRounded = "product.base_price.not_rounded";
        public const string ProductStorageEmpty = "product.storage.empty";
        public const string ProductStorageGbNotPositive = "product.storage.gb_not_positive";
        public const string ProductStorageDuplicate = "product.storage.duplicate";
        public const string ProductStorageMultNotPositive = "product.storage.mult_not_positive";
        public const string ProductStorageBaseMissing = "product.storage.base_missing";
        public const string ProductStorageBaseMultNotOne = "product.storage.base_mult_not_one";
        public const string ProductAgeNegative = "product.age.negative";
        public const string ProductAgeMinGreaterThanMax = "product.age.min_greater_than_max";

        // Değer tabloları (value_tables.json)
        public const string ValueTablesAgeEmpty = "value_tables.age.empty";
        public const string ValueTablesAgeFirstNotZero = "value_tables.age.first_not_zero";
        public const string ValueTablesAgeNotIncreasing = "value_tables.age.not_increasing";
        public const string ValueTablesAgeMultNotPositive = "value_tables.age.mult_not_positive";
        public const string ValueTablesBatteryInvalid = "value_tables.battery.invalid";
        public const string ValueTablesBodyInvalid = "value_tables.body.invalid";
        public const string ValueTablesScreenMissingId = "value_tables.screen.missing_id";
        public const string ValueTablesScreenDuplicate = "value_tables.screen.duplicate";
        public const string ValueTablesScreenMultNotPositive = "value_tables.screen.mult_not_positive";
        public const string ValueTablesCameraMissingId = "value_tables.camera.missing_id";
        public const string ValueTablesCameraDuplicate = "value_tables.camera.duplicate";
        public const string ValueTablesCameraMultNotPositive = "value_tables.camera.mult_not_positive";
        public const string ValueTablesPackageNegative = "value_tables.package.negative";

        // Durum profilleri (condition_profiles.json)
        public const string ProfilesEmpty = "profiles.empty";
        public const string ProfilesNoDayOne = "profiles.no_day_one";
        public const string ProfileIdEmpty = "profile.id.empty";
        public const string ProfileIdDuplicate = "profile.id.duplicate";
        public const string ProfileWeightNotPositive = "profile.weight.not_positive";
        public const string ProfileDayInvalid = "profile.available_from_day.invalid";
        public const string ProfileRangeInvalid = "profile.range.invalid";
        public const string ProfileChoicesEmpty = "profile.choices.empty";
        public const string ProfileChoiceWeightNotPositive = "profile.choice.weight_not_positive";
        public const string ProfileChoiceUnknownValue = "profile.choice.unknown_value";
        public const string ProfileChoiceDuplicate = "profile.choice.duplicate";
        public const string ProfileChanceRange = "profile.chance.range";

        // ID manifesti
        public const string ManifestDuplicate = "manifest.duplicate";
        public const string ManifestIdMissingInContent = "manifest.id_missing_in_content";
        public const string ManifestIdNotRegistered = "manifest.id_not_registered";
    }
}
