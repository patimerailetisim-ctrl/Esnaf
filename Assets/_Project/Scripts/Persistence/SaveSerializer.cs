using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Esnaf.Core;
using Esnaf.Domain.Game;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Esnaf.Persistence
{
    /// <summary>
    /// Kayıt dosyasının metin biçimi (GDD v0.3 6.2): <c>{ header, payload }</c>. Sağlama, <c>payload</c> düğümünün KANONİK (girintisiz) metninin
    /// SHA-256 özetidir: dosya girintili yazılsa da, boşlukları değişse de aynı çıkar. Sağlama bozulmayı fark etmek içindir, hile önleme değil.
    /// Okuma sırası: ayrıştır → biçim → sürüm → sağlama → migrasyon → tipli nesne. Yazma belirlenimcidir (aynı durum → aynı metin).
    /// </summary>
    public sealed class SaveSerializer
    {
        public const string FormatName = "esnaf-save";

        /// <summary>Üretimde kayıt biçimi sürümü (MVP'de yalnızca 1).</summary>
        public const int DefaultVersion = 1;

        private static readonly JsonSerializer Json = JsonSerializer.Create(new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            DateParseHandling = DateParseHandling.None,
            FloatParseHandling = FloatParseHandling.Double,
            MissingMemberHandling = MissingMemberHandling.Ignore
        });

        private readonly MigrationRunner _migrations;

        public int CurrentVersion
        {
            get { return _migrations.CurrentVersion; }
        }

        public SaveSerializer(MigrationRunner migrations = null)
        {
            _migrations = migrations ?? new MigrationRunner(DefaultVersion, new IMigration[0]);
        }

        public static string ComputeChecksum(string payloadText)
        {
            if (payloadText == null)
            {
                throw new ArgumentNullException(nameof(payloadText));
            }

            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(payloadText));
                var sb = new StringBuilder("sha256:", 7 + hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
                }

                return sb.ToString();
            }
        }

        public string Serialize(GameSnapshot snapshot, SaveMeta meta, SavePreview preview)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (meta == null)
            {
                throw new ArgumentNullException(nameof(meta));
            }

            if (preview == null)
            {
                throw new ArgumentNullException(nameof(preview));
            }

            JObject payload = JObject.FromObject(snapshot, Json);
            var header = new SaveHeader
            {
                Format = FormatName,
                SaveVersion = CurrentVersion,
                AppVersion = meta.AppVersion,
                ContentSchemaVersion = meta.ContentSchemaVersion,
                CreatedAtUtc = meta.CreatedAtUtc,
                SavedAtUtc = meta.SavedAtUtc,
                PlayTimeSeconds = meta.PlayTimeSeconds,
                Checksum = ComputeChecksum(payload.ToString(Formatting.None)),
                Preview = preview
            };

            var root = new JObject
            {
                ["header"] = JObject.FromObject(header, Json),
                ["payload"] = payload
            };
            return root.ToString(Formatting.Indented);
        }

        /// <summary>Yalnızca başlığı okur (önizleme): yükü doğrulamaz. Hatalar: save.parse, save.format.</summary>
        public Result<SaveHeader> ReadHeader(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            JObject root;
            Result<SaveHeader> failure;
            if (!TryParseRoot(text, out root, out failure))
            {
                return failure;
            }

            JObject headerToken = root["header"] as JObject;
            if (headerToken == null)
            {
                return Result<SaveHeader>.Fail("save.format", "The file has no header.");
            }

            try
            {
                SaveHeader header = headerToken.ToObject<SaveHeader>(Json);
                if (header == null || header.Format != FormatName)
                {
                    return Result<SaveHeader>.Fail("save.format", "This is not an " + FormatName + " file.");
                }

                return Result<SaveHeader>.Ok(header);
            }
            catch (JsonException ex)
            {
                return Result<SaveHeader>.Fail("save.format", "The header is malformed: " + ex.Message);
            }
        }

        /// <summary>Ayrıştırır ve doğrular. Hatalar: save.parse, save.format, save.version_newer, save.version_unsupported, save.checksum, save.migration, save.payload.</summary>
        public Result<ParsedSave> Parse(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            JObject root;
            Result<SaveHeader> parseFailure;
            if (!TryParseRoot(text, out root, out parseFailure))
            {
                return Result<ParsedSave>.Fail(parseFailure.ErrorCode, parseFailure.Message);
            }

            JObject headerToken = root["header"] as JObject;
            JObject payload = root["payload"] as JObject;
            if (headerToken == null || payload == null)
            {
                return Result<ParsedSave>.Fail("save.format", "The file needs a 'header' and a 'payload' object.");
            }

            SaveHeader header;
            JToken versionToken = headerToken["saveVersion"];
            JToken checksumToken = headerToken["checksum"];
            if (!string.Equals((string)headerToken["format"], FormatName, StringComparison.Ordinal)
                || versionToken == null || versionToken.Type != JTokenType.Integer
                || checksumToken == null || checksumToken.Type != JTokenType.String)
            {
                return Result<ParsedSave>.Fail("save.format", "The header is missing the format, saveVersion or checksum.");
            }

            try
            {
                header = headerToken.ToObject<SaveHeader>(Json);
            }
            catch (JsonException ex)
            {
                return Result<ParsedSave>.Fail("save.format", "The header is malformed: " + ex.Message);
            }

            if (header == null || header.SaveVersion > CurrentVersion)
            {
                return Result<ParsedSave>.Fail("save.version_newer", "The save was made by a newer version; update the game.");
            }

            string actual = ComputeChecksum(payload.ToString(Formatting.None));
            if (!string.Equals(actual, header.Checksum, StringComparison.Ordinal))
            {
                return Result<ParsedSave>.Fail("save.checksum", "The payload does not match its checksum.");
            }

            Result migrated = _migrations.Migrate(payload, header.SaveVersion);
            if (migrated.IsFailure)
            {
                return Result<ParsedSave>.Fail(migrated.ErrorCode, migrated.Message);
            }

            try
            {
                GameSnapshot snapshot = payload.ToObject<GameSnapshot>(Json);
                if (snapshot == null)
                {
                    return Result<ParsedSave>.Fail("save.payload", "The payload is empty.");
                }

                return Result<ParsedSave>.Ok(new ParsedSave(header, snapshot));
            }
            catch (JsonException ex)
            {
                return Result<ParsedSave>.Fail("save.payload", "The payload could not be read: " + ex.Message);
            }
        }

        private static bool TryParseRoot(string text, out JObject root, out Result<SaveHeader> failure)
        {
            root = null;
            failure = default(Result<SaveHeader>);
            if (string.IsNullOrWhiteSpace(text))
            {
                failure = Result<SaveHeader>.Fail("save.parse", "The file is empty.");
                return false;
            }

            try
            {
                using (var reader = new JsonTextReader(new StringReader(text)) { DateParseHandling = DateParseHandling.None })
                {
                    root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    while (reader.Read())
                    {
                        if (reader.TokenType != JsonToken.Comment)
                        {
                            throw new JsonReaderException("Additional text found after the JSON document.");
                        }
                    }
                }

                return true;
            }
            catch (JsonException ex)
            {
                failure = Result<SaveHeader>.Fail("save.parse", "The file is not valid JSON: " + ex.Message);
                return false;
            }
        }
    }
}
