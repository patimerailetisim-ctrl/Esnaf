using System.IO;
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
    /// Unity-only (Test Runner > EditMode): Toptancı ve Aksesuar Stoğu panelleri gerçek içerik ve gerçek oturumla çalışır.
    /// UI nesneleri kodla kurulur; ekranlar yalnızca UiFlow ile konuşur.
    /// </summary>
    public class WholesalePanelTests
    {
        private const string Supplier = "supplier.ucuz_toptan";

        private GameObject _canvasObject;
        private GameSession _session;
        private UiFlow _flow;
        private ListingsView _listings;
        private WholesalePanelView _wholesale;
        private AccessoryStockPanelView _stock;

        [SetUp]
        public void SetUp()
        {
            string dir = Path.Combine(Application.dataPath, "_Project", "Content", "Data");
            ContentLoadResult loaded = ContentDatabase.Load(new DirectoryContentSource(dir));
            Assert.IsTrue(loaded.IsSuccess, loaded.FormatIssues());
            _session = GameSession.NewGame(loaded.Database, 1UL);
            _flow = new UiFlow(_session.Api, new ContentPresentation(loaded.Database), _session.Bus);
            Canvas canvas = UiBuilder.CreateCanvas("TestCanvas");
            _canvasObject = canvas.gameObject;
            _listings = new ListingsView(canvas.transform, _flow);
            _wholesale = new WholesalePanelView(canvas.transform, _flow);
            _stock = new AccessoryStockPanelView(canvas.transform, _flow);
            _flow.Changed += Refresh;
        }

        [TearDown]
        public void TearDown()
        {
            _flow.Changed -= Refresh;
            _flow.Dispose();
            Object.DestroyImmediate(_canvasObject);
        }

        private void Refresh()
        {
            _listings.Show();
            _wholesale.Show();
            _stock.Show();
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

        [Test]
        public void TheListingsScreen_HasAllFiveNavigationButtons_AndTheNewOnesOpenTheirScreens()
        {
            Refresh();

            foreach (string name in new[] { "ShelfButton", "CustomersButton", "WholesaleButton", "AccessoryStockButton", "EndDayButton" })
            {
                Assert.IsNotNull(Find(name), name + " Listings ekran\u0131nda yok");
            }

            Assert.AreEqual("Toptanc\u0131", Find("WholesaleButton").GetComponentInChildren<Text>().text);
            Assert.AreEqual("Aksesuar (0/60)", Find("AccessoryStockButton").GetComponentInChildren<Text>().text);

            Find("WholesaleButton").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(UiScreen.Wholesale, _flow.CurrentScreen);
            _flow.Back();
            Find("AccessoryStockButton").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(UiScreen.AccessoryStock, _flow.CurrentScreen);
        }

        [Test]
        public void TheWholesalePanel_IsHiddenUntilOpened_ThenShowsAnOfferCardPerOffer()
        {
            Refresh();
            Assert.IsFalse(Find("WholesaleScreen").gameObject.activeSelf);

            _flow.OpenWholesale();

            Assert.IsTrue(Find("WholesaleScreen").gameObject.activeSelf);
            Assert.IsFalse(Find("AccessoryStockScreen").gameObject.activeSelf);
            Assert.IsNotNull(Find("Offer_accessory.charger_adapter"));
            Assert.IsNotNull(Find("Offer_accessory.powerbank"));
        }

        [Test]
        public void TheBuyButton_IsEnabled_ForAnOpenOffer_AndDisabled_ForALockedOne()
        {
            _flow.OpenWholesale();

            Assert.IsTrue(Find("Buy_accessory.charger_adapter").GetComponent<Button>().interactable);
            Assert.IsFalse(Find("Buy_accessory.powerbank").GetComponent<Button>().interactable, "powerbank 3. günde açılır");
            Assert.AreEqual("Gün 3'te açılır", Find("Buy_accessory.powerbank").GetComponentInChildren<Text>().text);
        }

        [Test]
        public void ClickingBuy_BuysThePack_AndRebuildsTheScreen()
        {
            _flow.OpenWholesale();

            Find("Buy_accessory.charger_adapter").GetComponent<Button>().onClick.Invoke();

            Assert.AreEqual(10, _session.AccessoryStock.Quantity("accessory.charger_adapter"));
            Assert.AreEqual("10 adet Şarj Adaptörü stoğa eklendi.", _flow.StatusMessage);
            Assert.IsNotNull(Find("Status"), "geri bildirim kartı gösterilir");
        }

        [Test]
        public void TheStockPanel_ShowsTheBoughtItems_AndIsSeparateFromThePhoneShelf()
        {
            _session.Api.BuyWholesalePack(Supplier, "accessory.phone_case");
            _flow.OpenAccessoryStock();

            Assert.IsTrue(Find("AccessoryStockScreen").gameObject.activeSelf);
            Assert.IsNotNull(Find("Item_accessory.phone_case"));
            Assert.AreEqual(0, _session.Api.GetInventory().Count, "telefon rafı boş kalır");
        }
    }
}
