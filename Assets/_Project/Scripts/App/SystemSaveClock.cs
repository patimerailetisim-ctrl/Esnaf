using System;
using Esnaf.Persistence;

namespace Esnaf.App
{
    /// <summary>
    /// Gerçek saat (<see cref="ISaveClock"/>). YALNIZCA App katmanı bilir: kural katmanları (Core/Domain/Persistence) gerçek saate bakmaz
    /// (GDD K8); saat yalnızca kayıt üst verisi ve ".corrupt-…" dosya adı için enjekte edilir.
    /// </summary>
    public sealed class SystemSaveClock : ISaveClock
    {
        public DateTime UtcNow
        {
            get { return DateTime.UtcNow; }
        }
    }
}
