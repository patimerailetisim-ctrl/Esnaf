using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Esnaf.Core;
using Esnaf.Domain.Appraisal;
using Esnaf.Domain.Business;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Game;
using Esnaf.Domain.Inventory;
using Esnaf.Domain.Market;
using Esnaf.Domain.Negotiation;
using Esnaf.Domain.Products;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Business
{
    /// <summary>Satış akışı (IGameApi üzerinden): etiket, müşteri, pazarlık, rapor göster, defter, talep baskısı.</summary>
    public class SellFlowTests
    {
        private static GameSession New(ulong seed = 42UL, IEventBus bus = null)
        {
            return GameSession.NewGame(MarketHarness.RealContent(), seed, bus);
        }

        private static long BuyGuided(GameSession s)
        {
            MarketListing g = s.Market.Listings.Single(l => l.IsGuided);
            Assert.IsTrue(s.Api.StartNegotiation(g.ListingId).IsSuccess);
            Assert.IsTrue(s.Api.MakeOffer(Money.FromTl(5800)).IsSuccess);
            return g.InstanceId;
        }

        private static long BuyGuidedAndPrice(GameSession s, long price = 5900)
        {
            long id = BuyGuided(s);
            Assert.IsTrue(s.Api.SetPrice(id, Money.FromTl(price)).IsSuccess);
            return id;
        }

        /// <summary>Verilen NPC'nin ilgilendiği bir müşteri çıkana kadar tohumları dener (belirlenimci).</summary>
        private static GameSession SessionWithCustomer(string npcId, out CustomerView customer, IEventBus busFactory = null)
        {
            for (ulong seed = 1; seed <= 200; seed++)
            {
                GameSession s = New(seed);
                BuyGuidedAndPrice(s);
                foreach (CustomerView v in s.Api.GetCustomers())
                {
                    if (v.NpcId == npcId)
                    {
                        customer = v;
                        return s;
                    }
                }
            }

            throw new InvalidOperationException("No seed gives a " + npcId + " customer.");
        }

        private static SaleView Ask(GameSession s, long tl)
        {
            Result<SaleView> r = s.Api.AskPrice(Money.FromTl(tl));
            Assert.IsTrue(r.IsSuccess, r.ErrorCode);
            return r.Value;
        }

        // ---------- etiket ----------

        [Test]
        public void SetPrice_TagsAnItemOnTheShelf_AndPublishesTheEvent()
        {
            var bus = new EventBus();
            var priced = new List<ItemPriced>();
            bus.Subscribe<ItemPriced>(priced.Add);
            GameSession s = New(42UL, bus);
            long id = BuyGuided(s);

            Result r = s.Api.SetPrice(id, Money.FromTl(5900));

            Assert.IsTrue(r.IsSuccess, r.ErrorCode);
            Assert.AreEqual(Money.FromTl(5900), s.Store.Get(id).ListPrice);
            Assert.AreEqual(1, priced.Count);
            Assert.AreEqual(id, priced[0].InstanceId);
            Assert.AreEqual(Money.FromTl(5900), priced[0].Price);
        }

        [Test]
        public void SetPrice_CanBeChanged_AndFailsForBadInput()
        {
            GameSession s = New();
            long id = BuyGuidedAndPrice(s);

            Assert.IsTrue(s.Api.SetPrice(id, Money.FromTl(6200)).IsSuccess);
            Assert.AreEqual(Money.FromTl(6200), s.Store.Get(id).ListPrice);
            Assert.AreEqual("price.invalid", s.Api.SetPrice(id, Money.FromTl(0)).ErrorCode);
            Assert.AreEqual("price.invalid", s.Api.SetPrice(id, Money.FromTl(-10)).ErrorCode);
            Assert.AreEqual("price.invalid", s.Api.SetPrice(id, Money.FromTl(6205)).ErrorCode);
            Assert.AreEqual("instance.unknown", s.Api.SetPrice(9999, Money.FromTl(100)).ErrorCode);
            Assert.AreEqual(Money.FromTl(6200), s.Store.Get(id).ListPrice, "hatalı komut durumu değiştirmez");
        }

        [Test]
        public void SetPrice_OnAnItemThatIsNotOnTheShelf_Fails()
        {
            GameSession s = New();
            MarketListing g = s.Market.Listings.Single(l => l.IsGuided);

            Assert.AreEqual("instance.not_in_inventory", s.Api.SetPrice(g.InstanceId, Money.FromTl(5900)).ErrorCode);
        }


        [Test]
        public void SetPrice_NeedsBothTheLocationAndTheShelfEntry()
        {
            GameSession s = New();
            MarketListing g = s.Market.Listings.Single(l => l.IsGuided);
            ProductInstance marketItem = s.Store.Get(g.InstanceId);
            marketItem.Location = ProductLocation.Inventory; // rafta değil ama konumu "envanter" görünüyor

            Assert.AreEqual("instance.not_in_inventory", s.Api.SetPrice(g.InstanceId, Money.FromTl(5900)).ErrorCode);

            marketItem.Location = ProductLocation.Market;
            long bought = BuyGuided(s);
            s.Store.Get(bought).Location = ProductLocation.Market; // rafta ama konumu "pazar" görünüyor

            Assert.AreEqual("instance.not_in_inventory", s.Api.SetPrice(bought, Money.FromTl(5900)).ErrorCode);
            Assert.IsTrue(s.Store.Get(bought).ListPrice.IsZero);
        }

        // ---------- satış başlatma ----------

        [Test]
        public void StartSale_UnknownCustomer_Fails()
        {
            GameSession s = New();
            BuyGuidedAndPrice(s);

            Assert.AreEqual("customer.unknown", s.Api.StartSale(99999).ErrorCode);
            Assert.IsNull(s.Api.GetSale());
        }

        [Test]
        public void StartSale_OpensAtTheCustomersOpeningOffer_WithGddKemalNumbers()
        {
            CustomerView customer;
            GameSession s = SessionWithCustomer("npc.kemal", out customer);

            Result<SaleView> r = s.Api.StartSale(customer.CustomerId);

            Assert.IsTrue(r.IsSuccess, r.ErrorCode);
            SaleView v = r.Value;
            Assert.AreEqual(customer.CustomerId, v.CustomerId);
            Assert.AreEqual("npc.kemal", v.NpcId);
            Assert.AreEqual(customer.InstanceId, v.InstanceId);
            Assert.AreEqual(NegotiationPhase.Active, v.Phase);
            Assert.AreEqual(0, v.Round);
            Assert.AreEqual(Money.FromTl(4430), v.ShownPrice, "0,80 × M (5.540) → 4.430");
            Assert.IsTrue(v.DealPrice.IsZero);
            Assert.IsFalse(v.LastAskTooExpensive);
            Assert.IsFalse(v.ReportShown);
            Assert.AreEqual(v.CustomerId, s.Api.GetSale().CustomerId);
        }

        [Test]
        public void StartSale_TheCustomerStartsWithTrustAroundFifty_AndTheirOwnPatience()
        {
            CustomerView customer;
            GameSession s = SessionWithCustomer("npc.kemal", out customer);

            s.Api.StartSale(customer.CustomerId);

            ActiveSale active = s.TradeState.CurrentSale;
            Assert.That(active.State.Trust, Is.InRange(40, 60));
            Assert.AreEqual(5, active.State.Patience, "Kemal Abi müşteri sabrı 5");
            Assert.AreEqual(5540.0, active.State.Max, 0.0);
            Assert.AreEqual(0.2, active.State.Setup.Urgency, 0.0, "aciliyet: NPC'nin kendi değeri");
        }

        [Test]
        public void OnlyOneNegotiationAtATime_AcrossBuyAndSell()
        {
            CustomerView customer;
            GameSession s = SessionWithCustomer("npc.kemal", out customer);
            Assert.IsTrue(s.Api.StartSale(customer.CustomerId).IsSuccess);

            Assert.AreEqual("negotiation.in_progress", s.Api.StartSale(customer.CustomerId).ErrorCode);
            Assert.AreEqual("negotiation.in_progress", s.Api.StartNegotiation(s.Market.Listings[0].ListingId).ErrorCode);
            Assert.AreEqual("negotiation.in_progress", s.Api.EndDay().ErrorCode);
            Assert.IsTrue(s.Api.LetCustomerGo().IsSuccess);

            GameSession t = New();
            BuyGuidedAndPrice(t);
            Assert.IsTrue(t.Api.StartNegotiation(t.Market.Listings[0].ListingId).IsSuccess);
            Assert.AreEqual("negotiation.in_progress", t.Api.StartSale(t.Api.GetCustomers()[0].CustomerId).ErrorCode);
        }

        [Test]
        public void SaleCommands_WithoutASale_Fail()
        {
            GameSession s = New();

            Assert.AreEqual("sale.none", s.Api.AskPrice(Money.FromTl(1000)).ErrorCode);
            Assert.AreEqual("sale.none", s.Api.ShowReport(1).ErrorCode);
            Assert.AreEqual("sale.none", s.Api.AcceptCustomerFinalOffer().ErrorCode);
            Assert.AreEqual("sale.none", s.Api.LetCustomerGo().ErrorCode);
            Assert.IsNull(s.Api.GetSale());
        }

        // ---------- teklif turları ----------

        [Test]
        public void AnAskAtOrBelowTheFirstOffer_SellsAtTheCustomersOffer_AndBooksTheProfit()
        {
            CustomerView customer;
            GameSession s = SessionWithCustomer("npc.kemal", out customer);
            Money cash = s.Api.GetCash();
            long item = customer.InstanceId;
            Money costBasis = s.Store.Get(item).CostBasis;
            Assert.IsTrue(s.Api.StartSale(customer.CustomerId).IsSuccess);

            Result<SaleView> r = s.Api.AskPrice(Money.FromTl(1000));

            Assert.IsTrue(r.IsSuccess, r.ErrorCode);
            SaleView v = r.Value;
            Assert.AreEqual(NegotiationPhase.Deal, v.Phase);
            Assert.GreaterOrEqual(v.DealPrice.Tl, 1000L);
            Assert.LessOrEqual(v.DealPrice.Tl, 5540L, "I4: müşteri için ≤ M");
            Assert.AreEqual(v.DealPrice, v.ShownPrice);
            Assert.AreEqual(cash + v.DealPrice, s.Api.GetCash());
            Assert.AreEqual(0, s.Api.GetInventory().Count);
            Assert.AreEqual(ProductLocation.Sold, s.Store.Get(item).Location);
            Assert.IsTrue(s.Npcs.HasBoughtFromPlayer("npc.kemal", item));
            Assert.IsNull(s.Api.GetSale());
            TransactionRecord sale = s.EconomyState.Ledger.Records.Last(x => x.TypeId == "sale");
            Assert.AreEqual(v.DealPrice, sale.Amount);
            Assert.AreEqual(costBasis, sale.SaleCostBasis.Value);
            Assert.AreEqual(item, sale.InstanceId.Value);
            Assert.AreEqual("npc.kemal", sale.NpcId);
        }

        [Test]
        public void ARoundOfHigherAsks_FollowsTheReferenceSequence()
        {
            CustomerView customer;
            GameSession s = SessionWithCustomer("npc.kemal", out customer);
            Assert.IsTrue(s.Api.StartSale(customer.CustomerId).IsSuccess);
            ActiveSale active = s.TradeState.CurrentSale;
            var reference = new SaleEngine(s.Content.Negotiation);
            SaleState expected = reference.Begin(active.State.Setup);

            foreach (long ask in new long[] { 6400, 6200, 6100 })
            {
                SaleView v = Ask(s, ask);
                SaleRound r = reference.Ask(expected, Money.FromTl(ask)).Value;
                Assert.AreEqual(r.ShownPrice, v.ShownPrice);
                Assert.AreEqual(r.Phase, v.Phase);
                if (v.Phase != NegotiationPhase.Active)
                {
                    break;
                }
            }
        }

        [Test]
        public void TooExpensiveAsk_IsReportedInTheView()
        {
            CustomerView customer;
            GameSession s = SessionWithCustomer("npc.kemal", out customer);
            s.Api.StartSale(customer.CustomerId);

            SaleView v = Ask(s, 9000); // 1,15 × 5.540 = 6.371

            Assert.IsTrue(v.LastAskTooExpensive);
            Assert.AreEqual(1, v.Round);
        }

        [Test]
        public void InvalidAsk_Fails_AndKeepsTheSale()
        {
            CustomerView customer;
            GameSession s = SessionWithCustomer("npc.kemal", out customer);
            s.Api.StartSale(customer.CustomerId);
            string digest = s.Api.GetStateDigest();

            Assert.AreEqual("ask.invalid", s.Api.AskPrice(Money.FromTl(0)).ErrorCode);
            Assert.AreEqual("ask.invalid", s.Api.AskPrice(Money.FromTl(6005)).ErrorCode);
            Assert.AreEqual(digest, s.Api.GetStateDigest());
        }

        [Test]
        public void PatienceRunningOut_LeadsToAFinalOffer_ThatCanBeAcceptedOrRefused()
        {
            CustomerView customer;
            GameSession s = SessionWithCustomer("npc.kemal", out customer);
            s.Api.StartSale(customer.CustomerId);
            SaleView v = null;
            for (int i = 0; i < 6 && s.Api.GetSale() != null && s.Api.GetSale().Phase == NegotiationPhase.Active; i++)
            {
                v = Ask(s, 6300);
            }

            Assert.AreEqual(NegotiationPhase.FinalOffer, v.Phase);
            Assert.AreEqual("negotiation.final_offer_only", s.Api.AskPrice(Money.FromTl(6300)).ErrorCode);
            Money offer = v.ShownPrice;
            Money cash = s.Api.GetCash();

            Result<SaleView> accepted = s.Api.AcceptCustomerFinalOffer();

            Assert.IsTrue(accepted.IsSuccess, accepted.ErrorCode);
            Assert.AreEqual(NegotiationPhase.Deal, accepted.Value.Phase);
            Assert.AreEqual(offer, accepted.Value.DealPrice);
            Assert.AreEqual(cash + offer, s.Api.GetCash());
        }

        [Test]
        public void AcceptFinal_WhileActive_Fails()
        {
            CustomerView customer;
            GameSession s = SessionWithCustomer("npc.kemal", out customer);
            s.Api.StartSale(customer.CustomerId);

            Assert.AreEqual("negotiation.no_final_offer", s.Api.AcceptCustomerFinalOffer().ErrorCode);
        }

        [Test]
        public void LettingTheCustomerGo_KeepsTheItem_AndMarksTheCustomerLeft()
        {
            CustomerView customer;
            GameSession s = SessionWithCustomer("npc.kemal", out customer);
            Money cash = s.Api.GetCash();
            s.Api.StartSale(customer.CustomerId);

            Result<SaleView> r = s.Api.LetCustomerGo();

            Assert.IsTrue(r.IsSuccess);
            Assert.AreEqual(NegotiationPhase.Failed, r.Value.Phase);
            Assert.AreEqual(1, s.Api.GetInventory().Count);
            Assert.AreEqual(cash, s.Api.GetCash());
            Assert.IsNull(s.Api.GetSale());
            Assert.IsFalse(s.Api.GetCustomers().Any(v => v.CustomerId == customer.CustomerId), "giden müşteri listeden çıkar");
            Assert.AreEqual("customer.unknown", s.Api.StartSale(customer.CustomerId).ErrorCode);
        }

        [Test]
        public void ASoldCustomer_CannotBeStartedAgain()
        {
            CustomerView customer;
            GameSession s = SessionWithCustomer("npc.kemal", out customer);
            s.Api.StartSale(customer.CustomerId);
            s.Api.AskPrice(Money.FromTl(1000));

            Assert.AreEqual("customer.unknown", s.Api.StartSale(customer.CustomerId).ErrorCode);
        }

        [Test]
        public void WholeLoop_BuyShelfSell_BooksTheProfitInTheDaySummary()
        {
            CustomerView customer;
            GameSession s = SessionWithCustomer("npc.kemal", out customer);
            Money wealthBefore = s.Wealth.Calculate().Total;
            Money cost = s.Store.Get(customer.InstanceId).CostBasis;
            Assert.AreEqual(Money.FromTl(5390), cost);
            s.Api.StartSale(customer.CustomerId);

            SaleView sold = s.Api.AskPrice(Money.FromTl(1000)).Value;

            DaySummary summary = s.Api.GetTodaySummary();
            Assert.AreEqual(sold.DealPrice - cost, summary.NetProfit);
            Assert.AreEqual(sold.DealPrice, summary.SalesIncome);
            Assert.AreEqual(wealthBefore + summary.NetProfit, s.Wealth.Calculate().Total, "servet = kâr kadar artar");
            Assert.AreEqual(1, summary.Sales.Count);
        }

        // ---------- rapor göster ----------

        private static long InjectReport(GameSession s, long instanceId, string level, long id = 900)
        {
            s.Knowledge.Add(new AppraisalResult(
                id, instanceId, "phone.yildiz_y5", level, 1, Money.Zero, 1UL, new AttributeFinding[0],
                new NumericRange(80, 90), new NumericRange(95, 100), new MoneyRange(Money.FromTl(5000), Money.FromTl(5500)), new TrumpCard[0]));
            return id;
        }

        [Test]
        public void ShowReport_HalvesTheError_AndRaisesTrustByTenForOrdinaryCustomers()
        {
            CustomerView customer;
            GameSession s = SessionWithCustomer("npc.kemal", out customer);
            long report = InjectReport(s, customer.InstanceId, "s3");
            s.Api.StartSale(customer.CustomerId);
            int trust = s.TradeState.CurrentSale.State.Trust;

            Result<SaleView> r = s.Api.ShowReport(report);

            Assert.IsTrue(r.IsSuccess, r.ErrorCode);
            Assert.IsTrue(r.Value.ReportShown);
            Assert.AreEqual(Math.Min(100, trust + 10), s.TradeState.CurrentSale.State.Trust);
        }

        [Test]
        public void ShowReport_SuspiciousCustomerGainsFifteen()
        {
            CustomerView customer;
            GameSession s = SessionWithCustomerOnDay("npc.riza", out customer);
            long report = InjectReport(s, customer.InstanceId, "s2");
            s.Api.StartSale(customer.CustomerId);
            int trust = s.TradeState.CurrentSale.State.Trust;

            Assert.IsTrue(s.Api.ShowReport(report).IsSuccess);

            Assert.AreEqual(Math.Min(100, trust + 15), s.TradeState.CurrentSale.State.Trust, "Rıza Bey: rapor güveni +15");
        }

        [Test]
        public void ShowReport_MovesMaxTowardsTheTrueValue_ForANoisyCustomer()
        {
            CustomerView customer;
            GameSession s = SessionWithCustomerOnDay("npc.hatice", out customer);
            long report = InjectReport(s, customer.InstanceId, "s3");
            s.Api.StartSale(customer.CustomerId);
            double before = s.TradeState.CurrentSale.State.Max;
            double trueValue = 5280 * 1.05; // mRatio 1,00 × prim

            Assert.IsTrue(s.Api.ShowReport(report).IsSuccess);

            double after = s.TradeState.CurrentSale.State.Max;
            Assert.LessOrEqual(Math.Abs(after - trueValue), Math.Abs(before - trueValue) + 10.0);
        }


        [Test]
        public void ShowReport_SetsMaxToTheReportMax_ForANoisyCustomer_Exactly()
        {
            for (ulong seed = 1; seed <= 300; seed++)
            {
                GameSession s = New(seed);
                long id = BuyGuided(s);
                Assert.IsTrue(s.Api.SetPrice(id, Money.FromTl(5900)).IsSuccess);
                for (int d = 1; d < 3; d++)
                {
                    s.Api.EndDay();
                }

                foreach (CustomerView v in s.Api.GetCustomers())
                {
                    if (v.NpcId != "npc.hatice")
                    {
                        continue;
                    }

                    CustomerSlot slot = s.Customers.State.Slots.Single(x => x.CustomerId == v.CustomerId);
                    ProductInstance item = s.Store.Get(v.InstanceId);
                    double plain = s.Customers.MaxFor(slot, item, false);
                    double withReport = s.Customers.MaxFor(slot, item, true);
                    if (plain == withReport)
                    {
                        continue;
                    }

                    long report = InjectReport(s, v.InstanceId, "s3");
                    Assert.IsTrue(s.Api.StartSale(v.CustomerId).IsSuccess);
                    Assert.AreEqual(plain, s.TradeState.CurrentSale.State.Max, 0.0);

                    Assert.IsTrue(s.Api.ShowReport(report).IsSuccess);

                    Assert.AreEqual(withReport, s.TradeState.CurrentSale.State.Max, 0.0);
                    return;
                }
            }

            Assert.Fail("test verisi: rapor Max'ı değiştiren bir Hatice müşterisi bulunamadı");
        }

        [Test]
        public void ShowReport_Rules()
        {
            CustomerView customer;
            GameSession s = SessionWithCustomer("npc.kemal", out customer);
            long s1 = InjectReport(s, customer.InstanceId, "s1", 901);
            long s3 = InjectReport(s, customer.InstanceId, "s3", 902);
            s.Api.StartSale(customer.CustomerId);
            string digest = s.Api.GetStateDigest();

            Assert.AreEqual("report.unknown", s.Api.ShowReport(12345).ErrorCode);
            Assert.AreEqual("report.not_eligible", s.Api.ShowReport(s1).ErrorCode, "yalnızca S2/S3 raporu gösterilebilir");
            Assert.AreEqual(digest, s.Api.GetStateDigest());
            Assert.IsTrue(s.Api.ShowReport(s3).IsSuccess);
            Assert.AreEqual("report.already_shown", s.Api.ShowReport(s3).ErrorCode);
        }

        [Test]
        public void ShowReport_OfAnotherItem_IsNotEligible()
        {
            CustomerView customer;
            GameSession s = SessionWithCustomer("npc.kemal", out customer);
            MarketListing other = s.Market.Listings.First(l => !l.IsGuided);
            long foreign = InjectReport(s, other.InstanceId, "s3", 903);
            s.Api.StartSale(customer.CustomerId);

            Assert.AreEqual("report.unknown", s.Api.ShowReport(foreign).ErrorCode);
        }

        [Test]
        public void TheView_ListsTheReportsThatCouldBeShown()
        {
            CustomerView customer;
            GameSession s = SessionWithCustomer("npc.kemal", out customer);
            long s1 = InjectReport(s, customer.InstanceId, "s1", 901);
            long s3 = InjectReport(s, customer.InstanceId, "s3", 902);

            SaleView v = s.Api.StartSale(customer.CustomerId).Value;

            Assert.AreEqual(1, v.Reports.Count);
            Assert.AreEqual(s3, v.Reports[0].AppraisalId);
            Assert.AreEqual("s3", v.Reports[0].LevelId);
            Assert.AreNotEqual(s1, v.Reports[0].AppraisalId);
        }

        // ---------- talep baskısı ve olaylar ----------

        [Test]
        public void ASale_RecordsTheDemandPressure_ForThatModel()
        {
            CustomerView customer;
            GameSession s = SessionWithCustomer("npc.kemal", out customer);
            string model = s.Store.Get(customer.InstanceId).DefinitionId;
            s.Api.StartSale(customer.CustomerId);

            s.Api.AskPrice(Money.FromTl(1000));

            Assert.AreEqual(1, s.DemandState.Sales.Count);
            Assert.AreEqual(model, s.DemandState.Sales[0].ModelId);
            Assert.AreEqual(1, s.DemandState.Sales[0].Day);
            Assert.AreEqual(0.985, s.Demand.Pressure(model, 1), 1e-12);
        }

        [Test]
        public void Events_AreNotificationsPublishedInOrder()
        {
            var bus = new EventBus();
            var log = new List<string>();
            bus.Subscribe<CustomerNegotiationStarted>(e => log.Add("started"));
            bus.Subscribe<CustomerOfferMade>(e => log.Add("offer:" + e.Round + ":" + e.Phase));
            bus.Subscribe<ItemSold>(e => log.Add("sold"));
            bus.Subscribe<CustomerNegotiationEnded>(e => log.Add("ended:" + e.Phase));
            GameSession s = null;
            CustomerView customer = null;
            for (ulong seed = 1; seed <= 200 && customer == null; seed++)
            {
                s = New(seed, bus);
                BuyGuidedAndPrice(s);
                customer = s.Api.GetCustomers().FirstOrDefault(v => v.NpcId == "npc.kemal");
            }

            log.Clear();
            s.Api.StartSale(customer.CustomerId);
            s.Api.AskPrice(Money.FromTl(1000));

            CollectionAssert.AreEqual(new[] { "started", "offer:1:Deal", "sold", "ended:Deal" }, log);
        }


        [Test]
        public void Events_ANonDealRoundPublishesTheOfferWithoutEndingTheNegotiation()
        {
            var bus = new EventBus();
            var offers = new List<CustomerOfferMade>();
            var ended = new List<CustomerNegotiationEnded>();
            bus.Subscribe<CustomerOfferMade>(offers.Add);
            bus.Subscribe<CustomerNegotiationEnded>(ended.Add);
            GameSession s = null;
            CustomerView customer = null;
            for (ulong seed = 1; seed <= 200 && customer == null; seed++)
            {
                s = New(seed, bus);
                BuyGuidedAndPrice(s);
                customer = s.Api.GetCustomers().FirstOrDefault(v => v.NpcId == "npc.kemal");
            }

            s.Api.StartSale(customer.CustomerId);
            offers.Clear();

            SaleView v = Ask(s, 9000); // çok pahalı ve anlaşma değil

            Assert.AreEqual(1, offers.Count);
            Assert.AreEqual(customer.CustomerId, offers[0].CustomerId);
            Assert.AreEqual(1, offers[0].Round);
            Assert.AreEqual(Money.FromTl(9000), offers[0].Ask);
            Assert.AreEqual(v.ShownPrice, offers[0].ShownPrice);
            Assert.AreEqual(NegotiationPhase.Active, offers[0].Phase);
            Assert.IsTrue(offers[0].TooExpensive);
            Assert.AreEqual(0, ended.Count, "pazarlık sürüyor");
        }

        [Test]
        public void Events_LettingTheCustomerGo()
        {
            var bus = new EventBus();
            var log = new List<string>();
            bus.Subscribe<CustomerNegotiationStarted>(e => log.Add("started"));
            bus.Subscribe<ItemSold>(e => log.Add("sold"));
            bus.Subscribe<CustomerNegotiationEnded>(e => log.Add("ended:" + e.Phase + ":" + e.DealPrice.Tl));
            GameSession s = New(1UL, bus);
            BuyGuidedAndPrice(s);
            log.Clear();
            s.Api.StartSale(s.Api.GetCustomers()[0].CustomerId);

            s.Api.LetCustomerGo();

            CollectionAssert.AreEqual(new[] { "started", "ended:Failed:0" }, log);
        }

        // ---------- gizlilik ----------

        [Test]
        public void Views_CarryNoHiddenSaleState()
        {
            var forbidden = new[] { "Max", "Trust", "Patience", "Urgency", "Sigma", "Draw", "Multiplier", "Opening" };
            foreach (Type type in new[] { typeof(CustomerView), typeof(SaleReportView) })
            {
                foreach (PropertyInfo p in type.GetProperties())
                {
                    foreach (string word in forbidden)
                    {
                        StringAssert.DoesNotContain(word, p.Name, type.Name);
                    }
                }
            }

            CollectionAssert.AreEquivalent(
                new[] { "CustomerId", "NpcId", "InstanceId", "Phase", "Round", "ShownPrice", "DealPrice", "Mood", "Patience", "LastAskTooExpensive", "ReportShown", "Reports" },
                typeof(SaleView).GetProperties().Select(p => p.Name).ToArray());
            Assert.AreEqual(typeof(NegotiationLevel), typeof(SaleView).GetProperty("Mood").PropertyType);
            Assert.AreEqual(typeof(NegotiationLevel), typeof(SaleView).GetProperty("Patience").PropertyType);
            CollectionAssert.AreEquivalent(new[] { "CustomerId", "NpcId", "InstanceId" }, typeof(CustomerView).GetProperties().Select(p => p.Name).ToArray());
        }

        // ---------- durum özeti ve belirlenimcilik ----------

        [Test]
        public void Digest_ChangesWithTheSaleState()
        {
            CustomerView customer;
            GameSession s = SessionWithCustomer("npc.kemal", out customer);
            string idle = s.Api.GetStateDigest();

            s.Api.StartSale(customer.CustomerId);
            string started = s.Api.GetStateDigest();
            Ask(s, 6300);
            string afterAsk = s.Api.GetStateDigest();

            Assert.AreNotEqual(idle, started);
            Assert.AreNotEqual(started, afterAsk);
            Assert.IsTrue(GameStateDigest.Describe(s).Contains("\nS|"));
        }

        [Test]
        public void Digest_ListsTheRosterAndTheDemandState()
        {
            GameSession s = New();
            s.DemandState.SetIndex("phone.yildiz_y5", 1.05);
            s.Demand.RecordSale("phone.yildiz_y5", 1);

            string text = GameStateDigest.Describe(s);

            Assert.AreEqual(5, text.Split('\n').Count(l => l.StartsWith("K|")));
            Assert.IsTrue(text.Split('\n').Any(l => l.StartsWith("D|phone.yildiz_y5|" + BitConverter.DoubleToInt64Bits(1.05).ToString("x16"))));
            Assert.IsTrue(text.Split('\n').Any(l => l == "T|phone.yildiz_y5|1"));
            Assert.IsTrue(text.Contains("customers.arrived=2"));
        }

        [Test]
        public void SetPrice_ChangesTheDigest()
        {
            GameSession s = New();
            long id = BuyGuided(s);
            string untagged = s.Api.GetStateDigest();

            s.Api.SetPrice(id, Money.FromTl(5900));

            Assert.AreNotEqual(untagged, s.Api.GetStateDigest());
        }

        private static string PlayWeek(ulong seed)
        {
            GameSession s = New(seed);
            for (int day = 0; day < 7; day++)
            {
                foreach (long id in s.Market.Listings.Select(l => l.ListingId).ToList())
                {
                    MarketListing listing;
                    if (!s.Market.TryGet(id, out listing) || s.Api.GetInventory().Count >= 4 || s.Api.StartNegotiation(id).IsFailure)
                    {
                        continue;
                    }

                    long offer = Money.RoundTo10((long)(listing.AskingPrice.Tl * 0.85));
                    for (int i = 0; i < 6; i++)
                    {
                        Result<NegotiationView> r = s.Api.MakeOffer(Money.FromTl(offer));
                        if (r.IsFailure || r.Value.Phase != NegotiationPhase.Active)
                        {
                            break;
                        }

                        offer += 100;
                    }

                    NegotiationView open = s.Api.GetNegotiation();
                    if (open != null)
                    {
                        if (open.Phase == NegotiationPhase.FinalOffer)
                        {
                            s.Api.AcceptFinalPrice();
                        }
                        else
                        {
                            s.Api.WalkAway();
                        }
                    }
                }

                foreach (StockLine line in s.Api.GetInventory().ToList())
                {
                    s.Api.SetPrice(line.InstanceId, Money.FromTl(Money.RoundTo10((long)(line.CostBasis.Tl * 1.12))));
                }

                foreach (CustomerView c in s.Api.GetCustomers().ToList())
                {
                    if (s.Api.StartSale(c.CustomerId).IsFailure)
                    {
                        continue;
                    }

                    long ask = Money.RoundTo10((long)(s.Store.Get(c.InstanceId).ListPrice.Tl));
                    for (int i = 0; i < 6; i++)
                    {
                        Result<SaleView> r = s.Api.AskPrice(Money.FromTl(ask));
                        if (r.IsFailure || r.Value.Phase != NegotiationPhase.Active)
                        {
                            break;
                        }

                        ask = Math.Max(10, ask - 60);
                    }

                    SaleView open = s.Api.GetSale();
                    if (open != null)
                    {
                        if (open.Phase == NegotiationPhase.FinalOffer)
                        {
                            s.Api.AcceptCustomerFinalOffer();
                        }
                        else
                        {
                            s.Api.LetCustomerGo();
                        }
                    }
                }

                s.Api.EndDay();
            }

            return s.Api.GetStateDigest();
        }

        [Test]
        public void SameSeedSameCommands_GiveTheSameDigest_AndDifferentSeedsDiffer()
        {
            Assert.AreEqual(PlayWeek(3UL), PlayWeek(3UL));
            Assert.AreNotEqual(PlayWeek(3UL), PlayWeek(4UL));
        }

        // ---------- değişmezler (rastgele oyun) ----------

        [Test]
        public void Fuzz_TheWholeLoop_KeepsTheInvariants()
        {
            int sales = 0;
            for (ulong seed = 1; seed <= 30; seed++)
            {
                GameSession s = New(seed);
                for (int day = 0; day < 8; day++)
                {
                    foreach (long id in s.Market.Listings.Select(l => l.ListingId).ToList())
                    {
                        MarketListing listing;
                        if (!s.Market.TryGet(id, out listing) || s.Api.GetInventory().Count >= 5 || s.Api.StartNegotiation(id).IsFailure)
                        {
                            continue;
                        }

                        Result<NegotiationView> r = s.Api.MakeOffer(listing.AskingPrice);
                        if (r.IsFailure || r.Value.Phase != NegotiationPhase.Deal)
                        {
                            s.Api.WalkAway();
                        }
                    }

                    foreach (StockLine line in s.Api.GetInventory().ToList())
                    {
                        s.Api.SetPrice(line.InstanceId, Money.FromTl(Money.RoundTo10((long)(line.CostBasis.Tl * 1.1))));
                    }

                    foreach (CustomerView c in s.Api.GetCustomers().ToList())
                    {
                        if (s.Api.StartSale(c.CustomerId).IsFailure)
                        {
                            continue;
                        }

                        long soldItem = s.TradeState.CurrentSale.InstanceId;
                        double max = s.TradeState.CurrentSale.State.Max;
                        long ask = s.Store.Get(soldItem).ListPrice.Tl;
                        SaleView last = null;
                        for (int i = 0; i < 8; i++)
                        {
                            Result<SaleView> r = s.Api.AskPrice(Money.FromTl(ask));
                            if (r.IsFailure)
                            {
                                break;
                            }

                            last = r.Value;
                            if (last.Phase != NegotiationPhase.Active)
                            {
                                break;
                            }

                            ask = Math.Max(10, ask - 40);
                        }

                        if (last != null && last.Phase == NegotiationPhase.FinalOffer)
                        {
                            last = s.Api.AcceptCustomerFinalOffer().Value;
                        }

                        if (last != null && last.Phase == NegotiationPhase.Deal)
                        {
                            sales++;
                            Assert.LessOrEqual((double)last.DealPrice.Tl, max + 1e-9, "I4: müşteri için ≤ M");
                            Assert.AreEqual(ProductLocation.Sold, s.Store.Get(soldItem).Location);
                            Assert.IsFalse(s.InventoryState.Contains(soldItem));
                        }
                        else if (s.Api.GetSale() != null)
                        {
                            s.Api.LetCustomerGo();
                        }

                        Assert.IsNull(s.Api.GetSale());
                    }

                    // I1: nakit = başlangıç + defter toplamı; I3: raf ≤ kapasite
                    Assert.AreEqual(s.Content.EconomyConstants.OpeningCapital + s.EconomyState.Ledger.Records.Where(r => r.TypeId != "opening_capital").Aggregate(Money.Zero, (a, r) => a + r.Amount), s.Api.GetCash());
                    Assert.LessOrEqual(s.InventoryState.Count, s.InventoryState.Capacity);
                    Assert.IsTrue(s.Api.EndDay().IsSuccess);
                }
            }

            Assert.Greater(sales, 20, "rastgele oyunda satışlar gerçekleşmeli (test anlamlı olsun)");
        }

        /// <summary>Gün 4+ NPC'leri için: müşterisi çıkana kadar tohum/gün dener.</summary>
        private static GameSession SessionWithCustomerOnDay(string npcId, out CustomerView customer)
        {
            int neededDay = MarketHarness.RealContent().GetNpc(npcId).Customer.AvailableFromDay;
            for (ulong seed = 1; seed <= 300; seed++)
            {
                GameSession s = New(seed);
                long id = BuyGuided(s);
                Assert.IsTrue(s.Api.SetPrice(id, Money.FromTl(5900)).IsSuccess);
                for (int i = 1; i < neededDay; i++)
                {
                    // eski ilanları beklemeden ilerlemek için gün sonu: rafın ürünü kalır
                    s.Api.EndDay();
                }

                foreach (CustomerView v in s.Api.GetCustomers())
                {
                    if (v.NpcId == npcId)
                    {
                        customer = v;
                        return s;
                    }
                }
            }

            throw new InvalidOperationException("No seed gives a " + npcId + " customer.");
        }
    }
}
