using System;
using Esnaf.Core;
using Esnaf.Domain.Business;

namespace Esnaf.Domain.Time
{
    /// <summary>Gün sonu adım 1: satın almadan giden (kaçan) müşterileri kaydeder (GDD v0.3 3.4; sahibi Business).</summary>
    public sealed class MissedCustomersStep : IDayEndStep
    {
        private readonly CustomerService _customers;

        public string Id
        {
            get { return "missed_customers"; }
        }

        public int Order
        {
            get { return DayEndOrder.MissedCustomers; }
        }

        public MissedCustomersStep(CustomerService customers)
        {
            if (customers == null)
            {
                throw new ArgumentNullException(nameof(customers));
            }

            _customers = customers;
        }

        public Result Execute(DayEndContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            context.MissedCustomers = _customers.EndDay();
            return Result.Ok();
        }
    }
}
