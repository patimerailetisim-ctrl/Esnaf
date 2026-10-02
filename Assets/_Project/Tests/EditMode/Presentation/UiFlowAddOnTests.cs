using System;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Accessories;
using Esnaf.Domain.Business;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Game;
using Esnaf.Domain.Market;
using Esnaf.Domain.Negotiation;
using Esnaf.Presentation;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Presentation
{
    /// <summary>
    /// Müşteri talepli aksesuar paneli (UiFlow, Gün 11.3.4): panel yalnızca müşteri aksesuar İSTEDİYSE açılır ve yalnızca istenen aksesuarları gösterir;
    /// hepsi IGameApi üstünde, UI'da kural yoktur.
    /// </summary>
    public class UiFlowAddOnTests
    {
        private const string Supplier = "supplier.ucuz_toptan";
        private const string Case = "accessory.phone_case";
        private const string Adapter = "accessory.charger_adapter";

        private static UiFlow Flow(GameSession s)
        {
            return new UiFlow(s.Api, new ContentPresentation(s.Content), s.Bus);
        }

        /// <summary>
        /// Bulunan satışın tohumunda, bu kez UI akışıyla (müşteri ekranı → StartSale → selam → 10 ₺ iste) anlaşmayı yapar. Satış API ile aynı adımları
        /// izlediği için talep aynıdır; bu da doğrulanır.
        /// </summary>
        private static UiFlow DealDone(AddOnDeal found, out GameSession s)
        {
            s = AddOnDeals.Prepare(found.Seed, found.Bumps);
            MarketListing guided = s.Market.Listings.Single(l => l.IsGuided);
            Assert.IsTrue(s.Api.StartNegotiation(guided.ListingId).IsSuccess);
            Assert.IsTrue(s.Api.MakeOffer(Money.FromTl(5800)).IsSuccess);
            Assert.IsTrue(s.Api.SetPrice(guided.InstanceId, Money.FromTl(5900)).IsSuccess);
            UiFlow flow = Flow(s);
            Assert.IsTrue(flow.OpenCustomers());
            Assert.IsTrue(flow.StartSale(s.Api.GetCustomers()[0].CustomerId).IsSuccess);
            Assert.IsTrue(flow.SaleGreet());
            flow.AdjustSalePrice(-100000);
            Result<SaleView> deal = flow.SaleAsk();
            Assert.IsTrue(deal.IsSuccess, deal.ErrorCode);
            Assert.AreEqual(NegotiationPhase.Deal, deal.Value.Phase);
            long recordId = s.EconomyState.Ledger.Records.Last(r => r.TypeId == TransactionTypeIds.Sale).Id;
            CollectionAssert.AreEqual(found.Requested.ToArray(), s.AccessoryAddOns.RequestedAccessories(recordId).ToArray(), "UI akışında da aynı talep");
            return flow;
        }

        private static void Stock(GameSession s, params string[] ids)
        {
            foreach (string id in ids)
            {
                // Paket gibi: kılıf 20 × 70, adaptör 10 × 150, diğerleri 10 × 50 (her aksesuar toptancıdan her gün alınamaz; stok doğrudan konur).
                int quantity = id == Case ? 20 : 10;
                Money cost = id == Case ? Money.FromTl(1400) : id == Adapter ? Money.FromTl(1500) : Money.FromTl(500);
                Assert.IsTrue(s.AccessoryStock.Add(id, quantity, cost).IsSuccess, id);
            }
        }

        private static int Count(GameSession s, string typeId)
        {
            return s.EconomyState.Ledger.Records.Count(r => r.TypeId == typeId);
        }

        // ---------- A, B ----------

        [Test]
        public void IfTheCustomerAskedForNothing_NoAddOnPanelOpens_AndTheSaleResultContinuesDirectly()
        {
            GameSession s;
            using (UiFlow flow = DealDone(AddOnDeals.NoRequest(), out s))
            {
                SaleScreenViewModel screen = flow.SaleScreen;

                Assert.AreEqual(SaleMode.Done, screen.Mode);
                Assert.IsNull(screen.AddOn, "talep yok → panel yok");
                CollectionAssert.AreEqual(new[] { SaleReplyKind.Continue }, screen.Replies.Select(r => r.Kind).ToArray());
                Assert.AreEqual(TurkishTexts.SaleDoneButton, screen.Replies[0].Text, "mevcut sonuç ekranı aynı");
                Assert.AreEqual("ui.no_addon_panel", flow.SaleAddAccessory(Case).ErrorCode);
                Assert.AreEqual(0, Count(s, TransactionTypeIds.AccessorySale), "aksesuar satışı oluşmaz");
                Assert.IsTrue(flow.SaleNext());
            }
        }

        [Test]
        public void IfTheCustomerAsked_TheAddOnPanelOpens_WithTheRequestTitleAndTheCustomersWords()
        {
            AddOnDeal found = AddOnDeals.Requesting(Case);
            GameSession s;
            using (UiFlow flow = DealDone(found, out s))
            {
                AddOnPanelViewModel panel = flow.SaleScreen.AddOn;

                Assert.IsNotNull(panel);
                Assert.AreEqual("AKSESUAR TALEBİ", panel.Title);
                StringAssert.DoesNotContain("ister misiniz", panel.Title);
                Assert.IsNotEmpty(panel.RequestLine);
                Assert.AreEqual(flow.Content.CustomerName(found.Customer.CustomerId, found.Customer.NpcId), panel.CustomerName);
                Assert.AreEqual(s.Api.GetAccessoryAddOns().PhoneSaleRecordId, panel.PhoneSaleRecordId);
            }
        }

        // ---------- C, D ----------

        [Test]
        public void TheCards_AreExactlyTheRequestedAccessories_AndNoOtherAccessoryIsOffered()
        {
            AddOnDeal found = AddOnDeals.WithCount(3);
            GameSession s;
            using (UiFlow flow = DealDone(found, out s))
            {
                foreach (AccessoryDefinition d in s.Content.Accessories.Definitions)
                {
                    s.AccessoryStock.Add(d.Id, 5, Money.FromTl(100)); // hepsi stokta: yine de yalnızca istenenler görünür
                }

                flow.Refresh();
                AddOnPanelViewModel panel = flow.SaleScreen.AddOn;

                Assert.AreEqual(3, panel.Cards.Count);
                CollectionAssert.AreEquivalent(found.Requested.ToArray(), panel.Cards.Select(c => c.AccessoryId).ToArray());
                foreach (AccessoryDefinition d in s.Content.Accessories.Definitions.Where(d => !found.Requested.Contains(d.Id)))
                {
                    Assert.IsFalse(panel.Cards.Any(c => c.AccessoryId == d.Id), d.Id + " istenmedi, görünmemeli");
                }

                Assert.AreEqual("0/3", panel.ProgressLine);
                Assert.AreEqual(3, panel.RequestedCount);
            }
        }

        [Test]
        public void ACardShowsNamePriceAndStock_AndAnOutOfStockCardIsDisabledWithTheOutOfStockText()
        {
            AddOnDeal found = AddOnDeals.Requesting(Case);
            GameSession s;
            using (UiFlow flow = DealDone(found, out s))
            {
                AddOnCardViewModel empty = flow.SaleScreen.AddOn.Cards.Single(c => c.AccessoryId == Case);
                Assert.IsFalse(empty.IsButtonEnabled);
                Assert.AreEqual("Stokta yok", empty.StockLine);
                Assert.AreEqual("Stokta yok", empty.ButtonText);

                Stock(s, Case);
                flow.Refresh();
                AddOnCardViewModel card = flow.SaleScreen.AddOn.Cards.Single(c => c.AccessoryId == Case);
                Assert.AreEqual(AccessoryName(s, Case), card.Name);
                Assert.AreEqual(MoneyFormatter.Format(Money.FromTl(160)), card.PriceLine);
                Assert.AreEqual("Stok: 20", card.StockLine);
                Assert.AreEqual("Ekle", card.ButtonText);
                Assert.IsTrue(card.IsButtonEnabled);
            }
        }

        private static string AccessoryName(GameSession s, string id)
        {
            return s.Content.Accessories.Definitions.Single(d => d.Id == id).Name;
        }

        [Test]
        public void TheRequestLine_NamesTheRequestedAccessories_AndOnlyThose()
        {
            AddOnDeal found = AddOnDeals.WithCount(2);
            GameSession s;
            using (UiFlow flow = DealDone(found, out s))
            {
                string line = flow.SaleScreen.AddOn.RequestLine.ToLowerInvariant();

                foreach (AccessoryDefinition d in s.Content.Accessories.Definitions)
                {
                    string name = d.Name.Replace('I', 'ı').Replace('İ', 'i').ToLowerInvariant();
                    if (found.Requested.Contains(d.Id))
                    {
                        StringAssert.Contains(name, line, d.Id);
                    }
                    else
                    {
                        StringAssert.DoesNotContain(name, line, d.Id);
                    }
                }
            }
        }

        // ---------- E, F, G ----------

        [Test]
        public void ARequestedAccessory_IsSold_ThroughTheApi_StockDrops_AndTheCardTurnsIntoAdded()
        {
            AddOnDeal found = AddOnDeals.Requesting(Case);
            GameSession s;
            using (UiFlow flow = DealDone(found, out s))
            {
                Stock(s, Case);
                flow.Refresh();
                Money cash = s.Api.GetCash();
                int cards = flow.SaleScreen.AddOn.Cards.Count;

                Result<AccessorySaleReceipt> r = flow.SaleAddAccessory(Case);

                Assert.IsTrue(r.IsSuccess, r.ErrorCode);
                Assert.AreEqual(19, s.AccessoryStock.Quantity(Case));
                Assert.AreEqual(cash + Money.FromTl(160), s.Api.GetCash());
                Assert.AreEqual(1, Count(s, TransactionTypeIds.AccessorySale));
                AddOnPanelViewModel panel = flow.SaleScreen.AddOn;
                Assert.AreEqual(cards, panel.Cards.Count, "istek sayısı değişmez");
                AddOnCardViewModel card = panel.Cards.Single(c => c.AccessoryId == Case);
                Assert.IsTrue(card.IsAdded);
                Assert.AreEqual("Eklendi", card.ButtonText);
                Assert.IsFalse(card.IsButtonEnabled, "eklenene tekrar Ekle yok");
                Assert.AreEqual(1, panel.AddedCount);
                Assert.IsFalse(panel.FeedbackIsError);
                StringAssert.Contains("eklendi", panel.Feedback);
            }
        }

        [Test]
        public void ThePlayerCanSellOnlyPartOfTheRequest_ThenContinue()
        {
            AddOnDeal found = AddOnDeals.WithCount(4);
            GameSession s;
            using (UiFlow flow = DealDone(found, out s))
            {
                Stock(s, found.Requested.ToArray());
                flow.Refresh();
                Assert.AreEqual(TurkishTexts.AddOnDeclineButton, flow.SaleScreen.Replies[0].Text, "hiç eklenmedi: İstemiyorum / Devam Et");

                Assert.IsTrue(flow.SaleAddAccessory(found.Requested[0]).IsSuccess);
                Assert.IsTrue(flow.SaleAddAccessory(found.Requested[1]).IsSuccess);

                Assert.AreEqual("2/4", flow.SaleScreen.AddOn.ProgressLine);
                Assert.AreEqual(TurkishTexts.AddOnContinueButton, flow.SaleScreen.Replies[0].Text);
                Assert.AreEqual(2, flow.SaleScreen.AddOn.Cards.Count(c => c.IsButtonEnabled), "2 istek hâlâ satılabilir");
                Assert.IsTrue(flow.SaleNext());
                Assert.IsNull(flow.SaleScreen.AddOn);
                Assert.AreEqual(2, Count(s, TransactionTypeIds.AccessorySale));
            }
        }

        [Test]
        public void WhenEveryRequestIsAdded_NoAddButtonIsLeft()
        {
            AddOnDeal found = AddOnDeals.WithCount(2);
            GameSession s;
            using (UiFlow flow = DealDone(found, out s))
            {
                Stock(s, found.Requested.ToArray());
                flow.Refresh();
                foreach (string id in found.Requested)
                {
                    Assert.IsTrue(flow.SaleAddAccessory(id).IsSuccess);
                }

                AddOnPanelViewModel panel = flow.SaleScreen.AddOn;
                Assert.AreEqual("2/2", panel.ProgressLine);
                Assert.IsTrue(panel.Cards.All(c => !c.IsButtonEnabled && c.ButtonText != "Ekle"));
                Assert.AreEqual("ui.no_addon_panel".Length > 0, true);
                Assert.AreEqual("addon.request_limit", flow.SaleAddAccessory(found.Requested[0]).ErrorCode);
            }
        }

        // ---------- tekrar tıklama: sınırsız satış yok ----------

        [Test]
        public void ClickingTheSameAccessoryManyTimes_SellsItOnlyOnce_AndRevenueNeverGrows()
        {
            AddOnDeal found = AddOnDeals.Requesting(Case);
            GameSession s;
            using (UiFlow flow = DealDone(found, out s))
            {
                Stock(s, Case);
                flow.Refresh();
                Assert.IsTrue(flow.SaleAddAccessory(Case).IsSuccess);
                Money revenue = s.Api.GetAccessoryAddOns().AccessoryRevenue;

                for (int i = 0; i < 10; i++)
                {
                    Assert.AreEqual("addon.request_limit", flow.SaleAddAccessory(Case).ErrorCode, "tıklama " + i);
                }

                Assert.AreEqual(1, Count(s, TransactionTypeIds.AccessorySale));
                Assert.AreEqual(19, s.AccessoryStock.Quantity(Case));
                Assert.AreEqual(revenue, s.Api.GetAccessoryAddOns().AccessoryRevenue);
                Assert.AreEqual(Money.FromTl(160), flow.SaleScreen.AddOn.AccessoriesPrice);
            }
        }

        [Test]
        public void EverySaleInTheGame_NeverGetsMoreAddOnsThanTheCustomerAskedFor_NoMatterHowOftenEverythingIsClicked()
        {
            int sales = 0;
            for (int bumps = 0; bumps < 12; bumps++)
            {
                for (ulong seed = 1; seed <= 20; seed++)
                {
                    AddOnDeal found = AddOnDeals.Try(seed, bumps);
                    if (found == null)
                    {
                        continue;
                    }

                    GameSession s;
                    using (UiFlow flow = DealDone(found, out s))
                    {
                        foreach (AccessoryDefinition d in s.Content.Accessories.Definitions)
                        {
                            s.AccessoryStock.Add(d.Id, 10, Money.FromTl(500));
                        }

                        flow.Refresh();
                        for (int round = 0; round < 4; round++)
                        {
                            foreach (AccessoryDefinition d in s.Content.Accessories.Definitions)
                            {
                                flow.SaleAddAccessory(d.Id);
                            }
                        }

                        string[] sold = s.AccessoryAddOns.AddOnsOf(found.SaleRecordId).Select(r => r.DefinitionId).ToArray();
                        Assert.AreEqual(sold.Length, sold.Distinct().Count(), "aynı aksesuar iki kez satılmadı");
                        Assert.LessOrEqual(sold.Length, found.Requested.Count, "istenenden fazla satılmadı");
                        Assert.LessOrEqual(sold.Length, AccessoryRequestPolicy.MaxRequests);
                        Assert.IsTrue(sold.All(found.Requested.Contains), "istenmeyen aksesuar satılmadı");
                        Assert.LessOrEqual(s.Api.GetAccessoryAddOns().AccessoryRevenue.Tl, 1630L, "6 aksesuarın toplam fiyatını aşamaz");
                        sales++;
                    }
                }
            }

            Assert.Greater(sales, 100);
        }

        // ---------- G ----------

        [Test]
        public void TheRequest_NeverHasMoreThanFiveCards()
        {
            AddOnDeal found = AddOnDeals.WithCount(5);
            GameSession s;
            using (UiFlow flow = DealDone(found, out s))
            {
                Assert.AreEqual(5, flow.SaleScreen.AddOn.Cards.Count);
                Assert.AreEqual("0/5", flow.SaleScreen.AddOn.ProgressLine);
                Assert.LessOrEqual(flow.SaleScreen.AddOn.Cards.Count, AccessoryRequestPolicy.MaxRequests);
            }
        }

        // ---------- I ----------

        [Test]
        public void AnOutOfStockRequest_CannotBeSold_AndTheRefusalComesFromTheGame()
        {
            AddOnDeal found = AddOnDeals.Requesting(Case);
            GameSession s;
            using (UiFlow flow = DealDone(found, out s))
            {
                Money cash = s.Api.GetCash();
                string digest = s.Api.GetStateDigest();

                Result<AccessorySaleReceipt> r = flow.SaleAddAccessory(Case);

                Assert.AreEqual("stock.insufficient", r.ErrorCode);
                Assert.AreEqual(cash, s.Api.GetCash());
                Assert.AreEqual(digest, s.Api.GetStateDigest());
                Assert.IsTrue(flow.SaleScreen.AddOn.FeedbackIsError);
                Assert.AreEqual("Stokta yok.", flow.SaleScreen.AddOn.Feedback);
            }
        }

        [Test]
        public void AnAccessoryTheCustomerDidNotAskFor_CannotBeAddedThroughTheUiFlowEither()
        {
            AddOnDeal found = AddOnDeals.WithCount(1);
            GameSession s;
            using (UiFlow flow = DealDone(found, out s))
            {
                foreach (AccessoryDefinition d in s.Content.Accessories.Definitions)
                {
                    s.AccessoryStock.Add(d.Id, 2, Money.FromTl(100));
                }

                string other = s.Content.Accessories.Definitions.First(d => d.Id != found.Requested[0]).Id;
                string digest = s.Api.GetStateDigest();

                Result<AccessorySaleReceipt> r = flow.SaleAddAccessory(other);

                Assert.AreEqual("addon.not_in_request", r.ErrorCode);
                Assert.AreEqual(digest, s.Api.GetStateDigest());
                Assert.IsTrue(flow.SaleScreen.AddOn.FeedbackIsError);
            }
        }

        // ---------- J, K, L ----------

        [Test]
        public void TheAddOn_NeverChangesThePhoneSale_OrRepeatsItsTransaction_AndTheCustomerStaysTheSame()
        {
            AddOnDeal found = AddOnDeals.WithCount(2);
            GameSession s;
            using (UiFlow flow = DealDone(found, out s))
            {
                Stock(s, found.Requested.ToArray());
                flow.Refresh();
                SaleScreenViewModel before = flow.SaleScreen;
                long recordId = before.AddOn.PhoneSaleRecordId;
                TransactionRecord sale;
                s.EconomyState.Ledger.TryGetById(recordId, out sale);
                int phoneSales = Count(s, TransactionTypeIds.Sale);

                foreach (string id in found.Requested)
                {
                    flow.SaleAddAccessory(id);
                }

                SaleScreenViewModel after = flow.SaleScreen;
                TransactionRecord now;
                s.EconomyState.Ledger.TryGetById(recordId, out now);
                Assert.AreEqual(phoneSales, Count(s, TransactionTypeIds.Sale), "telefon transaction'ı tekrar oluşmadı");
                Assert.AreEqual(sale.Amount, now.Amount, "telefon fiyatı aynı");
                Assert.AreEqual(sale.SaleCostBasis, now.SaleCostBasis);
                Assert.AreEqual(recordId, after.AddOn.PhoneSaleRecordId);
                Assert.AreEqual(before.AddOn.PhonePrice, after.AddOn.PhonePrice);
                Assert.AreEqual(before.AddOn.PhoneProfit, after.AddOn.PhoneProfit);
                Assert.AreEqual(before.CustomerName, after.CustomerName);
                Assert.AreEqual(before.NpcId, after.NpcId);
                Assert.AreEqual(before.ModelTitle, after.ModelTitle);
                Assert.AreEqual(before.DefinitionId, after.DefinitionId);
                Assert.AreEqual(before.Title, after.Title);
                Assert.AreEqual(before.CustomerLine, after.CustomerLine, "mevcut müşteri diyaloğu bozulmaz");
                Assert.AreEqual(found.Customer.NpcId, s.Api.GetAccessoryAddOns().BuyerNpcId);
            }
        }

        [Test]
        public void TheRevenueAndProfit_FollowTheGame()
        {
            AddOnDeal found = AddOnDeals.WithCount(3);
            GameSession s;
            using (UiFlow flow = DealDone(found, out s))
            {
                Stock(s, found.Requested.ToArray());
                flow.Refresh();
                Money phone = flow.SaleScreen.AddOn.PhonePrice;
                Money phoneProfit = flow.SaleScreen.AddOn.PhoneProfit;

                foreach (string id in found.Requested)
                {
                    Assert.IsTrue(flow.SaleAddAccessory(id).IsSuccess);
                }

                AddOnPanelViewModel panel = flow.SaleScreen.AddOn;
                AccessoryAddOnView api = s.Api.GetAccessoryAddOns();
                Money retail = Money.Zero;
                foreach (string id in found.Requested)
                {
                    retail += s.Content.Accessories.Definitions.Single(d => d.Id == id).RetailPrice;
                }

                Assert.AreEqual(retail, panel.AccessoriesPrice);
                Assert.AreEqual(phone, panel.PhonePrice);
                Assert.AreEqual(phone + retail, panel.TotalPrice);
                Assert.AreEqual(api.AccessoryRevenue, panel.AccessoriesPrice);
                Assert.AreEqual(phoneProfit, panel.PhoneProfit);
                Assert.AreEqual(api.AccessoryProfit, panel.AccessoryProfit);
                Assert.AreEqual(api.TotalProfit, panel.TotalProfit);
                Assert.AreEqual(s.AccessoryAddOns.TotalProfitOfSale(panel.PhoneSaleRecordId).Value, panel.TotalProfit);
                StringAssert.StartsWith("Telefon:", panel.PhoneLine);
                StringAssert.StartsWith("Aksesuarlar:", panel.AccessoriesLine);
                StringAssert.StartsWith("Toplam:", panel.TotalLine);
                StringAssert.StartsWith("Toplam kâr:", panel.TotalProfitLine);
            }
        }

        [Test]
        public void ThePhoneSaleResultScreen_StillShowsTheDeal_AndTheContinueReply()
        {
            GameSession s;
            using (UiFlow flow = DealDone(AddOnDeals.Requesting(Case), out s))
            {
                SaleScreenViewModel screen = flow.SaleScreen;

                Assert.AreEqual(SaleMode.Done, screen.Mode);
                CollectionAssert.AreEqual(new[] { SaleReplyKind.Continue }, screen.Replies.Select(r => r.Kind).ToArray());
                Assert.AreEqual(TurkishTexts.SaleDeal(screen.AddOn.PhonePrice), screen.Title);
                Assert.IsNotEmpty(screen.CustomerLine, "telefon anlaşma sözü korunur");
            }
        }

        // ---------- M ----------

        [Test]
        public void TheAddOnUi_UsesNoRandomness()
        {
            AddOnDeal found = AddOnDeals.WithCount(2);
            GameSession s;
            using (UiFlow flow = DealDone(found, out s))
            {
                Stock(s, found.Requested.ToArray());
                var rng = s.Capture().Rng;

                flow.Refresh();
                foreach (string id in found.Requested)
                {
                    flow.SaleAddAccessory(id);
                }

                flow.SaleNext();

                Assert.IsNull(DeepCompare.FirstDifference(rng, s.Capture().Rng));
            }
        }

        // ---------- diyalog ----------

        [Test]
        public void TheRequestDialogue_IsNaturalTurkish_ForOneTwoAndManyAccessories_AndStable()
        {
            string[] one = { "Telefon Kılıfı" };
            string[] two = { "Telefon Kılıfı", "Kırılmaz Cam" };
            string[] many = { "Telefon Kılıfı", "Kırılmaz Cam", "Şarj Kablosu", "Şarj Adaptörü" };

            Assert.AreEqual("Telefon tamam abi. Bir de telefon kılıfı var mı?", SaleDialogue.AccessoryRequest("easygoing", one));
            Assert.AreEqual("Abi bir de telefon kılıfı ve kırılmaz cam alayım.", SaleDialogue.AccessoryRequest("easygoing", two));
            StringAssert.Contains("telefon kılıfı, kırılmaz cam, şarj kablosu ve şarj adaptörü", SaleDialogue.AccessoryRequest("easygoing", many));
            Assert.AreEqual(SaleDialogue.AccessoryRequest("hurried", many), SaleDialogue.AccessoryRequest("hurried", many));
            Assert.AreEqual(string.Empty, SaleDialogue.AccessoryRequest("hurried", new string[0]));
            foreach (string personality in new[] { "haggler", "hurried", "indecisive", "tech_enthusiast", "price_focused", "easygoing", "informed_buyer", "showoff", "budget_limited", "trust_seeker", null })
            {
                Assert.IsNotEmpty(SaleDialogue.AccessoryRequest(personality, one), personality);
                Assert.IsNotEmpty(SaleDialogue.AccessoryRequest(personality, two), personality);
                Assert.IsNotEmpty(SaleDialogue.AccessoryRequest(personality, many), personality);
            }
        }

        [Test]
        public void TheAddOnTexts_AreFullTurkish_AndErrorsMapToReadableMessages()
        {
            foreach (string code in new[]
            {
                "stock.insufficient", "addon.no_sale", "addon.sale_closed", "addon.not_requested", "addon.not_in_request", "addon.request_limit",
                "accessory.unknown", "amount.invalid"
            })
            {
                StringAssert.DoesNotContain("Bir sorun", TurkishTexts.AddOnError(code), code);
            }
        }
    }
}
