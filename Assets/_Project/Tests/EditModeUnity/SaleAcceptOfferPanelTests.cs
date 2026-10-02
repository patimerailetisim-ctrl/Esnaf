using System;
using System.IO;
using System.Linq;
using Esnaf.App.Ui;
using Esnaf.Core;
using Esnaf.Domain.Content;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Game;
using Esnaf.Domain.Market;
using Esnaf.Domain.Negotiation;
using Esnaf.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Esnaf.App.Tests
{
    /// <summary>
    /// Unity-only (Test Runner > EditMode): müşteri son teklifini verince satış panelinde YALNIZCA "Teklifi Kabul Et" düğmesi vardır (pazarlığa devam / ayrılma / fiyat
    /// seçici yok); tırnaksız yazılır; tıklayınca müşterinin teklif fiyatıyla satış tamamlanır.
    /// </summary>
    public class SaleAcceptOfferPanelTests
    {
        private GameObject _canvasObject;
        private ContentDatabase _content;
        private GameSession _session;
        private UiFlow _flow;
        private SalePanelView _sale;

        // Satışı istenen aşamaya getirir: finalOffer ise sabır bitene kadar, değilse yalnızca BİR yüksek fiyat istenir (müşteri "… olsa alırım" der: Active, tur 1).
        private void Build(bool finalOffer)
        {
            string dir = Path.Combine(Application.dataPath, "_Project", "Content", "Data");
            ContentLoadResult loaded = ContentDatabase.Load(new DirectoryContentSource(dir));
            Assert.IsTrue(loaded.IsSuccess, loaded.FormatIssues());
            _content = loaded.Database;

            for (ulong seed = 1; seed <= 40 && _flow == null; seed++)
            {
                GameSession s = GameSession.NewGame(_content, seed);
                MarketListing guided = s.Market.Listings.Single(l => l.IsGuided);
                if (!s.Api.StartNegotiation(guided.ListingId).IsSuccess
                    || !s.Api.MakeOffer(Money.FromTl(5800)).IsSuccess
                    || !s.Api.SetPrice(guided.InstanceId, Money.FromTl(5900)).IsSuccess
                    || s.Api.GetCustomers().Count == 0)
                {
                    continue;
                }

                var flow = new UiFlow(s.Api, new ContentPresentation(_content), s.Bus);
                Canvas canvas = UiBuilder.CreateCanvas("TestCanvas");
                var view = new SalePanelView(canvas.transform, flow);
                flow.Changed += view.Show;
                view.Show();
                flow.OpenCustomers();
                flow.StartSale(s.Api.GetCustomers()[0].CustomerId);
                flow.SaleGreet();
                flow.AdjustSalePrice(3000);
                for (int i = 0; i < (finalOffer ? 8 : 1) && s.Api.GetSale() != null && s.Api.GetSale().Phase == NegotiationPhase.Active; i++)
                {
                    flow.SaleAsk();
                }

                SaleView reached = s.Api.GetSale();
                bool ok = reached != null && (finalOffer ? reached.Phase == NegotiationPhase.FinalOffer : reached.Phase == NegotiationPhase.Active && reached.Round == 1);
                if (ok)
                {
                    _session = s;
                    _flow = flow;
                    _sale = view;
                    _canvasObject = canvas.gameObject;
                }
                else
                {
                    flow.Changed -= view.Show;
                    flow.Dispose();
                    UnityEngine.Object.DestroyImmediate(canvas.gameObject);
                }
            }

            Assert.IsNotNull(_flow, "İstenen aşamaya gelen bir müşteri satışı bulunamadı.");
        }

        [TearDown]
        public void TearDown()
        {
            if (_flow != null)
            {
                _flow.Changed -= _sale.Show;
                _flow.Dispose();
            }

            if (_canvasObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_canvasObject);
            }
        }

        private Transform Find(string name)
        {
            foreach (Transform t in _canvasObject.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                {
                    return t;
                }
            }

            return null;
        }

        private int CountNamed(string prefix)
        {
            return _canvasObject.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith(prefix, StringComparison.Ordinal));
        }

        [Test]
        public void OnTheCustomersFinalOffer_OnlyTheAcceptOfferButtonIsShown_WithoutQuotes()
        {
            Build(true);

            Assert.IsNotNull(Find("Reply_" + SaleReplyKind.AcceptFinal));
            Assert.AreEqual("Teklifi Kabul Et", Find("Reply_" + SaleReplyKind.AcceptFinal).GetComponentInChildren<Text>().text);
            Assert.AreEqual(1, CountNamed("Reply_"), "tek eylem düğmesi");
            Assert.IsNull(Find("Reply_" + SaleReplyKind.Ask), "pazarlığa devam yok");
            Assert.IsNull(Find("Reply_" + SaleReplyKind.LetGo));
            Assert.AreEqual(0, CountNamed("Step_"), "fiyat seçici düğmeleri yok");
            Assert.IsNull(Find("Stepper"));
        }

        [Test]
        public void ClickingAcceptOffer_CompletesTheSale_AtTheCustomersOfferPrice()
        {
            Build(true);

            Money offer = _session.Api.GetSale().ShownPrice;
            Money cash = _session.Api.GetCash();

            Find("Reply_" + SaleReplyKind.AcceptFinal).GetComponent<Button>().onClick.Invoke();

            Assert.IsNull(_session.Api.GetSale());
            Assert.AreEqual(SaleMode.Done, _flow.SaleScreen.Mode);
            Assert.AreEqual(cash + offer, _session.Api.GetCash());
            TransactionRecord row = _session.EconomyState.Ledger.Records.Last(r => r.TypeId == TransactionTypeIds.Sale);
            Assert.AreEqual(offer, row.Amount);
            Assert.AreEqual(1, _session.EconomyState.Ledger.Records.Count(r => r.TypeId == TransactionTypeIds.Sale));
            Assert.IsNotNull(Find("Reply_" + SaleReplyKind.Continue), "talep yoksa sonuç ekranı, varsa aksesuar paneli ve devam düğmesi");
        }

        // Play Mode'da yakalanan senaryo: müşteri "… olsa alırım" (karşı teklif, pazarlık Active) → yalnızca "Teklifi Kabul Et"
        [Test]
        public void OnTheCustomersCounterOffer_OnlyTheAcceptOfferButtonIsShown_NoStepperNoAskNoLetGo()
        {
            Build(false);

            Assert.IsNotNull(Find("Reply_" + SaleReplyKind.AcceptFinal));
            Assert.AreEqual("Teklifi Kabul Et", Find("Reply_" + SaleReplyKind.AcceptFinal).GetComponentInChildren<Text>().text);
            Assert.AreEqual(1, CountNamed("Reply_"));
            Assert.IsNull(Find("Reply_" + SaleReplyKind.Ask), "\"… olur abi\" yok");
            Assert.IsNull(Find("Reply_" + SaleReplyKind.LetGo), "\"Olmadı abi, başka sefere\" yok");
            Assert.AreEqual(0, CountNamed("Step_"));
            Assert.IsNull(Find("Stepper"), "fiyat seçici yok");
        }

        [Test]
        public void ClickingAcceptOnTheCounterOffer_SellsAtExactlyTheCustomersOffer()
        {
            Build(false);
            Money offer = _session.Api.GetSale().ShownPrice;
            Money cash = _session.Api.GetCash();

            Find("Reply_" + SaleReplyKind.AcceptFinal).GetComponent<Button>().onClick.Invoke();

            Assert.IsNull(_session.Api.GetSale());
            Assert.AreEqual(cash + offer, _session.Api.GetCash());
            Assert.AreEqual(offer, _session.EconomyState.Ledger.Records.Last(r => r.TypeId == TransactionTypeIds.Sale).Amount);
            Assert.AreEqual(SaleMode.Done, _flow.SaleScreen.Mode);
        }
    }
}
