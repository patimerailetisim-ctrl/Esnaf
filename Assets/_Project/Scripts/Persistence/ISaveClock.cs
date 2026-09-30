using System;

namespace Esnaf.Persistence
{
    /// <summary>
    /// Gerçek saat kaynağı. YALNIZCA kayıt üst verisi ve ".corrupt-YYYYMMDD-HHMMSS" dosya adı için kullanılır; oyun kuralları asla
    /// gerçek saate bakmaz (GDD K8).
    /// </summary>
    public interface ISaveClock
    {
        DateTime UtcNow { get; }
    }
}
