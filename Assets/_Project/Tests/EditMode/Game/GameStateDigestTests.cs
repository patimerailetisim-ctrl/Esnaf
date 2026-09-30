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
    /// <summary>I6: aynı tohum + aynı komutlar → aynı durum özeti. Özet, durumun HER parçasına duyarlı olmalı.</summary>
    public class GameStateDigestTests
    {
        private static GameSession New(ulong seed = 42UL)
        {
            return GameSession.NewGame(MarketHarness.RealContent(), seed);
        }

        // Bağımsız FNV-1a 64 (test içi); bilinen test vektörleriyle kendini doğrular.
        private static string Fnv(string text)
        {
            unchecked
            {
                ulong hash = 0xcbf29ce484222325UL;
                foreach (byte b in System.Text.Encoding.UTF8.GetBytes(text))
                {
                    hash ^= b;
                    hash *= 0x100000001b3UL;
                }

                return hash.ToString("x16");
            }
        }

        [Test]
        public void TestFnv_MatchesTheKnownVectors()
        {
            Assert.AreEqual("cbf29ce484222325", Fnv(""));
            Assert.AreEqual("af63dc4c8601ec8c", Fnv("a"));
            Assert.AreEqual("85944171f73967e8", Fnv("foobar"));
        }

        [Test]
        public void Compute_IsTheFnv1a64OfTheDescription_AsSixteenLowercaseHexDigits()
        {
            GameSession s = New();

            string digest = GameStateDigest.Compute(s);

            Assert.AreEqual(Fnv(GameStateDigest.Describe(s)), digest);
            Assert.AreEqual(16, digest.Length);
            Assert.AreEqual(digest.ToLowerInvariant(), digest);
            Assert.AreEqual(digest, s.Api.GetStateDigest());
        }

        [Test]
        public void Describe_ListsEverySectionOfTheState()
        {
            GameSession s = New(42UL);
            s.Npcs.RecordEncounter("npc.kemal");
            s.Rng.Get("npc").NextUInt();

            string[] lines = GameStateDigest.Describe(s).Split('\n');

            Assert.AreEqual("esnaf.state.v1", lines[0]);
            CollectionAssert.Contains(lines, "seed=42");
            CollectionAssert.Contains(lines, "day=1");
            CollectionAssert.Contains(lines, "ids.instance=3");
            CollectionAssert.Contains(lines, "ids.listing=3");
            CollectionAssert.Contains(lines, "economy.cash=250000");
            CollectionAssert.Contains(lines, "economy.businessAssets=0");
            CollectionAssert.Contains(lines, "ledger.count=1");
            CollectionAssert.Contains(lines, "inventory.capacity=6");
            CollectionAssert.Contains(lines, "inventory.items=");
            Assert.AreEqual(1, lines.Count(l => l.StartsWith("L|", StringComparison.Ordinal)), "defter satırları");
            Assert.AreEqual(3, lines.Count(l => l.StartsWith("I|", StringComparison.Ordinal)), "ürün örnekleri");
            Assert.AreEqual(3, lines.Count(l => l.StartsWith("M|", StringComparison.Ordinal)), "ilanlar");
            Assert.AreEqual(1, lines.Count(l => l.StartsWith("N|", StringComparison.Ordinal)), "NPC durumları");
            Assert.AreEqual(3, lines.Count(l => l.StartsWith("R|", StringComparison.Ordinal)), "rastgelelik akışları (customers, market, npc)");
            Assert.AreEqual(5, lines.Count(l => l.StartsWith("K|", StringComparison.Ordinal)), "müşteri yuvaları");
        }

        [Test]
        public void Describe_ContainsTheHiddenListingFields_AndAllInstanceAttributes()
        {
            GameSession s = New(42UL);
            MarketListing guided = s.Market.Listings.Single(l => l.IsGuided);

            string text = GameStateDigest.Describe(s);

            StringAssert.Contains("|" + guided.RejectPrice.Tl + "|" + guided.BelievedValue.Tl + "|", text, "gizli R ve inandığı değer özete girer");
            StringAssert.Contains("battery=95", text);
            StringAssert.Contains("screen=original", text);
            StringAssert.Contains("box=False", text);
            StringAssert.Contains("|phone.yildiz_y5|64|24|", text);
        }

        private static string[] Lines(GameSession s, string prefix)
        {
            return GameStateDigest.Describe(s).Split('\n').Where(l => l.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        }

        [Test]
        public void MarketLines_CarryEveryListingField_InOrder()
        {
            GameSession s = New(42UL);
            for (int i = 0; i < 6; i++)
            {
                s.Api.EndDay();
            }

            string[] lines = Lines(s, "M|");

            Assert.AreEqual(s.Market.Count, lines.Length);
            for (int i = 0; i < lines.Length; i++)
            {
                MarketListing l = s.Market.Listings[i];
                string[] f = lines[i].Split('|');
                Assert.AreEqual(14, f.Length);
                Assert.AreEqual(l.IsGuided ? "1" : "0", f[13]);
                Assert.AreEqual(l.ListingId.ToString(), f[1]);
                Assert.AreEqual(l.InstanceId.ToString(), f[2]);
                Assert.AreEqual(l.SellerNpcId, f[3]);
                Assert.AreEqual(l.AskingPrice.Tl.ToString(), f[4]);
                Assert.AreEqual(string.Join(",", l.Tags), f[5]);
                Assert.AreEqual(l.DayListed.ToString(), f[6]);
                Assert.AreEqual(l.RemainingDays.ToString(), f[7]);
                Assert.AreEqual(l.RejectPrice.Tl.ToString(), f[8]);
                Assert.AreEqual(l.BelievedValue.Tl.ToString(), f[9]);
                Assert.AreEqual(l.IsOpportunity ? "1" : "0", f[10]);
                Assert.AreEqual(l.IsTrap ? "1" : "0", f[11]);
                Assert.AreEqual(l.IsJackpot ? "1" : "0", f[12]);
            }
        }

        [Test]
        public void MarketLines_ShowTrapAndGuidedFlags()
        {
            GameSession s = New(42UL);
            for (int i = 0; i < 6; i++)
            {
                s.Api.EndDay();
            }

            Assert.IsTrue(s.Market.Listings.Any(l => l.IsTrap), "Gün 7'de tuzak var");
            int trapLines = Lines(s, "M|").Count(l => l.Split('|')[11] == "1");
            Assert.AreEqual(s.Market.Listings.Count(l => l.IsTrap), trapLines);
        }

        [Test]
        public void Describe_GuidedFlagIsTheLastField()
        {
            GameSession s = New(42UL);

            string[] lines = Lines(s, "M|");

            Assert.AreEqual(s.Market.Listings.Count(l => l.IsGuided), lines.Count(l => l.EndsWith("|1", StringComparison.Ordinal)));
            Assert.AreEqual(1, lines.Count(l => l.EndsWith("|1", StringComparison.Ordinal)));
        }

        [Test]
        public void LedgerLines_CarryAmountAndBalance()
        {
            GameSession s = New();
            s.EconomyService.RecordPurchase(AnyInstanceId(s), "phone.x", "npc.kemal", Money.FromTl(1000), 1);
            s.EconomyService.RecordPurchase(AnyInstanceId(s), "phone.x", "npc.kemal", Money.FromTl(500), 1);

            string[] rows = Lines(s, "L|");

            Assert.AreEqual("L|1|0|opening_capital|250000|250000||||||ledger.type.opening_capital|", rows[0]);
            Assert.AreEqual("L|2|1|purchase|-1000|249000|" + AnyInstanceId(s) + "|phone.x|npc.kemal|||ledger.type.purchase|", rows[1]);
            string[] third = rows[2].Split('|');
            Assert.AreEqual("-500", third[4]);
            Assert.AreEqual("248500", third[5], "işlem sonrası bakiye");
        }

        [Test]
        public void BusinessAssets_AreListed()
        {
            GameSession s = New();
            s.InventoryService.UpgradeCapacity(8, Money.FromTl(15000), 1);

            CollectionAssert.Contains(GameStateDigest.Describe(s).Split('\n'), "economy.businessAssets=15000");
        }

        [Test]
        public void NpcLines_CarryEncountersAndInstances()
        {
            GameSession s = New();
            s.Npcs.RecordEncounter("npc.kemal");
            s.Npcs.RecordEncounter("npc.kemal");
            s.Npcs.RecordEncounter("npc.kemal");
            s.Npcs.RecordSoldToPlayer("npc.kemal", 9);
            s.Npcs.RecordBoughtFromPlayer("npc.kemal", 4);

            CollectionAssert.AreEqual(new[] { "N|npc.kemal|3|9|4" }, Lines(s, "N|"));
        }

        [Test]
        public void Describe_IsStable_ForTheSameState()
        {
            GameSession s = New();

            Assert.AreEqual(GameStateDigest.Describe(s), GameStateDigest.Describe(s));
            Assert.AreEqual(GameStateDigest.Compute(s), GameStateDigest.Compute(s));
        }

        [Test]
        public void RngStreams_AreListedInNameOrder()
        {
            GameSession s = New();
            s.Rng.Get("zeta").NextUInt();
            s.Rng.Get("alpha").NextUInt();

            string[] streams = GameStateDigest.Describe(s).Split('\n')
                .Where(l => l.StartsWith("R|", StringComparison.Ordinal)).Select(l => l.Split('|')[1]).ToArray();

            CollectionAssert.AreEqual(new[] { "alpha", "customers", "market", "zeta" }, streams);
        }

        [Test]
        public void Compute_RejectsANullSession()
        {
            Assert.Throws<ArgumentNullException>(() => GameStateDigest.Compute(null));
            Assert.Throws<ArgumentNullException>(() => GameStateDigest.Describe(null));
        }

        // ---------- duyarlılık: durumun her parçası özeti değiştirmeli ----------

        private static void AssertChanges(string what, Action<GameSession> change)
        {
            GameSession s = New();
            string before = GameStateDigest.Compute(s);
            string describedBefore = GameStateDigest.Describe(s);

            change(s);

            Assert.AreNotEqual(describedBefore, GameStateDigest.Describe(s), what + ": açıklama değişmeli");
            Assert.AreNotEqual(before, GameStateDigest.Compute(s), what + ": özet değişmeli");
        }

        private static ProductInstance AnyInstance(GameSession s)
        {
            return s.Store.All[0];
        }

        [Test]
        public void Digest_ChangesWhenAnyPartOfTheStateChanges()
        {
            AssertChanges("gün", s => s.Time.Advance());
            AssertChanges("rastgelelik akışı", s => s.Rng.Get("market").NextUInt());
            AssertChanges("yeni rastgelelik akışı", s => s.Rng.Get("npc"));
            AssertChanges("örnek sayacı", s => s.InstanceIds.Next());
            AssertChanges("ilan sayacı", s => s.ListingIds.Next());
            AssertChanges("NPC görüşmesi", s => s.Npcs.RecordEncounter("npc.kemal"));
            AssertChanges("NPC sattı", s => s.Npcs.RecordSoldToPlayer("npc.kemal", 1));
            AssertChanges("NPC aldı", s => s.Npcs.RecordBoughtFromPlayer("npc.kemal", 1));
            AssertChanges("ilan kalktı", s => s.Market.Remove(s.Market.Listings[0].ListingId));
            AssertChanges("nitelik", s => AnyInstance(s).Attributes["battery"] = AttributeValue.FromNumber(1));
            AssertChanges("yeni nitelik", s => AnyInstance(s).Attributes["extra"] = AttributeValue.FromFlag(true));
            AssertChanges("nitelik türü", s => AnyInstance(s).Attributes["screen"] = AttributeValue.FromNumber(0));
            AssertChanges("yaş", s => AnyInstance(s).AgeMonths += 1);
            AssertChanges("hafıza", s => AnyInstance(s).StorageGb += 1);
            AssertChanges("model", s => AnyInstance(s).DefinitionId = "phone.other");
            AssertChanges("satıcı", s => AnyInstance(s).SellerNpcId = "npc.other");
            AssertChanges("ilan kimliği", s => AnyInstance(s).ListingId += 5);
            AssertChanges("alım günü", s => AnyInstance(s).AcquiredDay = 3);
            AssertChanges("alış fiyatı", s => AnyInstance(s).PurchasePrice = Money.FromTl(10));
            AssertChanges("maliyet tabanı", s => AnyInstance(s).CostBasis = Money.FromTl(20));
            AssertChanges("konum", s => AnyInstance(s).Location = ProductLocation.Sold);
            AssertChanges("örnek kimliği", s => AnyInstance(s).InstanceId = 99);
            AssertChanges("nakit ve defter (alış)", s => s.InventoryService.Acquire(AnyInstance(s).InstanceId, Money.FromTl(1000), 1));
            AssertChanges("defter (gider)", s => s.EconomyService.ChargeDailyExpense(3));
            AssertChanges("bekleyen ekspertiz", s => s.EconomyService.PayAppraisal(AnyInstance(s).InstanceId, AnyInstance(s).DefinitionId, Money.FromTl(200), 1));
            AssertChanges("yatırım", s => s.InventoryService.UpgradeCapacity(8, Money.FromTl(15000), 1));
        }

        [Test]
        public void Digest_DistinguishesTheLedgerRowFields()
        {
            GameSession a = New();
            GameSession b = New();
            long id = AnyInstanceId(a);
            a.EconomyService.RecordPurchase(id, "phone.x", "npc.kemal", Money.FromTl(1000), 1);
            b.EconomyService.RecordPurchase(id, "phone.x", "npc.selin", Money.FromTl(1000), 1);
            Assert.AreNotEqual(GameStateDigest.Compute(a), GameStateDigest.Compute(b), "NPC farkı");

            GameSession c = New();
            GameSession d = New();
            c.EconomyService.RecordPurchase(id, "phone.x", "npc.kemal", Money.FromTl(1000), 1);
            d.EconomyService.RecordPurchase(id, "phone.x", "npc.kemal", Money.FromTl(1000), 2);
            Assert.AreNotEqual(GameStateDigest.Compute(c), GameStateDigest.Compute(d), "gün farkı");

            GameSession e = New();
            GameSession f = New();
            e.EconomyService.RecordPurchase(id, "phone.x", "npc.kemal", Money.FromTl(1000), 1);
            f.EconomyService.RecordPurchase(id, "phone.y", "npc.kemal", Money.FromTl(1000), 1);
            Assert.AreNotEqual(GameStateDigest.Compute(e), GameStateDigest.Compute(f), "model farkı");
        }

        [Test]
        public void Digest_DistinguishesSaleCostBasisAndWriteOffLinks()
        {
            GameSession a = New();
            GameSession b = New();
            long id = AnyInstanceId(a);
            a.EconomyService.RecordSale(id, "phone.x", "npc.a", Money.FromTl(2000), Money.FromTl(1000), 1);
            b.EconomyService.RecordSale(id, "phone.x", "npc.a", Money.FromTl(2000), Money.FromTl(1100), 1);
            Assert.AreNotEqual(GameStateDigest.Compute(a), GameStateDigest.Compute(b), "maliyet tabanı farkı");

            GameSession c = New();
            c.EconomyService.PayAppraisal(id, "phone.x", Money.FromTl(200), 1);
            string beforeWriteOff = GameStateDigest.Compute(c);
            c.EconomyService.WriteOffAppraisals(id, 1);
            Assert.AreNotEqual(beforeWriteOff, GameStateDigest.Compute(c), "gider yazma");
            string row = GameStateDigest.Describe(c).Split('\n').Single(l => l.StartsWith("L|3|", StringComparison.Ordinal));
            Assert.AreEqual("2", row.Split('|')[10], "gider satırı ilgili ekspertiz satırının kimliğini taşır");
        }

        private static long AnyInstanceId(GameSession s)
        {
            return s.Store.All[0].InstanceId;
        }

        [Test]
        public void Digest_IsIndependentOfWhichInstanceObjectsWereCreated_OnlyOfTheirContent()
        {
            GameSession a = New(5UL);
            GameSession b = New(5UL);

            Assert.AreEqual(GameStateDigest.Describe(a), GameStateDigest.Describe(b));
            Assert.AreEqual(GameStateDigest.Compute(a), GameStateDigest.Compute(b));
        }

        [Test]
        public void PendingAppraisals_AreListedSortedByInstance()
        {
            GameSession s = New();
            ProductInstance first = s.Store.All[0];
            ProductInstance second = s.Store.All[1];
            s.EconomyService.PayAppraisal(second.InstanceId, second.DefinitionId, Money.FromTl(200), 1);
            s.EconomyService.PayAppraisal(first.InstanceId, first.DefinitionId, Money.FromTl(300), 1);

            string[] pending = GameStateDigest.Describe(s).Split('\n')
                .Where(l => l.StartsWith("P|", StringComparison.Ordinal)).ToArray();

            CollectionAssert.AreEqual(new[] { "P|" + first.InstanceId + "|3", "P|" + second.InstanceId + "|2" }, pending);
        }

        [Test]
        public void InventoryItems_AreListedInShelfOrder()
        {
            GameSession s = New();
            long a = s.Store.All[2].InstanceId;
            long b = s.Store.All[0].InstanceId;
            s.InventoryService.Acquire(a, Money.FromTl(100), 1);
            s.InventoryService.Acquire(b, Money.FromTl(100), 1);

            CollectionAssert.Contains(GameStateDigest.Describe(s).Split('\n'), "inventory.items=" + a + "," + b);
        }
    }
}
