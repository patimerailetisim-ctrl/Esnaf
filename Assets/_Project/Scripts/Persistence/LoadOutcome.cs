using System.Collections.Generic;
using System.Collections.ObjectModel;
using Esnaf.Domain.Game;

namespace Esnaf.Persistence
{
    public enum LoadStatus
    {
        /// <summary>Kayıt (ve yedek) yok: yeni oyun.</summary>
        NoSave = 0,

        /// <summary>Güncel kayıt yüklendi.</summary>
        Loaded = 1,

        /// <summary>Güncel kayıt bozuktu, bir yedek yüklendi ("Son kayıt bozuktu, önceki kayıt yüklendi (Gün X)").</summary>
        LoadedFromBackup = 2,

        /// <summary>Güncel kayıt ve yedeklerin hiçbiri okunamadı ("Kayıt bozuk"); bozuk dosyalar saklandı.</summary>
        Corrupt = 3,

        /// <summary>Kayıt daha yeni sürümle oluşturulmuş ("oyunu güncelle"); dosyalara dokunulmadı.</summary>
        NewerVersion = 4,

        /// <summary>Kayıt eski sürümde ve bu uygulamada ona giden migrasyon yolu yok; dosyalara dokunulmadı.</summary>
        UnsupportedVersion = 5
    }

    /// <summary>Yükleme sonucu. Metinler anahtar olarak verilir (T18: metinler tabloda); <see cref="Detail"/> yalnızca geliştirici içindir.</summary>
    public sealed class LoadOutcome
    {
        public LoadStatus Status { get; }

        /// <summary>Yüklenen oturum (Loaded / LoadedFromBackup); aksi halde null.</summary>
        public GameSession Session { get; }

        /// <summary>Oyuncuya gösterilecek metnin anahtarı (örn. "save.recovered_from_backup").</summary>
        public string MessageKey { get; }

        /// <summary>Yüklenen kaydın günü (yoksa 0).</summary>
        public int Day { get; }

        /// <summary>Yedekten yüklendiyse hangi dosya ("slot0.bak1.json").</summary>
        public string BackupName { get; }

        /// <summary>Bozuk bulunup yeniden adlandırılan dosyalar (".corrupt-…").</summary>
        public IReadOnlyList<string> CorruptFiles { get; }

        public string Detail { get; }

        public LoadOutcome(LoadStatus status, GameSession session, string messageKey, int day, string backupName, IEnumerable<string> corruptFiles, string detail)
        {
            Status = status;
            Session = session;
            MessageKey = messageKey;
            Day = day;
            BackupName = backupName;
            CorruptFiles = new ReadOnlyCollection<string>(new List<string>(corruptFiles ?? new string[0]));
            Detail = detail;
        }
    }
}
