using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Esnaf.App.Ui;
using Esnaf.Core;
using Esnaf.Domain.Business;
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
    /// Unity-only (Test Runner > EditMode): müşteri çeşitliliği (Direct Accept). Hoşgörülü bir müşteri (Selin) istenen fiyatı pazarlıksız kabul eder: satış istenen fiyattan
    /// tamamlanır, talep varsa aksesuar paneli, yoksa yalnızca sonuç ekranı gelir. Pazarlıkçı müşteri (Kemal) aynı göreli fiyatta pazarlığı sürdürür ("Teklifi Kabul Et"
    /// karşı teklifle birlikte). Kural domain'dedir (DirectAcceptPolicy); bu testler gerçek UI nesneleriyle akışı doğrular. Fiyat varsayılanına ve eşiklere dokunulmaz.
    /// </summary>
    public class SaleDirectAcceptPanelTests
    {
        private GameObject _canvasObject;
        private ContentDatabase _content;
        private GameSession _session;
        private UiFlow _flow;
        private SalePanelView _sale;
        private CustomerView _customer;
        private long _ask;

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
            _customer = null;
        }

        private GameSession Prepare(ulong seed, int bumps)
        {
            GameSession s = GameSession.NewGame(_content, seed);
            for (int i = 0; i < bumps; i++)
            {
                Assert.IsTrue(s.EconomyService.RecordInvestment(Money.FromTl(10), s.Time.Day).IsSuccess);
            }

            return s;
        }

        // Etiketli ürünle hazır oturum; verilen NPC müşteri olarak yoksa null.
        private GameSession SessionWith(string npcId, ulong seed, int bumps, out CustomerView customer)
        {
            customer = null;
            GameSession s = Prepare(seed, bumps);
            MarketListing guided = s.Market.Listings.Single(l => l.IsGuided);
            if (!s.Api.StartNegotiation(guided.ListingId).IsSuccess
                || !s.Api.MakeOffer(Money.FromTl(5800)).IsSuccess
                || !s.Api.SetPrice(guided.InstanceId, Money.FromTl(5900)).IsSuccess)
            {
                return null;
            }

            customer = s.Api.GetCustomers().FirstOrDefault(v => v.NpcId == "npc." + npcId);
            return customer == null ? null : s;
        }

        // M'nin %98'i (10 ₺'ye aşağı yuvarlı): hoşgörülü müşterinin tavanının altında, ilk tur teklifinin üstünde (domain testleriyle aynı seçim).
        private static long AskNear(GameSession s, CustomerView customer)
        {
            CustomerSlot slot;
            Assert.IsTrue(s.Customers.TryGetWaiting(customer.CustomerId, out slot));
            double max = s.Customers.MaxFor(slot, s.Store.Get(customer.InstanceId), false);
            return (long)Math.Floor(max * 0.98 / 10.0) * 10L;
        }

        // Talebi öğrenmek için API ile (UI'sız) aynı satışı yapar.
        private static IReadOnlyList<string> RequestAfterDirectAccept(GameSession s, CustomerView customer, long ask)
        {
            if (!s.Api.StartSale(customer.CustomerId).IsSuccess)
            {
                return null;
            }

            Result<SaleView> deal = s.Api.AskPrice(Money.FromTl(ask));
            if (deal.IsFailure || deal.Value.Phase != NegotiationPhase.Deal)
            {
                return null;
            }

            long id = s.EconomyState.Ledger.Records.Last(r => r.TypeId == TransactionTypeIds.Sale).Id;
            return s.AccessoryAddOns.RequestedAccessories(id);
        }

        /// <summary>
        /// NPC'nin müşteri olduğu bir satışı UI'da hazırlar (selam verilmiş, fiyat seçici AskNear fiyatında). <paramref name="wantsRequest"/> null değilse direkt kabul sonrası
        /// aksesuar talebinin varlığı da koşuldur (yalnızca direkt kabul eden NPC'ler için anlamlı).
        /// </summary>
        private void Build(string npcId, bool? wantsRequest)
        {
            string dir = Path.Combine(Application.dataPath, "_Project", "Content", "Data");
            ContentLoadResult loaded = ContentDatabase.Load(new DirectoryContentSource(dir));
            Assert.IsTrue(loaded.IsSuccess, loaded.FormatIssues());
            _content = loaded.Database;
            _session = null;
            _flow = null;
            _sale = null;
            _canvasObject = null;

            ulong foundSeed = 0;
            int foundBumps = -1;
            for (int bumps = 0; bumps <= 60 && foundBumps < 0; bumps++)
            {
                for (ulong seed = 1; seed <= 30 && foundBumps < 0; seed++)
                {
                    CustomerView probeCustomer;
                    GameSession probe = SessionWith(npcId, seed, bumps, out probeCustomer);
                    if (probe == null)
                    {
                        continue;
                    }

                    if (wantsRequest == null)
                    {
                        foundSeed = seed;
                        foundBumps = bumps;
                        continue;
                    }

                    IReadOnlyList<string> request = RequestAfterDirectAccept(probe, probeCustomer, AskNear(probe, probeCustomer));
                    if (request != null && (request.Count > 0) == wantsRequest.Value)
                    {
                        foundSeed = seed;
                        foundBumps = bumps;
                    }
                }
            }

            Assert.GreaterOrEqual(foundBumps, 0, "İstenen müşteri/talep koşuluna sahip bir satış bulunamadı.");

            _session = SessionWith(npcId, foundSeed, foundBumps, out _customer);
            _ask = AskNear(_session, _customer);
            _flow = new UiFlow(_session.Api, new ContentPresentation(_content), _session.Bus);
            Canvas canvas = UiBuilder.CreateCanvas("TestCanvas");
            _canvasObject = canvas.gameObject;
            _sale = new SalePanelView(canvas.transform, _flow);
            _flow.Changed += _sale.Show;
            _sale.Show();

            Assert.IsTrue(_flow.OpenCustomers());
            Assert.IsTrue(_flow.StartSale(_customer.CustomerId).IsSuccess);
            Assert.IsTrue(_flow.SaleGreet());
            _flow.AdjustSalePrice(-100000);
            _flow.AdjustSalePrice((int)_ask - 10); // -100000 ile 10 ₺ minimuma inildi; fiyat seçici = _ask
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

        private void Click(string name)
        {
            Find(name).GetComponent<Button>().onClick.Invoke();
        }

        private int SaleRows()
        {
            return _session.EconomyState.Ledger.Records.Count(r => r.TypeId == TransactionTypeIds.Sale);
        }

        [Test]
        public void ADirectlyAcceptingCustomer_BuysAtTheAskedPrice_WhenTheAskButtonIsPressed()
        {
            Build("selin", null);
            Money cash = _session.Api.GetCash();

            Click("Reply_" + SaleReplyKind.Ask);

            Assert.IsNull(_session.Api.GetSale(), "pazarlık sürmedi: satış tamamlandı");
            Assert.AreEqual(SaleMode.Done, _flow.SaleScreen.Mode);
            Assert.AreEqual(1, SaleRows(), "tek satış satırı");
            TransactionRecord row = _session.EconomyState.Ledger.Records.Last(r => r.TypeId == TransactionTypeIds.Sale);
            Assert.AreEqual(Money.FromTl(_ask), row.Amount, "satış fiyatı = istenen fiyat");
            Assert.AreEqual(cash + Money.FromTl(_ask), _session.Api.GetCash());
            Assert.AreEqual(0, _session.Api.GetInventory().Count);
            Assert.IsNull(Find("Stepper"), "satış bitti: fiyat seçici yok");
            Assert.IsNotNull(Find("Reply_" + SaleReplyKind.Continue));
        }

        [Test]
        public void AfterADirectAccept_TheAccessoryPanelOpens_WhenTheCustomerAskedForAccessories()
        {
            Build("selin", true);

            Click("Reply_" + SaleReplyKind.Ask);

            Assert.IsNotNull(Find("AddOnPanel"), "talep var → aksesuar paneli");
            Assert.IsNotNull(Find("AddOnRequestLine"));
            Assert.AreEqual(Money.FromTl(_ask), _flow.SaleScreen.AddOn.PhonePrice, "telefon fiyatı doğrudan kabul edilen fiyat");
            Assert.AreEqual(1, SaleRows());
        }

        [Test]
        public void AfterADirectAccept_OnlyTheResultScreenShows_WhenTheCustomerAskedForNothing()
        {
            Build("selin", false);

            Click("Reply_" + SaleReplyKind.Ask);

            Assert.IsNull(Find("AddOnPanel"), "talep yok → aksesuar paneli yok");
            Assert.AreEqual(SaleMode.Done, _flow.SaleScreen.Mode);
            Assert.AreEqual(TurkishTexts.SaleDoneButton, Find("Reply_" + SaleReplyKind.Continue).GetComponentInChildren<Text>().text);
            Assert.AreEqual(0, _session.EconomyState.Ledger.Records.Count(r => r.TypeId == TransactionTypeIds.AccessorySale));
        }

        [Test]
        public void ABargainingCustomer_AtTheSameRelativePrice_KeepsNegotiating_WithAcceptOfferAlongsideTheUsualButtons()
        {
            Build("kemal", null);

            Click("Reply_" + SaleReplyKind.Ask);

            Assert.IsNotNull(_session.Api.GetSale(), "pazarlıkçı müşteri direkt kabul etmedi");
            Assert.AreEqual(0, SaleRows());
            Assert.IsNotNull(Find("Stepper"), "mevcut pazarlık ekranı");
            Assert.IsNotNull(Find("Reply_" + SaleReplyKind.Ask));
            Assert.IsNotNull(Find("Reply_" + SaleReplyKind.LetGo));
            Assert.IsNotNull(Find("Reply_" + SaleReplyKind.AcceptFinal), "karşı teklifle birlikte Teklifi Kabul Et");
            Assert.AreEqual("Teklifi Kabul Et", Find("Reply_" + SaleReplyKind.AcceptFinal).GetComponentInChildren<Text>().text);
        }

        [Test]
        public void ABargainingCustomersCounterOffer_CanStillBeAccepted_AtTheCustomersOwnPrice()
        {
            Build("kemal", null);
            Click("Reply_" + SaleReplyKind.Ask);
            Money offer = _session.Api.GetSale().ShownPrice;

            Click("Reply_" + SaleReplyKind.AcceptFinal);

            Assert.IsNull(_session.Api.GetSale());
            Assert.AreEqual(offer, _session.EconomyState.Ledger.Records.Last(r => r.TypeId == TransactionTypeIds.Sale).Amount, "Teklifi Kabul Et: müşterinin teklif fiyatı");
            Assert.Less(offer.Tl, _ask, "müşterinin teklifi istenen fiyattan düşüktü");
        }
    }
}
