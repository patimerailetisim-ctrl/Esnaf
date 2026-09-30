using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Esnaf.Core;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Market;
using Esnaf.Domain.Npc;
using Esnaf.Domain.Products;

namespace Esnaf.Domain.Game
{
    /// <summary>
    /// Oyun durumunun deterministik özeti (GDD I6: aynı tohum + aynı komutlar → aynı özet). Önce durumun her parçasını
    /// sabit sıralı, kültürden bağımsız bir METİN olarak yazar (<see cref="Describe"/>), sonra FNV-1a 64 ile 16 haneli
    /// onaltılık özete indirger (<see cref="Compute"/>). GİZLİ alanlar (ilan ret fiyatı vb.) ve rastgelelik akışlarının
    /// durumları da dahildir; yani özet "oyunun tüm geleceğini" belirleyen her şeye duyarlıdır.
    /// </summary>
    public static class GameStateDigest
    {
        public const string Version = "esnaf.state.v1";

        public static string Compute(GameSession session)
        {
            string text = Describe(session);
            unchecked
            {
                ulong hash = 0xcbf29ce484222325UL;
                byte[] bytes = Encoding.UTF8.GetBytes(text);
                for (int i = 0; i < bytes.Length; i++)
                {
                    hash ^= bytes[i];
                    hash *= 0x100000001b3UL;
                }

                return hash.ToString("x16", CultureInfo.InvariantCulture);
            }
        }

        public static string Describe(GameSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            var sb = new StringBuilder();
            sb.Append(Version).Append('\n');
            sb.Append("seed=").Append(session.Time.MasterSeed.ToString(CultureInfo.InvariantCulture)).Append('\n');
            sb.Append("day=").Append(N(session.Time.Day)).Append('\n');
            sb.Append("ids.instance=").Append(N(session.InstanceIds.LastIssued)).Append('\n');
            sb.Append("ids.listing=").Append(N(session.ListingIds.LastIssued)).Append('\n');

            AppendEconomy(sb, session);
            AppendInventory(sb, session);
            AppendInstances(sb, session);
            AppendMarket(sb, session);
            AppendNpcs(sb, session);
            AppendRng(sb, session);
            return sb.ToString();
        }

        private static void AppendEconomy(StringBuilder sb, GameSession session)
        {
            EconomyState economy = session.EconomyState;
            sb.Append("economy.cash=").Append(N(economy.Cash.Tl)).Append('\n');
            sb.Append("economy.businessAssets=").Append(N(economy.BusinessAssets.Tl)).Append('\n');
            sb.Append("ledger.count=").Append(N(economy.Ledger.Count)).Append('\n');
            foreach (TransactionRecord r in economy.Ledger.Records)
            {
                sb.Append("L|").Append(N(r.Id))
                    .Append('|').Append(N(r.Day))
                    .Append('|').Append(E(r.TypeId))
                    .Append('|').Append(N(r.Amount.Tl))
                    .Append('|').Append(N(r.BalanceAfter.Tl))
                    .Append('|').Append(r.InstanceId.HasValue ? N(r.InstanceId.Value) : string.Empty)
                    .Append('|').Append(E(r.DefinitionId))
                    .Append('|').Append(E(r.NpcId))
                    .Append('|').Append(r.SaleCostBasis.HasValue ? N(r.SaleCostBasis.Value.Tl) : string.Empty)
                    .Append('|').Append(r.RelatedRecordId.HasValue ? N(r.RelatedRecordId.Value) : string.Empty)
                    .Append('|').Append(E(r.MemoKey))
                    .Append('|').Append(r.MemoArgs == null ? string.Empty : string.Join(",", r.MemoArgs.Select(E)))
                    .Append('\n');
            }

            foreach (ProductInstance instance in session.Store.All.OrderBy(i => i.InstanceId))
            {
                IReadOnlyList<long> pending = economy.GetPendingAppraisalRecordIds(instance.InstanceId);
                if (pending.Count > 0)
                {
                    sb.Append("P|").Append(N(instance.InstanceId)).Append('|').Append(string.Join(",", pending.Select(N))).Append('\n');
                }
            }
        }

