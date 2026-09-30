using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Esnaf.Core;

namespace Esnaf.Domain.Time
{
    /// <summary>
    /// Gün sonu adımlarını sabit sırayla çalıştırır. Adım listesi kurulurken doğrulanır: sıra numaraları 1–8 aralığında ve
    /// KESİN artan, kimlikler benzersiz (programcı hatası = istisna). İlk başarısız adımda durur ve o adımı raporlar.
    /// Not: adımlar sırayla uygulanır; sonraki bir adım başarısız olursa önceki adımların etkisi geri alınmaz
    /// (yalnızca bozuk içerikte olur; oturum bu durumda atılmalıdır).
    /// </summary>
    public sealed class DayEndPipeline
    {
        private readonly ReadOnlyCollection<IDayEndStep> _steps;

        public IReadOnlyList<IDayEndStep> Steps
        {
            get { return _steps; }
        }

        public DayEndPipeline(IEnumerable<IDayEndStep> steps)
        {
            if (steps == null)
            {
                throw new ArgumentNullException(nameof(steps));
            }

            var list = new List<IDayEndStep>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            int previousOrder = 0;
            foreach (IDayEndStep step in steps)
            {
                if (step == null)
                {
                    throw new ArgumentNullException(nameof(steps), "A day-end step is null.");
                }

                if (string.IsNullOrWhiteSpace(step.Id))
                {
                    throw new ArgumentException("A day-end step has no id.", nameof(steps));
                }

                if (step.Order < DayEndOrder.MissedCustomers || step.Order > DayEndOrder.AutoSave)
                {
                    throw new ArgumentException("Step '" + step.Id + "' has order " + step.Order + "; it must be 1-8.", nameof(steps));
                }

                if (step.Order <= previousOrder)
                {
                    throw new ArgumentException(
                        "Step '" + step.Id + "' (order " + step.Order + ") must come after the previous step (order " + previousOrder + ").", nameof(steps));
                }

                if (!ids.Add(step.Id))
                {
                    throw new ArgumentException("Duplicate day-end step id '" + step.Id + "'.", nameof(steps));
                }

                previousOrder = step.Order;
                list.Add(step);
            }

            _steps = new ReadOnlyCollection<IDayEndStep>(list);
        }

        public Result Run(DayEndContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            for (int i = 0; i < _steps.Count; i++)
            {
                Result result = _steps[i].Execute(context);
                if (result.IsFailure)
                {
                    return Result.Fail(result.ErrorCode, "Day-end step '" + _steps[i].Id + "' failed: " + result.Message);
                }

                context.ExecutedStepIds.Add(_steps[i].Id);
            }

            return Result.Ok();
        }
    }
}
