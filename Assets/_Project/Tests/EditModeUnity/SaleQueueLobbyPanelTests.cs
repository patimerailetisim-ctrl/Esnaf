using System;
using System.Collections.Generic;
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
    /// Unity-only (Test Runner > EditMode): gerçek zamanlı mağaza arayüzü (Gün 12.6) gerçek UI nesneleriyle. Saat Tick ile akar (1 sn = 1 dk), müşteriler kendiliğinden gelir,
    /// bekleyenler listelenir; "Bekle", toplam müşteri sayacı ve sıradaki geliş saati yoktur; ürünü olmayan müşteri için "Gönder"; üst çubukta saat; İlanlar düğmesinde durum yazısı.
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

        private GameSession WithShelf(ulong seed, long listPriceTl = 5900)
        {
            GameSession s = GameSession.NewGame(_content, seed);
            var guided = s.Market.Listings.Single(l => l.IsGuided);
            Assert.IsTrue(s.Api.StartNegotiation(guided.ListingId).IsSuccess);
            Assert.IsTrue(s.Api.MakeOffer(Money.FromTl(5800)).IsSuccess);
            Assert.IsTrue(s.Api.SetPrice(guided.InstanceId, Money.FromTl(listPriceTl)).IsSuccess);
            return s;
        }

        // Oturum: raflı; ilk müşterisi geldiğinde ilgilendiği ürün vardır (aksi halde müşteri hiç gelmezdi).
        private void Build(int maxGapToSecond = int.MaxValue)
        {
            LoadContent();
            _session = null;
            for (ulong seed = 1; seed <= 1500 && _session == null; seed++)
            {
                GameSession probe = WithShelf(seed);
                IReadOnlyList<QueuedCustomer> plan = probe.CustomerQueue.PlanFor(probe.Time.Day);
                QueuedCustomer first = plan[0];
                if (plan[1].ArrivalMinute - first.ArrivalMinute > maxGapToSecond)
                {
                    continue;
                }

                probe.Api.AdvanceTime(first.ArrivalMinute - probe.Api.GetClock().MinuteOfDay);
                CustomerView v = probe.Api.GetActiveCustomer();
                if (v != null && v.InstanceId != 0)
                {
                    _session = WithShelf(seed);
                }
            }

            Assert.IsNotNull(_session, "Uygun oturum bulunamadı.");
            Attach();
        }

        // Gerçek zamanlı saati n gerçek saniye boyunca akıtır.
        // 1 gerçek sn = 3 oyun dk (Gün 12.6).
        private static readonly double OneMinute = 1.0 / UiFlow.GameMinutesPerRealSecond;

        private void TickSeconds(int seconds)
        {
            for (int i = 0; i < seconds; i++)
            {
                _flow.Tick(1.0);
            }
        }

        private void TickToArrival(QueuedCustomer customer)
        {
            while (Now() < customer.ArrivalMinute)
            {
                _flow.Tick(OneMinute); // dakika dakika: geliş dakikasını aşmaz
            }
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
        public void TheTopBar_ShowsTheClock_AndFollowsTheRealTimeTick()
        {
            Build();
            _flow.ClockTicked += Refresh;

            Assert.AreEqual("09:00", TextOf("ClockText"));
            TickSeconds(75);

            Assert.AreEqual("12:45", TextOf("ClockText"), "75 saniye = 225 dakika");
            StringAssert.Contains("Gün", TextOf("DayText"));
        }

        [Test]
        public void BeforeAnyoneArrives_TheLobbyShowsNoCountNoNextTimeAndNoWaitButton()
        {
            Build();

            _flow.OpenCustomers();

            Assert.AreEqual(TurkishTexts.NoCustomersLine, TextOf("StatusLine"));
            Assert.IsNull(Find("WaitButton"), "Bekle düğmesi kaldırıldı");
            Assert.IsNull(Find("Clock"), "lobide saat/ilerleme kartı yok (saat üst çubukta)");
            Assert.IsNull(Find("Progress"), "toplam müşteri sayacı yok");
            Assert.AreEqual(0, CountNamed("Customer_"));
            Assert.AreEqual(0, CountNamed("Waiting_"));
            Assert.IsNull(Find("DismissButton"));
        }

        [Test]
        public void ThePlayersMainButton_ShowsTheQueueState_NotTheOldSlotCountNorATime()
        {
            Build();
            Assert.AreEqual(TurkishTexts.NoCustomersButton, Find("CustomersButton").GetComponentInChildren<Text>().text);

            QueuedCustomer first = _session.CustomerQueue.PlanFor(1)[0];
            // İlanlar ekranında kalınır: düğme yalnızca bu ekranda görünür/aktiftir (OpenCustomers listeyi gizler, Find() de pasif nesneleri bulmaz).
            TickToArrival(first);

            string expected = TurkishTexts.CustomerArrivedButton(_flow.Content.CustomerName(first.CustomerId, first.NpcId));
            string text = Find("CustomersButton").GetComponentInChildren<Text>().text;
            Assert.AreEqual(expected, text, "ana buton kuyruk durumunu gösterir");
            StringAssert.DoesNotContain(TurkishTexts.CustomersButton(_session.Api.GetCustomers().Count), text, "eski slot sayısı yok");
            StringAssert.DoesNotContain(first.ArrivalText, text, "geliş saati yok");
            StringAssert.DoesNotContain("Sıradaki", text);
            StringAssert.DoesNotContain("Bekle", text);
        }

        [Test]
        public void ACustomerAppearsOnTheirOwn_AsExactlyOneCard_WithNoWaitButton()
        {
            Build();
            QueuedCustomer first = _session.CustomerQueue.PlanFor(1)[0];
            _flow.OpenCustomers();

            TickToArrival(first);

            Assert.AreEqual(first.ArrivalMinute, Now(), "tam geliş saatinde");
            Assert.IsNull(Find("WaitButton"));
            Assert.AreEqual(1, CountNamed("Customer_"), "aynı anda tek aktif müşteri kartı");
            Assert.IsNotNull(Find("Customer_" + first.CustomerId));
            Assert.AreEqual(_flow.Content.CustomerName(first.CustomerId, first.NpcId), TextOf("Name"));
            Assert.IsNotNull(Find("Portrait"), "mevcut portre sistemi");
        }

        [Test]
        public void AQueuedCustomer_IsListedLive_WithTheirEntryTime()
        {
            Build(45); // ilk iki müşteri 45 dk'dan yakın gelen tohum: ikincisi birincinin sabrı bitmeden kuyruğa girer
            IReadOnlyList<QueuedCustomer> plan = _session.CustomerQueue.PlanFor(1);

            _flow.OpenCustomers();
            TickToArrival(plan[1]);

            Assert.AreEqual(1, CountNamed("Customer_"), "aktif müşteri tek kart");
            Assert.IsNotNull(Find("Waiting_" + plan[1].CustomerId), "sıradaki müşteri listede");
            StringAssert.Contains(plan[1].ArrivalText, TextOf("Arrived"), "giriş saati");
        }

        [Test]
        public void TheOldRosterCards_AreNotDrawnInTheMainLobby()
        {
            Build();
            long[] oldIds = _session.Api.GetCustomers().Select(c => c.CustomerId).ToArray();
            _flow.OpenCustomers();
            TickToArrival(_session.CustomerQueue.PlanFor(1)[0]);

            foreach (long id in oldIds)
            {
                Assert.IsNull(Find("Customer_" + id), "eski yuva kartı çizilmez: " + id);
            }

            Assert.AreEqual(_session.Api.GetCustomers().Count, _flow.SaleScreen.Customers.Count, "ama altyapı/görünüm modeli korunur");
        }

        [Test]
        public void TappingTheActiveCustomer_StartsTheExistingSaleScreen()
        {
            Build();
            _flow.OpenCustomers();
            TickToArrival(_session.CustomerQueue.PlanFor(1)[0]);
            long id = _flow.SaleScreen.Queue.Customer.CustomerId;

            Find("Customer_" + id).GetComponent<Button>().onClick.Invoke();

            Assert.AreEqual(SaleMode.Talking, _flow.SaleScreen.Mode);
            Assert.AreEqual(id, _session.Api.GetSale().CustomerId);
            Assert.IsNull(Find("QueueStatus"), "lobi gizlendi");
            Assert.IsNotNull(Find("Reply_" + SaleReplyKind.Greet), "mevcut konuşma ekranı");
        }

        [Test]
        public void AfterTheSale_TheLobbyReturns_WithTheCustomerCompleted()
        {
            Build();
            _flow.OpenCustomers();
            TickToArrival(_session.CustomerQueue.PlanFor(1)[0]);
            Find("Customer_" + _flow.SaleScreen.Queue.Customer.CustomerId).GetComponent<Button>().onClick.Invoke();
            Find("Reply_" + SaleReplyKind.LetGo).GetComponent<Button>().onClick.Invoke();

            Find("Reply_" + SaleReplyKind.Continue).GetComponent<Button>().onClick.Invoke();

            Assert.AreEqual(SaleMode.Lobby, _flow.SaleScreen.Mode);
            Assert.AreEqual(1, _session.Api.GetCustomerQueue().Served, "müşteri tamamlandı");
            Assert.IsNull(Find("Progress"));
            Assert.LessOrEqual(CountNamed("Customer_"), 1);
        }

        [Test]
        public void ACustomerWhoWaitsAnHour_LeavesWithANaturalLine_AndTheCardDisappears()
        {
            Build();
            QueuedCustomer first = _session.CustomerQueue.PlanFor(1)[0];
            _flow.OpenCustomers();
            TickToArrival(first);
            Assert.IsNotNull(Find("Customer_" + first.CustomerId));

            TickSeconds(20); // 20 gerçek saniye = 60 oyun dakikası

            Assert.IsNull(Find("Customer_" + first.CustomerId), "sabrı bitti, çıktı");
            Assert.IsNotNull(_flow.StatusMessage);
            StringAssert.Contains(_flow.Content.CustomerName(first.CustomerId, first.NpcId), _flow.StatusMessage);
        }

        [Test]
        public void ACustomerWithNothingToBuy_ShowsADismissButton_AndNotAClickableCard()
        {
            // Gün 12.6: müşteri ilgilenebileceği ürün varken gelir; geldikten sonra ürün çok pahalıya etiketlenirse "ürün yok → Gönder" akışı sürer.
            Build();
            _flow.OpenCustomers();
            QueuedCustomer first = _session.CustomerQueue.PlanFor(1)[0];
            TickToArrival(first);
            Assert.IsTrue(_session.Api.SetPrice(_session.Api.GetInventory()[0].InstanceId, Money.FromTl(90000)).IsSuccess);
            _flow.Refresh();

            Assert.IsNotNull(Find("DismissButton"));
            Transform card = Find("Customer_" + first.CustomerId);
            Assert.IsNotNull(card);
            Assert.IsNull(card.GetComponent<Button>(), "ürünü yok: kart satışa başlatmaz");
            int t = Now();

            Find("DismissButton").GetComponent<Button>().onClick.Invoke();

            Assert.AreEqual(t + InteractionTime.CompleteCustomer, Now());
            Assert.IsNull(Find("DismissButton"));
        }

        [Test]
        public void WhenTheStoreIsClosed_NoCustomerCardIsShown_AndTheClockStopsAtTwentyOne()
        {
            Build();
            _flow.ClockTicked += Refresh;
            TickSeconds(245); // 4 gerçek dakika = tüm gün
            _flow.OpenCustomers();

            Assert.AreEqual(0, CountNamed("Customer_"));
            Assert.AreEqual(TurkishTexts.StoreClosedLine, TextOf("StatusLine"));
            Assert.AreEqual("21:00", TextOf("ClockText"));
            Assert.AreEqual(TurkishTexts.StoreClosedButton, _flow.QueueButtonText);
        }
    }
}
