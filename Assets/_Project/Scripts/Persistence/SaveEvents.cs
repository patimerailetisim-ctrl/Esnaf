namespace Esnaf.Persistence
{
    /// <summary>Bildirim: kayıt başarıyla yazıldı (GDD v0.3 3.5).</summary>
    public sealed class GameSaved
    {
        public int Day { get; }

        public GameSaved(int day)
        {
            Day = day;
        }
    }

    /// <summary>Bildirim: kayıt yüklendi (GDD v0.3 3.5).</summary>
    public sealed class GameLoaded
    {
        public int Day { get; }

        /// <summary>Güncel kayıt bozuk olduğundan bir yedekten yüklendi.</summary>
        public bool FromBackup { get; }

        public GameLoaded(int day, bool fromBackup)
        {
            Day = day;
            FromBackup = fromBackup;
        }
    }
}
