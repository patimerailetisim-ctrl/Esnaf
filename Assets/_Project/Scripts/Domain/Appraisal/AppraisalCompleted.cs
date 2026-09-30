using Esnaf.Core;

namespace Esnaf.Domain.Appraisal
{
    /// <summary>Bildirim: yeni bir ekspertiz sonucu üretildi ve kaydedildi (durum değişikliğinden SONRA yayınlanır; tekrar çağrı yayınlamaz).</summary>
    public sealed class AppraisalCompleted
    {
        public long ResultId { get; }
        public long InstanceId { get; }
        public string LevelId { get; }
        public int Day { get; }
        public Money Fee { get; }

        public AppraisalCompleted(long resultId, long instanceId, string levelId, int day, Money fee)
        {
            ResultId = resultId;
            InstanceId = instanceId;
            LevelId = levelId;
            Day = day;
            Fee = fee;
        }
    }
}
