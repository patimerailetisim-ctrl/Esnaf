namespace Esnaf.Domain.Business
{
    /// <summary>Günlük müşteri kuyruğunun sabitleri (Gün 12.2): günde 8–12 müşteri. Kod sabitidir (içerik/şema değişmez); popülerlikle henüz bağlı değildir.</summary>
    public static class QueuePolicy
    {
        public const int MinCustomersPerDay = 8;
        public const int MaxCustomersPerDay = 12;

        /// <summary>Geliş saatleri bu dakika adımına yuvarlanır (5 dk).</summary>
        public const int ArrivalStepMinutes = 5;
    }
}
