using System.IO;
using System.Linq;
using Esnaf.App.Ui;
using Esnaf.Core;
using Esnaf.Domain.Content;
using Esnaf.Domain.Game;
using Esnaf.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Esnaf.App.Tests
{
    /// <summary>
    /// Unity-only (Test Runner > EditMode): Dükkan ana ekranı ve kalıcı alt navigasyon (Gün 13.4) gerçek UI nesneleriyle: HUD, telefon karoları (görselli, fiyatlı), aksesuar satırları, aktif müşteri,
    /// boş durumlar, telefona dokununca mevcut Raf paneli, alt navigasyonun doğru ekranlara gitmesi. Her testin alanları baştan kurulur (TearDown sıfırlar).
    /// </summary>
    public class ShopPanelTests
    {
        private GameObject _canvasObject;
        private GameSession _session;
        private UiFlow _flow;
        private TopBarView _topBar;
        private ShopView _shop;
        private ShelfView _shelf;
        private ProfileView _profile;
        private NavBarView _nav;

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
            _topBar = null;
            _shop = null;
            _shelf = null;
            _profile = null;
            _nav = null;
            _canvasObject = null;
        }

        private void Refresh()
        {
            _topBar.Show(_flow.TopBar);
            _shop.Show();
            _shelf.Show();
            _profile.Show();
            _nav.Show();
        }

        private void Build(int phones = 0, int priced = 0, bool accessories = false)
        {
            string dir = Path.Combine(Application.dataPath, "_Project", "Content", "Data");
            ContentLoadResult loaded = ContentDatabase.Load(new DirectoryContentSource(dir));
            Assert.IsTrue(loaded.IsSuccess, loaded.FormatIssues());
            _session = GameSession.NewGame(loaded.Database, 20260101UL);
            foreach (var l in _session.Api.GetListings().Take(phones).ToList())
            {
                Assert.IsTrue(_session.Api.BuyListing(l.ListingId).IsSuccess);
            }

            var stock = _session.Api.GetInventory();
            for (int i = 0; i < priced; i++)
            {
                Assert.IsTrue(_session.Api.SetPrice(stock[i].InstanceId, Money.FromTl((stock[i].CostBasis.Tl + 9) / 10 * 10)).IsSuccess);
            }

            if (accessories)
            {
                Assert.IsTrue(_session.Api.BuyWholesalePack("supplier.ucuz_toptan", "accessory.charger_adapter").IsSuccess);
            }

            _flow = new UiFlow(_session.Api, new ContentPresentation(loaded.Database), _session.Bus);
            Canvas canvas = UiBuilder.CreateCanvas("TestCanvas");
            _canvasObject = canvas.gameObject;
            _topBar = new TopBarView(canvas.transform);
            _shop = new ShopView(canvas.transform, _flow);
            _shelf = new ShelfView(canvas.transform, _flow);
            _profile = new ProfileView(canvas.transform, _flow);
            _nav = new NavBarView(canvas.transform, _flow);
            _flow.Changed += Refresh;
            _flow.GoToShop();
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
        public void TheHud_ShowsDayClockAndCash_AboveTheShop()
        {
            Build();

            StringAssert.Contains("Gün", Find("DayText").GetComponent<Text>().text);
            Assert.AreEqual("09:00", Find("ClockText").GetComponent<Text>().text);
            Assert.AreEqual(_flow.TopBar.CashText, Find("CashText").GetComponent<Text>().text);
            Assert.IsNotNull(Find("ShopScreen"));
        }

        [Test]
        public void ThePhoneShelf_ShowsTilesWithImagesNamesAndPrices_AndASeparateOffSaleGroup()
        {
            Build(3, 2);
            var stock = _session.Api.GetInventory();

            Assert.AreEqual("RAF  3/15", Find("ShelfHeader").GetComponent<Text>().text);
            for (int i = 0; i < 3; i++)
            {
                Transform tile = Find("Phone_" + stock[i].InstanceId);
                Assert.IsNotNull(tile, "telefon karosu");
                Assert.IsNotNull(tile.Find("PhoneImage"), "telefon görseli");
                Assert.IsNotNull(tile.Find("PhoneImage").GetComponent<RectMask2D>(), "görsel taşmaz");
                Assert.IsNotNull(tile.Find("Model"));
                Assert.IsNotNull(tile.Find("Price"));
            }

            Assert.AreEqual(TurkishTexts.ShopOffSale, Find("Phone_" + stock[2].InstanceId).Find("Price").GetComponent<Text>().text);
            Assert.IsNotNull(Find("OffSaleHeader"), "fiyatsız telefonlar ayrı grup");
        }

        [Test]
        public void AnEmptyShelf_AndAnEmptyAccessoryStock_ShowTheirNotes()
        {
            Build();

            StringAssert.Contains("İlanlar", Find("ShelfEmpty").GetComponent<Text>().text);
            StringAssert.Contains("Toptancı", Find("AccessoriesEmpty").GetComponent<Text>().text);
            StringAssert.EndsWith("0/60", Find("AccessoryHeader").GetComponent<Text>().text);
            StringAssert.Contains("müşteri yok", Find("CustomerNote").GetComponent<Text>().text);
        }

        [Test]
        public void TheAccessoryShelf_ListsTheStockWithQuantities()
        {
            Build(0, 0, true);

            StringAssert.EndsWith("/60", Find("AccessoryHeader").GetComponent<Text>().text);
            Assert.IsNotNull(Find("Accessory_accessory.charger_adapter"));
            Assert.IsNull(Find("AccessoriesEmpty"));
        }

        [Test]
        public void TappingAPhone_OpensTheExistingShelfPricingPanel()
        {
            Build(2, 1);
            long id = _session.Api.GetInventory()[1].InstanceId;

            Find("Phone_" + id).GetComponent<Button>().onClick.Invoke();

            Assert.AreEqual(UiScreen.Shelf, _flow.CurrentScreen);
            Assert.IsNotNull(Find("PriceEditor"), "mevcut fiyat paneli");
            Assert.IsNull(Find("ShopScreen"), "Dükkan gizlendi");
            Find("BackButton").GetComponent<Button>().onClick.Invoke();
            Find("BackButton").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(UiScreen.Shop, _flow.CurrentScreen);
        }

        [Test]
        public void TheNavBar_ShowsFourTabs_AndGoesToTheRightScreens()
        {
            Build();
            foreach (string tab in new[] { "Nav_Shop", "Nav_Wholesale", "Nav_Listings", "Nav_Profile" })
            {
                Assert.IsNotNull(Find(tab), tab);
            }

            Find("Nav_Wholesale").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(UiScreen.Wholesale, _flow.CurrentScreen);
            Assert.IsNotNull(Find("Nav_Shop"), "alt navigasyon her ekranda sabit");
            Find("Nav_Listings").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(UiScreen.Listings, _flow.CurrentScreen);
            Find("Nav_Profile").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(UiScreen.Profile, _flow.CurrentScreen);
            Assert.IsNotNull(Find("ProfileScreen"));
            Find("Nav_Shop").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(UiScreen.Shop, _flow.CurrentScreen);
        }

        [Test]
        public void TheNavBar_DrawsAnIconPerTab_AndMarksOnlyTheActiveTabWithTheGoldBar()
        {
            Build();
            foreach (string tab in new[] { "Nav_Shop", "Nav_Wholesale", "Nav_Listings", "Nav_Profile" })
            {
                Transform icon = Find(tab).Find("Icon");
                Assert.IsNotNull(icon, tab + " ikonu");
                Assert.Greater(icon.childCount, 1, tab + " ikonu birden çok parçadan çizilir (emoji değil)");
                Assert.IsNotNull(Find(tab).Find("Label"), tab + " yazısı");
            }

            Assert.IsNotNull(Find("Nav_Shop").Find("ActiveBar"), "aktif sekme altın vurgu çizgisi");
            Assert.IsNull(Find("Nav_Wholesale").Find("ActiveBar"));
            Assert.IsNotNull(Find("NavBar").Find("TopBorder"), "ince üst çizgi");

            Find("Nav_Listings").GetComponent<Button>().onClick.Invoke();
            Assert.IsNotNull(Find("Nav_Listings").Find("ActiveBar"));
            Assert.IsNull(Find("Nav_Shop").Find("ActiveBar"));
        }

        [Test]
        public void TheActiveCustomer_ShowsOnTheShop_AndGoOpensTheExistingSale()
        {
            Build(1, 1);
            var plan = _session.CustomerQueue.PlanFor(1);
            _session.Api.AdvanceTime(plan[0].ArrivalMinute - _session.Api.GetClock().MinuteOfDay);
            Assert.IsNotNull(_session.Api.GetActiveCustomer(), "sahne tohumu 20260101: maliyetine fiyatlı E13 Pro ilk müşteriyi getirir");
            Assert.AreNotEqual(0L, _session.Api.GetActiveCustomer().InstanceId);

            _flow.Refresh();

            Assert.IsNotNull(Find("CustomerCard"));
            StringAssert.StartsWith("Müşteri mağazada:", Find("CustomerTitle").GetComponent<Text>().text);
            Find("GoToCustomerButton").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(UiScreen.Sale, _flow.CurrentScreen);
            Assert.IsNotNull(_session.Api.GetSale());
        }
    }
}
