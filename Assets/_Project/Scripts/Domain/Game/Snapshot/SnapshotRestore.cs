using System;
using System.Collections.Generic;
using System.Globalization;
using Esnaf.Core;
using Esnaf.Domain.Appraisal;
using Esnaf.Domain.Business;
using Esnaf.Domain.Content;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Inventory;
using Esnaf.Domain.Market;
using Esnaf.Domain.Negotiation;
using Esnaf.Domain.Npc;
using Esnaf.Domain.Products;

namespace Esnaf.Domain.Game
{
    /// <summary>
    /// <see cref="GameSnapshot"/>'tan yeni bir <see cref="GameSession"/> kurar ve her şeyi doğrular (GDD v0.3 6.5). Tutarsız görüntü
    /// <c>save.invalid</c> döner ve hiçbir oturum üretilmez. Defter, ekleme kuralları ve bakiye karşılaştırmasıyla YENİDEN OYNATILIR.
    /// İçerikte olmayan ürün kimlikleri GDD 6.7'ye göre onarılır (<c>LoadWarnings</c>).
    /// </summary>
    internal static class SnapshotRestore
    {
        private sealed class InvalidSnapshotException : Exception
        {
            public InvalidSnapshotException(string message)
                : base(message)
            {
            }
        }

        public static Result<GameSession> Restore(ContentDatabase content, GameSnapshot snap, IEventBus bus)
        {
            try
            {
                return Result<GameSession>.Ok(Build(content, snap, bus));
            }
            catch (InvalidSnapshotException ex)
            {
                return Result<GameSession>.Fail("save.invalid", ex.Message);
            }
            catch (ArgumentException ex)
            {
                return Result<GameSession>.Fail("save.invalid", ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Result<GameSession>.Fail("save.invalid", ex.Message);
            }
            catch (KeyNotFoundException ex)
            {
                return Result<GameSession>.Fail("save.invalid", ex.Message);
            }
            catch (FormatException ex)
            {
                return Result<GameSession>.Fail("save.invalid", ex.Message);
            }
            catch (OverflowException ex)
            {
                return Result<GameSession>.Fail("save.invalid", ex.Message);
            }
        }

        private static GameSession Build(ContentDatabase content, GameSnapshot snap, IEventBus bus)
        {
            Need(snap.Time, "time");
            Need(snap.Rng, "rng");
            Need(snap.Ids, "ids");
            Need(snap.Economy, "economy");
            Need(snap.Economy.Ledger, "economy.ledger");
            Need(snap.Economy.PendingAppraisals, "economy.pendingAppraisals");
            Need(snap.Economy.Demand, "economy.demand");
            Need(snap.Economy.Demand.Indexes, "economy.demand.indexes");
            Need(snap.Economy.Demand.Sales, "economy.demand.sales");
            Need(snap.Inventory, "inventory");
            Need(snap.Inventory.ItemIds, "inventory.itemIds");
            Need(snap.Business, "business");
            Need(snap.Business.Equipment, "business.equipment");
            Need(snap.Market, "market");
            Need(snap.Market.Listings, "market.listings");
            Need(snap.Instances, "instances");
            Need(snap.NpcStates, "npcStates");
            Need(snap.Knowledge, "knowledge");
            Need(snap.Customers, "customers");
            Need(snap.Customers.Slots, "customers.slots");

            ulong seed = ParseUlong(snap.Time.Seed, "time.seed");
            GameSession s = GameSession.CreateForRestore(content, seed, bus);
            if (snap.Time.Day < 1)
            {
                throw new InvalidSnapshotException("time.day must be at least 1.");
            }

            s.Time.Restore(snap.Time.Day);

            RestoreRng(s, snap);
            RestoreLedger(s, snap);
            RestoreInstances(s, content, snap, out HashSet<long> unknownIds);
            RestoreEquipment(s, snap);
            RestoreInventory(s, snap, unknownIds);
            RestoreMarket(s, content, snap, unknownIds);
            RestoreNpcStates(s, snap);
            RestoreKnowledge(s, snap, unknownIds);
            RestorePendingAppraisals(s, snap);
            RestoreDemand(s, content, snap);
            RestoreCustomers(s, content, snap);
            RestoreCounters(s, snap);
            RestoreTrade(s, content, snap, unknownIds);
            RepairUnknownDefinitions(s, unknownIds);

            Result verify = s.EconomyState.Ledger.Verify();
            if (verify.IsFailure)
            {
                throw new InvalidSnapshotException(verify.Message);
            }

            return s;
        }

        // ---------- bölümler ----------

        private static void RestoreRng(GameSession s, GameSnapshot snap)
        {
            var streams = new Dictionary<string, RngState>(StringComparer.Ordinal);
            foreach (RngStreamSnapshot r in snap.Rng)
            {
                Bad(string.IsNullOrEmpty(r.Name), "rng stream name (empty)");
                Bad(streams.ContainsKey(r.Name), "rng stream name '" + r.Name + "' (duplicate)");

                streams.Add(r.Name, new RngState(ParseUlong(r.State, "rng." + r.Name + ".state"), ParseUlong(r.Increment, "rng." + r.Name + ".increment")));
            }

            s.Rng.Restore(streams);
        }

        private static void RestoreLedger(GameSession s, GameSnapshot snap)
        {
            Ledger ledger = s.EconomyState.Ledger;
            foreach (LedgerRecordSnapshot r in snap.Economy.Ledger)
            {
                Result<TransactionRecord> appended = ledger.Append(
                    r.Type ?? string.Empty,
                    Money.FromTl(r.Amount),
                    r.Day,
                    r.InstanceId,
                    r.DefinitionId,
                    r.NpcId,
                    r.SaleCostBasis.HasValue ? Money.FromTl(r.SaleCostBasis.Value) : (Money?)null,
                    r.RelatedRecordId,
                    r.MemoKey,
                    r.MemoArgs);
                if (appended.IsFailure)
                {
                    throw new InvalidSnapshotException("ledger record " + r.Id + ": " + appended.Message);
                }

                TransactionRecord rec = appended.Value;
                if (rec.Id != r.Id || rec.BalanceAfter.Tl != r.BalanceAfter || !string.Equals(rec.MemoKey, r.MemoKey, StringComparison.Ordinal))
                {
                    throw new InvalidSnapshotException("ledger record " + r.Id + " does not match its replay (id, balance or memo).");
                }
            }

            if (snap.Economy.BusinessAssets < 0)
            {
                throw new InvalidSnapshotException("economy.businessAssets must not be negative.");
            }

            s.EconomyState.BusinessAssets = Money.FromTl(snap.Economy.BusinessAssets);
        }

        private static void RestoreInstances(GameSession s, ContentDatabase content, GameSnapshot snap, out HashSet<long> unknownIds)
        {
            unknownIds = new HashSet<long>();
            foreach (InstanceSnapshot i in snap.Instances)
            {
                Need(i.Attributes, "instance " + i.InstanceId + " attributes");
                Need(i.Provenance, "instance " + i.InstanceId + " provenance");
                Bad(i.InstanceId <= 0, "instance id");
                Bad(string.IsNullOrEmpty(i.DefinitionId), "instance " + i.InstanceId + " definitionId (empty)");
                Bad(i.PurchasePrice < 0, "instance " + i.InstanceId + " purchasePrice");
                Bad(i.CostBasis < 0, "instance " + i.InstanceId + " costBasis");
                Bad(i.ListPrice < 0, "instance " + i.InstanceId + " listPrice");
                Bad(i.StorageGb < 0, "instance " + i.InstanceId + " storageGb");
                Bad(i.AgeMonths < 0, "instance " + i.InstanceId + " ageMonths");

                ProductLocation location = ParseEnum<ProductLocation>(i.Location, "instance " + i.InstanceId + " location");

                var instance = new ProductInstance
                {
                    InstanceId = i.InstanceId,
                    DefinitionId = i.DefinitionId,
                    StorageGb = i.StorageGb,
                    AgeMonths = i.AgeMonths,
                    SellerNpcId = i.Provenance.SellerNpcId,
                    ListingId = i.Provenance.ListingId,
                    AcquiredDay = i.Provenance.AcquiredDay,
                    PurchasePrice = Money.FromTl(i.PurchasePrice),
                    CostBasis = Money.FromTl(i.CostBasis),
                    ListPrice = Money.FromTl(i.ListPrice),
                    Location = location
                };
                foreach (KeyValuePair<string, object> pair in i.Attributes)
                {
                    instance.Attributes.Add(pair.Key, ToAttribute(pair.Key, pair.Value, i.InstanceId));
                }

                s.Store.Add(instance);
                ProductDefinition definition;
                if (!content.TryGetProduct(i.DefinitionId, out definition))
                {
                    unknownIds.Add(i.InstanceId);
                }
            }
        }

        private static AttributeValue ToAttribute(string key, object value, long instanceId)
        {
            if (value is long)
            {
                return AttributeValue.FromNumber((long)value);
            }

            if (value is int)
            {
                return AttributeValue.FromNumber((int)value);
            }

            string text = value as string;
            if (text != null)
            {
                return AttributeValue.FromText(text);
            }

            if (value is bool)
            {
                return AttributeValue.FromFlag((bool)value);
            }

            throw new InvalidSnapshotException("instance " + instanceId + " attribute '" + key + "' must be a whole number, text or flag.");
        }

        private static void RestoreEquipment(GameSession s, GameSnapshot snap)
        {
            foreach (string id in snap.Business.Equipment)
            {
                s.Equipment.Grant(id);
            }
        }

        private static void RestoreInventory(GameSession s, GameSnapshot snap, HashSet<long> unknownIds)
        {
            if (snap.Inventory.Capacity <= 0)
            {
                throw new InvalidSnapshotException("inventory.capacity must be positive.");
            }

            s.InventoryState.Capacity = snap.Inventory.Capacity;
            var seen = new HashSet<long>();
            foreach (long id in snap.Inventory.ItemIds)
            {
                ProductInstance instance;
                if (!s.Store.TryGet(id, out instance) || instance.Location != ProductLocation.Inventory || !seen.Add(id))
                {
                    throw new InvalidSnapshotException("inventory item " + id + " is not a stored, unique, shelf item.");
                }

                if (!unknownIds.Contains(id))
                {
                    s.InventoryState.Add(id);
                }
            }

            foreach (ProductInstance instance in s.Store.All)
            {
                if (instance.Location == ProductLocation.Inventory && !seen.Contains(instance.InstanceId))
                {
                    throw new InvalidSnapshotException("instance " + instance.InstanceId + " claims to be on the shelf but is not listed there (I8).");
                }
            }

            if (s.InventoryState.Count > s.InventoryState.Capacity)
            {
                throw new InvalidSnapshotException("the shelf holds more items than its capacity (I3).");
            }
        }

        private static void RestoreMarket(GameSession s, ContentDatabase content, GameSnapshot snap, HashSet<long> unknownIds)
        {
            var listed = new HashSet<long>();
            foreach (ListingSnapshot l in snap.Market.Listings)
            {
                Need(l.Tags, "listing " + l.ListingId + " tags");
                ProductInstance instance;
                if (!s.Store.TryGet(l.InstanceId, out instance) || instance.Location != ProductLocation.Market || !listed.Add(l.InstanceId))
                {
                    throw new InvalidSnapshotException("listing " + l.ListingId + " must point to a unique instance on the market.");
                }

                NpcDefinition seller;
                if (!content.TryGetNpc(l.SellerNpcId, out seller))
                {
                    throw new InvalidSnapshotException("listing " + l.ListingId + " has an unknown seller '" + l.SellerNpcId + "'.");
                }

                Bad(l.ListingId <= 0, "listing id");
                Bad(l.AskingPrice < 0, "listing " + l.ListingId + " askingPrice");
                Bad(l.RejectPrice < 0, "listing " + l.ListingId + " rejectPrice");
                Bad(l.BelievedValue < 0, "listing " + l.ListingId + " believedValue");
                Bad(l.RemainingDays < 0, "listing " + l.ListingId + " remainingDays");

                if (unknownIds.Contains(l.InstanceId))
                {
                    continue; // içerikte kalmamış ürünün ilanı sessizce kaldırılır (onarım)
                }

                s.Market.Add(new MarketListing(
                    l.ListingId,
                    l.InstanceId,
                    l.SellerNpcId,
                    Money.FromTl(l.AskingPrice),
                    l.Tags,
                    l.DayListed,
                    l.RemainingDays,
                    Money.FromTl(l.RejectPrice),
                    Money.FromTl(l.BelievedValue),
                    l.IsOpportunity,
                    l.IsTrap,
                    l.IsJackpot,
                    l.IsGuided));
            }

            foreach (ProductInstance instance in s.Store.All)
            {
                if (instance.Location == ProductLocation.Market && !listed.Contains(instance.InstanceId))
                {
                    throw new InvalidSnapshotException("instance " + instance.InstanceId + " is on the market without a listing (I8).");
                }
            }
        }

        private static void RestoreNpcStates(GameSession s, GameSnapshot snap)
        {
            foreach (NpcStateSnapshot n in snap.NpcStates)
            {
                Need(n.SoldToPlayer, "npc " + n.NpcId + " soldToPlayer");
                Need(n.BoughtFromPlayer, "npc " + n.NpcId + " boughtFromPlayer");
                Bad(string.IsNullOrWhiteSpace(n.NpcId), "npc state id");
                Bad(n.EncounterCount < 0, "npc " + n.NpcId + " encounterCount");

                s.Npcs.Restore(n.NpcId, n.EncounterCount, n.SoldToPlayer, n.BoughtFromPlayer);
            }
        }

        private static void RestoreKnowledge(GameSession s, GameSnapshot snap, HashSet<long> unknownIds)
        {
            foreach (AppraisalSnapshot a in snap.Knowledge)
            {
                Need(a.Findings, "appraisal " + a.ResultId + " findings");
                Need(a.Cards, "appraisal " + a.ResultId + " cards");
                // Ürünü artık depoda olmayan (süresi dolan/vazgeçilen ilan) ekspertiz sonuçları da kayıtlıdır: sonuç kilidi (I5) sürer.
                Bad(a.ResultId <= 0, "appraisal result id");
                Bad(a.Fee < 0, "appraisal " + a.ResultId + " fee");

                if (unknownIds.Contains(a.InstanceId))
                {
                    continue;
                }

                var findings = new List<AttributeFinding>();
                foreach (FindingSnapshot f in a.Findings)
                {
                    findings.Add(new AttributeFinding(f.Attribute, f.WordingKey, f.Found, ParseConfidence(f.Confidence), f.EvidencePower, f.IsFalseAlarm));
                }

                var cards = new List<TrumpCard>();
                foreach (CardSnapshot c in a.Cards)
                {
                    cards.Add(new TrumpCard(c.Attribute, c.WordingKey, ParseConfidence(c.Confidence), c.EvidencePower, Money.FromTl(c.ProblemValue), c.IsFalseAlarm));
                }

                s.Knowledge.Add(new AppraisalResult(
                    a.ResultId,
                    a.InstanceId,
                    a.DefinitionId,
                    a.LevelId,
                    a.Day,
                    Money.FromTl(a.Fee),
                    ParseUlong(a.Seed, "appraisal " + a.ResultId + " seed"),
                    findings,
                    a.BatteryRange == null ? null : new NumericRange(a.BatteryRange.Min, a.BatteryRange.Max),
                    a.BodyRange == null ? null : new NumericRange(a.BodyRange.Min, a.BodyRange.Max),
                    a.ValueRange == null ? null : new MoneyRange(Money.FromTl(a.ValueRange.Min), Money.FromTl(a.ValueRange.Max)),
                    cards));
            }
        }

        private static AppraisalConfidence ParseConfidence(string text)
        {
            return ParseEnum<AppraisalConfidence>(text, "appraisal confidence");
        }

        private static void RestorePendingAppraisals(GameSession s, GameSnapshot snap)
        {
            foreach (PendingAppraisalSnapshot p in snap.Economy.PendingAppraisals)
            {
                Need(p.RecordIds, "pending appraisal " + p.InstanceId);
                foreach (long recordId in p.RecordIds)
                {
                    TransactionRecord record;
                    if (!s.EconomyState.Ledger.TryGetById(recordId, out record)
                        || record.TypeId != TransactionTypeIds.Appraisal
                        || record.InstanceId != p.InstanceId
                        || s.EconomyState.Ledger.IsWrittenOff(recordId))
                    {
                        throw new InvalidSnapshotException("pending appraisal row " + recordId + " is not an open appraisal row of instance " + p.InstanceId + ".");
                    }

                    s.EconomyState.AddPendingAppraisal(p.InstanceId, recordId);
                }
            }
        }

        private static void RestoreDemand(GameSession s, ContentDatabase content, GameSnapshot snap)
        {
            DemandConstants d = content.Demand;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (DemandIndexSnapshot i in snap.Economy.Demand.Indexes)
            {
                Bad(string.IsNullOrEmpty(i.ModelId), "demand index model id (empty)");
                Bad(!seen.Add(i.ModelId), "demand index of '" + i.ModelId + "' (duplicate)");
                Bad(double.IsNaN(i.Index), "demand index of '" + i.ModelId + "' (not a number)");
                Bad(i.Index < d.Min, "demand index of '" + i.ModelId + "' (below the band)");
                Bad(i.Index > d.Max, "demand index of '" + i.ModelId + "' (above the band)");

                s.DemandState.SetIndex(i.ModelId, i.Index);
            }

            foreach (DemandSaleSnapshot x in snap.Economy.Demand.Sales)
            {
                Bad(string.IsNullOrEmpty(x.ModelId), "demand sale model id (empty)");
                Bad(x.Day < 1, "demand sale day");

                s.DemandState.AddSale(x.ModelId, x.Day);
            }
        }

        private static void RestoreCustomers(GameSession s, ContentDatabase content, GameSnapshot snap)
        {
            CustomersSnapshot c = snap.Customers;
            Bad(c.Arrived < 0, "customers.arrived");
            Bad(c.Arrived > c.Slots.Count, "customers.arrived (more than the roster)");
            Bad(c.MissedTotal < 0, "customers.missedTotal");

            var ids = new HashSet<long>();
            var slots = new List<CustomerSlot>(c.Slots.Count);
            foreach (CustomerSlotSnapshot x in c.Slots)
            {
                NpcDefinition npc;
                Bad(x.CustomerId <= 0, "customer slot id");
                Bad(!ids.Add(x.CustomerId), "customer slot " + x.CustomerId + " (duplicate id)");
                Bad(!content.TryGetNpc(x.NpcId, out npc), "customer slot " + x.CustomerId + " npc '" + x.NpcId + "' (unknown)");

                CustomerStatus status = ParseEnum<CustomerStatus>(x.Status, "customer slot " + x.CustomerId + " status");

                var slot = new CustomerSlot(x.CustomerId, x.NpcId, x.ValueDraw, x.TrustDraw, x.PickDraw);
                slot.Status = status;
                slots.Add(slot);
            }

            s.Customers.State.Replace(slots);
            s.Customers.State.Arrived = c.Arrived;
            s.Customers.State.MissedTotal = c.MissedTotal;
        }

        private static void RestoreCounters(GameSession s, GameSnapshot snap)
        {
            RestoreCounter(s.InstanceIds, snap.Ids.Instance, MaxOf(snap.Instances, i => i.InstanceId), "instance");
            RestoreCounter(s.ListingIds, snap.Ids.Listing, MaxOf(snap.Market.Listings, l => l.ListingId), "listing");
            RestoreCounter(s.AppraisalIds, snap.Ids.Appraisal, MaxOf(snap.Knowledge, a => a.ResultId), "appraisal");
            RestoreCounter(s.CustomerIds, snap.Ids.Customer, MaxOf(snap.Customers.Slots, c => c.CustomerId), "customer");
        }

        private static long MaxOf<T>(IEnumerable<T> items, Func<T, long> key)
        {
            long max = 0;
            foreach (T item in items)
            {
                max = Math.Max(max, key(item));
            }

            return max;
        }

        private static void RestoreCounter(IdGenerator generator, long value, long maxUsed, string name)
        {
            Bad(value < 0, "ids." + name + " counter");
            Bad(value < maxUsed, "ids." + name + " counter (below an id already in use)");

            generator.Restore(value);
        }

        private static void RestoreTrade(GameSession s, ContentDatabase content, GameSnapshot snap, HashSet<long> unknownIds)
        {
            if (snap.ActiveNegotiation != null && snap.ActiveSale != null)
            {
                throw new InvalidSnapshotException("only one negotiation can be open at a time.");
            }

            NegotiationSnapshot n = snap.ActiveNegotiation;
            if (n != null && !unknownIds.Contains(n.InstanceId))
            {
                Need(n.Setup, "activeNegotiation.setup");
                Need(n.UsedCards, "activeNegotiation.usedCards");
                MarketListing listing;
                if (!s.Market.TryGet(n.ListingId, out listing) || listing.InstanceId != n.InstanceId)
                {
                    throw new InvalidSnapshotException("the open negotiation does not match a market listing.");
                }

                Bad(!content.TryGetNpc(n.SellerNpcId, out NpcDefinition _), "activeNegotiation seller (unknown)");
                Bad(n.SellerNpcId != listing.SellerNpcId, "activeNegotiation seller (not the listing's seller)");

                NegotiationPhase phase = ParseOpenPhase(n.Phase, "activeNegotiation");
                NegotiationSetupSnapshot u = n.Setup;
                Bad(n.Round < 0, "activeNegotiation.round");
                Bad(n.Patience < 0, "activeNegotiation.patience");
                Bad(n.Trust < 0, "activeNegotiation.trust");
                Bad(n.Trust > 100, "activeNegotiation.trust");
                Bad(n.Reject < 0, "activeNegotiation.reject");
                Bad(n.Price < 0, "activeNegotiation.price");
                Bad(n.ShownPrice < 0, "activeNegotiation.shownPrice");
                Bad(n.DealPrice != 0, "activeNegotiation.dealPrice (an open negotiation has no deal)");
                Bad(u.Ask < 0, "activeNegotiation.setup.ask");
                Bad(u.Reject < 0, "activeNegotiation.setup.reject");
                Bad(u.Floor < 0, "activeNegotiation.setup.floor");
                Bad(u.Patience < 0, "activeNegotiation.setup.patience");
                Bad(u.Trust < 0, "activeNegotiation.setup.trust");
                Bad(u.Trust > 100, "activeNegotiation.setup.trust");

                var setup = new NegotiationSetup(
                    Money.FromTl(u.Ask), u.Reject, u.Floor, u.Patience, u.Trust, u.Urgency, u.Persuasion, u.WrongCardMultiplier, u.Day);
                var state = new NegotiationState(setup)
                {
                    Phase = phase,
                    Round = n.Round,
                    Patience = n.Patience,
                    Trust = n.Trust,
                    Reject = n.Reject,
                    Price = n.Price,
                    ShownPrice = Money.FromTl(n.ShownPrice),
                    DealPrice = Money.Zero
                };
                var used = new HashSet<string>(StringComparer.Ordinal);
                foreach (string key in n.UsedCards)
                {
                    Bad(!used.Add(key), "activeNegotiation.usedCards (a card twice)");

                    state.MarkCardUsed(key);
                }

                s.TradeState.Current = new ActiveNegotiation(n.ListingId, n.InstanceId, n.SellerNpcId, state) { LastOfferInsulted = n.LastOfferInsulted };
            }

            SaleSnapshot a = snap.ActiveSale;
            if (a != null && !unknownIds.Contains(a.InstanceId))
            {
                Need(a.Setup, "activeSale.setup");
                CustomerSlot slot = null;
                foreach (CustomerSlot candidate in s.Customers.State.Slots)
                {
                    if (candidate.CustomerId == a.CustomerId)
                    {
                        slot = candidate;
                    }
                }

                Bad(slot == null, "activeSale customer (not in the roster)");
                Bad(slot.Status != CustomerStatus.Waiting, "activeSale customer (not waiting)");
                Bad(slot.NpcId != a.NpcId, "activeSale customer (a different NPC)");
                ProductInstance item;
                Bad(!s.Store.TryGet(a.InstanceId, out item), "activeSale item (not stored)");
                Bad(item.Location != ProductLocation.Inventory, "activeSale item (not on the shelf)");

                NegotiationPhase phase = ParseOpenPhase(a.Phase, "activeSale");
                SaleSetupSnapshot u = a.Setup;
                Bad(a.Round < 0, "activeSale.round");
                Bad(a.Patience < 0, "activeSale.patience");
                Bad(a.Trust < 0, "activeSale.trust");
                Bad(a.Trust > 100, "activeSale.trust");
                Bad(a.Max < 0, "activeSale.max");
                Bad(a.Offer < 0, "activeSale.offer");
                Bad(a.ShownPrice < 0, "activeSale.shownPrice");
                Bad(a.DealPrice != 0, "activeSale.dealPrice (an open sale has no deal)");
                Bad(u.Opening < 0, "activeSale.setup.opening");
                Bad(u.Max < 0, "activeSale.setup.max");
                Bad(u.Patience < 0, "activeSale.setup.patience");
                Bad(u.Trust < 0, "activeSale.setup.trust");
                Bad(u.Trust > 100, "activeSale.setup.trust");

                var setup = new SaleSetup(Money.FromTl(u.Opening), u.Max, u.Patience, u.Trust, u.Urgency, u.Day);
                var state = new SaleState(setup)
                {
                    Phase = phase,
                    Round = a.Round,
                    Patience = a.Patience,
                    Trust = a.Trust,
                    Max = a.Max,
                    Offer = a.Offer,
                    ShownPrice = Money.FromTl(a.ShownPrice),
                    DealPrice = Money.Zero,
                    ReportShown = a.ReportShown
                };
                s.TradeState.CurrentSale = new ActiveSale(a.CustomerId, a.NpcId, a.InstanceId, state) { LastAskTooExpensive = a.LastAskTooExpensive };
            }
            else if (a != null)
            {
                s.AddLoadWarning("The open sale of instance " + a.InstanceId + " was dropped (its product is gone from the content).");
            }

            if (n != null && unknownIds.Contains(n.InstanceId))
            {
                s.AddLoadWarning("The open negotiation for instance " + n.InstanceId + " was dropped (its product is gone from the content).");
            }
        }

        private static NegotiationPhase ParseOpenPhase(string text, string what)
        {
            NegotiationPhase phase = ParseEnum<NegotiationPhase>(text, what + ".phase");
            Bad(phase != NegotiationPhase.Active && phase != NegotiationPhase.FinalOffer, what + ".phase (closed negotiations are not saved)");
            return phase;
        }

        // ---------- GDD 6.7: içerikte kalmamış ürün ----------

        private static void RepairUnknownDefinitions(GameSession s, HashSet<long> unknownIds)
        {
            int day = s.Time.Day;
            foreach (long id in new List<long>(unknownIds))
            {
                ProductInstance instance = s.Store.Get(id);
                if (instance.Location == ProductLocation.Inventory)
                {
                    Money refund = instance.PurchasePrice;
                    if (refund.IsPositive)
                    {
                        Result<TransactionRecord> r = s.EconomyService.RecordContentRefund(id, instance.DefinitionId, refund, day);
                        if (r.IsFailure)
                        {
                            throw new InvalidSnapshotException("content refund of instance " + id + " failed: " + r.Message);
                        }
                    }

                    s.AddLoadWarning("Instance " + id + " ('" + instance.DefinitionId + "') is no longer in the content; refunded " + refund.Tl + " TL.");
                }
                else if (instance.Location == ProductLocation.Market)
                {
                    s.AddLoadWarning("Listing of instance " + id + " ('" + instance.DefinitionId + "') was removed: the product is no longer in the content.");
                }
                else
                {
                    continue; // satılmış ürün kaydı kalır
                }

                s.EconomyService.WriteOffAppraisals(id, day); // "nothing_pending" normaldir
                s.Store.Remove(id);
            }
        }

        // ---------- yardımcılar ----------

        /// <summary>Sabit listeli alanı ADIYLA (büyük/küçük harf duyarlı, sayısal değil) çözer.</summary>
        private static T ParseEnum<T>(string text, string what)
            where T : struct
        {
            Bad(text == null || Array.IndexOf(Enum.GetNames(typeof(T)), text) < 0, what + " '" + text + "'");
            return (T)Enum.Parse(typeof(T), text);
        }

        /// <summary>Tek bir alanın tek bir kuralı: ihlalde kaydı geçersiz sayar (her kural ayrı satır/ayrı test).</summary>
        private static void Bad(bool violated, string what)
        {
            if (violated)
            {
                throw new InvalidSnapshotException(what + " is invalid in the save (I9/I8).");
            }
        }

        private static void Need(object value, string name)
        {
            if (value == null)
            {
                throw new InvalidSnapshotException("Missing section '" + name + "'.");
            }
        }

        private static ulong ParseUlong(string text, string name)
        {
            ulong value;
            if (text == null || !ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value))
            {
                throw new InvalidSnapshotException(name + " must be an unsigned whole number.");
            }

            return value;
        }
    }
}
