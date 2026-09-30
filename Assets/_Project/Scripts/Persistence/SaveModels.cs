namespace Esnaf.Persistence
{
    /// <summary>Ana menüdeki "Devam Et" kartı için özet (yükleme yapmadan okunabilir; GDD v0.3 6.2).</summary>
    public sealed class SavePreview
    {
        public int Day { get; set; }
        public long Cash { get; set; }
        public long Wealth { get; set; }
    }

    /// <summary>Kaydı yazarken çağıranın verdiği üst veri. Gerçek saat yalnızca burada ve .corrupt dosya adında kullanılır (K8).</summary>
    public sealed class SaveMeta
    {
        public string AppVersion { get; set; }
        public int ContentSchemaVersion { get; set; }

        /// <summary>ISO-8601 UTC ("2026-01-02T03:04:05Z").</summary>
        public string CreatedAtUtc { get; set; }

        public string SavedAtUtc { get; set; }
        public long PlayTimeSeconds { get; set; }
    }

    /// <summary>Kayıt dosyasının başlığı (GDD v0.3 6.2).</summary>
    public sealed class SaveHeader
    {
        public string Format { get; set; }
        public int SaveVersion { get; set; }
        public string AppVersion { get; set; }
        public int ContentSchemaVersion { get; set; }
        public string CreatedAtUtc { get; set; }
        public string SavedAtUtc { get; set; }
        public long PlayTimeSeconds { get; set; }

        /// <summary>"sha256:…": yükün kanonik metninin özeti.</summary>
        public string Checksum { get; set; }

        public SavePreview Preview { get; set; }
    }

    /// <summary>Ayrıştırılıp doğrulanmış kayıt.</summary>
    public sealed class ParsedSave
    {
        public SaveHeader Header { get; }
        public Esnaf.Domain.Game.GameSnapshot Snapshot { get; }

        public ParsedSave(SaveHeader header, Esnaf.Domain.Game.GameSnapshot snapshot)
        {
            Header = header;
            Snapshot = snapshot;
        }
    }
}
