using System;
using System.Collections.Generic;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Business;
using Esnaf.Domain.Content;
using Esnaf.Domain.Game;
using Esnaf.Domain.Market;
using Esnaf.Domain.Time;
using Esnaf.Domain.Negotiation;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Game
{
    /// <summary>GameSession.Capture / Restore: durumun gidiş-dönüşü (GDD I7), rastgelelik devamı, geçersiz kayıt reddi.</summary>
    public class GameSnapshotTests
    {
        private static ContentDatabase Content
        {
            get { return MarketHarness.RealContent(); }
        }

        private static GameSession New(ulong seed = 42UL)
        {
            return GameSession.NewGame(Content, seed);
        }

        /// <summary>Birkaç gün oynanmış, ekspertiz/pazarlık/satış izleri olan zengin bir oturum.</summary>
        private static GameSession Rich(ulong seed, int steps = 260)
        {
            GameSession s = New(seed);
            SessionDriver.Run(s, new Random((int)seed * 31 + 7), steps);
            return s;
        }

        private static GameSession Restored(GameSnapshot snapshot)
        {
            Result<GameSession> r = GameSession.Restore(Content, snapshot);
            Assert.IsTrue(r.IsSuccess, r.ErrorCode + ": " + r.Message);
            return r.Value;
        }

        // ---------- Capture ----------

        [Test]
        public void Capture_DoesNotChangeTheState_AndIsRepeatable()
        {
            GameSession s = Rich(3UL);
            string digest = s.Api.GetStateDigest();

            GameSnapshot first = s.Capture();
            GameSnapshot second = s.Capture();

            Assert.AreEqual(digest, s.Api.GetStateDigest());
            Assert.IsNull(DeepCompare.FirstDifference(first, second));
        }

        [Test]
        public void Capture_OfANewGame_ListsTheOpeningState()
        {
            GameSession s = New(42UL);

            GameSnapshot snap = s.Capture();

            Assert.AreEqual(1, snap.Time.Day);
            Assert.AreEqual("42", snap.Time.Seed);
            Assert.AreEqual(1, snap.Economy.Ledger.Count);
            Assert.AreEqual("opening_capital", snap.Economy.Ledger[0].Type);
            Assert.AreEqual(250000L, snap.Economy.Ledger[0].Amount);
            Assert.AreEqual(6, snap.Inventory.Capacity);
            Assert.AreEqual(0, snap.Inventory.ItemIds.Count);
            Assert.AreEqual(3, snap.Market.Listings.Count);
            Assert.AreEqual(3, snap.Instances.Count);
            Assert.AreEqual(5, snap.Customers.Slots.Count);
            Assert.IsNull(snap.ActiveNegotiation);
            Assert.IsNull(snap.ActiveSale);
            Assert.AreEqual(3L, snap.Ids.Instance);
            Assert.AreEqual(3L, snap.Ids.Listing);
            Assert.AreEqual(5L, snap.Ids.Customer);
            CollectionAssert.AreEqual(new[] { "customers", "market" }, snap.Rng.Select(r => r.Name).ToArray());
        }

        [Test]
        public void Capture_KeepsGameStateButNoDefinitionsOrValues()
        {
            GameSession s = New(42UL);
            InstanceSnapshot guided = s.Capture().Instances.Single(i => i.DefinitionId == "phone.yildiz_y5");

            Assert.AreEqual(64, guided.StorageGb);
            Assert.AreEqual(24, guided.AgeMonths);
            Assert.AreEqual(95L, guided.Attributes["battery"]);
            Assert.AreEqual("original", guided.Attributes["screen"]);
            Assert.AreEqual(false, guided.Attributes["box"]);
            Assert.AreEqual("Market", guided.Location);
            Assert.AreEqual("npc.ayse", guided.Provenance.SellerNpcId);
        }

        // ---------- gidiş-dönüş (I7) ----------

        [TestCase(1UL)]
        [TestCase(2UL)]
        [TestCase(3UL)]
        [TestCase(4UL)]
        [TestCase(5UL)]
        [TestCase(6UL)]
        [TestCase(7UL)]
        [TestCase(8UL)]
        [TestCase(9UL)]
        [TestCase(10UL)]
        public void RoundTrip_OfARichSession_KeepsTheExactState(ulong seed)
        {
            GameSession original = Rich(seed);
            GameSnapshot snapshot = original.Capture();

            GameSession restored = Restored(snapshot);

            Assert.AreEqual(original.Api.GetStateDigest(), restored.Api.GetStateDigest(), "I7: Yükle(Kaydet(durum)) = durum");
            Assert.IsNull(DeepCompare.FirstDifference(snapshot, restored.Capture()), "ikinci görüntü birinciyle aynı");
            Assert.AreEqual(original.Api.GetCash(), restored.Api.GetCash());
            Assert.AreEqual(original.Api.GetDay(), restored.Api.GetDay());
            Assert.AreEqual(original.Wealth.Calculate().Total, restored.Wealth.Calculate().Total);
            Assert.IsTrue(restored.EconomyState.Ledger.Verify().IsSuccess);
        }

        [Test]
        public void RoundTrip_TheRichSessions_CoverTheInterestingStates()
        {
            bool sawSale = false, sawBuy = false, sawAppraisal = false, sawInventory = false, sawLedgerSale = false, sawDemandSale = false;
            for (ulong seed = 1; seed <= 25; seed++)
            {
                GameSession s = Rich(seed);
                GameSnapshot snap = s.Capture();
                sawSale |= snap.ActiveSale != null;
                sawBuy |= snap.ActiveNegotiation != null;
                sawAppraisal |= snap.Knowledge.Count > 0;
                sawInventory |= snap.Inventory.ItemIds.Count > 0;
                sawLedgerSale |= snap.Economy.Ledger.Any(r => r.Type == "sale");
                sawDemandSale |= snap.Economy.Demand.Sales.Count > 0;
            }

            Assert.IsTrue(sawSale, "test düzeneği: süren satış görülmeli");
            Assert.IsTrue(sawBuy, "test düzeneği: süren alış pazarlığı görülmeli");
            Assert.IsTrue(sawAppraisal && sawInventory && sawLedgerSale && sawDemandSale, "test düzeneği: ekspertiz, raf, satış, talep görülmeli");
        }

        [Test]
        public void RoundTrip_AnActiveBuyNegotiation_ResumesWhereItStopped()
        {
            GameSession s = New(42UL);
            MarketListing g = s.Market.Listings.Single(l => l.IsGuided);
            Assert.IsTrue(s.Api.StartNegotiation(g.ListingId).IsSuccess);
            Assert.IsTrue(s.Api.MakeOffer(Money.FromTl(4000)).IsSuccess);
            Assert.IsTrue(s.Api.MakeOffer(Money.FromTl(4300)).IsSuccess);
            GameSnapshot snap = s.Capture();
            Assert.IsNotNull(snap.ActiveNegotiation);
            Assert.AreEqual(2, snap.ActiveNegotiation.Round);

            GameSession restored = Restored(snap);

            NegotiationView before = restored.Api.GetNegotiation();
            Assert.AreEqual(2, before.Round);
            Assert.AreEqual(s.Api.GetNegotiation().ShownPrice, before.ShownPrice);
            Assert.AreEqual(s.Api.GetNegotiation().Mood, before.Mood);
            Assert.AreEqual(s.Api.GetNegotiation().Patience, before.Patience);
            NegotiationView a = s.Api.MakeOffer(Money.FromTl(4500)).Value;
            NegotiationView b = restored.Api.MakeOffer(Money.FromTl(4500)).Value;
            Assert.AreEqual(a.ShownPrice, b.ShownPrice);
            Assert.AreEqual(a.Phase, b.Phase);
            Assert.AreEqual(s.Api.GetStateDigest(), restored.Api.GetStateDigest());
            Assert.AreEqual("negotiation.in_progress", restored.Api.EndDay().ErrorCode, "T17: pazarlık kaldığı yerden sürer, gün bitirilemez");
        }

        [Test]
        public void RoundTrip_AnActiveSale_ResumesWhereItStopped()
        {
            GameSession s = New(1UL);
            MarketListing g = s.Market.Listings.Single(l => l.IsGuided);
            s.Api.StartNegotiation(g.ListingId);
            s.Api.MakeOffer(Money.FromTl(5800));
            s.Api.SetPrice(g.InstanceId, Money.FromTl(5900));
            CustomerView customer = s.Api.GetCustomers().First();
            Assert.IsTrue(s.Api.StartSale(customer.CustomerId).IsSuccess);
            Assert.IsTrue(s.Api.AskPrice(Money.FromTl(6400)).IsSuccess);
            GameSnapshot snap = s.Capture();
            Assert.IsNotNull(snap.ActiveSale);

            GameSession restored = Restored(snap);

            Assert.AreEqual(s.Api.GetSale().ShownPrice, restored.Api.GetSale().ShownPrice);
            SaleView a = s.Api.AskPrice(Money.FromTl(6100)).Value;
            SaleView b = restored.Api.AskPrice(Money.FromTl(6100)).Value;
            Assert.AreEqual(a.ShownPrice, b.ShownPrice);
            Assert.AreEqual(a.Phase, b.Phase);
            Assert.AreEqual(s.Api.GetStateDigest(), restored.Api.GetStateDigest());
        }

        // ---------- rastgelelik devamı ----------

        [TestCase(1UL)]
        [TestCase(2UL)]
        [TestCase(3UL)]
        public void RandomStreams_ContinueIdentically_AfterRestore(ulong seed)
        {
            GameSession original = Rich(seed);
            GameSnapshot snap = original.Capture();
            GameSession restored = Restored(snap);

            foreach (RngStreamSnapshot stream in snap.Rng)
            {
                IRandom a = original.Rng.Get(stream.Name);
                IRandom b = restored.Rng.Get(stream.Name);
                for (int i = 0; i < 5; i++)
                {
                    Assert.AreEqual(a.NextUInt(), b.NextUInt(), stream.Name + " #" + i);
                }
            }
        }

        [TestCase(11UL)]
        [TestCase(12UL)]
        [TestCase(13UL)]
        [TestCase(14UL)]
        [TestCase(15UL)]
        [TestCase(16UL)]
        public void TheGameContinuesIdentically_AfterRestore_ForHundredsOfCommands(ulong seed)
        {
            GameSession original = Rich(seed, 140);
            GameSession restored = Restored(original.Capture());
            var a = new Random(999 + (int)seed);
            var b = new Random(999 + (int)seed);

            for (int i = 0; i < 300; i++)
            {
                SessionDriver.Step(original, a);
                SessionDriver.Step(restored, b);
                Assert.AreEqual(original.Api.GetStateDigest(), restored.Api.GetStateDigest(), "komut #" + i);
            }

            Assert.IsTrue(original.Api.GetDay() >= restored.Api.GetDay());
        }

        // ---------- yeni oyun davranışı değişmedi ----------

        [Test]
        public void NewGame_StillOpensDayOne()
        {
            GameSession s = New(42UL);

            Assert.AreEqual(1, s.Api.GetDay());
            Assert.AreEqual(Money.FromTl(250000), s.Api.GetCash());
            Assert.AreEqual(3, s.Api.GetListings().Count);
            Assert.AreEqual(0, s.LoadWarnings.Count);
        }

        [Test]
        public void Restore_DoesNotOpenANewDay_OrWriteAnOpeningLedgerLine()
        {
            GameSession original = Rich(4UL);
            int ledger = original.EconomyState.Ledger.Count;
            int listings = original.Market.Count;

            GameSession restored = Restored(original.Capture());

            Assert.AreEqual(ledger, restored.EconomyState.Ledger.Count);
            Assert.AreEqual(listings, restored.Market.Count);
        }

        [Test]
        public void Restore_PublishesNoGameplayEvents()
        {
            GameSession original = Rich(4UL);
            var bus = new EventBus();
            int events = 0;
            bus.Subscribe<DayStarted>(e => events++);
            bus.Subscribe<ListingsGenerated>(e => events++);
            bus.Subscribe<Esnaf.Domain.Economy.TransactionRecorded>(e => events++);

            Result<GameSession> r = GameSession.Restore(Content, original.Capture(), bus);

            Assert.IsTrue(r.IsSuccess);
            Assert.AreEqual(0, events);
            Assert.AreSame(bus, r.Value.Bus);
        }

        // ---------- geçersiz görüntü ----------

        private static AppraisalSnapshot Appraisal(GameSnapshot s, Action<FindingSnapshot> tweakFinding, string seed = "1")
        {
            s.Ids.Appraisal = 1; // sayaç kontrolü bu kayıtları maskelemesin
            var finding = new FindingSnapshot { Attribute = "screen", WordingKey = "k", Found = true, Confidence = "Low", EvidencePower = 0.4, IsFalseAlarm = false };
            if (tweakFinding != null)
            {
                tweakFinding(finding);
            }

            return new AppraisalSnapshot
            {
                ResultId = 1,
                InstanceId = s.Instances[0].InstanceId,
                DefinitionId = s.Instances[0].DefinitionId,
                LevelId = "s1",
                Day = 1,
                Fee = 100,
                Seed = seed,
                Findings = new List<FindingSnapshot> { finding },
                Cards = new List<CardSnapshot>()
            };
        }

        private static readonly object[][] Corruptions =
        {
            new object[] { "null section: time", (Action<GameSnapshot>)(s => s.Time = null) },
            new object[] { "null section: rng", (Action<GameSnapshot>)(s => s.Rng = null) },
            new object[] { "null section: ids", (Action<GameSnapshot>)(s => s.Ids = null) },
            new object[] { "null section: economy", (Action<GameSnapshot>)(s => s.Economy = null) },
            new object[] { "null section: economy.ledger", (Action<GameSnapshot>)(s => s.Economy.Ledger = null) },
            new object[] { "null section: inventory", (Action<GameSnapshot>)(s => s.Inventory = null) },
            new object[] { "null section: business", (Action<GameSnapshot>)(s => s.Business = null) },
            new object[] { "null section: market", (Action<GameSnapshot>)(s => s.Market = null) },
            new object[] { "null section: instances", (Action<GameSnapshot>)(s => s.Instances = null) },
            new object[] { "null section: npcStates", (Action<GameSnapshot>)(s => s.NpcStates = null) },
            new object[] { "null section: knowledge", (Action<GameSnapshot>)(s => s.Knowledge = null) },
            new object[] { "null section: customers", (Action<GameSnapshot>)(s => s.Customers = null) },
            new object[] { "null section: demand", (Action<GameSnapshot>)(s => s.Economy.Demand = null) },
            new object[] { "day 0", (Action<GameSnapshot>)(s => s.Time.Day = 0) },
            new object[] { "seed not a number", (Action<GameSnapshot>)(s => s.Time.Seed = "abc") },
            new object[] { "seed negative", (Action<GameSnapshot>)(s => s.Time.Seed = "-1") },
            new object[] { "rng state not a number", (Action<GameSnapshot>)(s => s.Rng[0].State = "x") },
            new object[] { "rng duplicate name", (Action<GameSnapshot>)(s => s.Rng.Add(new RngStreamSnapshot { Name = s.Rng[0].Name, State = "1", Increment = "1" })) },
            new object[] { "negative instance id counter", (Action<GameSnapshot>)(s => s.Ids.Instance = -1) },
            new object[] { "negative customer id counter", (Action<GameSnapshot>)(s => s.Ids.Customer = -1) },
            new object[] { "ledger balance tampered", (Action<GameSnapshot>)(s => s.Economy.Ledger[0].BalanceAfter += 10) },
            new object[] { "ledger id gap", (Action<GameSnapshot>)(s => s.Economy.Ledger[0].Id = 2) },
            new object[] { "ledger unknown type", (Action<GameSnapshot>)(s => s.Economy.Ledger[0].Type = "nonsense") },
            new object[] { "ledger wrong sign", (Action<GameSnapshot>)(s => { s.Economy.Ledger[0].Amount = -250000; s.Economy.Ledger[0].BalanceAfter = -250000; }) },
            new object[] { "ledger amount not rounded", (Action<GameSnapshot>)(s => { s.Economy.Ledger[0].Amount = 250005; s.Economy.Ledger[0].BalanceAfter = 250005; }) },
            new object[] { "instance duplicate id", (Action<GameSnapshot>)(s => s.Instances[1].InstanceId = s.Instances[0].InstanceId) },
            new object[] { "instance bad location", (Action<GameSnapshot>)(s => s.Instances[0].Location = "Attic") },
            new object[] { "instance attribute of a bad type", (Action<GameSnapshot>)(s => s.Instances[0].Attributes["battery"] = 1.5) },
            new object[] { "instance id zero", (Action<GameSnapshot>)(s => s.Instances[0].InstanceId = 0) },
            new object[] { "inventory capacity zero", (Action<GameSnapshot>)(s => s.Inventory.Capacity = 0) },
            new object[] { "inventory item not stored", (Action<GameSnapshot>)(s => s.Inventory.ItemIds.Add(9999)) },
            new object[] { "inventory item on the market", (Action<GameSnapshot>)(s => s.Inventory.ItemIds.Add(s.Instances[0].InstanceId)) },
            new object[] { "listing instance missing", (Action<GameSnapshot>)(s => s.Market.Listings[0].InstanceId = 9999) },
            new object[] { "listing duplicate id", (Action<GameSnapshot>)(s => s.Market.Listings[1].ListingId = s.Market.Listings[0].ListingId) },
            new object[] { "listing instance not on the market", (Action<GameSnapshot>)(s => s.Instances.First(i => i.InstanceId == s.Market.Listings[0].InstanceId).Location = "Sold") },
            new object[] { "npc state without an id", (Action<GameSnapshot>)(s => s.NpcStates.Add(new NpcStateSnapshot { NpcId = "", SoldToPlayer = new List<long>(), BoughtFromPlayer = new List<long>() })) },
            new object[] { "customer slot bad status", (Action<GameSnapshot>)(s => s.Customers.Slots[0].Status = "Gone") },
            new object[] { "customer arrived beyond the roster", (Action<GameSnapshot>)(s => s.Customers.Arrived = 6) },
            new object[] { "customer arrived negative", (Action<GameSnapshot>)(s => s.Customers.Arrived = -1) },
            new object[] { "customer slot unknown npc", (Action<GameSnapshot>)(s => s.Customers.Slots[0].NpcId = "npc.nobody") },
            new object[] { "customer slot duplicate id", (Action<GameSnapshot>)(s => s.Customers.Slots[1].CustomerId = s.Customers.Slots[0].CustomerId) },
            new object[] { "demand sale without a model", (Action<GameSnapshot>)(s => s.Economy.Demand.Sales.Add(new DemandSaleSnapshot { ModelId = "", Day = 1 })) },
            new object[] { "rng increment even", (Action<GameSnapshot>)(s => s.Rng[0].Increment = "2") },
            new object[] { "instance counter below used ids", (Action<GameSnapshot>)(s => s.Ids.Instance = 0) },
            new object[] { "listing counter below used ids", (Action<GameSnapshot>)(s => s.Ids.Listing = 0) },
            new object[] { "customer counter below used ids", (Action<GameSnapshot>)(s => s.Ids.Customer = 0) },
            new object[] { "business assets negative", (Action<GameSnapshot>)(s => s.Economy.BusinessAssets = -10) },
            new object[] { "pending appraisal on a non-appraisal row", (Action<GameSnapshot>)(s => s.Economy.PendingAppraisals.Add(new PendingAppraisalSnapshot { InstanceId = 1, RecordIds = new List<long> { 1 } })) },
            new object[] { "pending appraisal row missing", (Action<GameSnapshot>)(s => s.Economy.PendingAppraisals.Add(new PendingAppraisalSnapshot { InstanceId = 1, RecordIds = new List<long> { 99 } })) },
            new object[] { "instance negative purchase price", (Action<GameSnapshot>)(s => s.Instances[0].PurchasePrice = -10) },
            new object[] { "instance negative cost basis", (Action<GameSnapshot>)(s => s.Instances[0].CostBasis = -10) },
            new object[] { "instance negative list price", (Action<GameSnapshot>)(s => s.Instances[0].ListPrice = -10) },
            new object[] { "instance negative age", (Action<GameSnapshot>)(s => s.Instances[0].AgeMonths = -1) },
            new object[] { "instance without definition", (Action<GameSnapshot>)(s => s.Instances[0].DefinitionId = "") },
            new object[] { "listing negative asking price", (Action<GameSnapshot>)(s => s.Market.Listings[0].AskingPrice = -10) },
            new object[] { "listing negative lifetime", (Action<GameSnapshot>)(s => s.Market.Listings[0].RemainingDays = -1) },
            new object[] { "listing unknown seller", (Action<GameSnapshot>)(s => s.Market.Listings[0].SellerNpcId = "npc.nobody") },
            new object[] { "listing id zero", (Action<GameSnapshot>)(s => s.Market.Listings[0].ListingId = 0) },
            new object[] { "market instance without a listing", (Action<GameSnapshot>)(s => s.Market.Listings.RemoveAt(0)) },
            new object[] { "npc negative encounters", (Action<GameSnapshot>)(s => s.NpcStates.Add(new NpcStateSnapshot { NpcId = "npc.kemal", EncounterCount = -1, SoldToPlayer = new List<long>(), BoughtFromPlayer = new List<long>() })) },
            new object[] { "npc instance id zero", (Action<GameSnapshot>)(s => s.NpcStates.Add(new NpcStateSnapshot { NpcId = "npc.kemal", SoldToPlayer = new List<long> { 0 }, BoughtFromPlayer = new List<long>() })) },
            new object[] { "equipment empty id", (Action<GameSnapshot>)(s => s.Business.Equipment.Add("")) },
            new object[] { "demand index NaN", (Action<GameSnapshot>)(s => s.Economy.Demand.Indexes.Add(new DemandIndexSnapshot { ModelId = "phone.yildiz_y5", Index = double.NaN })) },
            new object[] { "demand index duplicate", (Action<GameSnapshot>)(s => { s.Economy.Demand.Indexes.Add(new DemandIndexSnapshot { ModelId = "phone.yildiz_y5", Index = 1.0 }); s.Economy.Demand.Indexes.Add(new DemandIndexSnapshot { ModelId = "phone.yildiz_y5", Index = 1.0 }); }) },
            new object[] { "demand sale day zero", (Action<GameSnapshot>)(s => s.Economy.Demand.Sales.Add(new DemandSaleSnapshot { ModelId = "phone.yildiz_y5", Day = 0 })) },
            new object[] { "customers missed negative", (Action<GameSnapshot>)(s => s.Customers.MissedTotal = -1) },
            new object[] { "knowledge bad confidence", (Action<GameSnapshot>)(s => s.Knowledge.Add(Appraisal(s, f => f.Confidence = "Sure"))) },
            new object[] { "knowledge bad seed", (Action<GameSnapshot>)(s => s.Knowledge.Add(Appraisal(s, null, "x"))) },
            new object[] { "knowledge negative fee", (Action<GameSnapshot>)(s => { AppraisalSnapshot a = Appraisal(s, null); a.Fee = -10; s.Knowledge.Add(a); }) },
            new object[] { "knowledge duplicate result", (Action<GameSnapshot>)(s => { s.Knowledge.Add(Appraisal(s, null)); s.Knowledge.Add(Appraisal(s, null)); }) },
            new object[] { "demand index out of the band", (Action<GameSnapshot>)(s => s.Economy.Demand.Indexes.Add(new DemandIndexSnapshot { ModelId = "phone.yildiz_y5", Index = 5.0 })) }
        };

        [TestCaseSource(nameof(Corruptions))]
        public void Restore_RejectsAnInconsistentSnapshot(string name, Action<GameSnapshot> corrupt)
        {
            GameSnapshot snap = New(42UL).Capture();
            corrupt(snap);

            Result<GameSession> r = GameSession.Restore(Content, snap);

            Assert.IsTrue(r.IsFailure, name);
            Assert.AreEqual("save.invalid", r.ErrorCode, name);
        }

        [Test]
        public void Restore_RejectsInconsistentTradeState()
        {
            GameSession s = New(42UL);
            MarketListing g = s.Market.Listings.Single(l => l.IsGuided);
            s.Api.StartNegotiation(g.ListingId);
            s.Api.MakeOffer(Money.FromTl(4000));

            GameSnapshot a = s.Capture();
            a.ActiveNegotiation.ListingId = 9999;
            Assert.AreEqual("save.invalid", GameSession.Restore(Content, a).ErrorCode, "ilan yok");

            GameSnapshot b = s.Capture();
            b.ActiveNegotiation.Phase = "Deal";
            Assert.AreEqual("save.invalid", GameSession.Restore(Content, b).ErrorCode, "kapanmış pazarlık kaydedilmez");

            GameSnapshot c = s.Capture();
            c.ActiveNegotiation.InstanceId = 9999;
            Assert.AreEqual("save.invalid", GameSession.Restore(Content, c).ErrorCode, "ürün ilana ait değil");

            GameSnapshot d = s.Capture();
            d.ActiveNegotiation.Patience = -1;
            Assert.AreEqual("save.invalid", GameSession.Restore(Content, d).ErrorCode, "I9: negatif sabır yok");

            GameSnapshot e = s.Capture();
            e.ActiveNegotiation.Trust = 101;
            Assert.AreEqual("save.invalid", GameSession.Restore(Content, e).ErrorCode, "güven 0–100");

            GameSnapshot f = s.Capture();
            f.ActiveNegotiation.ShownPrice = -10;
            Assert.AreEqual("save.invalid", GameSession.Restore(Content, f).ErrorCode, "I9: negatif fiyat yok");

            GameSnapshot ok = s.Capture();
            Assert.IsTrue(GameSession.Restore(Content, ok).IsSuccess);
        }

        [Test]
        public void Restore_RejectsInconsistentSaleState()
        {
            GameSession s = New(1UL);
            MarketListing g = s.Market.Listings.Single(l => l.IsGuided);
            s.Api.StartNegotiation(g.ListingId);
            s.Api.MakeOffer(Money.FromTl(5800));
            s.Api.SetPrice(g.InstanceId, Money.FromTl(5900));
            s.Api.StartSale(s.Api.GetCustomers().First().CustomerId);

            GameSnapshot a = s.Capture();
            a.ActiveSale.CustomerId = 9999;
            Assert.AreEqual("save.invalid", GameSession.Restore(Content, a).ErrorCode, "müşteri yok");

            GameSnapshot b = s.Capture();
            b.ActiveSale.InstanceId = 9999;
            Assert.AreEqual("save.invalid", GameSession.Restore(Content, b).ErrorCode, "ürün rafta değil");

            GameSnapshot c = s.Capture();
            c.ActiveSale.Phase = "Failed";
            Assert.AreEqual("save.invalid", GameSession.Restore(Content, c).ErrorCode, "kapanmış satış kaydedilmez");

            GameSnapshot d = s.Capture();
            d.ActiveSale.Patience = -1;
            Assert.AreEqual("save.invalid", GameSession.Restore(Content, d).ErrorCode);

            GameSession other = New(1UL);
            other.Api.StartNegotiation(other.Market.Listings.First(l => !l.IsGuided).ListingId);
            GameSnapshot both = s.Capture();
            both.ActiveNegotiation = other.Capture().ActiveNegotiation;
            Assert.IsNotNull(both.ActiveNegotiation);
            Assert.AreEqual("save.invalid", GameSession.Restore(Content, both).ErrorCode, "aynı anda tek pazarlık");
        }

        [Test]
        public void Restore_RejectsNullArguments()
        {
            Assert.Throws<ArgumentNullException>(() => GameSession.Restore(null, New().Capture()));
            Assert.Throws<ArgumentNullException>(() => GameSession.Restore(Content, null));
        }
    }
}
