using System;
using System.Collections.Generic;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Business;
using Esnaf.Domain.Content;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Game;
using Esnaf.Domain.Inventory;
using Esnaf.Domain.Market;
using Esnaf.Domain.Negotiation;
using Esnaf.Domain.Npc;
using Esnaf.Domain.Products;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Business
{
    /// <summary>Müşteri üretimi, ilgi, Max hesabı, gün sonu "kaçan müşteri" (GDD v0.2 6.2, 10.4, 11.2; v0.3 P4).</summary>
    public class CustomerServiceTests
    {
        private static GameSession New(ulong seed = 42UL, IEventBus bus = null)
        {
            return GameSession.NewGame(MarketHarness.RealContent(), seed, bus);
        }

        private static void PassDays(GameSession s, int days)
        {
            for (int i = 0; i < days; i++)
            {
                Assert.IsTrue(s.Api.EndDay().IsSuccess);
            }
        }

        /// <summary>Gün 1 rehberli ürünü alıp rafa koyar ve fiyat etiketler; ürün kimliğini döndürür.</summary>
        private static long BuyGuidedAndPrice(GameSession s, long price = 5900)
        {
            MarketListing g = s.Market.Listings.Single(l => l.IsGuided);
            Assert.IsTrue(s.Api.StartNegotiation(g.ListingId).IsSuccess);
            Assert.IsTrue(s.Api.MakeOffer(Money.FromTl(5800)).IsSuccess);
            Assert.IsTrue(s.Api.SetPrice(g.InstanceId, Money.FromTl(price)).IsSuccess);
            return g.InstanceId;
        }

        private static CustomerSlot Slot(string npc, double value = 0.5, double trust = 0.5, double pick = 0.0, long id = 1)
        {
            return new CustomerSlot(id, npc, value, trust, pick);
        }

        private static ProductInstance GuidedInstance(GameSession s)
        {
            return s.Store.Get(s.InventoryState.ItemIds.First());
        }

        // ---------- günlük müşteri havuzu ----------

        [Test]
        public void DayOne_HasFiveSlots_OnlyFromTheDayOneCustomers()
        {
            GameSession s = New();

            IReadOnlyList<CustomerSlot> slots = s.Customers.State.Slots;

            Assert.AreEqual(5, slots.Count);
            foreach (CustomerSlot slot in slots)
            {
                Assert.IsTrue(slot.NpcId == "npc.kemal" || slot.NpcId == "npc.selin", slot.NpcId);
                Assert.AreEqual(CustomerStatus.Waiting, slot.Status);
                Assert.That(slot.ValueDraw, Is.InRange(0.0, 1.0));
                Assert.That(slot.TrustDraw, Is.InRange(0.0, 1.0));
                Assert.That(slot.PickDraw, Is.InRange(0.0, 1.0));
            }

            CollectionAssert.AllItemsAreUnique(slots.Select(x => x.CustomerId));
        }

        [Test]
        public void TheRoster_IsDeterministic_AndIndependentOfTheMarketStream()
        {
            string a = string.Join(",", New(5UL).Customers.State.Slots.Select(x => x.NpcId + ":" + x.PickDraw.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
            string b = string.Join(",", New(5UL).Customers.State.Slots.Select(x => x.NpcId + ":" + x.PickDraw.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
            string c = string.Join(",", New(6UL).Customers.State.Slots.Select(x => x.NpcId + ":" + x.PickDraw.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));

            Assert.AreEqual(a, b);
            Assert.AreNotEqual(a, c);
        }

        [Test]
        public void EveryDay_ANewRosterIsDrawn_WithNewCustomerIds()
        {
            GameSession s = New();
            long lastIdDayOne = s.Customers.State.Slots.Max(x => x.CustomerId);

            PassDays(s, 1);

            Assert.AreEqual(5, s.Customers.State.Slots.Count);
            Assert.Greater(s.Customers.State.Slots.Min(x => x.CustomerId), lastIdDayOne);
            Assert.IsTrue(s.Customers.State.Slots.All(x => x.Status == CustomerStatus.Waiting));
        }

        [Test]
        public void Fuzz_CustomersRespectAvailabilityDays_AndTheRichQuota()
        {
            ContentDatabase content = MarketHarness.RealContent();
            for (ulong seed = 1; seed <= 25; seed++)
            {
                GameSession s = New(seed);
                for (int day = 1; day <= 9; day++)
                {
                    int rich = 0;
                    foreach (CustomerSlot slot in s.Customers.State.Slots)
                    {
                        Assert.LessOrEqual(content.GetNpc(slot.NpcId).Customer.AvailableFromDay, day, "seed " + seed + " day " + day + " " + slot.NpcId);
                        if (content.Customers.IsRich(slot.NpcId))
                        {
                            rich++;
                        }
                    }

                    Assert.LessOrEqual(rich, content.Customers.RichMaxPerDay, "P4: zengin/koleksiyoncu ≤ 1/gün (seed " + seed + " gün " + day + ")");
                    Assert.IsTrue(s.Api.EndDay().IsSuccess);
                }
            }
        }

        [Test]
        public void TheProfileGroupsOpenOnTheGddDays()
        {
            ContentDatabase content = MarketHarness.RealContent();
            var seen = new Dictionary<int, HashSet<string>>();
            for (ulong seed = 1; seed <= 60; seed++)
            {
                GameSession s = New(seed);
                for (int day = 1; day <= 6; day++)
                {
                    if (!seen.ContainsKey(day))
                    {
                        seen[day] = new HashSet<string>();
                    }

                    foreach (CustomerSlot slot in s.Customers.State.Slots)
                    {
                        seen[day].Add(slot.NpcId);
                    }

                    s.Api.EndDay();
                }
            }

            CollectionAssert.AreEquivalent(new[] { "npc.kemal", "npc.selin" }, seen[1]);
            CollectionAssert.AreEquivalent(new[] { "npc.kemal", "npc.selin" }, seen[2]);
            Assert.IsFalse(seen[3].Contains("npc.riza") || seen[3].Contains("npc.murat"), "Şüpheli/Bilgili Gün 4'te açılır");
            Assert.IsTrue(seen[4].Contains("npc.riza") && seen[4].Contains("npc.murat"));
            Assert.IsFalse(seen[5].Contains("npc.berk") || seen[5].Contains("npc.nermin"), "Zengin/Koleksiyoncu Gün 6'da açılır");
            Assert.IsTrue(seen[6].Contains("npc.berk") && seen[6].Contains("npc.nermin"));
        }

        // ---------- gelen müşteri sayısı ----------

        [Test]
        public void Arrivals_StartFromTheShelfAtTheStartOfTheDay()
        {
            GameSession s = New();

            Assert.AreEqual(2, s.Customers.State.Arrived, "raf boş: 2 + ⌈0⌉");
        }

        [Test]
        public void Arrivals_GrowWithThePurchases_AndNeverShrinkAfterASale()
        {
            GameSession s = New();
            BuyGuidedAndPrice(s);

            Assert.AreEqual(3, s.Customers.State.Arrived, "raf 1: 2 + ⌈0,5⌉");

            long other = s.Market.Listings.First().ListingId;
            Assert.IsTrue(s.Api.StartNegotiation(other).IsSuccess);
            Assert.IsTrue(s.Api.MakeOffer(Money.FromTl(s.Api.GetListings().First(l => l.ListingId == other).AskingPrice.Tl)).IsSuccess);
            Assert.AreEqual(3, s.Customers.State.Arrived, "raf 2: 2 + ⌈1⌉ = 3");
        }

        [Test]
        public void Arrivals_AreCappedAtFive()
        {
            GameSession s = New();
            PassDays(s, 1);
            int bought = 0;
            foreach (long id in s.Market.Listings.Select(l => l.ListingId).ToList())
            {
                if (bought >= 6)
                {
                    break;
                }

                ListingView v = s.Api.GetListings().First(l => l.ListingId == id);
                if (s.Api.StartNegotiation(id).IsSuccess && s.Api.MakeOffer(v.AskingPrice).IsSuccess)
                {
                    bought++;
                }
            }

            Assert.GreaterOrEqual(bought, 4);
            Assert.LessOrEqual(s.Customers.State.Arrived, 5);
            Assert.AreEqual(Math.Min(5, 2 + (int)Math.Ceiling(bought * 0.5)), s.Customers.State.Arrived);
        }

        // ---------- ilgi ----------

        [Test]
        public void AnUnpricedItem_AttractsNoCustomer()
        {
            GameSession s = New();
            MarketListing g = s.Market.Listings.Single(l => l.IsGuided);
            Assert.IsTrue(s.Api.StartNegotiation(g.ListingId).IsSuccess);
            Assert.IsTrue(s.Api.MakeOffer(Money.FromTl(5800)).IsSuccess);

            Assert.AreEqual(0, s.Api.GetCustomers().Count);
        }

        [Test]
        public void APricedItem_AttractsTheArrivedCustomers_OnlyWhileTheTagIsWithinTheCeiling()
        {
            GameSession s = New();
            long item = BuyGuidedAndPrice(s, 5900);

            IReadOnlyList<CustomerView> views = s.Api.GetCustomers();
            Assert.AreEqual(3, views.Count, "3 müşteri geldi, hepsi bu tek ürünle ilgilenir");
            Assert.IsTrue(views.All(v => v.InstanceId == item));

            Assert.IsTrue(s.Api.SetPrice(item, Money.FromTl(20000)).IsSuccess);

            Assert.AreEqual(0, s.Api.GetCustomers().Count, "etiket M × 1,15'in üstünde: müşteri 'çok pahalı' der ve gider");
        }

        [Test]
        public void OnlyArrivedCustomers_AreListed()
        {
            GameSession s = New();
            BuyGuidedAndPrice(s);

            IReadOnlyList<CustomerView> views = s.Api.GetCustomers();

            var arrivedIds = s.Customers.State.Slots.Take(s.Customers.State.Arrived).Select(x => x.CustomerId).ToList();
            CollectionAssert.AreEqual(arrivedIds, views.Select(v => v.CustomerId).ToList());
        }

        [Test]
        public void AnItemAlreadySoldByThatNpcToThePlayer_NeverAttractsThatNpc_AntiArbitrage()
        {
            GameSession s = New();
            long item = BuyGuidedAndPrice(s);
            CustomerView first = s.Api.GetCustomers()[0];
            string npc = first.NpcId;

            s.Npcs.RecordSoldToPlayer(npc, item);

            Assert.IsFalse(s.Api.GetCustomers().Any(v => v.NpcId == npc), "aynı NPC'ye aynı ürünü geri satma yok");
            Assert.AreEqual("customer.no_interest", s.Api.StartSale(first.CustomerId).ErrorCode);
        }

        [Test]
        public void ACustomerWithASegmentList_IgnoresOtherSegments()
        {
            NpcCustomerRole cengiz = MarketHarness.RealContent().GetNpc("npc.cengiz").Customer;
            for (ulong seed = 1; seed <= 40; seed++)
            {
                GameSession s = New(seed);
                PassDays(s, 2);
                foreach (ProductInstance i in s.Store.All)
                {
                    ProductDefinition def = s.Content.GetProduct(i.DefinitionId);
                    if (def.Segment != ProductSegment.Upper)
                    {
                        continue;
                    }

                    i.ListPrice = Money.FromTl(1000);
                    Assert.IsFalse(cengiz.AcceptsSegment(def.Segment));
                    double max;
                    Assert.IsFalse(s.Customers.IsEligible(Slot("npc.cengiz"), i, out max), "Cengiz üst segmentle ilgilenmez");
                    Assert.IsTrue(s.Customers.IsEligible(Slot("npc.kemal"), i, out max), "aynı ürün, segment sınırı olmayan müşteri");
                    return;
                }
            }

            Assert.Fail("test verisi: üst segment ürün bulunamadı");
        }

        // ---------- Max (M) ----------

        [Test]
        public void Max_IsTrueValueTimesRatioTimesShopPremium_RoundedToTen()
        {
            GameSession s = New();
            BuyGuidedAndPrice(s);
            ProductInstance item = GuidedInstance(s);

            // V = 5.280; Kemal mRatio 1,00, σ yok, dükkân primi %5 → 5.544 → 5.540
            Assert.AreEqual(5540.0, s.Customers.MaxFor(Slot("npc.kemal"), item, false), 0.0);
            // Berk mRatio 1,10: 5.280 × 1,10 × 1,05 = 6.098,4 → 6.100
            Assert.AreEqual(6100.0, s.Customers.MaxFor(Slot("npc.berk"), item, false), 0.0);
        }

        [Test]
        public void Max_ForANoisyCustomer_FollowsTheValueDraw_AndTheReportHalvesTheError()
        {
            GameSession s = New();
            BuyGuidedAndPrice(s);
            ProductInstance item = GuidedInstance(s);

            // Hatice σ = 0,30: çekim 1,0 → +%30, çekim 0,0 → −%30, çekim 0,5 → 0
            double up = s.Customers.MaxFor(Slot("npc.hatice", value: 1.0), item, false);
            double down = s.Customers.MaxFor(Slot("npc.hatice", value: 0.0), item, false);
            double mid = s.Customers.MaxFor(Slot("npc.hatice", value: 0.5), item, false);

            Assert.AreEqual(Math.Min(5280 * 1.3 * 1.05, 5280 * 1.25) - 0.0, up, 10.0, "üst sınır: gerçek değer × 1,25");
            Assert.AreEqual(5280 * 0.7 * 1.05, down, 10.0);
            Assert.AreEqual(5540.0, mid, 0.0);
            // rapor: σ yarıya iner
            Assert.AreEqual(5280 * 0.85 * 1.05, s.Customers.MaxFor(Slot("npc.hatice", value: 0.0), item, true), 10.0);
            Assert.AreEqual(5280 * 1.15 * 1.05, s.Customers.MaxFor(Slot("npc.hatice", value: 1.0), item, true), 10.0);
        }

        [Test]
        public void Max_NeverExceedsOnePointTwoFiveTimesTheTrueValue()
        {
            GameSession s = New();
            BuyGuidedAndPrice(s);
            ProductInstance item = GuidedInstance(s);

            double max = s.Customers.MaxFor(Slot("npc.hatice", value: 1.0), item, false);

            Assert.LessOrEqual(max, 5280 * 1.25 + 5);
            Assert.AreEqual(6600.0, max, 0.0);
        }

        [Test]
        public void Max_PackageRatio_AppliesOnlyToBoxedOrInvoicedItems()
        {
            GameSession s = New();
            BuyGuidedAndPrice(s);
            ProductInstance plain = GuidedInstance(s);
            double without = s.Customers.MaxFor(Slot("npc.nermin"), plain, false);

            plain.Attributes["box"] = AttributeValue.FromFlag(true);
            double with = s.Customers.MaxFor(Slot("npc.nermin"), plain, false);

            Assert.Greater(with, without);
            Assert.That(with / without, Is.InRange(1.12, 1.12 * 1.05), "Nermin ×1,12 (kutu değer formülüne de +%2 ekler)");
        }

        [Test]
        public void Max_FollowsTheDemandMultiplier()
        {
            GameSession s = New();
            BuyGuidedAndPrice(s);
            ProductInstance item = GuidedInstance(s);
            double before = s.Customers.MaxFor(Slot("npc.kemal"), item, false);

            s.DemandState.SetIndex(item.DefinitionId, 1.10);
            double up = s.Customers.MaxFor(Slot("npc.kemal"), item, false);
            s.DemandState.SetIndex(item.DefinitionId, 0.90);
            double down = s.Customers.MaxFor(Slot("npc.kemal"), item, false);

            Assert.Greater(up, before);
            Assert.Less(down, before);
            Assert.AreEqual(before * 1.10, up, before * 0.01);
        }

        [Test]
        public void Opening_IsTheOpeningRatioOfMax_RoundedToTen()
        {
            GameSession s = New();

            Assert.AreEqual(Money.FromTl(4430), s.Customers.OpeningFor("npc.kemal", 5540.0)); // 0,80 × 5.540 = 4.432
            Assert.AreEqual(Money.FromTl(5100), s.Customers.OpeningFor("npc.selin", 5540.0)); // 0,92 × 5.540 = 5.096,8
            Assert.AreEqual(Money.FromTl(18370), s.Customers.OpeningFor("npc.berk", 20410.0)); // GDD Senaryo 3
            Assert.AreEqual(Money.FromTl(7320), s.Customers.OpeningFor("npc.kemal", 9150.0)); // GDD Senaryo 2
        }


        // ---------- sınır ve seçim kapatma testleri ----------

        [Test]
        public void ACustomerWhoHasNotArrivedYet_CannotBeStarted_AndIsNotListed()
        {
            GameSession s = New();
            BuyGuidedAndPrice(s);
            CustomerSlot notArrived = s.Customers.State.Slots[s.Customers.State.Arrived];
            Assert.Less(s.Customers.State.Arrived, s.Customers.State.Slots.Count);

            Assert.AreEqual("customer.unknown", s.Api.StartSale(notArrived.CustomerId).ErrorCode);
            Assert.IsFalse(s.Api.GetCustomers().Any(v => v.CustomerId == notArrived.CustomerId));
            Assert.AreEqual(s.Customers.State.Arrived, s.Api.GetCustomers().Count);
            long lastArrived = s.Customers.State.Slots[s.Customers.State.Arrived - 1].CustomerId;
            Assert.IsTrue(s.Customers.TryGetWaiting(lastArrived, out _), "gelen son müşteri bulunur");
            Assert.IsFalse(s.Customers.TryGetWaiting(notArrived.CustomerId, out _));
        }

        [Test]
        public void DayEnd_CountsExactlyTheArrivedCustomers_NotTheWholeRoster()
        {
            GameSession s = New();
            Assert.Less(s.Customers.State.Arrived, s.Customers.State.Slots.Count);

            Assert.AreEqual(s.Customers.State.Arrived, s.Customers.EndDay());
        }

        [Test]
        public void TheInterestPick_FollowsThePickDraw_AcrossTwoEligibleItems()
        {
            GameSession s = New();
            long first = BuyGuidedAndPrice(s);
            long secondListing = s.Market.Listings.First(l => !l.IsGuided).ListingId;
            long secondInstance = s.Market.Listings.First(l => l.ListingId == secondListing).InstanceId;
            Money ask = s.Api.GetListings().First(l => l.ListingId == secondListing).AskingPrice;
            Assert.IsTrue(s.Api.StartNegotiation(secondListing).IsSuccess);
            Assert.IsTrue(s.Api.MakeOffer(ask).IsSuccess);
            s.Store.Get(secondInstance).ListPrice = Money.FromTl(1000);
            s.Store.Get(first).ListPrice = Money.FromTl(1000);
            CustomerSlot kemal(double pick)
            {
                return new CustomerSlot(1, "npc.kemal", 0.5, 0.5, pick);
            }

            long[] items = s.InventoryState.ItemIds.ToArray();
            Assert.AreEqual(2, items.Length);
            Assert.AreEqual(items[0], s.Customers.FindInterest(kemal(0.0)));
            Assert.AreEqual(items[0], s.Customers.FindInterest(kemal(0.49)));
            Assert.AreEqual(items[1], s.Customers.FindInterest(kemal(0.51)));
            Assert.AreEqual(items[1], s.Customers.FindInterest(kemal(1.0)), "çekim 1,0 son ürüne sıkıştırılır");
        }

        [Test]
        public void Eligibility_UsesTheMaxWithoutAReport()
        {
            GameSession s = New();
            BuyGuidedAndPrice(s);
            ProductInstance item = GuidedInstance(s);
            item.ListPrice = Money.FromTl(5000);
            CustomerSlot low = Slot("npc.hatice", value: 0.0);

            // Rapor olmadan M = 3.880 → 1,15 × M = 4.462 < 5.000; rapor olsaydı M = 4.710 → 5.416 ≥ 5.000
            double max;
            Assert.IsFalse(s.Customers.IsEligible(low, item, out max));
            Assert.AreEqual(3880.0, max, 10.0);
        }

        [Test]
        public void Eligibility_TheTagCeilingIsInclusive()
        {
            GameSession s = New();
            BuyGuidedAndPrice(s);
            ProductInstance item = GuidedInstance(s);
            CustomerSlot kemal = Slot("npc.kemal");
            double found = 0;
            for (int i = 0; i <= 1000 && found == 0; i++)
            {
                double index = 0.90 + i * 0.0001;
                s.DemandState.SetIndex(item.DefinitionId, index);
                if (s.Customers.MaxFor(kemal, item, false) == 5000.0)
                {
                    found = index;
                }
            }

            Assert.AreNotEqual(0.0, found, "test verisi: M = 5.000 veren talep bulunamadı");
            double max;
            item.ListPrice = Money.FromTl(5750); // 1,15 × 5.000 = 5.750 (ikili kesirde tam)
            Assert.IsTrue(s.Customers.IsEligible(kemal, item, out max), "sınırdaki etiket kabul");
            item.ListPrice = Money.FromTl(5760);
            Assert.IsFalse(s.Customers.IsEligible(kemal, item, out max));
        }

        [Test]
        public void TheSaleSetup_UsesTheMaxWithoutAReport()
        {
            GameSession s = New();
            BuyGuidedAndPrice(s);
            ProductInstance item = GuidedInstance(s);

            SaleSetup setup = s.Customers.BuildSetup(Slot("npc.hatice", value: 0.0), item);

            Assert.AreEqual(s.Customers.MaxFor(Slot("npc.hatice", value: 0.0), item, false), setup.Max, 0.0);
            Assert.AreNotEqual(s.Customers.MaxFor(Slot("npc.hatice", value: 0.0), item, true), setup.Max);
            Assert.AreEqual(s.Customers.OpeningFor("npc.hatice", setup.Max), setup.Opening);
            Assert.AreEqual(4, setup.Patience, "Hatice Teyze müşteri sabrı 4");
            Assert.AreEqual(0.4, setup.Urgency, 0.0);
            Assert.AreEqual(1, setup.Day);
        }

        [TestCase(0.0, 40)]
        [TestCase(0.5, 50)]
        [TestCase(0.999999, 60)]
        [TestCase(0.25, 45)]
        public void TheSaleSetup_TrustFollowsTheTrustDraw(double draw, int expected)
        {
            GameSession s = New();
            BuyGuidedAndPrice(s);

            SaleSetup setup = s.Customers.BuildSetup(Slot("npc.kemal", trust: draw), GuidedInstance(s));

            Assert.AreEqual(expected, setup.Trust);
        }

        // ---------- gün sonu: kaçan müşteriler ----------

        [Test]
        public void DayEnd_RecordsTheUnservedCustomersAsMissed()
        {
            GameSession s = New();
            int arrived = s.Customers.State.Arrived;

            Result<DayEndReport> report = s.Api.EndDay();

            Assert.IsTrue(report.IsSuccess);
            Assert.AreEqual(arrived, report.Value.MissedCustomers);
            Assert.AreEqual(arrived, s.Customers.State.MissedTotal);
            Assert.IsTrue(report.Value.ExecutedSteps.Contains("missed_customers"));
            Assert.AreEqual("missed_customers", report.Value.ExecutedSteps[0], "adım 1");
        }

        [Test]
        public void DayEnd_ASoldCustomerIsNotMissed_AndTheTotalAccumulates()
        {
            GameSession s = New();
            BuyGuidedAndPrice(s);
            CustomerView customer = s.Api.GetCustomers()[0];
            Assert.IsTrue(s.Api.StartSale(customer.CustomerId).IsSuccess);
            Assert.IsTrue(s.Api.AskPrice(Money.FromTl(1000)).IsSuccess);
            int arrived = s.Customers.State.Arrived;

            Result<DayEndReport> first = s.Api.EndDay();
            int missedDayOne = first.Value.MissedCustomers;
            Result<DayEndReport> second = s.Api.EndDay();

            Assert.AreEqual(arrived - 1, missedDayOne);
            Assert.AreEqual(missedDayOne + second.Value.MissedCustomers, s.Customers.State.MissedTotal);
        }

        [Test]
        public void DayEnd_ACustomerWhoLeavesWithoutABuy_CountsAsMissed()
        {
            GameSession s = New();
            BuyGuidedAndPrice(s);
            CustomerView customer = s.Api.GetCustomers()[0];
            Assert.IsTrue(s.Api.StartSale(customer.CustomerId).IsSuccess);
            Assert.IsTrue(s.Api.LetCustomerGo().IsSuccess);
            int arrived = s.Customers.State.Arrived;

            Assert.AreEqual(arrived, s.Api.EndDay().Value.MissedCustomers);
        }

        // ---------- talep adımı ----------

        [Test]
        public void DemandIndexes_StayAtOneUntilDayFive_ThenMoveInsideTheBand()
        {
            GameSession s = New(9UL);
            PassDays(s, 3);
            Assert.IsTrue(s.Content.Products.All(p => s.Demand.Index(p.Id) == 1.0), "Gün 4 sabahı hâlâ 1,00");

            PassDays(s, 1);

            Assert.AreEqual(5, s.Api.GetDay());
            Assert.IsTrue(s.Content.Products.Any(p => s.Demand.Index(p.Id) != 1.0), "Gün 5 sabahı talep canlı");
            Assert.IsTrue(s.Content.Products.All(p => s.Demand.Index(p.Id) >= 0.90 && s.Demand.Index(p.Id) <= 1.10));
        }

        [Test]
        public void TheDemandStep_RunsAsStepFive_AndIsDeterministic()
        {
            GameSession a = New(9UL);
            GameSession b = New(9UL);
            Result<DayEndReport> report = default(Result<DayEndReport>);
            for (int i = 0; i < 5; i++)
            {
                report = a.Api.EndDay();
                b.Api.EndDay();
            }

            var ids = report.Value.ExecutedSteps.ToList();
            CollectionAssert.AreEqual(new[] { "missed_customers", "daily_expense", "listing_expiry", "demand_update", "new_day", "auto_save" }, ids);
            Assert.AreEqual(a.Api.GetStateDigest(), b.Api.GetStateDigest());
        }
    }
}
