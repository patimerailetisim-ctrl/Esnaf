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
    /// Unity-only (Test Runner > EditMode): Raf ekranında fiyat belirleme arayüzü (Gün 12.7) gerçek UI nesneleriyle: fiyatsız ürün uyarısı, ürüne dokununca fiyat paneli
    /// (maliyet/fiyat/kâr/marj), −/+ düğmeleri ve Kaydet. Her testin alanları baştan kurulur (fixture örneği paylaşılır; TearDown sıfırlar).
    /// </summary>
    public class ShelfPricingPanelTests
    {
        private GameObject _canvasObject;
        private GameSession _session;
        private UiFlow _flow;
        private ShelfView _shelf;

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
            _shelf = null;
            _canvasObject = null;
        }

        private void Refresh()
        {
            _shelf.Show();
        }

        private void Build()
        {
            string dir = Path.Combine(Application.dataPath, "_Project", "Content", "Data");
            ContentLoadResult loaded = ContentDatabase.Load(new DirectoryContentSource(dir));
            Assert.IsTrue(loaded.IsSuccess, loaded.FormatIssues());
            _session = GameSession.NewGame(loaded.Database, 1UL);
            Assert.IsTrue(_session.Api.BuyListing(_session.Api.GetListings()[0].ListingId).IsSuccess);
            _flow = new UiFlow(_session.Api, new ContentPresentation(loaded.Database), _session.Bus);
            Canvas canvas = UiBuilder.CreateCanvas("TestCanvas");
            _canvasObject = canvas.gameObject;
            _shelf = new ShelfView(canvas.transform, _flow);
            _flow.Changed += Refresh;
            Assert.IsTrue(_flow.OpenShelf());
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

        private string TextOf(string name)
        {
            return Find(name).GetComponent<Text>().text;
        }

        [Test]
        public void AnUnpricedPhone_ShowsTheNoPriceWarning_AndNoPanelUntilTapped()
        {
            Build();
            long id = _session.Api.GetInventory()[0].InstanceId;

            Assert.IsNotNull(Find("Item_" + id));
            Assert.AreEqual(TurkishTexts.ShelfNoPriceLine, TextOf("PriceLine"));
            Assert.IsNull(Find("PriceEditor"));
        }

        [Test]
        public void TappingTheItem_OpensThePanel_AndSavingMakesItSellable()
        {
            Build();
            var line = _session.Api.GetInventory()[0];

            Find("Item_" + line.InstanceId).GetComponent<Button>().onClick.Invoke();

            Assert.IsNotNull(Find("PriceEditor"));
            Assert.AreEqual(TurkishTexts.ShelfAcquisitionCost(line.CostBasis), TextOf("EditorCost"));
            long start = _flow.ShelfScreen.Editor.Price.Tl;
            Find("PricePlus1000").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(start + 1000, _flow.ShelfScreen.Editor.Price.Tl);
            StringAssert.StartsWith("Tahmini", TextOf("EditorProfit"));
            StringAssert.StartsWith("Marj", TextOf("EditorMargin"));

            Find("SavePriceButton").GetComponent<Button>().onClick.Invoke();

            Assert.IsTrue(_session.Api.GetInventory()[0].ListPrice.IsPositive);
            Assert.IsTrue(_session.Customers.HasSellableStock());
            Assert.IsNull(Find("PriceEditor"), "kayıttan sonra panel kapanır");
            Assert.AreEqual(TurkishTexts.ShelfSellableLine(_session.Api.GetInventory()[0].ListPrice), TextOf("PriceLine"));
        }

        [Test]
        public void ACancelledPanel_SavesNothing()
        {
            Build();
            long id = _session.Api.GetInventory()[0].InstanceId;
            Find("Item_" + id).GetComponent<Button>().onClick.Invoke();

            Find("CancelPriceButton").GetComponent<Button>().onClick.Invoke();

            Assert.IsNull(Find("PriceEditor"));
            Assert.IsFalse(_session.Api.GetInventory()[0].ListPrice.IsPositive);
        }

        [Test]
        public void ThePanel_ShowsImageStockAndTheRemoveButton_OnlyForAPricedPhone()
        {
            Build();
            long id = _session.Api.GetInventory()[0].InstanceId;
            Find("Item_" + id).GetComponent<Button>().onClick.Invoke();
            Assert.IsNotNull(Find("PhoneImage"), "telefon görseli");
            Assert.AreEqual(TurkishTexts.ShelfStock(1), Find("EditorStock").GetComponent<Text>().text);
            Assert.IsNull(Find("RemoveFromSaleButton"), "fiyatsız telefon: Satıştan Çıkar yok");

            Find("SavePriceButton").GetComponent<Button>().onClick.Invoke();
            Find("Item_" + id).GetComponent<Button>().onClick.Invoke();

            Assert.IsNotNull(Find("RemoveFromSaleButton"), "satıştaki telefon: Satıştan Çıkar var");
        }

        [Test]
        public void RemoveFromSale_PutsThePhoneOffSale_WithoutLosingIt()
        {
            Build();
            long id = _session.Api.GetInventory()[0].InstanceId;
            long cost = _session.Api.GetInventory()[0].CostBasis.Tl;
            Find("Item_" + id).GetComponent<Button>().onClick.Invoke();
            Find("SavePriceButton").GetComponent<Button>().onClick.Invoke();
            Find("Item_" + id).GetComponent<Button>().onClick.Invoke();

            Find("RemoveFromSaleButton").GetComponent<Button>().onClick.Invoke();

            Assert.IsFalse(_session.Api.GetInventory()[0].ListPrice.IsPositive, "satış dışı");
            Assert.AreEqual(1, _session.Api.GetInventory().Count, "telefon rafta duruyor");
            Assert.AreEqual(cost, _session.Api.GetInventory()[0].CostBasis.Tl, "maliyet aynı");
            Assert.IsFalse(_session.Customers.HasSellableStock());
            Assert.IsNull(Find("RemoveFromSaleButton"), "artık satıştan çıkarılacak bir şey yok");
            Assert.IsNotNull(Find("SavePriceButton"), "panel açık: yeniden fiyatlanabilir");
        }
    }
}
