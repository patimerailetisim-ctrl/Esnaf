using System;
using System.Collections.Generic;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Content;
using Esnaf.Domain.Game;
using Esnaf.Domain.Market;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Game
{
    /// <summary>
    /// Kayıt doğrulamasının ALAN ALAN tablosu: her alan için geçerli sınır değeri kabul, ihlal red edilir (GDD I8/I9, sayaç ve başvuru kuralları).
    /// Her satır tek bir kuralı sınar; bu yüzden hangi kuralın gevşediği tek testten anlaşılır.
    /// </summary>
    public class SnapshotFieldValidationTests
    {
        private static ContentDatabase Content
        {
            get { return MarketHarness.RealContent(); }
        }

        private static GameSnapshot Fresh()
        {
            return GameSession.NewGame(Content, 42UL).Capture();
        }

        private static GameSnapshot WithNegotiation()
        {
            GameSession s = GameSession.NewGame(Content, 42UL);
            MarketListing g = s.Market.Listings.Single(l => l.IsGuided);
            s.Api.StartNegotiation(g.ListingId);
            s.Api.MakeOffer(Money.FromTl(4000));
            return s.Capture();
        }

        private static GameSnapshot WithSale()
        {
            GameSession s = GameSession.NewGame(Content, 1UL);
            MarketListing g = s.Market.Listings.Single(l => l.IsGuided);
            s.Api.StartNegotiation(g.ListingId);
            s.Api.MakeOffer(Money.FromTl(5800));
            s.Api.SetPrice(g.InstanceId, Money.FromTl(5900));
            s.Api.StartSale(s.Api.GetCustomers().First().CustomerId);
            s.Api.AskPrice(Money.FromTl(6400));
            return s.Capture();
        }

        private static GameSnapshot WithKnowledge()
        {
            GameSession s = GameSession.NewGame(Content, 42UL);
            for (int i = 0; i < 2; i++)
            {
                s.Api.EndDay();
            }

            long listing = s.Market.Listings.First(l => l.DayListed == 3).ListingId;
            Assert.IsTrue(s.Api.StartAppraisal(listing, "s1").IsSuccess);
            return s.Capture();
        }

        private static void Check(GameSnapshot snap, bool expectValid, string name)
        {
            Result<GameSession> r = GameSession.Restore(Content, snap);
            if (expectValid)
            {
                Assert.IsTrue(r.IsSuccess, name + " geçerli olmalıydı: " + r.ErrorCode + " " + r.Message);
            }
            else
            {
                Assert.IsTrue(r.IsFailure, name + " geçersiz olmalıydı");
                Assert.AreEqual("save.invalid", r.ErrorCode, name);
            }
        }

        private static object[] Row(string name, bool valid, Action<GameSnapshot> mutate)
        {
            return new object[] { name, valid, mutate };
        }

        // ---------- ürün örnekleri, ilanlar, NPC, rng, sayaçlar, talep, müşteriler ----------

        private static readonly object[][] Fields =
        {
            Row("instance purchasePrice 0", true, s => s.Instances[0].PurchasePrice = 0),
            Row("instance purchasePrice -10", false, s => s.Instances[0].PurchasePrice = -10),
            Row("instance costBasis 0", true, s => s.Instances[0].CostBasis = 0),
            Row("instance costBasis -10", false, s => s.Instances[0].CostBasis = -10),
            Row("instance listPrice 0", true, s => s.Instances[0].ListPrice = 0),
            Row("instance listPrice -10", false, s => s.Instances[0].ListPrice = -10),
            Row("instance storageGb 0", true, s => s.Instances[0].StorageGb = 0),
            Row("instance storageGb -1", false, s => s.Instances[0].StorageGb = -1),
            Row("instance ageMonths 0", true, s => s.Instances[0].AgeMonths = 0),
            Row("instance ageMonths -1", false, s => s.Instances[0].AgeMonths = -1),
            Row("instance definitionId empty", false, s => s.Instances[0].DefinitionId = ""),
            Row("instance location Market", true, s => s.Instances[0].Location = "Market"),
            Row("instance location lower-case", false, s => s.Instances[0].Location = "market"),
            Row("instance location numeric", false, s => s.Instances[0].Location = "7"),
            Row("instance attribute bool", true, s => s.Instances[0].Attributes["box"] = true),
            Row("instance attribute int", true, s => s.Instances[0].Attributes["battery"] = 80),
            Row("instance attribute text", true, s => s.Instances[0].Attributes["screen"] = "cracked"),
            Row("instance attribute null", false, s => s.Instances[0].Attributes["screen"] = null),
            Row("listing askingPrice 0", true, s => s.Market.Listings[0].AskingPrice = 0),
            Row("listing askingPrice -10", false, s => s.Market.Listings[0].AskingPrice = -10),
            Row("listing rejectPrice 0", true, s => s.Market.Listings[0].RejectPrice = 0),
            Row("listing rejectPrice -10", false, s => s.Market.Listings[0].RejectPrice = -10),
            Row("listing believedValue 0", true, s => s.Market.Listings[0].BelievedValue = 0),
            Row("listing believedValue -10", false, s => s.Market.Listings[0].BelievedValue = -10),
            Row("listing remainingDays 0", true, s => s.Market.Listings[0].RemainingDays = 0),
            Row("listing remainingDays -1", false, s => s.Market.Listings[0].RemainingDays = -1),
            Row("listing tags null", false, s => s.Market.Listings[0].Tags = null),
            Row("npc encounterCount 0", true, s => s.NpcStates.Add(new NpcStateSnapshot { NpcId = "npc.kemal", EncounterCount = 0, SoldToPlayer = new List<long>(), BoughtFromPlayer = new List<long>() })),
            Row("npc encounterCount -1", false, s => s.NpcStates.Add(new NpcStateSnapshot { NpcId = "npc.kemal", EncounterCount = -1, SoldToPlayer = new List<long>(), BoughtFromPlayer = new List<long>() })),
            Row("npc id blank", false, s => s.NpcStates.Add(new NpcStateSnapshot { NpcId = "  ", SoldToPlayer = new List<long>(), BoughtFromPlayer = new List<long>() })),
            Row("rng name empty", false, s => s.Rng.Add(new RngStreamSnapshot { Name = "", State = "1", Increment = "1" })),
            Row("rng name duplicate", false, s => s.Rng.Add(new RngStreamSnapshot { Name = s.Rng[0].Name, State = "1", Increment = "1" })),
            Row("rng extra stream", true, s => s.Rng.Add(new RngStreamSnapshot { Name = "zzz", State = "5", Increment = "3" })),
            Row("ids.instance equals the highest id", true, s => s.Ids.Instance = s.Instances.Max(i => i.InstanceId)),
            Row("ids.instance one below the highest", false, s => s.Ids.Instance = s.Instances.Max(i => i.InstanceId) - 1),
            Row("ids.instance negative", false, s => s.Ids.Instance = -1),
            Row("ids.listing equals the highest id", true, s => s.Ids.Listing = s.Market.Listings.Max(l => l.ListingId)),
            Row("ids.listing one below the highest", false, s => s.Ids.Listing = s.Market.Listings.Max(l => l.ListingId) - 1),
            Row("ids.customer equals the highest id", true, s => s.Ids.Customer = s.Customers.Slots.Max(c => c.CustomerId)),
            Row("ids.customer one below the highest", false, s => s.Ids.Customer = s.Customers.Slots.Max(c => c.CustomerId) - 1),
            Row("ids.appraisal negative", false, s => s.Ids.Appraisal = -1),
            Row("demand index at the lower bound", true, s => s.Economy.Demand.Indexes.Add(new DemandIndexSnapshot { ModelId = "phone.yildiz_y5", Index = 0.90 })),
            Row("demand index at the upper bound", true, s => s.Economy.Demand.Indexes.Add(new DemandIndexSnapshot { ModelId = "phone.yildiz_y5", Index = 1.10 })),
            Row("demand index just below the band", false, s => s.Economy.Demand.Indexes.Add(new DemandIndexSnapshot { ModelId = "phone.yildiz_y5", Index = 0.8999 })),
            Row("demand index just above the band", false, s => s.Economy.Demand.Indexes.Add(new DemandIndexSnapshot { ModelId = "phone.yildiz_y5", Index = 1.1001 })),
            Row("demand index without a model id", false, s => s.Economy.Demand.Indexes.Add(new DemandIndexSnapshot { ModelId = "", Index = 1.0 })),
            Row("demand sale on day 1", true, s => s.Economy.Demand.Sales.Add(new DemandSaleSnapshot { ModelId = "phone.yildiz_y5", Day = 1 })),
            Row("demand sale on day 0", false, s => s.Economy.Demand.Sales.Add(new DemandSaleSnapshot { ModelId = "phone.yildiz_y5", Day = 0 })),
            Row("demand sale without a model", false, s => s.Economy.Demand.Sales.Add(new DemandSaleSnapshot { ModelId = "", Day = 1 })),
            Row("customers arrived 0", true, s => s.Customers.Arrived = 0),
            Row("customers arrived = roster size", true, s => s.Customers.Arrived = s.Customers.Slots.Count),
            Row("customers arrived beyond the roster", false, s => s.Customers.Arrived = s.Customers.Slots.Count + 1),
            Row("customers arrived -1", false, s => s.Customers.Arrived = -1),
            Row("customers missedTotal 0", true, s => s.Customers.MissedTotal = 0),
            Row("customers missedTotal -1", false, s => s.Customers.MissedTotal = -1),
            Row("customer slot id 0", false, s => s.Customers.Slots[0].CustomerId = 0),
            Row("customer slot duplicate id", false, s => s.Customers.Slots[1].CustomerId = s.Customers.Slots[0].CustomerId),
            Row("customer slot unknown npc", false, s => s.Customers.Slots[0].NpcId = "npc.nobody"),
            Row("customer slot status Sold", true, s => s.Customers.Slots[0].Status = "Sold"),
            Row("customer slot status Left", true, s => s.Customers.Slots[0].Status = "Left"),
            Row("customer slot status lower-case", false, s => s.Customers.Slots[0].Status = "waiting"),
            Row("ledger: balanceAfter off by ten", false, s => s.Economy.Ledger[0].BalanceAfter += 10),
            Row("ledger: memo key free text", true, s => s.Economy.Ledger[0].MemoKey = "ledger.memo.other"),
            Row("ledger: memo key missing", false, s => s.Economy.Ledger[0].MemoKey = null),
            Row("market instance without a listing", false, s => s.Market.Listings.RemoveAt(0)),
            Row("shelf item claims the market location", false, s => { s.Inventory.ItemIds.Add(s.Instances[0].InstanceId); }),
            Row("capacity below the shelf content is rejected", false, s => { s.Inventory.Capacity = 0; })
        };

        [TestCaseSource(nameof(Fields))]
        public void Field_Rule(string name, bool valid, Action<GameSnapshot> mutate)
        {
            GameSnapshot snap = Fresh();
            mutate(snap);

            Check(snap, valid, name);
        }

        // ---------- süren alış pazarlığı ----------

        private static readonly object[][] NegotiationFields =
        {
            Row("round 0", true, s => s.ActiveNegotiation.Round = 0),
            Row("round -1", false, s => s.ActiveNegotiation.Round = -1),
            Row("patience 0", true, s => s.ActiveNegotiation.Patience = 0),
            Row("patience -1", false, s => s.ActiveNegotiation.Patience = -1),
            Row("trust 0", true, s => s.ActiveNegotiation.Trust = 0),
            Row("trust 100", true, s => s.ActiveNegotiation.Trust = 100),
            Row("trust -1", false, s => s.ActiveNegotiation.Trust = -1),
            Row("trust 101", false, s => s.ActiveNegotiation.Trust = 101),
            Row("reject 0", true, s => s.ActiveNegotiation.Reject = 0),
            Row("reject -1", false, s => s.ActiveNegotiation.Reject = -1),
            Row("price 0", true, s => s.ActiveNegotiation.Price = 0),
            Row("price -1", false, s => s.ActiveNegotiation.Price = -1),
            Row("shownPrice 0", true, s => s.ActiveNegotiation.ShownPrice = 0),
            Row("shownPrice -10", false, s => s.ActiveNegotiation.ShownPrice = -10),
            Row("dealPrice 10", false, s => s.ActiveNegotiation.DealPrice = 10),
            Row("setup.ask 0", true, s => s.ActiveNegotiation.Setup.Ask = 0),
            Row("setup.ask -10", false, s => s.ActiveNegotiation.Setup.Ask = -10),
            Row("setup.reject 0", true, s => s.ActiveNegotiation.Setup.Reject = 0),
            Row("setup.reject -1", false, s => s.ActiveNegotiation.Setup.Reject = -1),
            Row("setup.floor 0", true, s => s.ActiveNegotiation.Setup.Floor = 0),
            Row("setup.floor -1", false, s => s.ActiveNegotiation.Setup.Floor = -1),
            Row("setup.patience 0", true, s => s.ActiveNegotiation.Setup.Patience = 0),
            Row("setup.patience -1", false, s => s.ActiveNegotiation.Setup.Patience = -1),
            Row("setup.trust 0", true, s => s.ActiveNegotiation.Setup.Trust = 0),
            Row("setup.trust 100", true, s => s.ActiveNegotiation.Setup.Trust = 100),
            Row("setup.trust -1", false, s => s.ActiveNegotiation.Setup.Trust = -1),
            Row("setup.trust 101", false, s => s.ActiveNegotiation.Setup.Trust = 101),
            Row("phase FinalOffer", true, s => s.ActiveNegotiation.Phase = "FinalOffer"),
            Row("phase Active", true, s => s.ActiveNegotiation.Phase = "Active"),
            Row("phase lower-case", false, s => s.ActiveNegotiation.Phase = "active"),
            Row("phase Deal", false, s => s.ActiveNegotiation.Phase = "Deal"),
            Row("phase Failed", false, s => s.ActiveNegotiation.Phase = "Failed"),
            Row("phase numeric", false, s => s.ActiveNegotiation.Phase = "0"),
            Row("lastOfferInsulted true", true, s => s.ActiveNegotiation.LastOfferInsulted = true),
            Row("seller unknown", false, s => s.ActiveNegotiation.SellerNpcId = "npc.nobody"),
            Row("seller is another known npc", false, s => s.ActiveNegotiation.SellerNpcId = "npc.kemal"),
            Row("usedCards one card", true, s => s.ActiveNegotiation.UsedCards = new List<string> { "1:0" }),
            Row("usedCards the same card twice", false, s => s.ActiveNegotiation.UsedCards = new List<string> { "1:0", "1:0" }),
            Row("usedCards null", false, s => s.ActiveNegotiation.UsedCards = null),
            Row("setup null", false, s => s.ActiveNegotiation.Setup = null),
            Row("listing id of another listing", false, s => s.ActiveNegotiation.ListingId = s.Market.Listings.First(l => l.ListingId != s.ActiveNegotiation.ListingId).ListingId)
        };

        [TestCaseSource(nameof(NegotiationFields))]
        public void OpenNegotiation_Field_Rule(string name, bool valid, Action<GameSnapshot> mutate)
        {
            GameSnapshot snap = WithNegotiation();
            Assert.IsNotNull(snap.ActiveNegotiation);
            mutate(snap);

            Check(snap, valid, name);
        }

        // ---------- süren satış ----------

        private static readonly object[][] SaleFields =
        {
            Row("round 0", true, s => s.ActiveSale.Round = 0),
            Row("round -1", false, s => s.ActiveSale.Round = -1),
            Row("patience 0", true, s => s.ActiveSale.Patience = 0),
            Row("patience -1", false, s => s.ActiveSale.Patience = -1),
            Row("trust 0", true, s => s.ActiveSale.Trust = 0),
            Row("trust 100", true, s => s.ActiveSale.Trust = 100),
            Row("trust -1", false, s => s.ActiveSale.Trust = -1),
            Row("trust 101", false, s => s.ActiveSale.Trust = 101),
            Row("max 0", true, s => s.ActiveSale.Max = 0),
            Row("max -1", false, s => s.ActiveSale.Max = -1),
            Row("offer 0", true, s => s.ActiveSale.Offer = 0),
            Row("offer -1", false, s => s.ActiveSale.Offer = -1),
            Row("shownPrice 0", true, s => s.ActiveSale.ShownPrice = 0),
            Row("shownPrice -10", false, s => s.ActiveSale.ShownPrice = -10),
            Row("dealPrice 10", false, s => s.ActiveSale.DealPrice = 10),
            Row("setup.opening 0", true, s => s.ActiveSale.Setup.Opening = 0),
            Row("setup.opening -10", false, s => s.ActiveSale.Setup.Opening = -10),
            Row("setup.max 0", true, s => s.ActiveSale.Setup.Max = 0),
            Row("setup.max -1", false, s => s.ActiveSale.Setup.Max = -1),
            Row("setup.patience 0", true, s => s.ActiveSale.Setup.Patience = 0),
            Row("setup.patience -1", false, s => s.ActiveSale.Setup.Patience = -1),
            Row("setup.trust 0", true, s => s.ActiveSale.Setup.Trust = 0),
            Row("setup.trust 100", true, s => s.ActiveSale.Setup.Trust = 100),
            Row("setup.trust -1", false, s => s.ActiveSale.Setup.Trust = -1),
            Row("setup.trust 101", false, s => s.ActiveSale.Setup.Trust = 101),
            Row("phase FinalOffer", true, s => s.ActiveSale.Phase = "FinalOffer"),
            Row("phase lower-case", false, s => s.ActiveSale.Phase = "active"),
            Row("phase Deal", false, s => s.ActiveSale.Phase = "Deal"),
            Row("reportShown true", true, s => s.ActiveSale.ReportShown = true),
            Row("setup null", false, s => s.ActiveSale.Setup = null),
            Row("customer not in the roster", false, s => s.ActiveSale.CustomerId = 9999),
            Row("customer already sold", false, s => s.Customers.Slots.First(c => c.CustomerId == s.ActiveSale.CustomerId).Status = "Sold"),
            Row("customer already left", false, s => s.Customers.Slots.First(c => c.CustomerId == s.ActiveSale.CustomerId).Status = "Left"),
            Row("a different npc", false, s => s.ActiveSale.NpcId = s.Customers.Slots.First(c => c.NpcId != s.ActiveSale.NpcId).NpcId),
            Row("item not stored", false, s => s.ActiveSale.InstanceId = 9999),
            Row("item on the market", false, s => s.ActiveSale.InstanceId = s.Market.Listings[0].InstanceId)
        };

        [TestCaseSource(nameof(SaleFields))]
        public void OpenSale_Field_Rule(string name, bool valid, Action<GameSnapshot> mutate)
        {
            GameSnapshot snap = WithSale();
            Assert.IsNotNull(snap.ActiveSale);
            mutate(snap);

            Check(snap, valid, name);
        }

        // ---------- ekspertiz sonuçları ve bekleyen ücretler ----------

        private static readonly object[][] KnowledgeFields =
        {
            Row("resultId 0", false, s => s.Knowledge[0].ResultId = 0),
            Row("fee 0", true, s => s.Knowledge[0].Fee = 0),
            Row("fee -10", false, s => s.Knowledge[0].Fee = -10),
            Row("seed not a number", false, s => s.Knowledge[0].Seed = "abc"),
            Row("seed negative", false, s => s.Knowledge[0].Seed = "-5"),
            Row("seed null", false, s => s.Knowledge[0].Seed = null),
            Row("findings null", false, s => s.Knowledge[0].Findings = null),
            Row("cards null", false, s => s.Knowledge[0].Cards = null),
            Row("confidence lower-case", false, s => { if (s.Knowledge[0].Findings.Count > 0) { s.Knowledge[0].Findings[0].Confidence = "low"; } else { s.Knowledge[0].Findings.Add(new FindingSnapshot { Attribute = "screen", WordingKey = "k", Confidence = "low" }); } }),
            Row("confidence unknown", false, s => { if (s.Knowledge[0].Findings.Count > 0) { s.Knowledge[0].Findings[0].Confidence = "Sure"; } else { s.Knowledge[0].Findings.Add(new FindingSnapshot { Attribute = "screen", WordingKey = "k", Confidence = "Sure" }); } }),
            Row("confidence Certain", true, s => { if (s.Knowledge[0].Findings.Count > 0) { s.Knowledge[0].Findings[0].Confidence = "Certain"; } else { s.Knowledge[0].Findings.Add(new FindingSnapshot { Attribute = "screen", WordingKey = "k", Confidence = "Certain" }); } }),
            Row("pending row of another instance", false, s => s.Economy.PendingAppraisals[0].InstanceId = s.Instances.First(i => i.InstanceId != s.Economy.PendingAppraisals[0].InstanceId).InstanceId),
            Row("pending row that is not an appraisal row", false, s => s.Economy.PendingAppraisals[0].RecordIds.Add(1)),
            Row("pending row twice", true, s => s.Economy.PendingAppraisals.Add(new PendingAppraisalSnapshot { InstanceId = 999999, RecordIds = new List<long>() })),
            Row("pending ids null", false, s => s.Economy.PendingAppraisals[0].RecordIds = null),
            Row("appraisal for an instance no longer stored stays valid", true, s => s.Knowledge[0].InstanceId = 888888)
        };

        [TestCaseSource(nameof(KnowledgeFields))]
        public void Appraisal_Field_Rule(string name, bool valid, Action<GameSnapshot> mutate)
        {
            GameSnapshot snap = WithKnowledge();
            Assert.AreEqual(1, snap.Knowledge.Count);
            Assert.AreEqual(1, snap.Economy.PendingAppraisals.Count);
            mutate(snap);

            Check(snap, valid, name);
        }

        [Test]
        public void APendingRow_ThatWasAlreadyWrittenOff_IsRejected()
        {
            GameSession s = GameSession.NewGame(Content, 42UL);
            for (int i = 0; i < 2; i++)
            {
                s.Api.EndDay();
            }

            MarketListing l = s.Market.Listings.First(x => x.DayListed == 3);
            s.Api.StartAppraisal(l.ListingId, "s1");
            long rowId = s.EconomyState.GetPendingAppraisalRecordIds(l.InstanceId)[0];
            s.Api.StartNegotiation(l.ListingId);
            s.Api.WalkAway(); // bekleyen ücret gider yazılır
            GameSnapshot snap = s.Capture();
            Assert.AreEqual(0, snap.Economy.PendingAppraisals.Count);
            Assert.IsTrue(snap.Economy.Ledger.Any(r => r.Type == "wasted_appraisal" && r.RelatedRecordId == rowId));

            snap.Economy.PendingAppraisals.Add(new PendingAppraisalSnapshot { InstanceId = l.InstanceId, RecordIds = new List<long> { rowId } });

            Check(snap, false, "gidere yazılmış satır bekleyen olamaz");
        }
    }
}
