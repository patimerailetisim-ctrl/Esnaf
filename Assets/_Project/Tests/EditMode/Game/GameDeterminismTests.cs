using System;
using System.Collections.Generic;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Game;
using Esnaf.Domain.Market;
using Esnaf.Domain.Products;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Game
{
    /// <summary>GDD I6 (determinizm) ve bin günlük değişmez kurallar (I1, I2, I8, defter zinciri).</summary>
    public class GameDeterminismTests
    {
        private static GameSession New(ulong seed)
        {
            return GameSession.NewGame(MarketHarness.RealContent(), seed);
        }

        [Test]
        public void I6_SameSeedAndSameCommands_GiveTheSameDigestEveryDay()
        {
            GameSession a = New(2024UL);
            GameSession b = New(2024UL);

            for (int day = 1; day <= 60; day++)
            {
                Assert.AreEqual(a.Api.GetStateDigest(), b.Api.GetStateDigest(), "gün " + day);
                Assert.AreEqual(GameStateDigest.Describe(a), GameStateDigest.Describe(b), "gün " + day);
                Assert.IsTrue(a.Api.EndDay().IsSuccess);
                Assert.IsTrue(b.Api.EndDay().IsSuccess);
            }
        }

        [Test]
        public void I6_DifferentSeeds_GiveDifferentDigests()
        {
            var digests = new HashSet<string>();
            for (ulong seed = 1; seed <= 20; seed++)
            {
                GameSession s = New(seed);
                s.Api.EndDay();
                s.Api.EndDay();
                digests.Add(s.Api.GetStateDigest());
            }

            Assert.AreEqual(20, digests.Count);
        }

        [Test]
        public void I6_EveryCommandMovesTheDigest()
        {
            GameSession s = New(3UL);
            var seen = new HashSet<string> { s.Api.GetStateDigest() };

            for (int i = 0; i < 30; i++)
            {
                s.Api.EndDay();
                Assert.IsTrue(seen.Add(s.Api.GetStateDigest()), "EndDay #" + (i + 1) + " özeti değiştirmedi");
            }
        }

        [Test]
        public void I6_QueriesBetweenCommands_DoNotChangeTheOutcome()
        {
            GameSession quiet = New(77UL);
            GameSession chatty = New(77UL);

            for (int day = 1; day <= 30; day++)
            {
                chatty.Api.GetListings();
                chatty.Api.GetInventory();
                chatty.Api.GetCash();
                chatty.Api.GetDay();
                chatty.Api.GetTodaySummary();
                chatty.Api.GetStateDigest();
                quiet.Api.EndDay();
                chatty.Api.EndDay();
            }

            Assert.AreEqual(quiet.Api.GetStateDigest(), chatty.Api.GetStateDigest());
        }

        [Test]
        public void ExtraRandomCallsInAnotherStream_DoNotChangeTheListings()
        {
            GameSession plain = New(31UL);
            GameSession noisy = New(31UL);

            for (int day = 1; day <= 25; day++)
            {
                noisy.Rng.Get("npc").NextUInt();
                noisy.Rng.Get("negotiation").NextDouble();
                plain.Api.EndDay();
                noisy.Api.EndDay();
            }

            string Listings(GameSession s)
            {
                return string.Join(
                    "|", s.Market.Listings.Select(l => l.ListingId + ":" + l.AskingPrice.Tl + ":" + l.RejectPrice.Tl + ":" + l.RemainingDays));
            }

            Assert.AreEqual(Listings(plain), Listings(noisy), "GDD 4.5: bir akıştaki fazladan çağrı diğer akışı etkilemez");
            Assert.AreNotEqual(plain.Api.GetStateDigest(), noisy.Api.GetStateDigest(), "ama özet, akış durumunu da içerir");
        }

        // ---------- bin gün ----------

        private static void AssertInvariants(GameSession s, int day)
        {
            string where = " (gün " + day + ")";

            // I1: nakit = başlangıç + Σ defter (defter tek gerçek kaynak)
            long rows = s.EconomyState.Ledger.Records.Sum(r => r.Amount.Tl);
            Assert.AreEqual(rows, s.EconomyState.Cash.Tl, "I1" + where);
            Assert.AreEqual(250000 + s.EconomyState.Ledger.Records.Where(r => r.TypeId != "opening_capital").Sum(r => r.Amount.Tl), s.EconomyState.Cash.Tl, "I1 250.000 + Σ" + where);
            Assert.IsTrue(s.EconomyState.Ledger.Verify().IsSuccess, "defter zinciri" + where);

            // I2: servet = başlangıç + Σ gün net kârı
            long netTotal = 0;
            for (int d = 0; d <= day; d++)
            {
                netTotal += s.Summaries.NetProfit(d).Tl;
            }

            Assert.AreEqual(250000 + netTotal, s.Wealth.Calculate().Total.Tl, "I2" + where);

            // I3
            Assert.LessOrEqual(s.InventoryState.Count, s.InventoryState.Capacity, "I3" + where);

            // I8: her ürün tek konumda; pazar ürünlerinin tam olarak bir ilanı var
            var listed = new HashSet<long>(s.Market.Listings.Select(l => l.InstanceId));
            Assert.AreEqual(s.Market.Count, listed.Count, "her ilan ayrı ürün" + where);
            foreach (ProductInstance instance in s.Store.All)
            {
                Assert.AreEqual(instance.Location == ProductLocation.Market, listed.Contains(instance.InstanceId), "I8 konum/ilan uyumu #" + instance.InstanceId + where);
            }

            // I9: negatif fiyat yok; ilanlar tutarlı
            foreach (MarketListing l in s.Market.Listings)
            {
                Assert.Greater(l.AskingPrice.Tl, 0, "I9" + where);
                Assert.Greater(l.RejectPrice.Tl, 0, "I9" + where);
                Assert.Less(l.RejectPrice.Tl, l.AskingPrice.Tl, "R < istenen" + where);
                Assert.GreaterOrEqual(l.RemainingDays, 1, "ilan ömrü" + where);
                Assert.LessOrEqual(l.RemainingDays, 4, "ilan ömrü" + where);
                Assert.LessOrEqual(l.DayListed, day, "ilan geleceğe ait olamaz" + where);
            }
        }

        [Test]
        public void ThousandDays_HoldTheInvariants_AndTheExpenseArithmetic()
        {
            GameSession s = New(1234UL);
            AssertInvariants(s, 1);

            for (int i = 1; i <= 1000; i++)
            {
                Result<DayEndReport> result = s.Api.EndDay();
                Assert.IsTrue(result.IsSuccess, "gün " + i + ": " + result);
                Assert.AreEqual(i, result.Value.EndedDay);
                Assert.AreEqual(i + 1, result.Value.NewDay);
                Assert.AreEqual(i >= 3 ? Money.FromTl(500) : Money.Zero, result.Value.ExpenseCharged, "gün " + i);
                Assert.AreEqual(i >= 3 ? Money.FromTl(-500) : Money.Zero, result.Value.Summary.NetProfit, "gün " + i);
                if (i % 25 == 0 || i < 12)
                {
                    AssertInvariants(s, i + 1);
                }
            }

            Assert.AreEqual(1001, s.Api.GetDay());
            Assert.AreEqual(Money.FromTl(250000 - 500 * 998), s.Api.GetCash(), "Gün 3–1000 arası 998 gün × 500 (iflas politikası yok: nakit negatife düşer)");
            Assert.AreEqual(Money.FromTl(250000 - 500 * 998), s.Wealth.Calculate().Total);
            Assert.AreEqual(s.Market.Count, s.Store.Count, "ürün deposu sınırsız büyümez: yalnızca canlı ilanların ürünleri");
        }

        [TestCase(1UL)]
        [TestCase(2UL)]
        [TestCase(3UL)]
        public void ThousandDaysAcrossSeeds_NeverFail_AndStayDeterministic(ulong seed)
        {
            GameSession a = New(seed);
            GameSession b = New(seed);

            for (int i = 0; i < 1000; i++)
            {
                Assert.IsTrue(a.Api.EndDay().IsSuccess);
                Assert.IsTrue(b.Api.EndDay().IsSuccess);
            }

            Assert.AreEqual(a.Api.GetStateDigest(), b.Api.GetStateDigest());
        }

        [Test]
        public void PlayerActions_KeepTheInvariants_AcrossManyDays()
        {
            GameSession s = New(555UL);
            var rng = new PcgRandom(555UL, 99UL); // yalnızca test senaryosunu sürer; oyun akışlarından bağımsız

            for (int day = 1; day <= 300; day++)
            {
                // Oyuncu bazen bir ilanın ürününü sabit fiyata alır, bazen satar (Gün 7/8 akışlarının yerine servis çağrıları).
                MarketListing pick = s.Market.Listings.Count > 0 ? s.Market.Listings[rng.NextInt(s.Market.Listings.Count)] : null;
                if (pick != null && rng.Chance(0.5) && s.InventoryState.Count < s.InventoryState.Capacity)
                {
                    ProductInstance instance = s.Store.Get(pick.InstanceId);
                    if (instance.Location == ProductLocation.Market)
                    {
                        Money price = Money.FromTl(pick.AskingPrice.Tl);
                        if (s.EconomyState.Cash >= price)
                        {
                            Assert.IsTrue(s.InventoryService.Acquire(instance.InstanceId, price, day).IsSuccess);
                            Assert.IsTrue(s.Market.Remove(pick.ListingId), "alınan ürünün ilanı pazardan çekilir (Gün 7 alış akışı bunu yapacak)");
                        }
                    }
                }

                if (s.InventoryState.Count > 0 && rng.Chance(0.4))
                {
                    long soldId = s.InventoryState.ItemIds[0];
                    ProductInstance owned = s.Store.Get(soldId);
                    Money sellPrice = Money.FromTl(RoundTo10(owned.CostBasis.Tl * 11 / 10));
                    Assert.IsTrue(s.InventoryService.Sell(soldId, sellPrice, day, "npc.berk").IsSuccess);
                }

                Assert.IsTrue(s.Api.EndDay().IsSuccess);
                AssertOwnedItemsSurvive(s);
                AssertInvariants(s, day + 1);
            }
        }

        private static long RoundTo10(long v)
        {
            return (v + 5) / 10 * 10;
        }

        private static void AssertOwnedItemsSurvive(GameSession s)
        {
            foreach (long id in s.InventoryState.ItemIds)
            {
                ProductInstance instance;
                Assert.IsTrue(s.Store.TryGet(id, out instance), "raftaki ürün depodan silinmemeli");
                Assert.AreEqual(ProductLocation.Inventory, instance.Location);
            }
        }
    }
}
