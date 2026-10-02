using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
    /// <summary>Canlı oturumdan <see cref="GameSnapshot"/> üretir. Durumu değiştirmez; listeler oyundaki sırayla, sözlükler anahtara göre sıralı.</summary>
    internal static class SnapshotCapture
    {
        public static GameSnapshot Capture(GameSession s)
        {
            return new GameSnapshot
            {
                Time = new TimeSnapshot
                {
                    Day = s.Time.Day,
                    Seed = U(s.Time.MasterSeed),
                    Minute = s.Time.MinuteOfDay == Esnaf.Domain.Time.StoreHours.OpenMinute ? (int?)null : s.Time.MinuteOfDay
                },
                Rng = CaptureRng(s),
                Ids = new IdsSnapshot
                {
                    Instance = s.InstanceIds.LastIssued,
                    Listing = s.ListingIds.LastIssued,
                    Appraisal = s.AppraisalIds.LastIssued,
                    Customer = s.CustomerIds.LastIssued
                },
                Economy = CaptureEconomy(s),
                Inventory = new InventorySnapshot { Capacity = s.InventoryState.Capacity, ItemIds = s.InventoryState.ItemIds.ToList() },
                Business = new BusinessSnapshot { Equipment = s.Equipment.All.ToList() },
                Market = new MarketSnapshot { Listings = s.Market.Listings.Select(CaptureListing).ToList() },
                Instances = s.Store.All.Select(CaptureInstance).ToList(),
                NpcStates = s.Npcs.All.Select(CaptureNpc).ToList(),
                Knowledge = s.Knowledge.All.Select(CaptureAppraisal).ToList(),
                Customers = CaptureCustomers(s),
                ActiveNegotiation = CaptureNegotiation(s.TradeState.Current),
                ActiveSale = CaptureSale(s.TradeState.CurrentSale),
                Accessories = CaptureAccessories(s)
            };
        }

        // Boş stok yazılmaz (null): aksesuarsız kayıtlar eskisiyle aynı metni/sağlamayı verir.
        private static AccessoriesSnapshot CaptureAccessories(GameSession s)
        {
            if (s.AccessoryStock.TotalUnits == 0)
            {
                return null;
            }

            return new AccessoriesSnapshot
            {
                Stock = s.AccessoryStock.AccessoryIds
                    .Select(id => new AccessoryStockLineSnapshot
                    {
                        AccessoryId = id,
                        Quantity = s.AccessoryStock.Quantity(id),
                        TotalCost = s.AccessoryStock.TotalCost(id).Tl
                    })
                    .ToList()
            };
        }

        internal static string U(ulong value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static List<RngStreamSnapshot> CaptureRng(GameSession s)
        {
            return s.Rng.Capture()
                .OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new RngStreamSnapshot { Name = p.Key, State = U(p.Value.State), Increment = U(p.Value.Increment) })
                .ToList();
        }

        private static EconomySnapshot CaptureEconomy(GameSession s)
        {
            EconomyState e = s.EconomyState;
            var pending = new List<PendingAppraisalSnapshot>();
            foreach (ProductInstance instance in s.Store.All.OrderBy(i => i.InstanceId))
            {
                IReadOnlyList<long> ids = e.GetPendingAppraisalRecordIds(instance.InstanceId);
                if (ids.Count > 0)
                {
                    pending.Add(new PendingAppraisalSnapshot { InstanceId = instance.InstanceId, RecordIds = ids.ToList() });
                }
            }

            return new EconomySnapshot
            {
                Ledger = e.Ledger.Records.Select(CaptureRecord).ToList(),
                BusinessAssets = e.BusinessAssets.Tl,
                PendingAppraisals = pending,
                Demand = new DemandSnapshot
                {
                    Indexes = s.DemandState.Indexes.Select(p => new DemandIndexSnapshot { ModelId = p.Key, Index = p.Value }).ToList(),
                    Sales = s.DemandState.Sales.Select(x => new DemandSaleSnapshot { ModelId = x.ModelId, Day = x.Day }).ToList()
                }
            };
        }

        private static LedgerRecordSnapshot CaptureRecord(TransactionRecord r)
        {
            return new LedgerRecordSnapshot
            {
                Id = r.Id,
                Day = r.Day,
                Type = r.TypeId,
                Amount = r.Amount.Tl,
                BalanceAfter = r.BalanceAfter.Tl,
                InstanceId = r.InstanceId,
                DefinitionId = r.DefinitionId,
                NpcId = r.NpcId,
                SaleCostBasis = r.SaleCostBasis.HasValue ? r.SaleCostBasis.Value.Tl : (long?)null,
                RelatedRecordId = r.RelatedRecordId,
                MemoKey = r.MemoKey,
                MemoArgs = r.MemoArgs.ToList()
            };
        }

        private static ListingSnapshot CaptureListing(MarketListing l)
        {
            return new ListingSnapshot
            {
                ListingId = l.ListingId,
                InstanceId = l.InstanceId,
                SellerNpcId = l.SellerNpcId,
                AskingPrice = l.AskingPrice.Tl,
                Tags = l.Tags.ToList(),
                DayListed = l.DayListed,
                RemainingDays = l.RemainingDays,
                RejectPrice = l.RejectPrice.Tl,
                BelievedValue = l.BelievedValue.Tl,
                IsOpportunity = l.IsOpportunity,
                IsTrap = l.IsTrap,
                IsJackpot = l.IsJackpot,
                IsGuided = l.IsGuided
            };
        }

        private static InstanceSnapshot CaptureInstance(ProductInstance i)
        {
            var attributes = new Dictionary<string, object>();
            foreach (KeyValuePair<string, AttributeValue> pair in i.Attributes.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                switch (pair.Value.Kind)
                {
                    case AttributeKind.Number:
                        attributes.Add(pair.Key, pair.Value.Number);
                        break;
                    case AttributeKind.Text:
                        attributes.Add(pair.Key, pair.Value.Text);
                        break;
                    default:
                        attributes.Add(pair.Key, pair.Value.Flag);
                        break;
                }
            }

            return new InstanceSnapshot
            {
                InstanceId = i.InstanceId,
                DefinitionId = i.DefinitionId,
                StorageGb = i.StorageGb,
                AgeMonths = i.AgeMonths,
                Attributes = attributes,
                Provenance = new ProvenanceSnapshot { SellerNpcId = i.SellerNpcId, ListingId = i.ListingId, AcquiredDay = i.AcquiredDay },
                PurchasePrice = i.PurchasePrice.Tl,
                CostBasis = i.CostBasis.Tl,
                ListPrice = i.ListPrice.Tl,
                Location = i.Location.ToString()
            };
        }

        private static NpcStateSnapshot CaptureNpc(NpcState n)
        {
            return new NpcStateSnapshot
            {
                NpcId = n.NpcId,
                EncounterCount = n.EncounterCount,
                SoldToPlayer = n.SoldToPlayer.ToList(),
                BoughtFromPlayer = n.BoughtFromPlayer.ToList()
            };
        }

        private static AppraisalSnapshot CaptureAppraisal(AppraisalResult r)
        {
            return new AppraisalSnapshot
            {
                ResultId = r.ResultId,
                InstanceId = r.InstanceId,
                DefinitionId = r.DefinitionId,
                LevelId = r.LevelId,
                Day = r.Day,
                Fee = r.Fee.Tl,
                Seed = U(r.Seed),
                Findings = r.Findings.Select(f => new FindingSnapshot
                {
                    Attribute = f.Attribute,
                    WordingKey = f.WordingKey,
                    Found = f.Found,
                    Confidence = f.Confidence.ToString(),
                    EvidencePower = f.EvidencePower,
                    IsFalseAlarm = f.IsFalseAlarm
                }).ToList(),
                BatteryRange = r.BatteryRange == null ? null : new NumericRangeSnapshot { Min = r.BatteryRange.Min, Max = r.BatteryRange.Max },
                BodyRange = r.BodyRange == null ? null : new NumericRangeSnapshot { Min = r.BodyRange.Min, Max = r.BodyRange.Max },
                ValueRange = r.ValueRange == null ? null : new MoneyRangeSnapshot { Min = r.ValueRange.Min.Tl, Max = r.ValueRange.Max.Tl },
                Cards = r.Cards.Select(c => new CardSnapshot
                {
                    Attribute = c.Attribute,
                    WordingKey = c.WordingKey,
                    Confidence = c.Confidence.ToString(),
                    EvidencePower = c.EvidencePower,
                    ProblemValue = c.ProblemValue.Tl,
                    IsFalseAlarm = c.IsFalseAlarm
                }).ToList()
            };
        }

        private static CustomersSnapshot CaptureCustomers(GameSession s)
        {
            CustomerState c = s.Customers.State;
            return new CustomersSnapshot
            {
                Arrived = c.Arrived,
                MissedTotal = c.MissedTotal,
                QueueCursor = c.QueueCursor > 0 ? c.QueueCursor : (int?)null,
                Slots = c.Slots.Select(x => new CustomerSlotSnapshot
                {
                    CustomerId = x.CustomerId,
                    NpcId = x.NpcId,
                    ValueDraw = x.ValueDraw,
                    TrustDraw = x.TrustDraw,
                    PickDraw = x.PickDraw,
                    Status = x.Status.ToString()
                }).ToList()
            };
        }

        private static NegotiationSnapshot CaptureNegotiation(ActiveNegotiation a)
        {
            if (a == null)
            {
                return null;
            }

            NegotiationState n = a.State;
            NegotiationSetup setup = n.Setup;
            return new NegotiationSnapshot
            {
                ListingId = a.ListingId,
                InstanceId = a.InstanceId,
                SellerNpcId = a.SellerNpcId,
                LastOfferInsulted = a.LastOfferInsulted,
                Setup = new NegotiationSetupSnapshot
                {
                    Ask = setup.Ask.Tl,
                    Reject = setup.Reject,
                    Floor = setup.Floor,
                    Patience = setup.Patience,
                    Trust = setup.Trust,
                    Urgency = setup.Urgency,
                    Persuasion = setup.Persuasion,
                    WrongCardMultiplier = setup.WrongCardMultiplier,
                    Day = setup.Day
                },
                Phase = n.Phase.ToString(),
                Round = n.Round,
                Patience = n.Patience,
                Trust = n.Trust,
                Reject = n.Reject,
                Price = n.Price,
                ShownPrice = n.ShownPrice.Tl,
                DealPrice = n.DealPrice.Tl,
                UsedCards = n.UsedCards.ToList()
            };
        }

        private static SaleSnapshot CaptureSale(ActiveSale a)
        {
            if (a == null)
            {
                return null;
            }

            SaleState n = a.State;
            SaleSetup setup = n.Setup;
            return new SaleSnapshot
            {
                CustomerId = a.CustomerId,
                NpcId = a.NpcId,
                InstanceId = a.InstanceId,
                LastAskTooExpensive = a.LastAskTooExpensive,
                Setup = new SaleSetupSnapshot
                {
                    Opening = setup.Opening.Tl,
                    Max = setup.Max,
                    Patience = setup.Patience,
                    Trust = setup.Trust,
                    Urgency = setup.Urgency,
                    Day = setup.Day
                },
                Phase = n.Phase.ToString(),
                Round = n.Round,
                Patience = n.Patience,
                Trust = n.Trust,
                Max = n.Max,
                Offer = n.Offer,
                ShownPrice = n.ShownPrice.Tl,
                DealPrice = n.DealPrice.Tl,
                ReportShown = n.ReportShown
            };
        }
    }
}
