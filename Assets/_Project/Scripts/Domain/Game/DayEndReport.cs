using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Esnaf.Core;
using Esnaf.Domain.Economy;

namespace Esnaf.Domain.Game
{
    /// <summary>"Günü Bitir" sonucu: biten günün özeti ve gün sonu adımlarının neler yaptığı.</summary>
    public sealed class DayEndReport
    {
        public int EndedDay { get; }
        public int NewDay { get; }

        /// <summary>Adım 2: o gün işlenen günlük gider (Gün 1–2'de 0).</summary>
        public Money ExpenseCharged { get; }

        /// <summary>Adım 4: kaldırılan ilanlar.</summary>
        public IReadOnlyList<long> ExpiredListingIds { get; }

        /// <summary>Adım 7: yeni günün ilanları.</summary>
        public IReadOnlyList<long> NewListingIds { get; }

        /// <summary>Çalışan adımların kimlikleri, sırasıyla.</summary>
        public IReadOnlyList<string> ExecutedSteps { get; }

        /// <summary>Biten günün özeti (Gün Sonu Özeti ekranı).</summary>
        public DaySummary Summary { get; }

        /// <summary>Gün sonu adım 1: satın almadan giden müşteri sayısı.</summary>
        public int MissedCustomers { get; }

        public DayEndReport(
            int endedDay,
            int newDay,
            Money expenseCharged,
            IEnumerable<long> expiredListingIds,
            IEnumerable<long> newListingIds,
            IEnumerable<string> executedSteps,
            DaySummary summary,
            int missedCustomers = 0)
        {
            if (expiredListingIds == null)
            {
                throw new ArgumentNullException(nameof(expiredListingIds));
            }

            if (newListingIds == null)
            {
                throw new ArgumentNullException(nameof(newListingIds));
            }

            if (executedSteps == null)
            {
                throw new ArgumentNullException(nameof(executedSteps));
            }

            if (summary == null)
            {
                throw new ArgumentNullException(nameof(summary));
            }

            EndedDay = endedDay;
            NewDay = newDay;
            ExpenseCharged = expenseCharged;
            ExpiredListingIds = new ReadOnlyCollection<long>(new List<long>(expiredListingIds));
            NewListingIds = new ReadOnlyCollection<long>(new List<long>(newListingIds));
            ExecutedSteps = new ReadOnlyCollection<string>(new List<string>(executedSteps));
            Summary = summary;
            MissedCustomers = missedCustomers;
        }
    }
}
