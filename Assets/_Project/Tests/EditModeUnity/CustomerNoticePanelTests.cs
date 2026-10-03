using System.IO;
using System.Linq;
using Esnaf.App.Ui;
using Esnaf.Domain.Content;
using Esnaf.Domain.Game;
using Esnaf.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Esnaf.App.Tests
{
    /// <summary>
    /// Unity-only (Test Runner > EditMode): global müşteri bildirimi (Gün 13.1) gerçek UI nesneleriyle. Sahnedeki tohumla (20260101: ilk müşteri 09:16) yaklaşık 6 gerçek saniyede
    /// bildirim görünür; Raf gibi başka bir ekranda da görünür ve ekranı değiştirmez; "Müşteriye Git" doğru müşteriyi açar; "Kapat" kaldırır.
    /// Her testin alanları baştan kurulur (fixture örneği paylaşılır; TearDown sıfırlar).
    /// </summary>
    public class CustomerNoticePanelTests
    {
        private GameObject _canvasObject;
        private GameSession _session;
        private UiFlow _flow;
        private CustomerNoticeView _notices;

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
                Object.DestroyImmediate(_canvasObject);
            }

            _session = null;
            _flow = null;
            _notices = null;
            _canvasObject = null;
        }

        private void Refresh()
        {
            _notices.Show();
        }

        private void Build()
        {
            string dir = Path.Combine(Application.dataPath, "_Project", "Content", "Data");
            ContentLoadResult loaded = ContentDatabase.Load(new DirectoryContentSource(dir));
            Assert.IsTrue(loaded.IsSuccess, loaded.FormatIssues());
            _session = GameSession.NewGame(loaded.Database, 20260101UL); // GameBootstrap sahne tohumu
            var listing = _session.Api.GetListings()[0];
            Assert.IsTrue(_session.Api.BuyListing(listing.ListingId).IsSuccess);
            Assert.IsTrue(_session.Api.SetPrice(_session.Api.GetInventory()[0].InstanceId, listing.AskingPrice).IsSuccess);
            _flow = new UiFlow(_session.Api, new ContentPresentation(loaded.Database), _session.Bus);
            Canvas canvas = UiBuilder.CreateCanvas("TestCanvas");
            _canvasObject = canvas.gameObject;
            _notices = new CustomerNoticeView(canvas.transform, _flow);
            _flow.Changed += Refresh;
            Refresh();
        }

        private void TickRealSeconds(double seconds)
        {
            for (double t = 0; t < seconds; t += 1.0 / 60.0)
            {
                _flow.Tick(1.0 / 60.0);
            }
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

        [Test]
        public void WithinAboutSixRealSeconds_TheFirstCustomersNoticeIsVisible()
        {
            Build();
            Assert.AreEqual(540, _session.Api.GetClock().MinuteOfDay);
            long firstId = _session.CustomerQueue.PlanFor(1)[0].CustomerId;
            Assert.AreEqual(556, _session.CustomerQueue.PlanFor(1)[0].ArrivalMinute, "tohum 20260101: ilk müşteri 09:16");
            Assert.IsNull(Find("Notice_" + firstId));

            TickRealSeconds(6.0);

            Assert.IsNotNull(Find("Notice_" + firstId), "09:16'da gelen müşterinin bildirimi 6 sn içinde görünür");
            StringAssert.StartsWith("Müşteri geldi:", Find("NoticeTitle").GetComponent<Text>().text);
            Assert.IsNotNull(Find("GoToCustomerButton"));
            Assert.AreEqual(1, _canvasObject.GetComponentsInChildren<Transform>(false).Count(t => t.name == "Notice_" + firstId), "tek kart");
        }

        [Test]
        public void TheNotice_IsShownOnTheShelfScreen_WithoutChangingIt()
        {
            Build();
            Assert.IsTrue(_flow.OpenShelf());

            TickRealSeconds(6.0);

            Assert.IsNotNull(Find("GoToCustomerButton"));
            Assert.AreEqual(UiScreen.Shelf, _flow.CurrentScreen, "oyuncu zorla başka ekrana geçirilmez");
        }

        [Test]
        public void GoToCustomer_OpensTheCustomersConversation()
        {
            Build();
            Assert.IsTrue(_flow.OpenWholesale());
            TickRealSeconds(6.0);
            long id = _flow.Notices[0].CustomerId;

            Find("GoToCustomerButton").GetComponent<Button>().onClick.Invoke();

            Assert.AreEqual(UiScreen.Sale, _flow.CurrentScreen);
            Assert.IsNotNull(_session.Api.GetSale(), "konuşma açıldı");
            Assert.AreEqual(id, _session.Api.GetSale().CustomerId);
        }

        [Test]
        public void ClosingTheNotice_RemovesTheCard_ButTheCustomerKeepsWaiting()
        {
            Build();
            TickRealSeconds(6.0);
            long id = _flow.Notices[0].CustomerId;

            Find("DismissNoticeButton").GetComponent<Button>().onClick.Invoke();

            Assert.IsNull(Find("Notice_" + id));
            Assert.AreEqual(id, _session.Api.GetCustomerQueue().Current.CustomerId);
        }

        [Test]
        public void WhenTheCustomerLeaves_TheCardTurnsIntoALeftNotice()
        {
            Build();
            TickRealSeconds(6.0);
            long id = _flow.Notices[0].CustomerId;

            TickRealSeconds(20.0); // 60 oyun dakikası

            StringAssert.StartsWith("Müşteri ayrıldı:", Find("NoticeTitle").GetComponent<Text>().text);
            Assert.IsNull(Find("GoToCustomerButton"), "ayrılan müşteriye gidilmez");
            Assert.IsNotNull(Find("Notice_" + id));
        }
    }
}
