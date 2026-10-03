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
    /// Unity-only (Test Runner > EditMode): İlanlar pazarı arayüzü (Gün 13.3) gerçek UI nesneleriyle. Kartlarda telefon görseli + model/kondisyon/satıcı/istenen fiyat/tahmini değer + "İncele";
    /// "İncele" detayı açar; detayda büyük görsel, ekspertiz/pazarlık/satın al/geri düğmeleri; ilan yoksa açıklayıcı not. Her testin alanları baştan kurulur (TearDown sıfırlar).
    /// </summary>
    public class ListingsMarketPanelTests
    {
        private GameObject _canvasObject;
        private ContentDatabase _content;
        private GameSession _session;
        private UiFlow _flow;
        private ListingsView _listings;
        private DetailView _detail;

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
            _listings = null;
            _detail = null;
            _canvasObject = null;
        }

        private void Refresh()
        {
            _listings.Show();
            _detail.Show();
        }

        private void Build(bool empty = false)
        {
            string dir = Path.Combine(Application.dataPath, "_Project", "Content", "Data");
            ContentLoadResult loaded = ContentDatabase.Load(new DirectoryContentSource(dir));
            Assert.IsTrue(loaded.IsSuccess, loaded.FormatIssues());
            _content = loaded.Database;
            _session = GameSession.NewGame(_content, 20260101UL);
            if (empty)
            {
                foreach (var l in _session.Api.GetListings().ToList())
                {
                    Assert.IsTrue(_session.Api.BuyListing(l.ListingId).IsSuccess);
                }
            }

            _flow = new UiFlow(_session.Api, new ContentPresentation(_content), _session.Bus);
            Canvas canvas = UiBuilder.CreateCanvas("TestCanvas");
            _canvasObject = canvas.gameObject;
            _listings = new ListingsView(canvas.transform, _flow);
            _detail = new DetailView(canvas.transform, _flow);
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

        [Test]
        public void EveryCard_ShowsImageModelConditionSellerAskingEstimateAndInspect()
        {
            Build();
            foreach (var row in _flow.Listings)
            {
                Transform card = Find("Listing_" + row.ListingId);
                Assert.IsNotNull(card, "kart");
                Assert.IsNotNull(card.Find("PhoneImage"), "telefon görseli");
                Assert.AreEqual(row.Title, card.Find("Title").GetComponent<Text>().text);
                Assert.AreEqual(row.ConditionText, card.Find("Condition").GetComponent<Text>().text);
                Assert.AreEqual(row.SellerText, card.Find("Seller").GetComponent<Text>().text);
                Assert.AreEqual(row.AskingText, card.Find("Asking").GetComponent<Text>().text);
                Assert.AreEqual(row.EstimatedText, card.Find("Estimated").GetComponent<Text>().text);
                Assert.AreEqual("İncele", card.Find("InspectButton").GetComponentInChildren<Text>().text);
                Assert.IsNotNull(card.Find("PhoneImage").GetComponent<RectMask2D>(), "görsel kartın dışına taşmaz");
            }
        }

        [Test]
        public void Inspect_OpensTheDetailOfThatListing_WithBigImageAndTheActionButtons()
        {
            Build();
            var row = _flow.Listings[1];

            Find("Listing_" + row.ListingId).Find("InspectButton").GetComponent<Button>().onClick.Invoke();

            Assert.AreEqual(UiScreen.Detail, _flow.CurrentScreen);
            Assert.AreEqual(row.ListingId, _flow.Detail.ListingId);
            Assert.IsNotNull(Find("PhoneImage"), "büyük görsel");
            Assert.AreEqual(row.Title, Find("Title").GetComponent<Text>().text);
            Assert.AreEqual(_flow.Detail.ConditionLine, Find("Condition").GetComponent<Text>().text);
            Assert.AreEqual(_flow.Detail.EstimatedLine, Find("Estimated").GetComponent<Text>().text);
            Assert.AreEqual("Ekspertiz: yapılmadı", Find("AppraisalStatus").GetComponent<Text>().text);
            Assert.AreEqual("Ekspertiz Yap", Find("AppraisalButton").GetComponentInChildren<Text>().text);
            Assert.AreEqual("Pazarlık Yap", Find("NegotiationButton").GetComponentInChildren<Text>().text);
            Assert.IsNotNull(Find("BuyButton"));
            Assert.AreEqual("Geri", Find("BackButton").GetComponentInChildren<Text>().text);
        }

        [Test]
        public void TheDetailButtons_ConnectToTheExistingFlows_AndBackReturnsToTheList()
        {
            Build();
            _flow.OpenListing(_flow.Listings[0].ListingId);

            Find("AppraisalButton").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(UiScreen.Appraisal, _flow.CurrentScreen);
            _flow.Back();
            Find("NegotiationButton").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(UiScreen.Negotiation, _flow.CurrentScreen);
            _flow.Back();
            Assert.AreEqual(UiScreen.Detail, _flow.CurrentScreen);

            Find("BackButton").GetComponent<Button>().onClick.Invoke();

            Assert.AreEqual(UiScreen.Listings, _flow.CurrentScreen);
        }

        [Test]
        public void WithoutListings_AnExplainingNoteIsShown()
        {
            Build(true);

            Assert.AreEqual(0, _flow.Listings.Count);
            StringAssert.Contains("Bugün ilan yok", Find("Empty").GetComponent<Text>().text);
            StringAssert.Contains("günü bitirebilirsin", Find("Empty").GetComponent<Text>().text);
        }
    }
}
