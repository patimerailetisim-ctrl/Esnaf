using System;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Appraisal;
using Esnaf.Domain.Business;
using Esnaf.Domain.Game;
using Esnaf.Domain.Market;
using Esnaf.Domain.Negotiation;
using Esnaf.Presentation;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Presentation
{
    /// <summary>Müşteri satış ekranı (UiFlow): lobi, konuşma, doğal cevap düğmeleri, anlaşma. Hepsi mevcut satış API'sinin üstünde; oyun kuralı yok.</summary>
    public class UiFlowSaleTests
    {
        private static GameSession SessionWithCustomer(out CustomerView customer)
        {
            for (ulong seed = 1; seed <= 200; seed++)
            {
                GameSession s = GameSession.NewGame(MarketHarness.RealContent(), seed);
                MarketListing g = s.Market.Listings.Single(l => l.IsGuided);
                Assert.IsTrue(s.Api.StartNegotiation(g.ListingId).IsSuccess);
                Assert.IsTrue(s.Api.MakeOffer(Money.FromTl(5800)).IsSuccess);
                Assert.IsTrue(s.Api.SetPrice(g.InstanceId, Money.FromTl(5900)).IsSuccess);
                if (s.Api.GetCustomers().Count > 0)
                {
                    customer = s.Api.GetCustomers()[0];
                    return s;
                }
            }

            throw new InvalidOperationException("No seed gives a customer.");
        }

        private static UiFlow Flow(GameSession s)
        {
            return new UiFlow(s.Api, new ContentPresentation(s.Content), s.Bus);
        }

        private static UiFlow OpenSale(GameSession s)
        {
            UiFlow flow = Flow(s);
            Assert.IsTrue(flow.OpenCustomers());
            return flow;
        }

        // ---------- lobi ----------

        [Test]
        public void TheCustomersButton_CountsTheCustomersInTheShop()
        {
            CustomerView customer;
            GameSession s = SessionWithCustomer(out customer);
            using (UiFlow flow = Flow(s))
            {
                Assert.AreEqual(TurkishTexts.CustomersButton(s.Api.GetCustomers().Count), flow.CustomersButtonText);
                StringAssert.Contains("(" + s.Api.GetCustomers().Count + ")", flow.CustomersButtonText);
            }
        }

        [Test]
        public void OpenCustomers_ShowsTheLobby_WithTheRealCustomerCards()
        {
            CustomerView customer;
            GameSession s = SessionWithCustomer(out customer);
            using (UiFlow flow = OpenSale(s))
            {
                SaleScreenViewModel screen = flow.SaleScreen;

                Assert.AreEqual(UiScreen.Sale, flow.CurrentScreen);
                Assert.AreEqual(SaleMode.Lobby, screen.Mode);
                Assert.AreEqual(s.Api.GetCustomers().Count, screen.Customers.Count);
                SaleCustomerCardViewModel card = screen.Customers.Single(c => c.CustomerId == customer.CustomerId);
                Assert.AreEqual(flow.Content.NpcName(customer.NpcId), card.Name);
                Assert.AreEqual(customer.Profile.PersonalityName, card.PersonalityName);
                StringAssert.Contains(flow.Content.ModelName("phone.yildiz_y5"), card.InterestLine);
                Assert.IsNull(screen.EmptyNote);
            }
        }

        [Test]
        public void AnEmptyShop_ExplainsWhyNoCustomerComes()
        {
            GameSession s = GameSession.NewGame(MarketHarness.RealContent(), 1UL);
            using (UiFlow flow = OpenSale(s))
            {
                Assert.AreEqual(0, flow.SaleScreen.Customers.Count);
                Assert.AreEqual(TurkishTexts.NoCustomers, flow.SaleScreen.EmptyNote);
            }
        }

        [Test]
        public void OpenCustomers_OnlyWorksFromTheListings_AndBackReturnsThere()
        {
            CustomerView customer;
            GameSession s = SessionWithCustomer(out customer);
            using (UiFlow flow = Flow(s))
            {
                Assert.IsTrue(flow.OpenListing(flow.Listings[0].ListingId));
                Assert.IsFalse(flow.OpenCustomers());
                flow.Back();
                Assert.IsTrue(flow.OpenCustomers());

                flow.Back();

                Assert.AreEqual(UiScreen.Listings, flow.CurrentScreen);
                Assert.IsNull(flow.SaleScreen);
            }
        }

        // ---------- konuşma ----------

        [Test]
        public void StartingASale_ShowsTheCustomerWithItsProfile_ThePhone_AndANaturalGreeting()
        {
            CustomerView customer;
            GameSession s = SessionWithCustomer(out customer);
            using (UiFlow flow = OpenSale(s))
            {
                Assert.IsTrue(flow.StartSale(customer.CustomerId).IsSuccess);
                SaleScreenViewModel screen = flow.SaleScreen;

                Assert.AreEqual(SaleMode.Talking, screen.Mode);
                Assert.AreEqual(flow.Content.NpcName(customer.NpcId), screen.CustomerName);
                Assert.AreEqual(customer.NpcId, screen.NpcId);
                StringAssert.StartsWith(customer.Profile.PersonalityName, screen.InfoLine);
                StringAssert.Contains("Bütçe", screen.InfoLine);
                Assert.AreEqual("phone.yildiz_y5", screen.DefinitionId);
                Assert.AreEqual(flow.Content.ModelName("phone.yildiz_y5"), screen.ModelTitle);
                StringAssert.Contains(screen.ModelTitle, screen.CustomerLine);
                CollectionAssert.AreEqual(new[] { SaleReplyKind.Greet, SaleReplyKind.LetGo }, screen.Replies.Select(r => r.Kind).ToArray());
                Assert.AreEqual("Tabii abi, buyur.", screen.Replies[0].Text);
                Assert.IsFalse(screen.ShowsPriceStepper);
            }
        }

        [Test]
        public void TheReplies_AreNaturalSentences_NeverMenuLabels()
        {
            CustomerView customer;
            GameSession s = SessionWithCustomer(out customer);
            using (UiFlow flow = OpenSale(s))
            {
                flow.StartSale(customer.CustomerId);
                flow.SaleGreet();

                foreach (SaleReplyViewModel reply in flow.SaleScreen.Replies)
                {
                    StringAssert.DoesNotContain("Fiyat Söyle", reply.Text);
                    StringAssert.DoesNotContain("Rapor Göster", reply.Text);
                }

                Assert.AreEqual(SaleReplyKind.Ask, flow.SaleScreen.Replies[0].Kind);
                StringAssert.EndsWith("₺ olur abi.", flow.SaleScreen.Replies[0].Text);
            }
        }

        [Test]
        public void Greeting_MovesTheTalkOn_WithoutTouchingTheGame()
        {
            CustomerView customer;
            GameSession s = SessionWithCustomer(out customer);
            using (UiFlow flow = OpenSale(s))
            {
                flow.StartSale(customer.CustomerId);
                string digest = s.Api.GetStateDigest();

                Assert.IsTrue(flow.SaleGreet());

                Assert.AreEqual(digest, s.Api.GetStateDigest());
                Assert.IsTrue(flow.SaleScreen.ShowsPriceStepper);
                StringAssert.Contains("Kaç yazdın buna?", flow.SaleScreen.CustomerLine);
                Assert.AreEqual("Tabii abi, buyur.", flow.SaleScreen.PlayerLine);
                Assert.IsFalse(flow.SaleGreet(), "ikinci kez selam yok");
            }
        }

        [Test]
        public void ThePriceStepper_MovesInSteps_AndNeverGoesBelowTen()
        {
            CustomerView customer;
            GameSession s = SessionWithCustomer(out customer);
            using (UiFlow flow = OpenSale(s))
            {
                flow.StartSale(customer.CustomerId);
                flow.SaleGreet();
                Money start = flow.SaleScreen.AskPrice;
                string digest = s.Api.GetStateDigest();

                flow.AdjustSalePrice(500);
                Assert.AreEqual(start.Tl + 500, flow.SaleScreen.AskPrice.Tl);
                flow.AdjustSalePrice(-100);
                Assert.AreEqual(start.Tl + 400, flow.SaleScreen.AskPrice.Tl);
                flow.AdjustSalePrice(-10000000);
                Assert.AreEqual(10L, flow.SaleScreen.AskPrice.Tl);

                Assert.AreEqual(digest, s.Api.GetStateDigest());
                Assert.AreEqual(MoneyFormatter.Format(flow.SaleScreen.AskPrice), flow.SaleScreen.AskPriceText);
            }
        }

        [Test]
        public void ATooHighAsk_GetsACounterOffer_AndTheCustomerObjects()
        {
            CustomerView customer;
            GameSession s = SessionWithCustomer(out customer);
            using (UiFlow flow = OpenSale(s))
            {
                flow.StartSale(customer.CustomerId);
                flow.SaleGreet();
                flow.AdjustSalePrice(100000);

                Result<SaleView> result = flow.SaleAsk();

                Assert.IsTrue(result.IsSuccess, result.ErrorCode);
                Assert.AreEqual(SaleMode.Talking, flow.SaleScreen.Mode);
                Assert.IsNotNull(s.Api.GetSale());
                StringAssert.Contains(MoneyFormatter.Format(result.Value.ShownPrice), flow.SaleScreen.CustomerLine);
                StringAssert.EndsWith("olur abi.", flow.SaleScreen.PlayerLine);
            }
        }

        [Test]
        public void AnAcceptedAsk_SellsOnTheGame_AndTheScreenShowsTheDeal()
        {
            CustomerView customer;
            GameSession s = SessionWithCustomer(out customer);
            using (UiFlow flow = OpenSale(s))
            {
                flow.StartSale(customer.CustomerId);
                flow.SaleGreet();
                flow.AdjustSalePrice(-100000); // 10 ₺: her müşteri kabul eder
                Money cash = s.Api.GetCash();

                Result<SaleView> result = flow.SaleAsk();

                Assert.IsTrue(result.IsSuccess, result.ErrorCode);
                Assert.AreEqual(NegotiationPhase.Deal, result.Value.Phase);
                Assert.AreEqual(cash + result.Value.DealPrice, s.Api.GetCash(), "para oyundan gelir");
                Assert.IsNull(s.Api.GetSale());
                SaleScreenViewModel screen = flow.SaleScreen;
                Assert.AreEqual(SaleMode.Done, screen.Mode);
                Assert.AreEqual(TurkishTexts.SaleDeal(result.Value.DealPrice), screen.Title);
                StringAssert.Contains(MoneyFormatter.Format(result.Value.DealPrice), screen.CustomerLine);
                CollectionAssert.AreEqual(new[] { SaleReplyKind.Continue }, screen.Replies.Select(r => r.Kind).ToArray());

                Assert.IsTrue(flow.SaleNext());

                Assert.AreEqual(SaleMode.Lobby, flow.SaleScreen.Mode);
                Assert.IsFalse(flow.SaleNext());
            }
        }

        [Test]
        public void LettingTheCustomerGo_EndsTheSale_AndTheItemStaysOnTheShelf()
        {
            CustomerView customer;
            GameSession s = SessionWithCustomer(out customer);
            using (UiFlow flow = OpenSale(s))
            {
                flow.StartSale(customer.CustomerId);

                Result<SaleView> result = flow.SaleLetGo();

                Assert.IsTrue(result.IsSuccess, result.ErrorCode);
                Assert.AreEqual(1, s.Api.GetInventory().Count);
                Assert.AreEqual(SaleMode.Done, flow.SaleScreen.Mode);
                Assert.AreEqual(SaleDialogue.Left(), flow.SaleScreen.CustomerLine);
                Assert.AreEqual(flow.Content.ModelName("phone.yildiz_y5"), flow.SaleScreen.ModelTitle, "ürün adı satış bitince de görünür");
            }
        }

        [Test]
        public void TheReportReply_AppearsOnlyWithAShowableReport_AndShowsItOnce()
        {
            CustomerView customer;
            GameSession s = SessionWithCustomer(out customer);
            long report = 900;
            s.Knowledge.Add(new AppraisalResult(
                report, customer.InstanceId, "phone.yildiz_y5", "s3", 1, Money.Zero, 1UL, new AttributeFinding[0],
                new NumericRange(80, 90), new NumericRange(95, 100), new MoneyRange(Money.FromTl(5000), Money.FromTl(5500)), new TrumpCard[0]));
            using (UiFlow flow = OpenSale(s))
            {
                flow.StartSale(customer.CustomerId);
                flow.SaleGreet();
                SaleReplyViewModel reply = flow.SaleScreen.Replies.Single(r => r.Kind == SaleReplyKind.ShowReport);
                Assert.AreEqual(report, reply.ReportId);
                Assert.AreEqual("Ekspertizi yapıldı, raporu göstereyim.", reply.Text);

                Result<SaleView> result = flow.SaleShowReport(reply.ReportId);

                Assert.IsTrue(result.IsSuccess, result.ErrorCode);
                Assert.IsTrue(result.Value.ReportShown);
                CollectionAssert.DoesNotContain(flow.SaleScreen.Replies.Select(r => r.Kind).ToArray(), SaleReplyKind.ShowReport);
            }
        }

        [Test]
        public void WithoutAReport_ThereIsNoReportReply()
        {
            CustomerView customer;
            GameSession s = SessionWithCustomer(out customer);
            using (UiFlow flow = OpenSale(s))
            {
                flow.StartSale(customer.CustomerId);
                flow.SaleGreet();

                CollectionAssert.DoesNotContain(flow.SaleScreen.Replies.Select(r => r.Kind).ToArray(), SaleReplyKind.ShowReport);
            }
        }

        [Test]
        public void TheSaleKeepsRunning_WhenTheScreenIsLeft_AndResumesWhereItWas()
        {
            CustomerView customer;
            GameSession s = SessionWithCustomer(out customer);
            using (UiFlow flow = OpenSale(s))
            {
                flow.StartSale(customer.CustomerId);
                flow.SaleGreet();
                flow.Back();
                Assert.IsNotNull(s.Api.GetSale());

                Assert.IsTrue(flow.OpenCustomers());

                Assert.AreEqual(SaleMode.Talking, flow.SaleScreen.Mode);
                Assert.IsTrue(flow.SaleScreen.ShowsPriceStepper, "selamdan sonraki aşama korunur");
            }
        }

        [Test]
        public void ANewFlow_ResumesARunningSale_FromTheGame()
        {
            CustomerView customer;
            GameSession s = SessionWithCustomer(out customer);
            s.Api.StartSale(customer.CustomerId);
            using (UiFlow flow = OpenSale(s))
            {
                Assert.AreEqual(SaleMode.Talking, flow.SaleScreen.Mode);
                Assert.AreEqual(flow.Content.NpcName(customer.NpcId), flow.SaleScreen.CustomerName);
                Assert.AreEqual("phone.yildiz_y5", flow.SaleScreen.DefinitionId);
            }
        }

        [Test]
        public void StartingAGoneCustomer_SaysSoInTurkish_AndChangesNothing()
        {
            CustomerView customer;
            GameSession s = SessionWithCustomer(out customer);
            using (UiFlow flow = OpenSale(s))
            {
                string digest = s.Api.GetStateDigest();

                Result<SaleView> result = flow.StartSale(987654L);

                Assert.IsTrue(result.IsFailure);
                Assert.AreEqual(TurkishTexts.Error("customer.unknown"), flow.StatusMessage);
                Assert.AreEqual(digest, s.Api.GetStateDigest());
                Assert.AreEqual(SaleMode.Lobby, flow.SaleScreen.Mode);
            }
        }

        [Test]
        public void TheCommands_DoNothing_OutsideTheSaleScreen()
        {
            CustomerView customer;
            GameSession s = SessionWithCustomer(out customer);
            using (UiFlow flow = Flow(s))
            {
                Assert.IsTrue(flow.StartSale(customer.CustomerId).IsFailure);
                Assert.IsFalse(flow.SaleGreet());
                Assert.IsTrue(flow.SaleAsk().IsFailure);
                Assert.IsTrue(flow.SaleLetGo().IsFailure);
                Assert.IsFalse(flow.SaleNext());
                Assert.IsNull(s.Api.GetSale());
            }
        }

        [Test]
        public void TheScreenModel_NeverCarriesHiddenSaleNumbers()
        {
            var forbidden = new[] { "Max", "Trust", "Sigma", "Draw", "Multiplier", "Opening", "Urgency" };
            foreach (var type in new[] { typeof(SaleScreenViewModel), typeof(SaleCustomerCardViewModel), typeof(SaleReplyViewModel) })
            {
                foreach (var p in type.GetProperties())
                {
                    foreach (string word in forbidden)
                    {
                        StringAssert.DoesNotContain(word, p.Name, type.Name);
                    }
                }
            }
        }

        [Test]
        public void SaleDialogue_IsPure_SameInputSameSentence()
        {
            Money price = Money.FromTl(5400);

            Assert.AreEqual(
                SaleDialogue.AfterAsk(NegotiationPhase.Active, price, false, NegotiationLevel.Medium, NegotiationLevel.High, NegotiationLevel.Medium),
                SaleDialogue.AfterAsk(NegotiationPhase.Active, price, false, NegotiationLevel.Medium, NegotiationLevel.High, NegotiationLevel.Medium));
            StringAssert.Contains("5.400 ₺", SaleDialogue.Deal(price));
            StringAssert.Contains("Son sözüm 5.400 ₺", SaleDialogue.AfterAsk(NegotiationPhase.FinalOffer, price, false, NegotiationLevel.Medium, NegotiationLevel.Low, NegotiationLevel.Medium));
        }
    }
}
