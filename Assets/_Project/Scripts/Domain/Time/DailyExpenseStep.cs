using System;
using Esnaf.Core;
using Esnaf.Domain.Economy;

namespace Esnaf.Domain.Time
{
    /// <summary>Gün sonu adım 2: günlük gider (Gün 1–2'de 0, Gün 3'ten itibaren 500; sayılar veri dosyasından).</summary>
    public sealed class DailyExpenseStep : IDayEndStep
    {
        private readonly EconomyService _economy;

        public string Id
        {
            get { return "daily_expense"; }
        }

        public int Order
        {
            get { return DayEndOrder.DailyExpense; }
        }

        public DailyExpenseStep(EconomyService economy)
        {
            if (economy == null)
            {
                throw new ArgumentNullException(nameof(economy));
            }

            _economy = economy;
        }

        public Result Execute(DayEndContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            Result<Money> charged = _economy.ChargeDailyExpense(context.Day);
            if (charged.IsFailure)
            {
                return Result.Fail(charged.ErrorCode, charged.Message);
            }

            context.ExpenseCharged = charged.Value;
            return Result.Ok();
        }
    }
}
