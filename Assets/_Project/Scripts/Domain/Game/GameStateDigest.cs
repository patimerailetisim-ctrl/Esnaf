using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Esnaf.Core;
using Esnaf.Domain.Appraisal;
using Esnaf.Domain.Business;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Market;
using Esnaf.Domain.Negotiation;
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
            sb.Append("ids.appraisal=").Append(N(session.AppraisalIds.LastIssued)).Append('\n');
            sb.Append("ids.customer=").Append(N(session.CustomerIds.LastIssued)).Append('\n');

            AppendEconomy(sb, session);
            AppendInventory(sb, session);
            AppendInstances(sb, session);
            AppendMarket(sb, session);
            AppendNpcs(sb, session);
            AppendKnowledge(sb, session);
            AppendTrade(sb, session);
            AppendBusiness(sb, session);
            AppendAccessories(sb, session);
            AppendRng(sb, session);
            return sb.ToString();
        }

        // Yalnızca stok doluyken yazılır: aksesuarsız oyunların özeti (ve fixture özeti) eskisiyle aynı kalır.
        private static void AppendAccessories(StringBuilder sb, GameSession session)
        {
            if (session.AccessoryStock.TotalUnits == 0)
            {
                return;
            }

            foreach (string id in session.AccessoryStock.AccessoryIds)
            {
                sb.Append("accessory.").Append(id).Append('=')
                    .Append(N(session.AccessoryStock.Quantity(id))).Append(',')
                    .Append(N(session.AccessoryStock.TotalCost(id).Tl)).Append('\n');
            }
        }

        private static void AppendTrade(StringBuilder sb, GameSession session)
        {
            ActiveNegotiation a = session.TradeState.Current;
            if (a == null)
            {
                return;
            }

            NegotiationState n = a.State;
            sb.Append("G|").Append(N(a.ListingId))
                .Append('|').Append(N(a.InstanceId))
                .Append('|').Append(E(a.SellerNpcId))
                .Append('|').Append(N((int)n.Phase))
                .Append('|').Append(N(n.Round))
                .Append('|').Append(N(n.Patience))
                .Append('|').Append(N(n.Trust))
                .Append('|').Append(Bits(n.Reject))
                .Append('|').Append(Bits(n.Price))
                .Append('|').Append(N(n.ShownPrice.Tl))
                .Append('|').Append(N(n.DealPrice.Tl))
                .Append('|').Append(a.LastOfferInsulted ? "1" : "0")
                .Append('|').Append(string.Join(",", n.UsedCards.Select(E)))
                .Append('|').Append(Bits(n.Setup.Reject))
                .Append('|').Append(Bits(n.Setup.Floor))
                .Append('|').Append(N(n.Setup.Patience))
                .Append('|').Append(N(n.Setup.Trust))
                .Append('|').Append(N(n.Setup.Ask.Tl))
                .Append('|').Append(Bits(n.Setup.Urgency))
                .Append('|').Append(Bits(n.Setup.Persuasion))
                .Append('|').Append(Bits(n.Setup.WrongCardMultiplier))
                .Append('|').Append(N(n.Setup.Day))
                .Append('\n');
        }

        private static void AppendBusiness(StringBuilder sb, GameSession session)
        {
            CustomerState customers = session.Customers.State;
            sb.Append("customers.arrived=").Append(N(customers.Arrived)).Append('\n');
            sb.Append("customers.missedTotal=").Append(N(customers.MissedTotal)).Append('\n');
            foreach (CustomerSlot slot in customers.Slots)
            {
                sb.Append("K|").Append(N(slot.CustomerId))
                    .Append('|').Append(E(slot.NpcId))
                    .Append('|').Append(Bits(slot.ValueDraw))
                    .Append('|').Append(Bits(slot.TrustDraw))
                    .Append('|').Append(Bits(slot.PickDraw))
                    .Append('|').Append(N((int)slot.Status))
                    .Append('\n');
            }

            ActiveSale sale = session.TradeState.CurrentSale;
            if (sale != null)
            {
                SaleState n = sale.State;
                sb.Append("S|").Append(N(sale.CustomerId))
                    .Append('|').Append(N(sale.InstanceId))
                    .Append('|').Append(E(sale.NpcId))
                    .Append('|').Append(N((int)n.Phase))
                    .Append('|').Append(N(n.Round))
                    .Append('|').Append(N(n.Patience))
                    .Append('|').Append(N(n.Trust))
                    .Append('|').Append(Bits(n.Max))
                    .Append('|').Append(Bits(n.Offer))
                    .Append('|').Append(N(n.ShownPrice.Tl))
                    .Append('|').Append(N(n.DealPrice.Tl))
                    .Append('|').Append(n.ReportShown ? "1" : "0")
                    .Append('|').Append(sale.LastAskTooExpensive ? "1" : "0")
                    .Append('|').Append(N(n.Setup.Opening.Tl))
                    .Append('|').Append(Bits(n.Setup.Max))
                    .Append('|').Append(N(n.Setup.Patience))
                    .Append('|').Append(N(n.Setup.Trust))
                    .Append('|').Append(Bits(n.Setup.Urgency))
                    .Append('|').Append(N(n.Setup.Day))
                    .Append('\n');
            }

            foreach (KeyValuePair<string, double> index in session.DemandState.Indexes)
            {
                sb.Append("D|").Append(E(index.Key)).Append('|').Append(Bits(index.Value)).Append('\n');
            }

            foreach (DemandSale demandSale in session.DemandState.Sales)
            {
                sb.Append("T|").Append(E(demandSale.ModelId)).Append('|').Append(N(demandSale.Day)).Append('\n');
            }
        }

        /// <summary>double'ı kültürden bağımsız, kesin (bit deseni) yazar.</summary>
        private static string Bits(double value)
        {
            return BitConverter.DoubleToInt64Bits(value).ToString("x16", CultureInfo.InvariantCulture);
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
                    .Append('|').Append(N(i.ListPrice.Tl))
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

        private static void AppendKnowledge(StringBuilder sb, GameSession session)
        {
            foreach (string equipment in session.Equipment.All)
            {
                sb.Append("E|").Append(E(equipment)).Append('\n');
            }

            foreach (AppraisalResult r in session.Knowledge.All)
            {
                sb.Append("A|").Append(N(r.ResultId))
                    .Append('|').Append(N(r.InstanceId))
                    .Append('|').Append(E(r.LevelId))
                    .Append('|').Append(N(r.Day))
                    .Append('|').Append(N(r.Fee.Tl))
                    .Append('|').Append(r.Seed.ToString(CultureInfo.InvariantCulture))
                    .Append('|').Append(string.Join(";", r.Findings.Select(f => E(f.Attribute) + ":" + f.Found + ":" + f.IsFalseAlarm + ":" + f.Confidence)))
                    .Append('|').Append(Range(r.BatteryRange))
                    .Append('|').Append(Range(r.BodyRange))
                    .Append('|').Append(r.ValueRange == null ? "-" : N(r.ValueRange.Min.Tl) + ".." + N(r.ValueRange.Max.Tl))
                    .Append('|').Append(string.Join(";", r.Cards.Select(c => E(c.Attribute) + ":" + N(c.ProblemValue.Tl) + ":" + c.IsFalseAlarm)))
                    .Append('\n');
            }
        }

        private static string Range(NumericRange range)
        {
            return range == null ? "-" : N(range.Min) + ".." + N(range.Max);
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
