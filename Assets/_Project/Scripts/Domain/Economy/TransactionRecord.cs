using System.Collections.Generic;
using System.Collections.ObjectModel;
using Esnaf.Core;

namespace Esnaf.Domain.Economy
{
    /// <summary>
    /// Defter satırı (GDD v0.3 4.3). DEĞİŞMEZ: bir kez yazılınca hiç değişmez. Tutar işaretlidir (giriş +, çıkış −).
    /// Satış satırı, satılan ürünün o andaki MALİYET TABANINI da taşır (satılan ürün ayrıca saklanmaz).
    /// </summary>
    public sealed class TransactionRecord
    {
        public long Id { get; }

        /// <summary>Oyun günü. Başlangıç sermayesi 0. gündedir.</summary>
        public int Day { get; }

        public string TypeId { get; }
        public TransactionCategory Category { get; }

        /// <summary>Hesap: MVP'de yalnızca "cash". İleride örn. "loan:&lt;id&gt;".</summary>
        public string Account { get; }

        public Money Amount { get; }

        /// <summary>Bu satırdan sonraki nakit bakiyesi.</summary>
        public Money BalanceAfter { get; }

        public long? InstanceId { get; }
        public string DefinitionId { get; }
        public string NpcId { get; }

        /// <summary>Yalnızca satışta: satılan ürünün maliyet tabanı (alış + ekspertiz + tamir).</summary>
        public Money? SaleCostBasis { get; }

        /// <summary>Yalnızca gider yazma satırında: yazılan ekspertiz satırının kimliği.</summary>
        public long? RelatedRecordId { get; }

        public string MemoKey { get; }
        public IReadOnlyList<string> MemoArgs { get; }

        public TransactionRecord(
            long id,
            int day,
            string typeId,
            TransactionCategory category,
            string account,
            Money amount,
            Money balanceAfter,
            long? instanceId,
            string definitionId,
            string npcId,
            Money? saleCostBasis,
            long? relatedRecordId,
            string memoKey,
            IEnumerable<string> memoArgs)
        {
            Id = id;
            Day = day;
            TypeId = typeId;
            Category = category;
            Account = account;
            Amount = amount;
            BalanceAfter = balanceAfter;
            InstanceId = instanceId;
            DefinitionId = definitionId;
            NpcId = npcId;
            SaleCostBasis = saleCostBasis;
            RelatedRecordId = relatedRecordId;
            MemoKey = memoKey;
            MemoArgs = new ReadOnlyCollection<string>(
                memoArgs == null ? new List<string>() : new List<string>(memoArgs));
        }
    }
}
