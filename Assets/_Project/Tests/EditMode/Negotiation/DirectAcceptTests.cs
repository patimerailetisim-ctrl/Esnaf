using System;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Business;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Game;
using Esnaf.Domain.Negotiation;
using Esnaf.Domain.Npc;
using Esnaf.Presentation;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Negotiation
{
    /// <summary>
    /// Müşteri çeşitliliği: bazı müşteriler oyuncunun istediği fiyatı pazarlıksız kabul eder, bazıları pazarlık eder. Mevcut müşteri kişilik sayıları
    /// (acele, bütçe, sabır, bilgi, pazarlık eğilimi, güven) kullanılır; rastgelelik ve yeni durum yoktur.
    /// </summary>
    public class DirectAcceptTests
    {
        private static readonly string[] Tolerant = { "selin", "berk", "ozan", "cengiz" };
        private static readonly string[] Hagglers = { "kemal", "hatice", "riza", "nermin", "murat", "ayse" };

        private static SaleEngine Engine()
        {
            return new SaleEngine(MarketHarness.RealContent().Negotiation);
        }

        private static SaleSetup SetupOf(string npc, double max)
        {
            NpcDefinition def = MarketHarness.RealContent().GetNpc("npc." + npc);
            long opening = (long)Math.Floor(def.Customer.OpeningOfferRatio * max / 10.0 + 0.5) * 10L;
            return new SaleSetup(Money.FromTl(opening), max, def.Customer.Patience, 50, def.Seller.Urgency, 10);
        }

        private static double RatioOf(string npc, int trust = 50)
        {
            NpcDefinition def = MarketHarness.RealContent().GetNpc("npc." + npc);
            return DirectAcceptPolicy.Ratio(def.Customer.OpeningOfferRatio, def.Customer.ValueRatio, def.Customer.Patience, def.Seller.Urgency, def.Customer.ValueSigma, trust);
        }

        // ---------- politika ----------

        [Test]
        public void TheRatio_StaysInsideItsBounds_ForEveryCustomerAndTrust_SoNobodyPaysMoreThanTheirMax()
        {
            foreach (string npc in Tolerant.Concat(Hagglers))
            {
                for (int trust = 0; trust <= 100; trust += 10)
                {
                    double ratio = RatioOf(npc, trust);
                    Assert.GreaterOrEqual(ratio, DirectAcceptPolicy.MinRatio, npc);
                    Assert.LessOrEqual(ratio, 1.0, npc);
                }
            }
        }

        [Test]
        public void TheRatio_FollowsTheCustomerPersonality_HurriedAndFlexibleAreEasier_HagglersAreHarder()
        {
            Assert.Greater(RatioOf("selin"), RatioOf("kemal"), "acelesi olan pazarlıkçıdan kolay");
            Assert.Greater(RatioOf("berk"), RatioOf("riza"), "bütçesi esnek şüpheciden kolay");
            Assert.Greater(RatioOf("cengiz"), RatioOf("hatice"), "borçlu/aceleci bilgisizden kolay");
            Assert.Greater(RatioOf("ozan"), RatioOf("nermin"), "sabırsız koleksiyoncudan kolay");
            Assert.Greater(RatioOf("selin", 90), RatioOf("selin", 10), "güven arttıkça kolaylaşır");
            Assert.AreEqual(RatioOf("kemal"), RatioOf("kemal"), "deterministik");
        }

        // ---------- motor ----------

        [Test]
        public void TheOldBehaviour_IsKept_WhenNoDirectAcceptRatioIsGiven()
        {
            SaleEngine e = Engine();
            SaleState s = e.Begin(SetupOf("selin", 10000));

            SaleRound round = e.Ask(s, Money.FromTl(9900)).Value;

            Assert.AreEqual(NegotiationPhase.Active, round.Phase, "oran yok: pazarlık sürer");
            Assert.AreEqual(Money.Zero, round.DealPrice);
        }

        [Test]
        public void ACustomerWhoAcceptsDirectly_BuysAtExactlyTheAskedPrice_WhichIsNeverAboveTheirMax()
        {
            SaleEngine e = Engine();
            SaleState s = e.Begin(SetupOf("selin", 10000));

            SaleRound round = e.Ask(s, Money.FromTl(9900), RatioOf("selin")).Value;

            Assert.AreEqual(NegotiationPhase.Deal, round.Phase);
            Assert.AreEqual(Money.FromTl(9900), round.DealPrice, "istenen fiyat aynen");
            Assert.LessOrEqual(round.DealPrice.Tl, 10000L, "M'yi aşmaz");
            Assert.AreEqual(1, round.Round);
        }

        [Test]
        public void AnAskAboveTheCeiling_StillBargains_AndAnAskAboveTheMaxIsNeverAccepted()
        {
            SaleEngine e = Engine();
            double ratio = RatioOf("selin");

            SaleState above = e.Begin(SetupOf("selin", 10000));
            SaleRound a = e.Ask(above, Money.FromTl(10500), ratio).Value;
            SaleState way = e.Begin(SetupOf("selin", 10000));
            SaleRound b = e.Ask(way, Money.FromTl(15000), ratio).Value;

            Assert.AreEqual(NegotiationPhase.Active, a.Phase, "M'nin üstü kabul edilmez");
            Assert.AreEqual(NegotiationPhase.Active, b.Phase);
        }

        [Test]
        public void HagglersNeverAcceptDirectly_WhileTheTolerantOnesDo_AcrossEveryAsk()
        {
            SaleEngine e = Engine();
            foreach (string npc in Hagglers)
            {
                Assert.AreEqual(0, DirectDeals(e, npc), npc + " hep pazarlık eder");
            }

            foreach (string npc in Tolerant)
            {
                Assert.Greater(DirectDeals(e, npc), 0, npc + " bazı fiyatları doğrudan kabul eder");
            }
        }

        // Müşterinin teklifinin ÜSTÜNDE olup DOĞRUDAN kabul edilen (anlaşma = istenen fiyat) istek sayısı.
        private static int DirectDeals(SaleEngine e, string npc)
        {
            int count = 0;
            for (long ask = 5000; ask <= 11000; ask += 10)
            {
                SaleState s = e.Begin(SetupOf(npc, 10000));
                SaleRound r = e.Ask(s, Money.FromTl(ask), RatioOf(npc)).Value;
                if (r.Phase == NegotiationPhase.Deal && r.DealPrice == Money.FromTl(ask) && r.DealPrice > r.ShownPrice)
                {
                    count++;
                }
            }

            return count;
        }

        [Test]
        public void TheSameInputs_AlwaysGiveTheSameResult()
        {
            SaleEngine e = Engine();
            SaleRound a = e.Ask(e.Begin(SetupOf("berk", 10000)), Money.FromTl(9650), RatioOf("berk")).Value;
            SaleRound b = e.Ask(e.Begin(SetupOf("berk", 10000)), Money.FromTl(9650), RatioOf("berk")).Value;

            Assert.AreEqual(a.Phase, b.Phase);
            Assert.AreEqual(a.DealPrice, b.DealPrice);
            Assert.AreEqual(a.ShownPrice, b.ShownPrice);
        }

        // ---------- gerçek akış (IGameApi) ----------

        private static GameSession Prepare(ulong seed, int bumps)
        {
            return AddOnDeals.Prepare(seed, bumps);
        }

        /// <summary>Verilen NPC'nin müşteri olarak geldiği bir oturum (tohum/kaydırma aranır).</summary>
        private static GameSession SessionWithNpc(string npc, out CustomerView customer, Func<GameSession, bool> extra = null)
        {
            for (int bumps = 0; bumps <= 60; bumps++)
            {
                for (ulong seed = 1; seed <= 30; seed++)
                {
                    GameSession s = Prepare(seed, bumps);
                    var guided = s.Market.Listings.Single(l => l.IsGuided);
                    if (!s.Api.StartNegotiation(guided.ListingId).IsSuccess
                        || !s.Api.MakeOffer(Money.FromTl(5800)).IsSuccess
                        || !s.Api.SetPrice(guided.InstanceId, Money.FromTl(5900)).IsSuccess)
                    {
                        continue;
                    }

                    foreach (CustomerView v in s.Api.GetCustomers())
                    {
                        if (v.NpcId == "npc." + npc && (extra == null || extra(s)))
                        {
                            customer = v;
                            return s;
                        }
                    }
                }
            }

            throw new InvalidOperationException("No session with " + npc);
        }

        private static long MaxOf(GameSession s, CustomerView customer)
        {
            CustomerSlot slot;
            Assert.IsTrue(s.Customers.TryGetWaiting(customer.CustomerId, out slot));
            return (long)s.Customers.MaxFor(slot, s.Store.Get(customer.InstanceId), false);
        }

        // M'nin %98'i (10 ₺'ye aşağı yuvarlı): hoşgörülü müşterilerin tavanı (güvene göre ~%99,5–%100) altında, ilk tur teklifinin (~%95,7) üstünde.
        private static long AskNear(GameSession s, CustomerView customer)
        {
            return (long)Math.Floor(MaxOf(s, customer) * 0.98 / 10.0) * 10L;
        }

        [Test]
        public void ADirectlyAcceptingCustomer_CompletesTheSale_AtTheAskedPrice_ThroughTheNormalLedger()
        {
            CustomerView customer;
            GameSession s = SessionWithNpc("selin", out customer);
            long ask = AskNear(s, customer);
            Money cash = s.Api.GetCash();
            Assert.IsTrue(s.Api.StartSale(customer.CustomerId).IsSuccess);

            Result<SaleView> result = s.Api.AskPrice(Money.FromTl(ask));

            Assert.IsTrue(result.IsSuccess, result.ErrorCode);
            Assert.AreEqual(NegotiationPhase.Deal, result.Value.Phase, "pazarlıksız kabul");
            Assert.AreEqual(Money.FromTl(ask), result.Value.DealPrice, "satış fiyatı = istenen fiyat");
            Assert.AreEqual(1, result.Value.Round, "tek turda");
            Assert.AreEqual(cash + Money.FromTl(ask), s.Api.GetCash());
            TransactionRecord row = s.EconomyState.Ledger.Records.Last(r => r.TypeId == TransactionTypeIds.Sale);
            Assert.AreEqual(Money.FromTl(ask), row.Amount);
            Assert.AreEqual(0, s.Api.GetInventory().Count);
            Assert.AreEqual(Money.FromTl(ask) - row.SaleCostBasis.Value, s.AccessoryAddOns.TotalProfitOfSale(row.Id).Value, "kâr doğru");
        }

        [Test]
        public void ABargainingCustomer_AtTheSameRelativePrice_KeepsTheExistingNegotiation()
        {
            CustomerView customer;
            GameSession s = SessionWithNpc("kemal", out customer);
            long ask = AskNear(s, customer);
            Assert.IsTrue(s.Api.StartSale(customer.CustomerId).IsSuccess);

            Result<SaleView> result = s.Api.AskPrice(Money.FromTl(ask));

            Assert.IsTrue(result.IsSuccess, result.ErrorCode);
            Assert.AreNotEqual(NegotiationPhase.Deal, result.Value.Phase, "pazarlıkçı müşteri direkt kabul etmez");
            Assert.IsNotNull(s.Api.GetSale(), "pazarlık sürüyor");
            Assert.Less(result.Value.ShownPrice.Tl, ask, "müşteri karşı teklif verdi");
            Assert.AreEqual(1, result.Value.Round);
        }

        [Test]
        public void TheExistingAcceptOffer_StillWorks_AfterAHagglersCounterOffer()
        {
            CustomerView customer;
            GameSession s = SessionWithNpc("kemal", out customer);
            long ask = AskNear(s, customer);
            s.Api.StartSale(customer.CustomerId);
            Money offer = s.Api.AskPrice(Money.FromTl(ask)).Value.ShownPrice;

            Result<SaleView> accepted = s.Api.AcceptCustomerOffer();

            Assert.IsTrue(accepted.IsSuccess, accepted.ErrorCode);
            Assert.AreEqual(offer, accepted.Value.DealPrice, "Teklifi Kabul Et: müşterinin teklif fiyatı");
        }

        [Test]
        public void TheSameSeedAndAsk_AlwaysGiveTheSameOutcome_AndTheRngIsUntouched()
        {
            CustomerView c1;
            GameSession a = SessionWithNpc("selin", out c1);
            long ask = AskNear(a, c1);
            var rng = a.Capture().Rng;
            a.Api.StartSale(c1.CustomerId);
            SaleView first = a.Api.AskPrice(Money.FromTl(ask)).Value;

            CustomerView c2;
            GameSession b = SessionWithNpc("selin", out c2);
            b.Api.StartSale(c2.CustomerId);
            SaleView second = b.Api.AskPrice(Money.FromTl(ask)).Value;

            Assert.AreEqual(first.Phase, second.Phase);
            Assert.AreEqual(first.DealPrice, second.DealPrice);
            Assert.AreEqual(a.Api.GetStateDigest(), b.Api.GetStateDigest(), "aynı tohum, aynı durum");
            Assert.IsNull(DeepCompare.FirstDifference(rng, a.Capture().Rng), "RNG akışı değişmedi");
        }

        [Test]
        public void AfterADirectAccept_TheAccessoryFlowOpens_WhenTheCustomerAsked_AndTheResultScreenWhenNot()
        {
            bool sawRequest = false;
            bool sawNone = false;
            for (int bumps = 0; bumps <= 60 && !(sawRequest && sawNone); bumps++)
            {
                for (ulong seed = 1; seed <= 30 && !(sawRequest && sawNone); seed++)
                {
                    GameSession s = Prepare(seed, bumps);
                    var guided = s.Market.Listings.Single(l => l.IsGuided);
                    s.Api.StartNegotiation(guided.ListingId);
                    s.Api.MakeOffer(Money.FromTl(5800));
                    s.Api.SetPrice(guided.InstanceId, Money.FromTl(5900));
                    CustomerView customer = s.Api.GetCustomers().FirstOrDefault(v => v.NpcId == "npc.selin");
                    if (customer == null)
                    {
                        continue;
                    }

                    long ask = AskNear(s, customer);
                    using (var flow = new UiFlow(s.Api, new ContentPresentation(s.Content), s.Bus))
                    {
                        Assert.IsTrue(flow.OpenCustomers());
                        Assert.IsTrue(flow.StartSale(customer.CustomerId).IsSuccess);
                        flow.SaleGreet();
                        flow.AdjustSalePrice(-100000);
                        flow.AdjustSalePrice((int)ask - 10); // -100000 ile 10 ₺ minimuma inildi
                        Result<SaleView> deal = flow.SaleAsk();

                        Assert.IsTrue(deal.IsSuccess);
                        Assert.AreEqual(NegotiationPhase.Deal, deal.Value.Phase);
                        Assert.AreEqual(SaleMode.Done, flow.SaleScreen.Mode);
                        long id = s.EconomyState.Ledger.Records.Last(r => r.TypeId == TransactionTypeIds.Sale).Id;
                        bool asked = s.AccessoryAddOns.RequestedAccessories(id).Count > 0;
                        if (asked)
                        {
                            Assert.IsNotNull(flow.SaleScreen.AddOn, "talep var → aksesuar paneli");
                            Assert.AreEqual(Money.FromTl(ask), flow.SaleScreen.AddOn.PhonePrice, "doğrudan kabul fiyatı satış fiyatı");
                            sawRequest = true;
                        }
                        else
                        {
                            Assert.IsNull(flow.SaleScreen.AddOn, "talep yok → sonuç ekranı");
                            sawNone = true;
                        }
                    }
                }
            }

            Assert.IsTrue(sawRequest && sawNone, "iki durum da görüldü");
        }
    }
}
