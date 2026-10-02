using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Esnaf.App.Ui;
using Esnaf.Core;
using Esnaf.Domain.Accessories;
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
    /// Unity-only (Test Runner > EditMode): MÜŞTERİ TALEPLİ aksesuar paneli (Gün 11.3.4) gerçek içerik, gerçek oturum ve gerçek UI nesneleriyle çalışır.
    /// Panel yalnızca müşteri aksesuar istediyse açılır ve yalnızca istenen aksesuarları gösterir. Düğmelere tıklama = onClick.Invoke().
    /// Müşterinin talebi satışın değişmez verisinden türer (rastgelelik yok); testler istenen talebe sahip bir satışı (tohum + defter kaydırması) arar.
    /// </summary>
    public class SaleAddOnPanelTests
    {
        private const string Case = "accessory.phone_case";
        private const string Adapter = "accessory.charger_adapter";

        private GameObject _canvasObject;
        private ContentDatabase _content;
        private GameSession _session;
        private UiFlow _flow;
        private SalePanelView _sale;
        private IReadOnlyList<string> _requested;

        [SetUp]
        public void SetUp()
        {
            string dir = Path.Combine(Application.dataPath, "_Project", "Content", "Data");
            ContentLoadResult loaded = ContentDatabase.Load(new DirectoryContentSource(dir));
            Assert.IsTrue(loaded.IsSuccess, loaded.FormatIssues());
            _content = loaded.Database;
        }

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
        }

        private void Refresh()
        {
            _sale.Show();
        }

        // Taze oturum: tohum + satıştan önce 'bumps' küçük yatırım satırı (defter satırı kimliğini kaydırır; talep çeşitliliği için).
        private GameSession Prepare(ulong seed, int bumps)
        {
            GameSession s = GameSession.NewGame(_content, seed);
            for (int i = 0; i < bumps; i++)
            {
                Assert.IsTrue(s.EconomyService.RecordInvestment(Money.FromTl(10), s.Time.Day).IsSuccess);
            }

            return s;
        }

        // Müşteriyle gerçek akış (API): anlaşma olursa talebi döndürür; yoksa null. Yalnızca arama içindir.
        private IReadOnlyList<string> Probe(ulong seed, int bumps)
        {
            GameSession s = Prepare(seed, bumps);
            MarketListing guided = s.Market.Listings.Single(l => l.IsGuided);
            if (!s.Api.StartNegotiation(guided.ListingId).IsSuccess
                || !s.Api.MakeOffer(Money.FromTl(5800)).IsSuccess
                || !s.Api.SetPrice(guided.InstanceId, Money.FromTl(5900)).IsSuccess
                || s.Api.GetCustomers().Count == 0
                || !s.Api.StartSale(s.Api.GetCustomers()[0].CustomerId).IsSuccess)
            {
                return null;
            }

            Result<SaleView> deal = s.Api.AskPrice(Money.FromTl(10));
            if (deal.IsFailure || deal.Value.Phase != NegotiationPhase.Deal)
            {
                return null;
            }

            long id = s.EconomyState.Ledger.Records.Last(r => r.TypeId == TransactionTypeIds.Sale).Id;
            return s.AccessoryAddOns.RequestedAccessories(id);
        }

        /// <summary>Talebi koşulu sağlayan bir satış bulur, UI akışıyla (müşteri ekranı → anlaşma) tamamlar ve paneli kurar.</summary>
        private void CompleteSaleWhere(Func<IReadOnlyList<string>, bool> wanted)
        {
            ulong foundSeed = 0;
            int foundBumps = -1;
            for (int bumps = 0; bumps <= 80 && foundBumps < 0; bumps++)
            {
                for (ulong seed = 1; seed <= 30 && foundBumps < 0; seed++)
                {
                    IReadOnlyList<string> request = Probe(seed, bumps);
                    if (request != null && wanted(request))
                    {
                        foundSeed = seed;
                        foundBumps = bumps;
                    }
                }
            }

            Assert.GreaterOrEqual(foundBumps, 0, "İstenen talebe sahip bir müşteri satışı bulunamadı.");

            _session = Prepare(foundSeed, foundBumps);
            MarketListing guided = _session.Market.Listings.Single(l => l.IsGuided);
            Assert.IsTrue(_session.Api.StartNegotiation(guided.ListingId).IsSuccess);
            Assert.IsTrue(_session.Api.MakeOffer(Money.FromTl(5800)).IsSuccess);
            Assert.IsTrue(_session.Api.SetPrice(guided.InstanceId, Money.FromTl(5900)).IsSuccess);

            _flow = new UiFlow(_session.Api, new ContentPresentation(_content), _session.Bus);
            Canvas canvas = UiBuilder.CreateCanvas("TestCanvas");
            _canvasObject = canvas.gameObject;
            _sale = new SalePanelView(canvas.transform, _flow);
            _flow.Changed += Refresh;
            Refresh();

            Assert.IsTrue(_flow.OpenCustomers());
            Assert.IsTrue(_flow.StartSale(_session.Api.GetCustomers()[0].CustomerId).IsSuccess);
            Assert.IsTrue(_flow.SaleGreet());
            _flow.AdjustSalePrice(-100000);
            Result<SaleView> deal = _flow.SaleAsk();
            Assert.IsTrue(deal.IsSuccess, deal.ErrorCode);
            Assert.AreEqual(NegotiationPhase.Deal, deal.Value.Phase);

            long recordId = _session.EconomyState.Ledger.Records.Last(r => r.TypeId == TransactionTypeIds.Sale).Id;
            _requested = _session.AccessoryAddOns.RequestedAccessories(recordId);
            Assert.IsTrue(wanted(_requested), "UI akışında da aynı talep olmalı");
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

        private static int Count(GameSession s, string typeId)
        {
            return s.EconomyState.Ledger.Records.Count(r => r.TypeId == typeId);
        }

        private void Stock(params string[] ids)
        {
            foreach (string id in ids)
            {
                Assert.IsTrue(_session.AccessoryStock.Add(id, 10, Money.FromTl(500)).IsSuccess, id);
            }

            _flow.Refresh();
        }

        private string TextOf(string name)
        {
            return Find(name).GetComponentInChildren<Text>().text;
        }

        private void Click(string name)
        {
            Find(name).GetComponent<Button>().onClick.Invoke();
        }

        // A: talep yok → panel yok
        [Test]
        public void IfTheCustomerAsksForNothing_NoAddOnPanelAppears_AndTheResultContinuesDirectly()
        {
            CompleteSaleWhere(r => r.Count == 0);

            Assert.IsTrue(Find("SaleScreen").gameObject.activeSelf);
            Assert.IsNull(Find("AddOnPanel"));
            Assert.IsNull(Find("AddOnRequest"));
            Assert.AreEqual(0, CountNamed("AddButton_"), "hiç Ekle düğmesi yok");
            Assert.AreEqual(TurkishTexts.SaleDoneButton, TextOf("Reply_" + SaleReplyKind.Continue));
            Click("Reply_" + SaleReplyKind.Continue);
            Assert.AreEqual(SaleMode.Lobby, _flow.SaleScreen.Mode);
            Assert.AreEqual(0, Count(_session, TransactionTypeIds.AccessorySale));
        }

        // B: talep var → panel var (müşterinin gerçek sözü + AKSESUAR TALEBİ)
        [Test]
        public void IfTheCustomerAsks_TheRequestPanelAppears_WithTheCustomersWords()
        {
            CompleteSaleWhere(r => r.Count >= 1);

            Assert.IsNotNull(Find("AddOnPanel"));
            Assert.IsNotNull(Find("AddOnRequest"));
            StringAssert.StartsWith("AKSESUAR TALEBİ", Find("AddOnTitle").GetComponent<Text>().text);
            Assert.AreEqual(_flow.SaleScreen.AddOn.RequestLine, Find("AddOnRequestLine").GetComponent<Text>().text);
            Assert.IsNotEmpty(Find("AddOnRequestLine").GetComponent<Text>().text);
            Assert.IsNull(Find("AddOnSubtitle"), "eski 'Yanında bir aksesuar ister misiniz?' paneli yok");
        }

        // C, D: yalnızca istenen aksesuarlar görünür
        [Test]
        public void OnlyTheRequestedAccessories_AreShown_EvenWhenEverythingIsInStock()
        {
            CompleteSaleWhere(r => r.Count == 2);
            Stock(_content.Accessories.Definitions.Select(d => d.Id).ToArray());

            Assert.AreEqual(2, CountNamed("AddOn_accessory."), "iki istek, iki kart");
            foreach (AccessoryDefinition d in _content.Accessories.Definitions)
            {
                if (_requested.Contains(d.Id))
                {
                    Assert.IsNotNull(Find("AddOn_" + d.Id), d.Id);
                    Assert.IsNotNull(Find("AddButton_" + d.Id), d.Id);
                }
                else
                {
                    Assert.IsNull(Find("AddOn_" + d.Id), d.Id + " istenmedi, satış seçeneği olmamalı");
                    Assert.IsNull(Find("AddButton_" + d.Id), d.Id);
                }
            }
        }

        // I: stok yok → disabled
        [Test]
        public void ARequestedAccessoryWithoutStock_IsDisabledAndSaysOutOfStock()
        {
            CompleteSaleWhere(r => r.Count >= 1);
            string id = _requested[0];

            Assert.IsFalse(Find("AddButton_" + id).GetComponent<Button>().interactable);
            Assert.AreEqual("Stokta yok", TextOf("AddButton_" + id));
            StringAssert.Contains("Stokta yok", Find("AddOn_" + id).Find("Detail").GetComponent<Text>().text);
        }

        // E: talep edilen satılır; kart "Eklendi" olur
        [Test]
        public void ARequestedAccessory_IsSold_StockDrops_AndTheCardShowsAdded()
        {
            CompleteSaleWhere(r => r.Count >= 1);
            string id = _requested[0];
            Stock(id);
            Money cash = _session.Api.GetCash();
            Money retail = _content.Accessories.Definitions.Single(d => d.Id == id).RetailPrice;

            Click("AddButton_" + id);

            Assert.AreEqual(9, _session.AccessoryStock.Quantity(id));
            Assert.AreEqual(cash + retail, _session.Api.GetCash());
            Assert.AreEqual(1, Count(_session, TransactionTypeIds.AccessorySale));
            Assert.IsNull(Find("AddButton_" + id), "eklenen istek için artık Ekle düğmesi yok");
            Assert.AreEqual("Eklendi", TextOf("AddedBadge_" + id));
            Assert.IsNotNull(Find("AddOnFeedback"), "kısa başarı geri bildirimi");
            StringAssert.Contains("1/" + _requested.Count, Find("AddOnTitle").GetComponent<Text>().text);
        }

        // F: kısmi satış, sonra devam
        [Test]
        public void ThePlayerCanSellOnlyPartOfTheRequest_AndContinue()
        {
            CompleteSaleWhere(r => r.Count >= 2);
            Stock(_requested.ToArray());
            Assert.AreEqual(TurkishTexts.AddOnDeclineButton, TextOf("Reply_" + SaleReplyKind.Continue), "hiç eklenmedi: İstemiyorum / Devam Et");

            Click("AddButton_" + _requested[0]);

            Assert.AreEqual(TurkishTexts.AddOnContinueButton, TextOf("Reply_" + SaleReplyKind.Continue));
            Assert.IsNotNull(Find("AddButton_" + _requested[1]), "diğer istek hâlâ satılabilir");
            Click("Reply_" + SaleReplyKind.Continue);

            Assert.AreEqual(SaleMode.Lobby, _flow.SaleScreen.Mode);
            Assert.IsNull(Find("AddOnPanel"));
            Assert.AreEqual(1, Count(_session, TransactionTypeIds.AccessorySale));
            Assert.AreEqual(10, _session.AccessoryStock.Quantity(_requested[1]), "satılmayan istek stokta kaldı");
        }

        // Aynı aksesuar ikinci kez satılamaz (Play Mode'da bildirilen "sınırsız ekleniyor" hatasına karşı)
        [Test]
        public void TheSameAccessory_CannotBeSoldASecondTime_NotEvenByRepeatedOrStaleClicks()
        {
            CompleteSaleWhere(r => r.Count >= 1);
            string id = _requested[0];
            Stock(id);
            Money retail = _content.Accessories.Definitions.Single(d => d.Id == id).RetailPrice;

            Click("AddButton_" + id);
            Assert.AreEqual(1, Count(_session, TransactionTypeIds.AccessorySale));
            Assert.IsNull(Find("AddButton_" + id), "satılan kartta Ekle düğmesi kalmadı");

            // Eski (yenilenmeden önce yakalanmış) bir düğme ya da çift tıklama aynı yolu kullanır: UiFlow → API; oyun reddeder.
            for (int i = 0; i < 5; i++)
            {
                Result<AccessorySaleReceipt> again = _flow.SaleAddAccessory(id);
                Assert.AreEqual("addon.request_limit", again.ErrorCode, "tekrar " + i);
            }

            Assert.AreEqual(1, Count(_session, TransactionTypeIds.AccessorySale));
            Assert.AreEqual(9, _session.AccessoryStock.Quantity(id));
            Assert.AreEqual(retail, _session.Api.GetAccessoryAddOns().AccessoryRevenue);
            Assert.AreEqual("Aksesuarlar: " + MoneyFormatter.Format(retail), TextOf("AccessoriesLine"));
            Assert.AreEqual("Bu aksesuar zaten eklendi.", TextOf("Message"), "UI oyunun reddini gösterir");
        }

        // Her düğmeye defalarca basılsa da satış sayısı müşterinin talebini aşmaz
        [Test]
        public void PressingEveryButtonRepeatedly_NeverSellsMoreThanTheCustomerAskedFor()
        {
            CompleteSaleWhere(r => r.Count >= 2);
            Stock(_content.Accessories.Definitions.Select(d => d.Id).ToArray());

            for (int round = 0; round < 4; round++)
            {
                foreach (AccessoryDefinition d in _content.Accessories.Definitions)
                {
                    if (Find("AddButton_" + d.Id) != null)
                    {
                        Click("AddButton_" + d.Id);
                    }

                    _flow.SaleAddAccessory(d.Id); // düğmesi olmayan (istenmeyen ya da eklenmiş) için de doğrudan dene
                }
            }

            Assert.AreEqual(_requested.Count, Count(_session, TransactionTypeIds.AccessorySale), "tam olarak istenen kadar");
            Assert.LessOrEqual(Count(_session, TransactionTypeIds.AccessorySale), AccessoryRequestPolicy.MaxRequests);
            Assert.AreEqual(0, CountNamed("AddButton_"));
            long expected = _requested.Sum(id => _content.Accessories.Definitions.Single(d => d.Id == id).RetailPrice.Tl);
            Assert.AreEqual(expected, _session.Api.GetAccessoryAddOns().AccessoryRevenue.Tl);
        }

        // G: 5 talep sınırı
        [Test]
        public void AtMostFiveAccessories_AreOffered_AndAfterTheFifthNoAddButtonIsLeft()
        {
            CompleteSaleWhere(r => r.Count == AccessoryRequestPolicy.MaxRequests);
            Stock(_content.Accessories.Definitions.Select(d => d.Id).ToArray());

            Assert.AreEqual(5, CountNamed("AddOn_accessory."), "6 aksesuardan yalnızca istenen 5'i görünür");
            Assert.AreEqual(5, CountNamed("AddButton_"));

            foreach (string id in _requested.ToArray())
            {
                Click("AddButton_" + id);
            }

            Assert.AreEqual(5, Count(_session, TransactionTypeIds.AccessorySale));
            Assert.AreEqual(0, CountNamed("AddButton_"), "5/5: daha fazla Ekle yok");
            Assert.AreEqual(5, CountNamed("AddedBadge_"));
            StringAssert.Contains("5/5", Find("AddOnTitle").GetComponent<Text>().text);
        }

        // J, K, L: toplamlar ve telefon satışı
        [Test]
        public void TheTotalsFollowTheGame_AndThePhoneSaleIsNotRepeated()
        {
            CompleteSaleWhere(r => r.Count >= 2);
            Stock(_requested.ToArray());
            AddOnPanelViewModel before = _flow.SaleScreen.AddOn;
            int phoneSales = Count(_session, TransactionTypeIds.Sale);
            string customer = _flow.SaleScreen.CustomerName;
            string title = _flow.SaleScreen.Title;

            Click("AddButton_" + _requested[0]);
            Click("AddButton_" + _requested[1]);

            AccessoryAddOnView api = _session.Api.GetAccessoryAddOns();
            Assert.AreEqual(phoneSales, Count(_session, TransactionTypeIds.Sale));
            Assert.AreEqual("Telefon: " + MoneyFormatter.Format(before.PhonePrice), TextOf("PhoneLine"));
            Assert.AreEqual("Aksesuarlar: " + MoneyFormatter.Format(api.AccessoryRevenue), TextOf("AccessoriesLine"));
            Assert.AreEqual("Toplam: " + MoneyFormatter.Format(before.PhonePrice + api.AccessoryRevenue), TextOf("TotalLine"));
            Assert.AreEqual("Aksesuar kârı: " + MoneyFormatter.Format(api.AccessoryProfit), TextOf("AccessoryProfitLine"));
            Assert.AreEqual("Toplam kâr: " + MoneyFormatter.Format(api.TotalProfit), TextOf("TotalProfitLine"));
            Assert.AreEqual(customer, _flow.SaleScreen.CustomerName, "müşteri aynı");
            Assert.AreEqual(title, _flow.SaleScreen.Title, "telefon satış sonucu aynı");
            Assert.AreEqual(before.PhoneSaleRecordId, _flow.SaleScreen.AddOn.PhoneSaleRecordId);
        }

        // N, O: mevcut müşteri satış ekranı
        [Test]
        public void TheCustomerSaleScreen_StillShowsPortraitPhoneAndTheDealDialogue()
        {
            CompleteSaleWhere(r => r.Count >= 1);

            Assert.IsTrue(Find("SaleScreen").gameObject.activeSelf);
            Assert.IsNotNull(Find("Stage"));
            Assert.IsNotNull(Find("PortraitPanel"));
            Assert.IsNotNull(Find("PhonePanel"));
            Assert.IsNotNull(Find("Subtitle"), "telefon anlaşma sözü korunur");
            Assert.IsNotNull(Find("Reply_" + SaleReplyKind.Continue));
        }
    }
}