        private static void AppendInventory(StringBuilder sb, GameSession session)
        {
            sb.Append("inventory.capacity=").Append(N(session.InventoryState.Capacity)).Append('\n');
            sb.Append("inventory.items=").Append(string.Join(",", session.InventoryState.ItemIds.Select(N))).Append('\n');
        }

        private static void AppendInstances(StringBuilder sb, GameSession session)
        {
            foreach (ProductInstance i in session.Store.All)
            {
                sb.Append("I|").Append(N(i.InstanceId))
                    .Append('|').Append(E(i.DefinitionId))
                    .Append('|').Append(N(i.StorageGb))
                    .Append('|').Append(N(i.AgeMonths))
                    .Append('|').Append(E(i.SellerNpcId))
                    .Append('|').Append(N(i.ListingId))
                    .Append('|').Append(i.AcquiredDay.HasValue ? N(i.AcquiredDay.Value) : string.Empty)
                    .Append('|').Append(N(i.PurchasePrice.Tl))
                    .Append('|').Append(N(i.CostBasis.Tl))
                    .Append('|').Append(((int)i.Location).ToString(CultureInfo.InvariantCulture))
                    .Append('|').Append(string.Join(";", i.Attributes.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => E(p.Key) + "=" + Value(p.Value))))
                    .Append('\n');
            }
        }

        private static void AppendMarket(StringBuilder sb, GameSession session)
        {
            foreach (MarketListing l in session.Market.Listings)
            {
                sb.Append("M|").Append(N(l.ListingId))
                    .Append('|').Append(N(l.InstanceId))
                    .Append('|').Append(E(l.SellerNpcId))
                    .Append('|').Append(N(l.AskingPrice.Tl))
                    .Append('|').Append(string.Join(",", l.Tags.Select(E)))
                    .Append('|').Append(N(l.DayListed))
                    .Append('|').Append(N(l.RemainingDays))
                    .Append('|').Append(N(l.RejectPrice.Tl))
                    .Append('|').Append(N(l.BelievedValue.Tl))
                    .Append('|').Append(l.IsOpportunity ? '1' : '0')
                    .Append('|').Append(l.IsTrap ? '1' : '0')
                    .Append('|').Append(l.IsJackpot ? '1' : '0')
                    .Append('|').Append(l.IsGuided ? '1' : '0')
                    .Append('\n');
            }
        }

        private static void AppendNpcs(StringBuilder sb, GameSession session)
        {
            foreach (NpcState npc in session.Npcs.All)
            {
                sb.Append("N|").Append(E(npc.NpcId))
                    .Append('|').Append(N(npc.EncounterCount))
                    .Append('|').Append(string.Join(",", npc.SoldToPlayer.Select(N)))
                    .Append('|').Append(string.Join(",", npc.BoughtFromPlayer.Select(N)))
                    .Append('\n');
            }
        }

        private static void AppendRng(StringBuilder sb, GameSession session)
        {
            Dictionary<string, RngState> streams = session.Rng.Capture();
            foreach (string name in streams.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                sb.Append("R|").Append(E(name))
                    .Append('|').Append(streams[name].State.ToString(CultureInfo.InvariantCulture))
                    .Append('|').Append(streams[name].Increment.ToString(CultureInfo.InvariantCulture))
                    .Append('\n');
            }
        }

        private static string Value(AttributeValue v)
        {
            switch (v.Kind)
            {
                case AttributeKind.Number:
                    return N(v.Number);
                case AttributeKind.Text:
                    return E(v.Text);
                default:
                    return v.Flag ? "True" : "False";
            }
        }

        private static string N(long value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Ayraç karakterlerini kaçırır; böylece farklı alan bölünmeleri aynı metni üretemez.</summary>
        private static string E(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return text.Replace("\\", "\\\\").Replace("|", "\\|").Replace("\n", "\\n").Replace(",", "\\,").Replace(";", "\\;");
        }
    }
}
