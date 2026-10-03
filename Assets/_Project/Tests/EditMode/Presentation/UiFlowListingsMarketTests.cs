using System.IO;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Appraisal;
using Esnaf.Domain.Game;
using Esnaf.Domain.Market;
using Esnaf.Presentation;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Presentation
{
    /// <summary>
    /// Gün 13.3 — İlanlar pazarı: kartlarda telefon görseli, model, kondisyon, satıcı, istenen fiyat, tahmini değer ve "İncele"; ilan detayı ve mevcut ekspertiz / pazarlık / BuyNow
    /// akışlarına bağlantı, temiz geri dönüş, boş ilan durumu. Tahmini değer yalnızca ekspertiz sonucundan gelir (gizli R/gerçek değer sızmaz); "kârlı/kötü ilan" kararı yoktur.
    /// </summary>
    public class UiFlowListingsMarketTests
    {
        private static GameSession New(ulong seed = 20260101UL)
        {
            return GameSession.NewGame(MarketHarness.RealContent(), seed);
        }

        private static UiFlow Flow(GameSession s)
        {
            return new UiFlow(s.Api, new ContentPresentation(s.Content), s.Bus);
        }

        // ---------- liste ----------

        [Test]
        public void TheList_ShowsTheMarketsListings_WithAllTheCardFields()
        {
            GameSession s = New();
            using (UiFlow flow = Flow(s))
            {
                var listings = s.Api.GetListings();

                Assert.AreEqual(listings.Count, flow.Listings.Count);
                for (int i = 0; i < listings.Count; i++)
                {
                    ListingView l = listings[i];
                    ListingRowViewModel row = flow.Listings[i];
                    Assert.AreEqual(l.ListingId, row.ListingId, "doğru ilan");
                    Assert.AreEqual(flow.Content.ModelName(l.DefinitionId), row.Title, "model");
                    Assert.AreEqual(TurkishTexts.Condition(l.AgeMonths, l.HasBox, l.HasInvoice, l.Tags.Contains(ListingTags.UrgentSale)), row.ConditionText, "kondisyon");
                    Assert.AreEqual(TurkishTexts.Seller(flow.Content.NpcName(l.SellerNpcId)), row.SellerText, "satıcı");
                    Assert.AreEqual(TurkishTexts.AskingFull(l.AskingPrice), row.AskingText, "istenen fiyat");
                    Assert.AreEqual("İncele", row.InspectButtonText);
                    Assert.AreEqual(TurkishTexts.EstimatedUnknown, row.EstimatedText, "ekspertiz yok: tahmin ekspertizle öğrenilir");
                    Assert.IsNull(flow.ListingsEmptyNote);
                }
            }
        }

        [Test]
        public void TheCardChips_AndPrice_AreBuiltFromPublicListingData_WithoutAHiddenConditionLabel()
        {
            GameSession s = New();
            using (UiFlow flow = Flow(s))
            {
                foreach (ListingView l in s.Api.GetListings())
                {
                    ListingRowViewModel row = flow.Listings.Single(r => r.ListingId == l.ListingId);
                    Assert.AreEqual(TurkishTexts.Age(l.AgeMonths), row.AgeChip);
                    Assert.AreEqual(l.HasBox ? "Kutu var" : "Kutu yok", row.BoxChip);
                    Assert.AreEqual(l.HasInvoice ? "Fatura var" : "Fatura yok", row.InvoiceChip);
                    Assert.AreEqual(l.Tags.Contains(ListingTags.UrgentSale) ? "Acil satış" : null, row.UrgentChip);
                    Assert.AreEqual("İstenen fiyat", row.AskingLabelText);
                    Assert.AreEqual(MoneyFormatter.Format(l.AskingPrice), row.PriceValueText, "altın vurgulu fiyat değeri");
                    foreach (string hidden in new[] { "Temiz", "Çok iyi", "Orta", "Kötü" })
                    {
                        StringAssert.DoesNotContain(hidden, row.AgeChip + row.BoxChip + row.InvoiceChip, "gizli durum etiketi uydurulmaz");
                    }
                }

                Assert.AreEqual("İkinci el telefon ilanları", TurkishTexts.ListingsSubtitle);
            }
        }

        [Test]
        public void EveryCard_UsesTheListingsOwnPhoneImage_AndThatImageExistsInThePhoneCatalog()
        {
            GameSession s = New();
            string phones = Path.Combine(Directory.GetParent(Directory.GetParent(TestPaths.ContentDataDirectory()).FullName).FullName, "Art", "Phones");
            var folders = Directory.GetDirectories(phones).Select(d => Path.GetFileName(d).ToLowerInvariant()).ToList();
            using (UiFlow flow = Flow(s))
            {
                foreach (ListingView l in s.Api.GetListings())
                {
                    ListingRowViewModel row = flow.Listings.Single(r => r.ListingId == l.ListingId);
                    Assert.AreEqual(l.DefinitionId, row.DefinitionId, "kart görseli ilanın modeli");
                    CollectionAssert.Contains(folders, PhoneAssetNaming.ModelKey(row.DefinitionId), "PhoneImageCatalog klasörü var: " + row.DefinitionId);
                }
            }
        }

        [Test]
        public void TheListsTexts_NeverPassAVerdictOnAListing()
        {
            GameSession s = New();
            using (UiFlow flow = Flow(s))
            {
                foreach (ListingRowViewModel row in flow.Listings)
                {
                    string all = (row.ConditionText + row.EstimatedText + row.AskingText + row.SellerText).ToLowerInvariant();
                    foreach (string verdict in new[] { "kârlı", "karlı", "fırsat", "tuzak", "kötü ilan", "iyi ilan", "ucuz", "pahalı" })
                    {
                        StringAssert.DoesNotContain(verdict, all);
                    }
                }
            }
        }

        // ---------- detay ----------

        [Test]
        public void TheDetail_OpensTheChosenListing_WithImageModelConditionSellerPriceAndAppraisalState()
        {
            GameSession s = New();
            using (UiFlow flow = Flow(s))
            {
                ListingView l = s.Api.GetListings()[1];

                Assert.IsTrue(flow.OpenListing(l.ListingId));

                Assert.AreEqual(UiScreen.Detail, flow.CurrentScreen);
                ListingDetailViewModel d = flow.Detail;
                Assert.AreEqual(l.ListingId, d.ListingId);
                Assert.AreEqual(flow.Content.ModelName(l.DefinitionId), d.Title);
                Assert.AreEqual(l.DefinitionId, d.DefinitionId, "büyük görsel ilanın modeli");
                Assert.AreEqual(TurkishTexts.Condition(l.AgeMonths, l.HasBox, l.HasInvoice, l.Tags.Contains(ListingTags.UrgentSale)), d.ConditionLine);
                Assert.AreEqual(TurkishTexts.Seller(flow.Content.NpcName(l.SellerNpcId)), d.SellerLine);
                Assert.AreEqual(TurkishTexts.AskingFull(l.AskingPrice), d.AskingLine);
                Assert.AreEqual(TurkishTexts.EstimatedUnknown, d.EstimatedLine);
                Assert.AreEqual(TurkishTexts.AppraisalNotDone, d.AppraisalStatusLine);
                Assert.IsNull(d.AppraisalRangeLine);
                Assert.AreEqual("Ekspertiz Yap", d.AppraisalButtonText);
                Assert.AreEqual("Pazarlık Yap", d.NegotiationButtonText);
                Assert.AreEqual(TurkishTexts.BuyNowLabel(l.AskingPrice), d.BuyButtonText);
                Assert.AreEqual("Geri", d.BackButtonText);
            }
        }

        // ---------- mevcut sistemlere bağlantı ----------

        [Test]
        public void Appraisal_GoesThroughTheExistingSystem_AndTheDetailAndCardShowItsResult()
        {
            GameSession s = New();
            Assert.IsTrue(s.Api.EndDay().IsSuccess); // s1 (Hızlı kontrol) 3. günden itibaren açılır
            Assert.IsTrue(s.Api.EndDay().IsSuccess);
            using (UiFlow flow = Flow(s))
            {
                ListingView l = s.Api.GetListings()[0];
                flow.OpenListing(l.ListingId);
                Assert.IsTrue(flow.OpenAppraisal());
                Assert.AreEqual(UiScreen.Appraisal, flow.CurrentScreen);

                flow.SelectLevel("s1");
                Result<AppraisalView> r = flow.PerformAppraisal();
                AppraisalView done = r.IsSuccess && r.Value.ValueRange != null ? r.Value : null;

                Assert.IsNotNull(done, "s1 ekspertizi değer aralığı verir");
                Assert.AreEqual(done.ResultId, s.Api.GetAppraisals(l.ListingId).Last().ResultId, "mevcut ekspertiz sistemi");
                flow.Back();
                Assert.AreEqual(UiScreen.Detail, flow.CurrentScreen);
                StringAssert.StartsWith("Ekspertiz: yapıldı", flow.Detail.AppraisalStatusLine);
                Assert.AreEqual(TurkishTexts.AppraisalValueRange(done.ValueRange.Min, done.ValueRange.Max), flow.Detail.AppraisalRangeLine);
                Assert.AreEqual(TurkishTexts.EstimatedFromAppraisal(done.ValueRange.Min, done.ValueRange.Max), flow.Detail.EstimatedLine, "tahmini değer = ekspertiz aralığı");
                flow.Back();
                Assert.AreEqual(TurkishTexts.EstimatedFromAppraisal(done.ValueRange.Min, done.ValueRange.Max), flow.Listings.Single(x => x.ListingId == l.ListingId).EstimatedText, "kart da aynı");
                Assert.AreEqual(TurkishTexts.EstimatedUnknown, flow.Listings.First(x => x.ListingId != l.ListingId).EstimatedText, "diğer ilanlar etkilenmez");
            }
        }

        [Test]
        public void Negotiation_GoesThroughTheExistingSystem()
        {
            GameSession s = New();
            using (UiFlow flow = Flow(s))
            {
                ListingView l = s.Api.GetListings()[0];
                flow.OpenListing(l.ListingId);

                Assert.IsTrue(flow.OpenNegotiation());

                Assert.AreEqual(UiScreen.Negotiation, flow.CurrentScreen);
                Assert.IsNotNull(s.Api.GetNegotiation(), "mevcut pazarlık sistemi açıldı");
                Assert.AreEqual(l.ListingId, s.Api.GetNegotiation().ListingId);
                flow.SetOfferText((l.AskingPrice.Tl / 2 / 10 * 10).ToString());
                Assert.IsTrue(flow.SubmitOffer().IsSuccess, "teklif mevcut sistemden geçer");
                flow.Back();
                Assert.AreEqual(UiScreen.Detail, flow.CurrentScreen, "pazarlıktan detaya dönüş");
            }
        }

        [Test]
        public void BuyNow_GoesThroughTheExistingSystem()
        {
            GameSession s = New();
            using (UiFlow flow = Flow(s))
            {
                ListingView l = s.Api.GetListings()[0];
                long cash = s.Api.GetCash().Tl;
                int stock = s.Api.GetInventory().Count;
                flow.OpenListing(l.ListingId);

                Result<Money> bought = flow.BuyNow();

                Assert.IsTrue(bought.IsSuccess, bought.ErrorCode);
                Assert.AreEqual(cash - l.AskingPrice.Tl, s.Api.GetCash().Tl, "istenen fiyat ödendi");
                Assert.AreEqual(stock + 1, s.Api.GetInventory().Count);
                Assert.IsFalse(s.Api.GetListings().Any(x => x.ListingId == l.ListingId), "ilan pazardan kalktı");
                Assert.AreEqual(UiScreen.Listings, flow.CurrentScreen, "satın alınca listeye dönülür");
            }
        }

        // ---------- geri dönüş ----------

        [Test]
        public void Navigation_ListToDetailAndBack_IsClean()
        {
            GameSession s = New();
            using (UiFlow flow = Flow(s))
            {
                Assert.AreEqual(UiScreen.Listings, flow.CurrentScreen);
                flow.OpenListing(s.Api.GetListings()[2].ListingId);
                Assert.AreEqual(UiScreen.Detail, flow.CurrentScreen);

                flow.Back();

                Assert.AreEqual(UiScreen.Listings, flow.CurrentScreen);
                Assert.AreEqual(s.Api.GetListings().Count, flow.Listings.Count, "liste aynı");
                flow.OpenListing(s.Api.GetListings()[0].ListingId);
                flow.OpenAppraisal();
                flow.Back();
                Assert.AreEqual(UiScreen.Detail, flow.CurrentScreen, "ekspertizden detaya");
                Assert.AreEqual(s.Api.GetListings()[0].ListingId, flow.Detail.ListingId);
            }
        }

        // ---------- boş ilan ----------

        [Test]
        public void WhenThereAreNoListings_AnExplainingNoteIsShown_AndTheListIsEmpty()
        {
            GameSession s = New();
            foreach (ListingView l in s.Api.GetListings().ToList())
            {
                Assert.IsTrue(s.Api.BuyListing(l.ListingId).IsSuccess);
            }

            using (UiFlow flow = Flow(s))
            {
                Assert.AreEqual(0, flow.Listings.Count);
                Assert.AreEqual(TurkishTexts.ListingsEmptyNote, flow.ListingsEmptyNote);
                StringAssert.Contains("Bugün ilan yok", flow.ListingsEmptyNote);
                StringAssert.Contains("günü bitirebilirsin", flow.ListingsEmptyNote);
            }
        }

        // ---------- oyun durumu ----------

        [Test]
        public void BrowsingTheMarket_DoesNotChangeTheGameState_OrTheRngStreams()
        {
            GameSession s = New();
            string digest = s.Api.GetStateDigest();
            var rng = s.Capture().Rng;
            using (UiFlow flow = Flow(s))
            {
                foreach (ListingView l in s.Api.GetListings())
                {
                    flow.OpenListing(l.ListingId);
                    flow.Refresh();
                    flow.Back();
                }
            }

            Assert.AreEqual(digest, s.Api.GetStateDigest(), "gezinmek oyunu değiştirmez");
            Assert.IsNull(DeepCompare.FirstDifference(rng, s.Capture().Rng));
        }

        [Test]
        public void TheListingView_StillCarriesNoHiddenInformation()
        {
            string[] names = typeof(ListingView).GetProperties().Select(p => p.Name).ToArray();

            CollectionAssert.DoesNotContain(names, "ReferencePrice");
            CollectionAssert.DoesNotContain(names, "TrueValue");
        }
    }
}
