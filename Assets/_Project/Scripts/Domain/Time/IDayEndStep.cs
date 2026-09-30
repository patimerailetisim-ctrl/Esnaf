using Esnaf.Core;

namespace Esnaf.Domain.Time
{
    /// <summary>
    /// Gün sonu boru hattının tek adımı (GDD v0.3 3.4, T16). Her adım tek iş yapar; sırası <see cref="DayEndOrder"/> numarasıyla
    /// sabittir. Yeni adım eklemek = sınıf yazıp <see cref="DayEndPipeline"/> listesine eklemek; mevcut adımlar değişmez.
    /// </summary>
    public interface IDayEndStep
    {
        /// <summary>Kararlı, benzersiz ad (rapor ve test için).</summary>
        string Id { get; }

        /// <summary><see cref="DayEndOrder"/> tablosundaki sıra numarası (1–8).</summary>
        int Order { get; }

        Result Execute(DayEndContext context);
    }
}
