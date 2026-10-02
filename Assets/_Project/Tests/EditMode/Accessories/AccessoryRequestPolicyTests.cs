using System.Collections.Generic;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Accessories;
using Esnaf.Domain.Game;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Accessories
{
    /// <summary>Müşteri aksesuar talebi (Gün 11.3.4): saf, deterministik, 0–5 adet, tekrarsız; gerçek oturumda defterden türer, RNG'ye dokunmaz.</summary>
    public class AccessoryRequestPolicyTests
    {
        private static IReadOnlyList<AccessoryDefinition> Catalog()
        {
            return MarketHarness.RealContent().Accessories.Definitions;
        }

        private static IReadOnlyList<string> Make(int i)
        {
            return AccessoryRequestPolicy.Generate("npc.n" + (i % 40), 1 + i % 7, 10 + i, Money.FromTl(1000 + 10 * (i % 500)), Catalog());
        }

        [Test]
        public void TheSameSale_AlwaysGivesTheSameRequest()
        {
            for (int i = 0; i < 300; i++)
            {
                CollectionAssert.AreEqual(Make(i).ToArray(), Make(i).ToArray());
            }
        }

        [Test]
        public void TheRequest_IsNeverMoreThanFive_HasNoDuplicates_AndOnlyKnownAccessories()
        {
            var known = new HashSet<string>(Catalog().Select(d => d.Id));
            for (int i = 0; i < 5000; i++)
            {
                IReadOnlyList<string> request = Make(i);

                Assert.LessOrEqual(request.Count, AccessoryRequestPolicy.MaxRequests);
                Assert.AreEqual(request.Count, request.Distinct().Count(), "tekrarsız");
                Assert.IsTrue(request.All(known.Contains));
            }
        }

        [Test]
        public void EveryRequestSize_FromZeroToFive_Occurs_AndMostCustomersAskForNothing()
        {
            var counts = new int[AccessoryRequestPolicy.MaxRequests + 1];
            for (int i = 0; i < 5000; i++)
            {
                counts[Make(i).Count]++;
            }

            for (int c = 0; c <= AccessoryRequestPolicy.MaxRequests; c++)
            {
                Assert.Greater(counts[c], 0, c + " aksesuar isteyen müşteri hiç çıkmadı");
            }

            Assert.Greater(counts[0], counts[1], "çoğu müşteri aksesuar istemez");
            Assert.Greater(counts[0], 5000 * 35 / 100, "yaklaşık %45");
            Assert.Less(counts[5], 5000 * 5 / 100, "5 talep nadirdir");
        }

        [Test]
        public void ARequestIsNeverLargerThanTheCatalog_AndAnEmptyCatalogGivesNone()
        {
            IReadOnlyList<AccessoryDefinition> two = Catalog().Take(2).ToList();
            for (int i = 0; i < 500; i++)
            {
                Assert.LessOrEqual(AccessoryRequestPolicy.Generate("npc.a", 1, 10 + i, Money.FromTl(1000), two).Count, 2);
            }

            Assert.AreEqual(0, AccessoryRequestPolicy.Generate("npc.a", 1, 10, Money.FromTl(1000), new AccessoryDefinition[0]).Count);
            Assert.AreEqual(0, AccessoryRequestPolicy.Generate("npc.a", 1, 10, Money.FromTl(1000), null).Count);
        }

        [Test]
        public void TheRequest_DoesNotDependOnStock_OrOnWhatIsSoldAfterwards()
        {
            AddOnDeal deal = AddOnDeals.WithCount(3);
            GameSession s = deal.Session;
            string[] before = deal.Requested.ToArray();

            s.WholesaleService.BuyPack("supplier.ucuz_toptan", "accessory.phone_case", 1);
            s.AccessoryAddOns.SellAddOn(deal.SaleRecordId, before[0], s.Time.Day);

            CollectionAssert.AreEqual(before, s.AccessoryAddOns.RequestedAccessories(deal.SaleRecordId).ToArray());
        }

        [Test]
        public void TheRequest_SurvivesSaveAndLoad_WithoutAnyNewSavedState()
        {
            AddOnDeal deal = AddOnDeals.WithCount(2);

            Result<GameSession> back = GameSession.Restore(MarketHarness.RealContent(), deal.Session.Capture());

            Assert.IsTrue(back.IsSuccess, back.ErrorCode + ": " + back.Message);
            CollectionAssert.AreEqual(deal.Requested.ToArray(), back.Value.AccessoryAddOns.RequestedAccessories(deal.SaleRecordId).ToArray());
        }

        [Test]
        public void AskingForTheRequest_DoesNotTouchTheRng_OrAnyState()
        {
            AddOnDeal deal = AddOnDeals.WithCount(4);
            GameSession s = deal.Session;
            var rng = s.Capture().Rng;
            string digest = s.Api.GetStateDigest();

            for (int i = 0; i < 20; i++)
            {
                s.AccessoryAddOns.RequestedAccessories(deal.SaleRecordId);
                s.Api.GetAccessoryAddOns();
            }

            Assert.IsNull(DeepCompare.FirstDifference(rng, s.Capture().Rng));
            Assert.AreEqual(digest, s.Api.GetStateDigest());
        }

        [Test]
        public void ANonSaleRow_HasNoRequest()
        {
            GameSession s = GameSession.NewGame(MarketHarness.RealContent(), 1UL);

            Assert.AreEqual(0, s.AccessoryAddOns.RequestedAccessories(1L).Count);
            Assert.AreEqual(0, s.AccessoryAddOns.RequestedAccessories(99999L).Count);
        }
    }
}
