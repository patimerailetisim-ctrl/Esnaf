using System;
using System.IO;
using System.Linq;
using Esnaf.App.Ui;
using Esnaf.Core;
using Esnaf.Domain.Business;
using Esnaf.Domain.Content;
using Esnaf.Domain.Game;
using Esnaf.Domain.Time;
using Esnaf.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Esnaf.App.Tests
{
    /// <summary>
    /// Unity-only (Test Runner > EditMode): günlük müşteri akışı arayüzü (Gün 12.5) gerçek UI nesneleriyle. Satış ekranı lobisi yalnızca kuyruğun şu an aktif müşterisini gösterir
    /// (eski 5-yuva kartları çizilmez); "Bekle" saati sıradaki geliş saatine ilerletir; ürünü olmayan müşteri için "Gönder"; üst çubukta saat; İlanlar düğmesinde durum yazısı.
    /// Her testin alanları baştan kurulur (fixture örneği testler arasında paylaşılır; TearDown alanları sıfırlar).
    /// </summary>
    public class SaleQueueLobbyPanelTests
    {
        private GameObject _canvasObject;
        private ContentDatabase _content;
        private GameSession _session;
        private UiFlow _flow;
        private SalePanelView _sale;
        private ListingsView _listings;
        private TopBarView _topBar;

        [TearDown]
        public void TearDown()
        {
            if (_flow != null)
            {
                _flow.Changed -= Refresh;
                _flow.Dispose();
            }

            if (_canvasObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_canvasObject);
            }

            _session = null;
            _flow = null;
            _sale = null;
            _listings = null;
            _topBar = null;
            _canvasObject = null;
        }

        private void Refresh()
        {
            _topBar.Show(_flow.TopBar);
            _listings.Show();
            _sale.Show();
        }

        private void LoadContent()
        {
            string dir = Path.Combine(Application.dataPath, "_Project", "Content", "Data");
            ContentLoadResult loaded = ContentDatabase.Load(new DirectoryContentSource(dir));
            Assert.IsTrue(loaded.IsSuccess, loaded.FormatIssues());
            _content = loaded.Database;
        }

        private GameSession WithShelf(ulong seed)
        {
            GameSession s = GameSession.NewGame(_content, seed);
            var guided = s.Market.Listings.Single(l => l.IsGuided);
            Assert.IsTrue(s.Api.StartNegotiation(guided.ListingId).IsSuccess);
            Assert.IsTrue(s.Api.MakeOffer(Money.FromTl(5800)).IsSuccess);
            Assert.IsTrue(s.Api.SetPrice(guided.InstanceId, Money.FromTl(5900)).IsSuccess);
            return s;
        }

        // Oturum: raflı (ilk müşterisi açılıştan sonra gelir ve ilgilendiği ürün vardır) ya da rafsız (ilk müşteri ilgilenecek ürün bulamaz).
        private void Build(bool shelf)
        {
            LoadContent();
            _session = null;
            for (ulong seed = 1; seed <= 400 && _session == null; seed++)
            {
                GameSession probe = shelf ? WithShelf(seed) : GameSession.NewGame(_content, seed);
                QueuedCustomer first = probe.CustomerQueue.PlanFor(probe.Time.Day)[0];
                if (first.ArrivalMinute <= StoreHours.OpenMinute)
                {
                    continue;
                }

                if (shelf)
                {
                    probe.Api.AdvanceTime(first.ArrivalMinute - probe.Api.GetClock().MinuteOfDay);
                    CustomerView v = probe.Api.GetActiveCustomer();
                    if (v == null || v.InstanceId == 0)
                    {
                        continue;
                    }
                }

                _session = shelf ? WithShelf(seed) : GameSession.NewGame(_content, seed);
            }

            Assert.IsNotNull(_session, "Uygun oturum bulunamadı.");
            Attach();
        }

        private void Attach()
        {
            _flow = new UiFlow(_session.Api, new ContentPresentation(_content), _session.Bus);
            Canvas canvas = UiBuilder.CreateCanvas("TestCanvas");
            _canvasObject = canvas.gameObject;
            _topBar = new TopBarView(canvas.transform);
            _listings = new ListingsView(canvas.transform, _flow);
            _sale = new SalePanelView(canvas.transform, _flow);
            _flow.Changed += Refresh;
            Refresh();
        }

        private Transform Find(string name)
        {
            foreach (Transform t in _canvasObject.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name && t.gameObject.activeInHierarchy)
                {
                    return t;
                }
            }

            return null;
        }

        private int CountNamed(string prefix)
        {
            return _canvasObject.GetComponentsInChildren<Transform>(false)
                .Count(t => t.name.StartsWith(prefix, StringComparison.Ordinal) && t.gameObject.activeInHierarchy);
        }

        private string TextOf(string name)
        {
            return Find(name).GetComponent<Text>().text;
        }

        private int Now()
        {
            return _session.Api.GetClock().MinuteOfDay;
        }

        [Test]
        public void TheTopBar_ShowsTheClock_AndFollowsIt()
        {
            Build(true);

            Assert.AreEqual("09:00", TextOf("ClockText"));
            Assert.IsTrue(_flow.OpenCustomers());
            Find("WaitButton").GetComponent<Button>().onClick.Invoke();

            Assert.AreEqual(StoreHours.Format(Now()), TextOf("ClockText"));
            StringAssert.Contains("Gün", TextOf("DayText"));
        }

        [Test]
        public void BeforeTheNextCustomer_TheLobbyShowsTheNextTimeAndAWaitButton_NoCustomerCard()
        {
            Build(true);
            QueuedCustomer first = _session.CustomerQueue.PlanFor(1)[0];

            _flow.OpenCustomers();

            Assert.IsNotNull(Find("QueueStatus"));
            Assert.AreEqual("09:00", TextOf("Clock"));
            Assert.AreEqual(TurkishTexts.NextCustomerLine(first.ArrivalText), TextOf("StatusLine"));
            Assert.IsNotNull(Find("WaitButton"));
            Assert.AreEqual("Bekle", Find("WaitButton").GetComponentInChildren<Text>().text);
            Assert.AreEqual(0, CountNamed("Customer_"), "geliş saati gelmeden müşteri kartı yok");
            Assert.IsNull(Find("DismissButton"));
        }

        [Test]
        public void ThePlayersMainButton_ShowsTheQueueState_NotTheOldSlotCount()
        {
            Build(true);
            QueuedCustomer first = _session.CustomerQueue.PlanFor(1)[0];

            Assert.AreEqual(TurkishTexts.NextCustomerButton(first.ArrivalText), Find("CustomersButton").GetComponentInChildren<Text>().text);
        }

        [Test]
        public void ClickingWait_MovesToTheArrivalTime_AndExactlyOneCustomerCardAppears()
        {
            Build(true);
            QueuedCustomer first = _session.CustomerQueue.PlanFor(1)[0];
            _flow.OpenCustomers();

            Find("WaitButton").GetComponent<Button>().onClick.Invoke();

            Assert.AreEqual(first.ArrivalMinute, Now(), "tam geliş saatine");
            Assert.IsNull(Find("WaitButton"), "müşteri geldi: Bekle yok");
            Assert.AreEqual(1, CountNamed("Customer_"), "aynı anda tek müşteri kartı");
            Assert.IsNotNull(Find("Customer_" + first.CustomerId));
            Assert.AreEqual(_flow.Content.CustomerName(first.CustomerId, first.NpcId), TextOf("Name"));
            Assert.IsNotNull(Find("Portrait"), "mevcut portre sistemi");
        }

        [Test]
        public void TheOldRosterCards_AreNotDrawnInTheMainLobby()
        {
            Build(true);
            long[] oldIds = _session.Api.GetCustomers().Select(c => c.CustomerId).ToArray();
            _flow.OpenCustomers();
            Find("WaitButton").GetComponent<Button>().onClick.Invoke();

            foreach (long id in oldIds)
            {
                Assert.IsNull(Find("Customer_" + id), "eski yuva kartı çizilmez: " + id);
            }

            Assert.AreEqual(_session.Api.GetCustomers().Count, _flow.SaleScreen.Customers.Count, "ama altyapı/görünüm modeli korunur");
        }

        [Test]
        public void TappingTheActiveCustomer_StartsTheExistingSaleScreen()
        {
            Build(true);
            _flow.OpenCustomers();
            Find("WaitButton").GetComponent<Button>().onClick.Invoke();
            long id = _flow.SaleScreen.Queue.Customer.CustomerId;

            Find("Customer_" + id).GetComponent<Button>().onClick.Invoke();

            Assert.AreEqual(SaleMode.Talking, _flow.SaleScreen.Mode);
            Assert.AreEqual(id, _session.Api.GetSale().CustomerId);
            Assert.IsNull(Find("QueueStatus"), "lobi gizlendi");
            Assert.IsNotNull(Find("Reply_" + SaleReplyKind.Greet), "mevcut konuşma ekranı");
            Assert.IsNull(Find("WaitButton"));
        }

        [Test]
        public void AfterTheSale_TheLobbyReturns_WithTheCustomerCompleted()
        {
            Build(true);
            _flow.OpenCustomers();
            Find("WaitButton").GetComponent<Button>().onClick.Invoke();
            Find("Customer_" + _flow.SaleScreen.Queue.Customer.CustomerId).GetComponent<Button>().onClick.Invoke();
            Find("Reply_" + SaleReplyKind.LetGo).GetComponent<Button>().onClick.Invoke();

            Find("Reply_" + SaleReplyKind.Continue).GetComponent<Button>().onClick.Invoke();

            Assert.IsNotNull(Find("QueueStatus"));
            StringAssert.Contains("1/", TextOf("Progress"), "müşteri tamamlandı");
            Assert.LessOrEqual(CountNamed("Customer_"), 1);
        }

        [Test]
        public void ACustomerWithNothingToBuy_ShowsADismissButton_AndNotAClickableCard()
        {
            Build(false);
            _flow.OpenCustomers();
            Find("WaitButton").GetComponent<Button>().onClick.Invoke();
            QueuedCustomer first = _session.CustomerQueue.PlanFor(1)[0];

            Assert.IsNotNull(Find("DismissButton"));
            Assert.IsNull(Find("WaitButton"));
            Transform card = Find("Customer_" + first.CustomerId);
            Assert.IsNotNull(card);
            Assert.IsNull(card.GetComponent<Button>(), "ürünü yok: kart satışa başlatmaz");
            int t = Now();

            Find("DismissButton").GetComponent<Button>().onClick.Invoke();

            Assert.AreEqual(t + InteractionTime.CompleteCustomer, Now());
            Assert.IsNull(Find("DismissButton"));
            StringAssert.Contains("1/", TextOf("Progress"));
        }

        [Test]
        public void WhenTheStoreIsClosed_NoCustomerCardAndNoWaitButtonAreShown()
        {
            Build(true);
            _session.Api.AdvanceTime(5000);
            _flow.Refresh();
            _flow.OpenCustomers();

            Assert.AreEqual(0, CountNamed("Customer_"));
            Assert.IsNull(Find("WaitButton"));
            Assert.AreEqual(TurkishTexts.StoreClosedLine, TextOf("StatusLine"));
            Assert.AreEqual("21:00", TextOf("ClockText"));
            Assert.AreEqual(TurkishTexts.StoreClosedButton, _flow.QueueButtonText);
        }
    }
}
