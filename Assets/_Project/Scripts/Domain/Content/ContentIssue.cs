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

        // ID manifesti
        public const string ManifestDuplicate = "manifest.duplicate";
        public const string ManifestIdMissingInContent = "manifest.id_missing_in_content";
        public const string ManifestIdNotRegistered = "manifest.id_not_registered";
    }
}
