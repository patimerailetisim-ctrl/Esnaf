using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Esnaf.Core;
using Esnaf.Domain.Content;
using Esnaf.Domain.Game;

namespace Esnaf.Persistence
{
    /// <summary>
    /// Kayıt/yükleme/yedek/bozuk kurtarma (GDD v0.3 6.1, 6.3, 6.5). Kayıt <c>IGameApi</c>'den geçmez (UA2): bu sınıf doğrudan
    /// <c>GameSession.Capture/Restore</c> kullanır. 1 slot ("slot0"), 2 yedek.
    ///
    /// Dosyalar: <c>slot0.json</c> (güncel), <c>slot0.bak1.json</c>, <c>slot0.bak2.json</c>, <c>slot0.tmp</c> (yazılmakta; yarım kalırsa yok sayılır),
    /// <c>slot0.corrupt-YYYYMMDD-HHMMSS.json</c> (bozuk bulunan; asla silinmez).
    /// </summary>
    public sealed class SaveService
    {
        public const string SlotName = "slot0";
        public const string CurrentFile = SlotName + ".json";
        public const string Backup1File = SlotName + ".bak1.json";
        public const string Backup2File = SlotName + ".bak2.json";
        public const string TempFile = SlotName + ".tmp";

        private readonly ISaveStorage _storage;
        private readonly SaveSerializer _serializer;
        private readonly ISaveClock _clock;
        private readonly string _appVersion;

        /// <summary>Son başarısız kaydın nedeni (kayıt başarılı olunca temizlenir).</summary>
        public string LastError { get; private set; }

        public SaveService(ISaveStorage storage, SaveSerializer serializer, ISaveClock clock, string appVersion)
        {
            if (storage == null)
            {
                throw new ArgumentNullException(nameof(storage));
            }

            if (serializer == null)
            {
                throw new ArgumentNullException(nameof(serializer));
            }

            if (clock == null)
            {
                throw new ArgumentNullException(nameof(clock));
            }

            _storage = storage;
            _serializer = serializer;
            _clock = clock;
            _appVersion = appVersion ?? string.Empty;
        }

        // ---------- yazma (GDD 6.3) ----------

        /// <summary>
        /// Atomik kayıt: topla → JSON → tmp'ye yaz/boşalt → tmp'yi GERİ OKU ve doğrula → bak1→bak2, slot0→bak1 KOPYALA → tmp→slot0 atomik değiştir.
        /// Herhangi bir adım başarısızsa <c>slot0</c> dokunulmadan kalır (yedekleme adımları kopyadır), tmp silinmeye çalışılır, hata döner.
        /// </summary>
        public Result Save(GameSession session, long playTimeSeconds)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            try
            {
                string now = Stamp(_clock.UtcNow);
                GameSnapshot snapshot = session.Capture();
                var preview = new SavePreview
                {
                    Day = session.Api.GetDay(),
                    Cash = session.Api.GetCash().Tl,
                    Wealth = session.Wealth.Calculate().Total.Tl
                };
                var meta = new SaveMeta
                {
                    AppVersion = _appVersion,
                    ContentSchemaVersion = ContentFileNames.SupportedSchemaVersion,
                    CreatedAtUtc = ExistingCreatedAt() ?? now,
                    SavedAtUtc = now,
                    PlayTimeSeconds = playTimeSeconds
                };
                string text = _serializer.Serialize(snapshot, meta, preview);

                _storage.Delete(TempFile); // önceki yarım kalmış yazım
                _storage.WriteAllText(TempFile, text);
                Result<ParsedSave> verified = _serializer.Parse(_storage.ReadAllText(TempFile));
                if (verified.IsFailure)
                {
                    return Fail(session, "save.verify_failed", "The written file did not verify: " + verified.ErrorCode + " " + verified.Message);
                }

                if (_storage.Exists(Backup1File))
                {
                    _storage.Copy(Backup1File, Backup2File);
                }

                if (_storage.Exists(CurrentFile))
                {
                    _storage.Copy(CurrentFile, Backup1File);
                }

                _storage.Replace(TempFile, CurrentFile);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return Fail(session, "save.io", ex.Message);
            }

            LastError = null;
            session.Bus.Publish(new GameSaved(session.Api.GetDay()));
            return Result.Ok();
        }

        private Result Fail(GameSession session, string code, string message)
        {
            try
            {
                _storage.Delete(TempFile);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // tmp zaten yok sayılır; temizlenememesi kaydı etkilemez
            }

            LastError = message;
            return Result.Fail(code, message);
        }

        private string ExistingCreatedAt()
        {
            if (!_storage.Exists(CurrentFile))
            {
                return null;
            }

            try
            {
                Result<SaveHeader> header = _serializer.ReadHeader(_storage.ReadAllText(CurrentFile));
                return header.IsSuccess && !string.IsNullOrEmpty(header.Value.CreatedAtUtc) ? header.Value.CreatedAtUtc : null;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return null;
            }
        }

