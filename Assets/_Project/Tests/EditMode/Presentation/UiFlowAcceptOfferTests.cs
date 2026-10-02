using System;
using System.Collections.Generic;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Business;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Game;
using Esnaf.Domain.Market;
using Esnaf.Domain.Negotiation;
using Esnaf.Presentation;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Presentation
{
    /// <summary>
    /// "Teklifi Kabul Et" akışı: mevcut pazarlık ekranı AYNEN korunur (fiyat seçici, "… olur abi", "Olmadı abi"); müşteri teklif verdikten sonra buna EK olarak
    /// "Teklifi Kabul Et" gelir (ayrı "pazarlığa devam" düğmesi yok). Kabul, müşterinin teklif fiyatıyla normal satış akışından tamamlanır; talep varsa aksesuar paneli,
    /// yoksa sonuç ekranı gelir. Teklif öncesinde "Teklifi Kabul Et" görünmez.
    /// </summary>
    public class UiFlowAcceptOfferTests
    {
        private static readonly Dictionary<string, KeyValuePair<ulong, int>> Cache = new Dictionary<string, KeyValuePair<ulong, int>>();

        /// <summary>Müşteri satışını UI akışıyla yüksek fiyat isteyerek müşterinin SON TEKLİFİ aşamasına getirir; gelinemezse null.</summary>
        private static UiFlow ToFinalOffer(ulong seed, int bumps, out GameSession s)
        {
            s = AddOnDeals.Prepare(seed, bumps);
            MarketListing guided = s.Market.Listings.Single(l => l.IsGuided);
            if (!s.Api.StartNegotiation(guided.ListingId).IsSuccess
                || !s.Api.MakeOffer(Money.FromTl(5800)).IsSuccess
                || !s.Api.SetPrice(guided.InstanceId, Money.FromTl(5900)).IsSuccess
                || s.Api.GetCustomers().Count == 0)
            {
                return null;
            }

            var flow = new UiFlow(s.Api, new ContentPresentation(s.Content), s.Bus);
            if (!flow.OpenCustomers() || !flow.StartSale(s.Api.GetCustomers()[0].CustomerId).IsSuccess || !flow.SaleGreet())
            {
                flow.Dispose();
                return null;
            }

            flow.AdjustSalePrice(3000);
            for (int i = 0; i < 8 && s.Api.GetSale() != null && s.Api.GetSale().Phase == NegotiationPhase.Active; i++)
            {
                if (flow.SaleAsk().IsFailure)
                {
                    break;
                }
            }

            SaleView view = s.Api.GetSale();
            if (view != null && view.Phase == NegotiationPhase.FinalOffer)
            {
                return flow;
            }

            flow.Dispose();
            return null;
        }

        /// <summary>Son teklif aşamasına gelen ve kabulünden sonra talebi <paramref name="wantsRequest"/> olan bir satış bulur.</summary>
        private static UiFlow FinalOfferWhere(string key, bool wantsRequest, out GameSession s)
        {
            KeyValuePair<ulong, int> known;
            if (Cache.TryGetValue(key, out known))
            {
                UiFlow cached = ToFinalOffer(known.Key, known.Value, out s);
                Assert.IsNotNull(cached);
                return cached;
            }

            for (int bumps = 0; bumps <= 60; bumps++)
            {
                for (ulong seed = 1; seed <= 20; seed++)
                {
                    GameSession probe;
                    UiFlow flow = ToFinalOffer(seed, bumps, out probe);
                    if (flow == null)
                    {
                        continue;
                    }

                    // Talebi öğrenmek için ayrı bir kopyada kabul et.
                    GameSession copy;
                    UiFlow other = ToFinalOffer(seed, bumps, out copy);
                    other.SaleAcceptFinal();
                    long id = copy.EconomyState.Ledger.Records.Last(r => r.TypeId == TransactionTypeIds.Sale).Id;
                    bool hasRequest = copy.AccessoryAddOns.RequestedAccessories(id).Count > 0;
                    other.Dispose();
                    if (hasRequest == wantsRequest)
                    {
                        Cache[key] = new KeyValuePair<ulong, int>(seed, bumps);
                        s = probe;
                        return flow;
                    }

                    flow.Dispose();
                }
            }

            throw new InvalidOperationException("No final-offer sale found for " + key);
        }

        /// <summary>Müşteriye bir kez çok yüksek fiyat istenir; müşteri "… olsa alırım" diye KARŞI TEKLİF verir (pazarlık hâlâ Active, tur 1). Olmazsa null.</summary>
        private static UiFlow ToCounterOffer(ulong seed, int bumps, out GameSession s)
        {
            s = AddOnDeals.Prepare(seed, bumps);
            MarketListing guided = s.Market.Listings.Single(l => l.IsGuided);
            if (!s.Api.StartNegotiation(guided.ListingId).IsSuccess
                || !s.Api.MakeOffer(Money.FromTl(5800)).IsSuccess
                || !s.Api.SetPrice(guided.InstanceId, Money.FromTl(5900)).IsSuccess
                || s.Api.GetCustomers().Count == 0)
            {
                return null;
            }

            var flow = new UiFlow(s.Api, new ContentPresentation(s.Content), s.Bus);
            if (!flow.OpenCustomers() || !flow.StartSale(s.Api.GetCustomers()[0].CustomerId).IsSuccess || !flow.SaleGreet())
            {
                flow.Dispose();
                return null;
            }

            flow.AdjustSalePrice(30000);
            SaleView view = flow.SaleAsk().IsSuccess ? s.Api.GetSale() : null;
            if (view != null && view.Phase == NegotiationPhase.Active && view.Round == 1)
            {
                return flow;
            }

            flow.Dispose();
            return null;
        }

        private static UiFlow CounterOffer(out GameSession s)
        {
            for (ulong seed = 1; seed <= 30; seed++)
            {
                UiFlow flow = ToCounterOffer(seed, 0, out s);
                if (flow != null)
                {
                    return flow;
                }
            }

            throw new InvalidOperationException("No seed gives a customer counter-offer.");
        }

        [Test]
        public void AfterACounterOffer_TheNegotiationScreenStaysAsItWas_AndAcceptOfferIsAddedToIt()
        {
            GameSession s;
            using (UiFlow flow = CounterOffer(out s))
            {
                SaleScreenViewModel screen = flow.SaleScreen;
                SaleView sale = s.Api.GetSale();
                SaleReplyKind[] kinds = screen.Replies.Select(r => r.Kind).ToArray();

                Assert.AreEqual(NegotiationPhase.Active, sale.Phase, "karşı teklif: pazarlık Active");
                Assert.AreEqual(SaleMode.Talking, screen.Mode);
                Assert.IsTrue(screen.ShowsPriceStepper, "fiyat seçici kaybolmaz");
                Assert.Contains(SaleReplyKind.Ask, kinds, "\"… olur abi\" kalır");
                Assert.Contains(SaleReplyKind.LetGo, kinds, "\"Olmadı abi, başka sefere\" kalır");
                Assert.Contains(SaleReplyKind.AcceptFinal, kinds, "Teklifi Kabul Et eklenir");
                Assert.AreEqual("Teklifi Kabul Et", screen.Replies.Single(r => r.Kind == SaleReplyKind.AcceptFinal).Text);
                Assert.AreEqual(SaleReplyKind.Ask, screen.Replies[0].Kind, "ilk ve vurgulu cevap hâlâ fiyat");
                Assert.IsTrue(screen.Replies[0].IsPrimary);
                Assert.IsFalse(screen.Replies.Any(r => r.Text.ToLowerInvariant().Contains("devam")), "ayrı pazarlığa devam düğmesi yok");
                Assert.AreEqual(1, kinds.Count(k => k == SaleReplyKind.AcceptFinal));
                StringAssert.Contains(MoneyFormatter.Format(sale.ShownPrice), screen.CustomerLine, "müşteri teklifini söylüyor");
            }
        }

        [Test]
        public void AfterACounterOffer_AskingAnotherPrice_ContinuesTheNegotiation_AsBefore()
        {
            GameSession s;
            using (UiFlow flow = CounterOffer(out s))
            {
                Money offer = s.Api.GetSale().ShownPrice;
                flow.AdjustSalePrice(-100000);
                flow.AdjustSalePrice((int)(offer.Tl + 5000));

                Result<SaleView> again = flow.SaleAsk();

                Assert.IsTrue(again.IsSuccess, again.ErrorCode);
                Assert.IsTrue(again.Value.Round == 2 || again.Value.Phase == NegotiationPhase.Deal, "pazarlık sürdü ya da anlaşıldı");
                Assert.AreNotEqual(NegotiationPhase.Failed, again.Value.Phase);
            }
        }

        [Test]
        public void AfterACounterOffer_LettingTheCustomerGo_WorksAsBefore()
        {
            GameSession s;
            using (UiFlow flow = CounterOffer(out s))
            {
                int sales = s.EconomyState.Ledger.Records.Count(r => r.TypeId == TransactionTypeIds.Sale);

                Result<SaleView> result = flow.SaleLetGo();

                Assert.IsTrue(result.IsSuccess, result.ErrorCode);
                Assert.AreEqual(NegotiationPhase.Failed, result.Value.Phase);
                Assert.AreEqual(sales, s.EconomyState.Ledger.Records.Count(r => r.TypeId == TransactionTypeIds.Sale), "satış olmadı");
                Assert.AreEqual(1, s.Api.GetInventory().Count, "ürün rafta kaldı");
                Assert.IsNull(flow.SaleScreen.AddOn);
            }
        }

        [Test]
        public void AcceptingTheCounterOffer_UsesExactlyTheCustomersShownPrice_AndTheNormalLedger()
        {
            GameSession s;
            using (UiFlow flow = CounterOffer(out s))
            {
                Money offer = s.Api.GetSale().ShownPrice;
                Money cash = s.Api.GetCash();

                Result<SaleView> result = flow.SaleAcceptFinal();

                Assert.IsTrue(result.IsSuccess, result.ErrorCode);
                Assert.AreEqual(NegotiationPhase.Deal, result.Value.Phase);
                Assert.AreEqual(offer, result.Value.DealPrice, "müşterinin teklif ettiği fiyat aynen");
                Assert.AreEqual(cash + offer, s.Api.GetCash());
                TransactionRecord row = s.EconomyState.Ledger.Records.Last(r => r.TypeId == TransactionTypeIds.Sale);
                Assert.AreEqual(offer, row.Amount);
                Assert.AreEqual(1, s.EconomyState.Ledger.Records.Count(r => r.TypeId == TransactionTypeIds.Sale));
                Assert.AreEqual(offer - row.SaleCostBasis.Value, s.AccessoryAddOns.TotalProfitOfSale(row.Id).Value, "kâr = teklif − maliyet");
                Assert.AreEqual(SaleMode.Done, flow.SaleScreen.Mode);
            }
        }

        [Test]
        public void BeforeTheFirstAsk_TheCustomerHasMadeNoOffer_SoNothingCanBeAccepted_AndTheNormalRepliesStay()
        {
            GameSession s = AddOnDeals.Prepare(1UL, 0);
            MarketListing guided = s.Market.Listings.Single(l => l.IsGuided);
            s.Api.StartNegotiation(guided.ListingId);
            s.Api.MakeOffer(Money.FromTl(5800));
            s.Api.SetPrice(guided.InstanceId, Money.FromTl(5900));
            using (var flow = new UiFlow(s.Api, new ContentPresentation(s.Content), s.Bus))
            {
                flow.OpenCustomers();
                flow.StartSale(s.Api.GetCustomers()[0].CustomerId);
                flow.SaleGreet();
                string digest = s.Api.GetStateDigest();

                Assert.AreEqual("negotiation.no_offer", s.Api.AcceptCustomerOffer().ErrorCode);
                Assert.AreEqual(digest, s.Api.GetStateDigest());
                Assert.IsTrue(flow.SaleScreen.ShowsPriceStepper);
                CollectionAssert.AreEqual(new[] { SaleReplyKind.Ask, SaleReplyKind.LetGo }, flow.SaleScreen.Replies.Select(r => r.Kind).ToArray(), "teklif öncesi Teklifi Kabul Et yok");
            }
        }

        [Test]
        public void TheOfferAcceptance_UsesNoRandomness()
        {
            GameSession s;
            using (UiFlow flow = CounterOffer(out s))
            {
                var rng = s.Capture().Rng;

                flow.SaleAcceptFinal();

                Assert.IsNull(DeepCompare.FirstDifference(rng, s.Capture().Rng));
            }
        }

        [Test]
        public void OnTheFinalOffer_TheExistingScreenIsKept_WithAcceptOfferAndLetGo()
        {
            GameSession s;
            using (UiFlow flow = FinalOfferWhere("any-no", false, out s))
            {
                SaleScreenViewModel screen = flow.SaleScreen;

                Assert.AreEqual(SaleMode.Talking, screen.Mode);
                Assert.AreEqual(NegotiationPhase.FinalOffer, s.Api.GetSale().Phase);
                CollectionAssert.AreEqual(new[] { SaleReplyKind.AcceptFinal, SaleReplyKind.LetGo }, screen.Replies.Select(r => r.Kind).ToArray(), "mevcut son teklif ekranı: kabul + ayrılma");
                Assert.AreEqual("Teklifi Kabul Et", screen.Replies[0].Text);
                Assert.IsTrue(screen.Replies[0].IsPrimary);
                Assert.AreEqual(1, screen.Replies.Count(r => r.Kind == SaleReplyKind.AcceptFinal));
                Assert.IsFalse(screen.ShowsPriceStepper, "oyun son teklifte fiyat istemeyi reddeder; ekran eskisi gibi");
                Assert.IsFalse(screen.Replies.Any(r => r.Text.ToLowerInvariant().Contains("devam")), "ayrı pazarlığa devam düğmesi yok");
            }
        }

        [Test]
        public void AcceptingTheOffer_CompletesTheSale_AtExactlyTheCustomersOfferPrice_ThroughTheNormalLedger()
        {
            GameSession s;
            using (UiFlow flow = FinalOfferWhere("any-no", false, out s))
            {
                Money offer = s.Api.GetSale().ShownPrice;
                Money cash = s.Api.GetCash();
                int sales = s.EconomyState.Ledger.Records.Count(r => r.TypeId == TransactionTypeIds.Sale);

                Result<SaleView> result = flow.SaleAcceptFinal();

                Assert.IsTrue(result.IsSuccess, result.ErrorCode);
                Assert.AreEqual(NegotiationPhase.Deal, result.Value.Phase);
                Assert.AreEqual(offer, result.Value.DealPrice, "müşterinin teklifi aynen");
                Assert.AreEqual(cash + offer, s.Api.GetCash());
                Assert.AreEqual(sales + 1, s.EconomyState.Ledger.Records.Count(r => r.TypeId == TransactionTypeIds.Sale), "tek satış satırı");
                TransactionRecord row = s.EconomyState.Ledger.Records.Last(r => r.TypeId == TransactionTypeIds.Sale);
                Assert.AreEqual(offer, row.Amount);
                Assert.AreEqual(0, s.Api.GetInventory().Count, "telefon satıldı");
            }
        }

        [Test]
        public void TheProfit_IsTheOfferMinusTheRealCost()
        {
            GameSession s;
            using (UiFlow flow = FinalOfferWhere("any-no", false, out s))
            {
                Money offer = s.Api.GetSale().ShownPrice;

                flow.SaleAcceptFinal();

                TransactionRecord row = s.EconomyState.Ledger.Records.Last(r => r.TypeId == TransactionTypeIds.Sale);
                Money expected = offer - row.SaleCostBasis.Value;
                Assert.AreEqual(expected, s.AccessoryAddOns.TotalProfitOfSale(row.Id).Value, "telefon kârı = teklif − maliyet");
                Assert.AreEqual(expected, s.Api.GetTodaySummary().GrossProfit);
            }
        }

        [Test]
        public void IfTheCustomerAsksForAccessories_TheAddOnFlowOpensAfterAccepting()
        {
            GameSession s;
            using (UiFlow flow = FinalOfferWhere("with-request", true, out s))
            {
                flow.SaleAcceptFinal();

                Assert.AreEqual(SaleMode.Done, flow.SaleScreen.Mode);
                Assert.IsNotNull(flow.SaleScreen.AddOn, "talep var → aksesuar paneli");
                Assert.AreEqual(s.Api.GetAccessoryAddOns().PhoneSalePrice, flow.SaleScreen.AddOn.PhonePrice);
            }
        }

        [Test]
        public void IfThereIsNoRequest_AcceptingGoesStraightToTheResultScreen()
        {
            GameSession s;
            using (UiFlow flow = FinalOfferWhere("no-request", false, out s))
            {
                flow.SaleAcceptFinal();

                Assert.AreEqual(SaleMode.Done, flow.SaleScreen.Mode);
                Assert.IsNull(flow.SaleScreen.AddOn);
                CollectionAssert.AreEqual(new[] { SaleReplyKind.Continue }, flow.SaleScreen.Replies.Select(r => r.Kind).ToArray());
                Assert.AreEqual(TurkishTexts.SaleDoneButton, flow.SaleScreen.Replies[0].Text);
            }
        }

        [Test]
        public void TheActivePhase_StillOffersTheNormalReplies_AndLetGo()
        {
            GameSession s = AddOnDeals.Prepare(1UL, 0);
            MarketListing guided = s.Market.Listings.Single(l => l.IsGuided);
            s.Api.StartNegotiation(guided.ListingId);
            s.Api.MakeOffer(Money.FromTl(5800));
            s.Api.SetPrice(guided.InstanceId, Money.FromTl(5900));
            using (var flow = new UiFlow(s.Api, new ContentPresentation(s.Content), s.Bus))
            {
                flow.OpenCustomers();
                flow.StartSale(s.Api.GetCustomers()[0].CustomerId);
                flow.SaleGreet();

                CollectionAssert.AreEqual(new[] { SaleReplyKind.Ask, SaleReplyKind.LetGo }, flow.SaleScreen.Replies.Select(r => r.Kind).ToArray());
            }
        }
    }
}
