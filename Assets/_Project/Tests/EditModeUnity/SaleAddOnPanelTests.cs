using System;
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
    /// Unity-only (Test Runner > EditMode): telefon satışı bittikten sonra aksesuar ek satış paneli gerçek içerik, gerçek oturum ve gerçek
    /// UI nesneleriyle çalışır. Düğmelere tıklama = onClick.Invoke(); oyun kuralı UI'da değil, IGameApi'dedir.
    /// </summary>
    public class SaleAddOnPanelTests
    {
        private const string Supplier = "supplier.ucuz_toptan";
        private const string Case = "accessory.phone_case";
        private const string Adapter = "accessory.charger_adapter";

        private GameObject _canvasObject;
        private ContentDatabase _content;
        private GameSession _session;
        private UiFlow _flow;
        private SalePanelView _sale;
        private CustomerView _customer;

        [SetUp]
        public void SetUp()
        {
            string dir = Path.Combine(Application.dataPath, "_Project", "Content", "Data");
            ContentLoadResult loaded = ContentDatabase.Load(new DirectoryContentSource(dir));
            Assert.IsTrue(loaded.IsSuccess, loaded.FormatIssues());
            _content = loaded.Database;

            _session = null;
            for (ulong seed = 1; seed <= 200 && _session == null; seed++)
            {
                GameSession s = GameSession.NewGame(_content, seed);
                MarketListing g = s.Market.Listings.Single(l => l.IsGuided);
                Assert.IsTrue(s.Api.StartNegotiation(g.ListingId).IsSuccess);
                Assert.IsTrue(s.Api.MakeOffer(Money.FromTl(5800)).IsSuccess);
                Assert.IsTrue(s.Api.SetPrice(g.InstanceId, Money.FromTl(5900)).IsSuccess);
                if (s.Api.GetCustomers().Count > 0)
                {
                    _session = s;
                    _customer = s.Api.GetCustomers()[0];
                }
            }

            Assert.IsNotNull(_session, "Müşterisi olan bir seed bulunamadı.");
            _flow = new UiFlow(_session.Api, new ContentPresentation(_content), _session.Bus);
            Canvas canvas = UiBuilder.CreateCanvas("TestCanvas");
            _canvasObject = canvas.gameObject;
            _sale = new SalePanelView(canvas.transform, _flow);
            _flow.Changed += Refresh;
        }

        [TearDown]
        public void TearDown()
        {
            _flow.Changed -= Refresh;
            _flow.Dispose();
            UnityEngine.Object.DestroyImmediate(_canvasObject);
        }

        private void Refresh()
        {
            _sale.Show();
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

        private static int Count(GameSession s, string typeId)
        {
            return s.EconomyState.Ledger.Records.Count(r => r.TypeId == typeId);
        }

        private void StockUp()
        {
            Assert.IsTrue(_session.WholesaleService.BuyPack(Supplier, Case, 1).IsSuccess);
            Assert.IsTrue(_session.WholesaleService.BuyPack(Supplier, Adapter, 1).IsSuccess);
        }

        /// <summary>Gerçek müşteri akışı: ekranı aç, müşteriyle konuş, 10 ₺ iste → anlaşma.</summary>
        private void CompleteThePhoneSale()
        {
            Assert.IsTrue(_flow.OpenCustomers());
            Assert.IsTrue(_flow.StartSale(_customer.CustomerId).IsSuccess);
            Assert.IsTrue(_flow.SaleGreet());
            _flow.AdjustSalePrice(-100000);
            Result<SaleView> deal = _flow.SaleAsk();
            Assert.IsTrue(deal.IsSuccess, deal.ErrorCode);
            Assert.AreEqual(NegotiationPhase.Deal, deal.Value.Phase);
        }

        private string TextOf(string name)
        {
            return Find(name).GetComponentInChildren<Text>().text;
        }

        private void Click(string name)
        {
            Find(name).GetComponent<Button>().onClick.Invoke();
        }

        // A
        [Test]
        public void AfterThePhoneSale_TheAddOnPanelAppears_WithTitleAndSubtitle()
        {
            Refresh();
            Assert.IsNull(Find("AddOnPanel"), "satış öncesi panel yok");

            CompleteThePhoneSale();

            Assert.IsNotNull(Find("AddOnPanel"));
            Assert.AreEqual("Yanında bir aksesuar ister misiniz?", Find("AddOnTitle").GetComponent<Text>().text);
            Assert.AreEqual("Bu satışa aksesuar ekleyebilirsiniz.", Find("AddOnSubtitle").GetComponent<Text>().text);
        }

        // B, I
        [Test]
        public void TheCards_ShowNamePriceAndStock_AndAnOutOfStockCardHasADisabledButton()
        {
            Assert.IsTrue(_session.WholesaleService.BuyPack(Supplier, Case, 1).IsSuccess);
            CompleteThePhoneSale();

            Assert.IsNotNull(Find("AddOn_" + Case));
            StringAssert.Contains("Stok: 20", Find("AddOn_" + Case).Find("Detail").GetComponent<Text>().text);
            Assert.IsTrue(Find("AddButton_" + Case).GetComponent<Button>().interactable);
            Assert.AreEqual("Ekle", TextOf("AddButton_" + Case));

            Assert.IsNotNull(Find("AddOn_" + Adapter), "stokta olmayan da gösterilir");
            Assert.IsFalse(Find("AddButton_" + Adapter).GetComponent<Button>().interactable);
            Assert.AreEqual("Stokta yok", TextOf("AddButton_" + Adapter));
        }

        // C, D
        [Test]
        public void ClickingAdd_SellsThroughTheGame_AndTheStockDropsAndTheCardRefreshes()
        {
            StockUp();
            CompleteThePhoneSale();
            Money cash = _session.Api.GetCash();

            Click("AddButton_" + Case);

            Assert.AreEqual(19, _session.AccessoryStock.Quantity(Case));
            Assert.AreEqual(cash + Money.FromTl(160), _session.Api.GetCash());
            Assert.AreEqual(1, Count(_session, TransactionTypeIds.AccessorySale));
            StringAssert.Contains("Stok: 19", Find("AddOn_" + Case).Find("Detail").GetComponent<Text>().text);
            Assert.IsNotNull(Find("AddOnFeedback"), "kısa başarı geri bildirimi");
            StringAssert.Contains("eklendi", Find("AddOnFeedback").GetComponentInChildren<Text>().text);
        }

        // E, J
        [Test]
        public void SeveralAccessories_CanBeAdded_AndThePanelTotalsAreUpdated()
        {
            StockUp();
            CompleteThePhoneSale();
            Money phone = _session.Api.GetAccessoryAddOns().PhoneSalePrice;

            Click("AddButton_" + Case);
            Click("AddButton_" + Case);
            Click("AddButton_" + Adapter);

            Assert.AreEqual(3, Count(_session, TransactionTypeIds.AccessorySale));
            Money accessories = Money.FromTl(160 + 160 + 250);
            Assert.AreEqual("Telefon: " + MoneyFormatter.Format(phone), TextOf("PhoneLine"));
            Assert.AreEqual("Aksesuarlar: " + MoneyFormatter.Format(accessories), TextOf("AccessoriesLine"));
            Assert.AreEqual("Toplam: " + MoneyFormatter.Format(phone + accessories), TextOf("TotalLine"));
            AccessoryAddOnViewShim api = new AccessoryAddOnViewShim(_session);
            Assert.AreEqual("Aksesuar kârı: " + MoneyFormatter.Format(api.AccessoryProfit), TextOf("AccessoryProfitLine"));
            Assert.AreEqual("Toplam kâr: " + MoneyFormatter.Format(api.TotalProfit), TextOf("TotalProfitLine"));
        }

        // F
        [Test]
        public void TheSaleCanBeFinishedWithoutAnyAccessory()
        {
            StockUp();
            CompleteThePhoneSale();

            Click("Reply_" + SaleReplyKind.Continue);

            Assert.AreEqual(SaleMode.Lobby, _flow.SaleScreen.Mode);
            Assert.IsNull(Find("AddOnPanel"));
            Assert.AreEqual(0, Count(_session, TransactionTypeIds.AccessorySale));
            Assert.AreEqual(20, _session.AccessoryStock.Quantity(Case));
        }

        // G, H, K
        [Test]
        public void TheAddOn_LeavesThePhoneSaleAndItsTransactionUntouched_AndTheCustomerStaysTheSame()
        {
            StockUp();
            CompleteThePhoneSale();
            string customer = _flow.SaleScreen.CustomerName;
            string model = _flow.SaleScreen.ModelTitle;
            string title = _flow.SaleScreen.Title;
            int phoneSales = Count(_session, TransactionTypeIds.Sale);
            long recordId = _flow.SaleScreen.AddOn.PhoneSaleRecordId;
            TransactionRecord before;
            _session.EconomyState.Ledger.TryGetById(recordId, out before);

            Click("AddButton_" + Case);
            Click("AddButton_" + Adapter);

            TransactionRecord after;
            _session.EconomyState.Ledger.TryGetById(recordId, out after);
            Assert.AreEqual(phoneSales, Count(_session, TransactionTypeIds.Sale));
            Assert.AreEqual(before.Amount, after.Amount);
            Assert.AreEqual(customer, _flow.SaleScreen.CustomerName);
            Assert.AreEqual(model, _flow.SaleScreen.ModelTitle);
            Assert.AreEqual(title, _flow.SaleScreen.Title);
            Assert.AreEqual(_customer.NpcId, _flow.SaleScreen.NpcId);
            Assert.AreEqual(recordId, _flow.SaleScreen.AddOn.PhoneSaleRecordId);
        }

        // I
        [Test]
        public void ClickingAnOutOfStockCard_DoesNothingToTheGame()
        {
            CompleteThePhoneSale();
            string digest = _session.Api.GetStateDigest();

            Click("AddButton_" + Adapter); // devre dışı: onClick.Invoke yine de UiFlow'a gider, oyun reddeder

            Assert.AreEqual(digest, _session.Api.GetStateDigest());
            Assert.AreEqual(0, Count(_session, TransactionTypeIds.AccessorySale));
        }

        // L
        [Test]
        public void TheCustomerSaleScreen_StillWorks_PortraitStageAndTheContinueButton()
        {
            CompleteThePhoneSale();

            Assert.IsTrue(Find("SaleScreen").gameObject.activeSelf);
            Assert.IsNotNull(Find("Stage"));
            Assert.IsNotNull(Find("Subtitle"));
            Assert.IsNotNull(Find("PortraitPanel"));
            Assert.IsNotNull(Find("PhonePanel"));
            Assert.IsNotNull(Find("Reply_" + SaleReplyKind.Continue));
        }

        // Domain görünümünden okunan kâr değerleri (UI'nın hesaplamadığını doğrulamak için).
        private sealed class AccessoryAddOnViewShim
        {
            public Money AccessoryProfit { get; }
            public Money TotalProfit { get; }

            public AccessoryAddOnViewShim(GameSession session)
            {
                var v = session.Api.GetAccessoryAddOns();
                AccessoryProfit = v.AccessoryProfit;
                TotalProfit = v.TotalProfit;
            }
        }
    }
}