        // ---------- yükleme (GDD 6.5) ----------

        /// <summary>
        /// Yükleme akışı: slot0 → (bozuksa .corrupt olarak yeniden adlandır) → bak1 → bak2. Daha yeni/desteklenmeyen sürüm bozuk SAYILMAZ,
        /// dosyalara dokunulmaz. Yüklenen oturumun olay yoluna <c>GameLoaded</c> yayınlanır.
        /// </summary>
        public LoadOutcome Load(ContentDatabase content, IEventBus events = null)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            var corrupt = new List<string>();
            string[] candidates = { CurrentFile, Backup1File, Backup2File };
            bool any = false;
            string lastDetail = null;
            for (int i = 0; i < candidates.Length; i++)
            {
                string name = candidates[i];
                if (!_storage.Exists(name))
                {
                    continue;
                }

                any = true;
                GameSession session;
                string code;
                string detail;
                int day;
                if (TryLoad(name, content, events, out session, out code, out detail, out day))
                {
                    bool backup = i > 0;
                    session.Bus.Publish(new GameLoaded(day, backup));
                    return new LoadOutcome(
                        backup ? LoadStatus.LoadedFromBackup : LoadStatus.Loaded,
                        session,
                        backup ? "save.recovered_from_backup" : "save.loaded",
                        day,
                        backup ? name : null,
                        corrupt,
                        null);
                }

                if (code == "save.version_newer")
                {
                    return new LoadOutcome(LoadStatus.NewerVersion, null, "save.newer_version", 0, null, corrupt, detail);
                }

                if (code == "save.version_unsupported")
                {
                    return new LoadOutcome(LoadStatus.UnsupportedVersion, null, "save.unsupported_version", 0, null, corrupt, detail);
                }

                lastDetail = detail;
                corrupt.Add(MoveToCorrupt(name));
            }

            if (!any)
            {
                return new LoadOutcome(LoadStatus.NoSave, null, "save.none", 0, null, corrupt, null);
            }

            return new LoadOutcome(LoadStatus.Corrupt, null, "save.corrupt", 0, null, corrupt, lastDetail);
        }

        private bool TryLoad(string name, ContentDatabase content, IEventBus events, out GameSession session, out string code, out string detail, out int day)
        {
            session = null;
            code = null;
            detail = null;
            day = 0;
            string text;
            try
            {
                text = _storage.ReadAllText(name);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                code = "save.io";
                detail = ex.Message;
                return false;
            }

            Result<ParsedSave> parsed = _serializer.Parse(text);
            if (parsed.IsFailure)
            {
                code = parsed.ErrorCode;
                detail = parsed.ErrorCode + ": " + parsed.Message;
                return false;
            }

            Result<GameSession> restored = GameSession.Restore(content, parsed.Value.Snapshot, events);
            if (restored.IsFailure)
            {
                code = restored.ErrorCode;
                detail = restored.ErrorCode + ": " + restored.Message;
                return false;
            }

            session = restored.Value;
            day = session.Api.GetDay();
            return true;
        }

        private string MoveToCorrupt(string name)
        {
            string stamp = _clock.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string target = SlotName + ".corrupt-" + stamp + ".json";
            for (int n = 2; _storage.Exists(target); n++)
            {
                target = SlotName + ".corrupt-" + stamp + "-" + n.ToString(CultureInfo.InvariantCulture) + ".json";
            }

            try
            {
                _storage.Replace(name, target);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return name; // yeniden adlandırılamadı; dosya yerinde kalır (yine de bozuk sayıldı)
            }

            return target;
        }

        // ---------- önizleme, silme ----------

        /// <summary>"Devam Et" kartı için: slot0 başlığındaki özet (yüklemeden). Kayıt yoksa ya da başlık okunamazsa null.</summary>
        public SavePreview ReadPreview()
        {
            if (!_storage.Exists(CurrentFile))
            {
                return null;
            }

            try
            {
                Result<SaveHeader> header = _serializer.ReadHeader(_storage.ReadAllText(CurrentFile));
                return header.IsSuccess ? header.Value.Preview : null;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return null;
            }
        }

        /// <summary>Kaydı ve yedeklerini siler (GDD 6.8 "Kayıt silme"); ".corrupt-…" dosyaları incelenmek üzere SAKLANIR.</summary>
        public Result Delete()
        {
            try
            {
                foreach (string name in new[] { CurrentFile, Backup1File, Backup2File, TempFile })
                {
                    _storage.Delete(name);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return Result.Fail("save.io", ex.Message);
            }

            return Result.Ok();
        }

        private static string Stamp(DateTime utc)
        {
            return utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        }
    }
}
