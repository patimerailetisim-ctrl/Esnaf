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
    /// Unity-only (Test Runner > EditMode): mevcut pazarlık ekranı KORUNUR (fiyat seçici, "… olur abi", "Olmadı abi"); müşteri teklif verdikten sonra buna EK olarak
    /// "Teklifi Kabul Et" gelir (tırnaksız); teklif öncesi görünmez; tıklayınca müşterinin teklif fiyatıyla satış tamamlanır.
    /// </summary>
    public class SaleAcceptOfferPanelTests
    {
        private GameObject _canvasObject;
        private ContentDatabase _content;
        private GameSession _session;
        private UiFlow _flow;
        private SalePanelView _sale;

        private const int BeforeAsk = 0;      // müşteri henüz teklif vermedi (selamlaşıldı, fiyat seçici açık)
        private const int CounterOffer = 1;   // BİR yüksek fiyat istendi; müşteri "… olsa alırım" der (Active, tur 1)
        private const int FinalOffer = 2;     // sabır bitene kadar; müşteri son teklifini verdi

        // Satışı istenen aşamaya getirir.
        private void Build(int stage)
        {
            string dir = Path.Combine(Application.dataPath, "_Project", "Content", "Data");
            ContentLoadResult loaded = ContentDatabase.Load(new DirectoryContentSource(dir));
            Assert.IsTrue(loaded.IsSuccess, loaded.FormatIssues());
            _content = loaded.Database;

            // NUnit tüm testler için TEK fixture örneği kullanır: alanlar önceki testten kalmamalı (kalırsa döngü hiç çalışmaz ve silinmiş nesneler kullanılır).
            _session = null;
            _flow = null;
            _sale = null;
            _canvasObject = null;

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
                for (int i = 0; i < (stage == FinalOffer ? 8 : stage == CounterOffer ? 1 : 0) && s.Api.GetSale() != null && s.Api.GetSale().Phase == NegotiationPhase.Active; i++)
                {
                    flow.SaleAsk();
                }

                SaleView reached = s.Api.GetSale();
                bool ok = reached != null && (stage == FinalOffer
                    ? reached.Phase == NegotiationPhase.FinalOffer
                    : reached.Phase == NegotiationPhase.Active && reached.Round == (stage == CounterOffer ? 1 : 0));
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

            _session = null;
            _flow = null;
            _sale = null;
            _canvasObject = null;
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
        public void OnTheCustomersFinalOffer_TheExistingScreenIsKept_AcceptOfferAndLetGo_WithoutQuotes()
        {
            Build(FinalOffer);

            Assert.IsNotNull(Find("Reply_" + SaleReplyKind.AcceptFinal));
            Assert.AreEqual("Teklifi Kabul Et", Find("Reply_" + SaleReplyKind.AcceptFinal).GetComponentInChildren<Text>().text);
            Assert.IsNotNull(Find("Reply_" + SaleReplyKind.LetGo), "\"Olmadı abi, başka sefere\" kalır");
            Assert.AreEqual(2, CountNamed("Reply_"));
            Assert.IsNull(Find("Reply_" + SaleReplyKind.Ask), "oyun son teklifte fiyat istemeyi reddeder; ekran eskisi gibi");
            Assert.AreEqual(0, CountNamed("Step_"));
        }

        [Test]
        public void ClickingAcceptOffer_CompletesTheSale_AtTheCustomersOfferPrice()
        {
            Build(FinalOffer);

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

        // Play Mode'da yakalanan senaryo: müşteri "… olsa alırım" (karşı teklif, pazarlık Active) → mevcut ekran + "Teklifi Kabul Et"
        [Test]
        public void OnTheCustomersCounterOffer_TheNegotiationScreenStays_AndAcceptOfferIsAdded()
        {
            Build(CounterOffer);

            Assert.IsNotNull(Find("Stepper"), "fiyat seçici kaybolmaz");
            Assert.Greater(CountNamed("Step_"), 0);
            Assert.IsNotNull(Find("Reply_" + SaleReplyKind.Ask), "\"… olur abi\" kalır");
            Assert.IsNotNull(Find("Reply_" + SaleReplyKind.LetGo), "\"Olmadı abi, başka sefere\" kalır");
            Assert.IsNotNull(Find("Reply_" + SaleReplyKind.AcceptFinal), "Teklifi Kabul Et eklenir");
            Assert.AreEqual("Teklifi Kabul Et", Find("Reply_" + SaleReplyKind.AcceptFinal).GetComponentInChildren<Text>().text);
            Assert.AreEqual(3, CountNamed("Reply_"), "ayrı bir pazarlığa devam düğmesi yok");
        }

        [Test]
        public void BeforeTheCustomersFirstOffer_AcceptOfferIsNotShown()
        {
            Build(BeforeAsk);

            Assert.IsNull(Find("Reply_" + SaleReplyKind.AcceptFinal));
            Assert.IsNotNull(Find("Reply_" + SaleReplyKind.LetGo));
        }

        [Test]
        public void ClickingTheNormalAskAfterACounterOffer_ContinuesTheNegotiation()
        {
            Build(CounterOffer);
            Money offer = _session.Api.GetSale().ShownPrice;
            _flow.AdjustSalePrice(-100000);
            _flow.AdjustSalePrice((int)(offer.Tl + 5000));

            Find("Reply_" + SaleReplyKind.Ask).GetComponent<Button>().onClick.Invoke();

            SaleView after = _session.Api.GetSale();
            Assert.IsTrue(after == null ? _flow.SaleScreen.Mode == SaleMode.Done : after.Round == 2, "pazarlık sürdü ya da anlaşıldı");
        }

        [Test]
        public void ClickingLetGoAfterACounterOffer_EndsTheSaleAsBefore()
        {
            Build(CounterOffer);
            int sales = _session.EconomyState.Ledger.Records.Count(r => r.TypeId == TransactionTypeIds.Sale);

            Find("Reply_" + SaleReplyKind.LetGo).GetComponent<Button>().onClick.Invoke();

            Assert.IsNull(_session.Api.GetSale());
            Assert.AreEqual(sales, _session.EconomyState.Ledger.Records.Count(r => r.TypeId == TransactionTypeIds.Sale));
            Assert.AreEqual(1, _session.Api.GetInventory().Count, "ürün rafta kaldı");
        }

        [Test]
        public void ClickingAcceptOnTheCounterOffer_SellsAtExactlyTheCustomersOffer()
        {
            Build(CounterOffer);
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
