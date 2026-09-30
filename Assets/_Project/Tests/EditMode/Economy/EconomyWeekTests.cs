using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Esnaf.Core;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Inventory;
using Esnaf.Domain.Products;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Economy
{
    public class EconomyWeekTests
    {
        private static Money Tl(long value)
        {
            return Money.FromTl(value);
        }

        private static void CheckDay(
            EconomyHarness h, int day, long cash, long stock, long net, long wealth, long assets, long openingCash)
        {
            DaySummary s = h.CloseDay(day);
            string label = "Gün " + day;

            Assert.AreEqual(Tl(openingCash), s.OpeningCash, label + " sabah nakit");
            Assert.AreEqual(Tl(cash), s.ClosingCash, label + " akşam nakit");
            Assert.AreEqual(Tl(stock), s.StockCostBasis, label + " stok (maliyet)");
            Assert.AreEqual(Tl(net), s.NetProfit, label + " gün net kâr");
            Assert.AreEqual(Tl(wealth), s.Wealth.Total, label + " toplam servet");
            Assert.AreEqual(Tl(assets), s.Wealth.GetAmount(WealthKeys.BusinessAssets), label + " işletme varlıkları");
            Assert.AreEqual(Tl(net), s.WealthChange, label + " servet farkı = net kâr");
            h.AssertInvariants(Tl(250000), day, label);
        }

        /// <summary>
        /// GDD v0.3 Bölüm 1.3 "Aktif dengeli oyuncu" haftası. Fiyatlar SABİT verilir (pazarlık yok);
        /// yalnızca defter/envanter/servet aritmetiğini doğrular, denge testi DEĞİLDİR.
        /// </summary>
        [Test]
        public void T6_TheGddWeek_MatchesTheTableExactly()
        {
            var h = new EconomyHarness();

            // Gün 1
            ProductInstance y5 = h.NewMarketInstance("phone.yildiz_y5");
            h.Buy(y5, 4800, 1);
            h.Sell(y5, 5750, 1);
            h.ChargeExpense(1);
            CheckDay(h, 1, cash: 250950, stock: 0, net: 950, wealth: 250950, assets: 0, openingCash: 250000);

            // Gün 2
            ProductInstance n1 = h.NewMarketInstance("phone.nova_n1_lite");
            ProductInstance vegaA3 = h.NewMarketInstance("phone.samsun_vega_a3");
            ProductInstance y5b = h.NewMarketInstance("phone.yildiz_y5");
            h.Buy(n1, 3700, 2);
            h.Buy(vegaA3, 7900, 2);
            h.Buy(y5b, 5100, 2);
            h.Sell(n1, 4400, 2);
            h.Sell(y5b, 5900, 2);
            h.ChargeExpense(2);
            CheckDay(h, 2, cash: 244550, stock: 7900, net: 1500, wealth: 252450, assets: 0, openingCash: 250950);

            // Gün 3
            ProductInstance n3 = h.NewMarketInstance("phone.nova_n3_pro");
            ProductInstance z5 = h.NewMarketInstance("phone.zirve_z5");
            h.PayAppraisal(n3, 200, 3);
            h.Buy(n3, 9100, 3);
            h.PayAppraisal(z5, 200, 3);
            h.Buy(z5, 12000, 3);
            h.Sell(vegaA3, 9000, 3);
            h.ChargeExpense(3);
            CheckDay(h, 3, cash: 231550, stock: 21500, net: 600, wealth: 253050, assets: 0, openingCash: 244550);

            // Gün 4
            ProductInstance y8 = h.NewMarketInstance("phone.yildiz_y8_plus");
            ProductInstance skipped = h.NewMarketInstance("phone.elma_e11");
            h.PayAppraisal(y8, 200, 4);
            h.Buy(y8, 13600, 4);
            h.PayAppraisal(skipped, 200, 4);
            h.WriteOff(skipped, 4);
            h.Sell(n3, 10600, 4);
            h.Sell(z5, 13700, 4);
            h.ChargeExpense(4);
            CheckDay(h, 4, cash: 241350, stock: 13800, net: 2100, wealth: 255150, assets: 0, openingCash: 231550);

            // Gün 5
            ProductInstance s21 = h.NewMarketInstance("phone.samsun_vega_s21");
            ProductInstance e11 = h.NewMarketInstance("phone.elma_e11");
            h.Buy(s21, 15390, 5);
            h.PayAppraisal(e11, 600, 5);
            h.Buy(e11, 16000, 5);
            Assert.IsTrue(h.Shop.UpgradeCapacity(8, Tl(15000), 5).IsSuccess);
            h.Sell(y8, 15700, 5);
            h.ChargeExpense(5);
            CheckDay(h, 5, cash: 209560, stock: 31990, net: 1400, wealth: 256550, assets: 15000, openingCash: 241350);
            Assert.AreEqual(8, h.Inventory.Capacity);

            // Gün 6
            ProductInstance e13 = h.NewMarketInstance("phone.elma_e13_pro");
            ProductInstance vegaA3b = h.NewMarketInstance("phone.samsun_vega_a3");
            h.PayAppraisal(e13, 2000, 6);
            h.Buy(e13, 25900, 6);
            h.PayAppraisal(vegaA3b, 200, 6);
            h.Buy(vegaA3b, 8000, 6);
            Assert.IsTrue(h.Economy.RecordInvestment(Tl(12000), 6, "ledger.memo.test_equipment").IsSuccess);
            h.Sell(s21, 19920, 6);
            h.Sell(e11, 18700, 6);
            h.ChargeExpense(6);
            CheckDay(h, 6, cash: 199580, stock: 36100, net: 6130, wealth: 262680, assets: 27000, openingCash: 209560);

            // Gün 7
            ProductInstance brokenN3 = h.NewMarketInstance("phone.nova_n3_pro");
            h.PayAppraisal(brokenN3, 200, 7);
            h.Buy(brokenN3, 4800, 7);
            Assert.IsTrue(h.Shop.AddRepairCost(brokenN3.InstanceId, Tl(1800), 7).IsSuccess);
            h.Sell(e13, 29900, 7);
            h.Sell(vegaA3b, 9300, 7);
            h.ChargeExpense(7);
            CheckDay(h, 7, cash: 231480, stock: 6800, net: 2600, wealth: 265280, assets: 27000, openingCash: 199580);

            // Hafta özeti (GDD hesap kontrolü)
            List<DaySummary> days = Enumerable.Range(1, 7).Select(d => h.CloseDay(d)).ToList();
            Assert.AreEqual(142870L, days.Sum(d => d.SalesIncome.Tl), "Satış toplamı");
            Assert.AreEqual(124890L, days.SelectMany(d => d.Sales).Sum(x => x.CostBasis.Tl), "Satılanların maliyeti");
            Assert.AreEqual(17980L, days.Sum(d => d.GrossProfit.Tl), "Brüt kâr");
            Assert.AreEqual(200L, days.Sum(d => d.WastedAppraisal.Tl), "Boşa ekspertiz");
            Assert.AreEqual(2500L, days.Sum(d => d.DailyExpense.Tl), "Günlük gider");
            Assert.AreEqual(15280L, days.Sum(d => d.NetProfit.Tl), "Hafta net kâr");
            Assert.AreEqual(11, days.Sum(d => d.Sales.Count), "11 satış");
            Assert.AreEqual(Tl(265280), h.TotalWealth(), "Toplam servet");
            Assert.AreEqual(1, h.Inventory.Count, "Elde yalnızca tamirli N3 Pro");
            Assert.AreEqual(Tl(27000), h.State.BusinessAssets);
        }

        // ---------- rastgele işlem dizileri: değişmezler her adımda korunmalı ----------

        private sealed class FuzzStats
        {
            public int Purchases;
            public int Sales;
            public int Appraisals;
            public int WriteOffs;
            public int Repairs;
            public int Investments;
            public int Expenses;
            public int FailedOps;
        }

        private static string Snapshot(EconomyHarness h)
        {
            var b = new StringBuilder();
            b.Append(h.Cash.Tl).Append('|').Append(h.State.Ledger.Count).Append('|').Append(h.Inventory.Count).Append('|')
                .Append(h.Inventory.Capacity).Append('|').Append(h.State.BusinessAssets.Tl).Append('|');
            foreach (ProductInstance i in h.Store.All)
            {
                b.Append(i.InstanceId).Append(':').Append((int)i.Location).Append(':').Append(i.CostBasis.Tl).Append(':')
                    .Append(h.Economy.PendingAppraisalCost(i.InstanceId).Tl).Append(';');
            }

            return b.ToString();
        }

        private static string LedgerFingerprint(EconomyHarness h)
        {
            var b = new StringBuilder();
            foreach (TransactionRecord r in h.State.Ledger.Records)
            {
                b.Append(r.Id).Append(',').Append(r.Day).Append(',').Append(r.TypeId).Append(',').Append(r.Amount.Tl).Append(',')
                    .Append(r.BalanceAfter.Tl).Append(',').Append(r.InstanceId).Append(',').Append(r.SaleCostBasis).Append(',')
                    .Append(r.RelatedRecordId).Append(';');
            }

            return b.ToString();
        }

        private static string RunRandomSession(ulong seed, int operations, FuzzStats stats, bool assertEveryStep)
        {
            var h = new EconomyHarness();
            var rng = new PcgRandom(seed, 4UL);
            var market = new List<ProductInstance>();
            string[] models = { "phone.nova_n3_pro", "phone.yildiz_y8_plus", "phone.elma_e13_pro" };
            int day = 1;

            for (int step = 0; step < operations; step++)
            {
                string before = Snapshot(h);
                Money cashBefore = h.Cash;
                bool succeeded = true;
                bool wasExpense = false;
                int choice = rng.NextInt(100);
                string op;

                if (choice < 8)
                {
                    op = "new_day";
                    day++;
                    Result<Money> expense = h.Economy.ChargeDailyExpense(day);
                    succeeded = expense.IsSuccess;
                    wasExpense = true;
                    if (expense.IsSuccess && expense.Value.IsPositive)
                    {
                        stats.Expenses++;
                    }
                }
                else if (choice < 30)
                {
                    op = "new_listing";
                    ProductInstance p = h.NewMarketInstance(models[rng.NextInt(models.Length)]);
                    market.Add(p);
                    before = Snapshot(h); // ürünün pazara çıkması başarısız ekspertiz denemesinden bağımsız bir durum değişikliğidir
                    if (rng.NextInt(10) < 6)
                    {
                        long fee = (rng.NextInt(1, 21)) * 100L;
                        succeeded = h.Economy.PayAppraisal(p.InstanceId, p.DefinitionId, Tl(fee), day).IsSuccess;
                        stats.Appraisals += succeeded ? 1 : 0;
                    }
                }
                else if (choice < 50 && market.Count > 0)
                {
                    op = "buy";
                    ProductInstance p = market[rng.NextInt(market.Count)];
                    long price = rng.NextInt(100, 3001) * 10L;
                    succeeded = h.Shop.Acquire(p.InstanceId, Tl(price), day).IsSuccess;
                    if (succeeded)
                    {
                        market.Remove(p);
                        stats.Purchases++;
                    }
                }
                else if (choice < 58 && market.Count > 0)
                {
                    op = "abandon";
                    ProductInstance p = market[rng.NextInt(market.Count)];
                    succeeded = h.Economy.WriteOffAppraisals(p.InstanceId, day).IsSuccess;
                    market.Remove(p);
                    stats.WriteOffs += succeeded ? 1 : 0;
                }
                else if (choice < 82 && h.Inventory.Count > 0)
                {
                    op = "sell";
                    long id = h.Inventory.ItemIds[rng.NextInt(h.Inventory.Count)];
                    ProductInstance p = h.Store.Get(id);
                    long price = Money.RoundTo10(p.CostBasis.Tl * rng.NextInt(60, 151) / 100);
                    if (price <= 0)
                    {
                        price = 10;
                    }

                    succeeded = h.Shop.Sell(id, Tl(price), day, "npc.fuzz").IsSuccess;
                    stats.Sales += succeeded ? 1 : 0;
                }
                else if (choice < 90 && h.Inventory.Count > 0)
                {
                    op = "repair";
                    long id = h.Inventory.ItemIds[rng.NextInt(h.Inventory.Count)];
                    succeeded = h.Shop.AddRepairCost(id, Tl(rng.NextInt(1, 21) * 100L), day).IsSuccess;
                    stats.Repairs += succeeded ? 1 : 0;
                }
                else if (choice < 95)
                {
                    op = "equipment";
                    succeeded = h.Economy.RecordInvestment(Tl(rng.NextInt(1, 16) * 1000L), day).IsSuccess;
                    stats.Investments += succeeded ? 1 : 0;
                }
                else
                {
                    op = "upgrade";
                    succeeded = h.Shop.UpgradeCapacity(h.Inventory.Capacity + rng.NextInt(1, 3), Tl(rng.NextInt(1, 21) * 1000L), day).IsSuccess;
                    stats.Investments += succeeded ? 1 : 0;
                }

                if (!succeeded)
                {
                    stats.FailedOps++;
                    if (assertEveryStep)
                    {
                        Assert.AreEqual(before, Snapshot(h), "Başarısız işlem durumu DEĞİŞTİRMEMELİ (seed " + seed + ", adım " + step + ", " + op + ")");
                    }
                }

                if (assertEveryStep)
                {
                    string context = "(seed " + seed + ", adım " + step + ", " + op + ")";
                    h.AssertInvariants(Tl(250000), day, context);

                    // I9: nakit yalnızca zorunlu günlük giderle negatife düşebilir
                    if (!wasExpense)
                    {
                        Assert.IsTrue(h.Cash.Tl >= 0 || h.Cash.Tl >= cashBefore.Tl, "Nakit gider dışında negatife düştü " + context);
                    }
                }
            }

            return LedgerFingerprint(h);
        }

        [Test]
        public void RandomSessions_KeepEveryInvariant_AndFailedOperationsChangeNothing()
        {
            var stats = new FuzzStats();

            for (ulong seed = 1; seed <= 12; seed++)
            {
                RunRandomSession(seed, 350, stats, assertEveryStep: true);
            }

            // Test boş geçmesin: gerçekten çeşitli işlemler yapıldı mı?
            Assert.Greater(stats.Purchases, 100, "alış");
            Assert.Greater(stats.Sales, 80, "satış");
            Assert.Greater(stats.Appraisals, 100, "ekspertiz");
            Assert.Greater(stats.WriteOffs, 20, "boşa ekspertiz");
            Assert.Greater(stats.Repairs, 20, "tamir");
            Assert.Greater(stats.Investments, 20, "yatırım");
            Assert.Greater(stats.Expenses, 30, "günlük gider");
            Assert.Greater(stats.FailedOps, 10, "başarısız işlem (kapasite/nakit)");
        }

        [Test]
        public void SameSeed_SameLedger_Deterministic()
        {
            string first = RunRandomSession(99UL, 300, new FuzzStats(), assertEveryStep: false);
            string second = RunRandomSession(99UL, 300, new FuzzStats(), assertEveryStep: false);
            string other = RunRandomSession(100UL, 300, new FuzzStats(), assertEveryStep: false);

            Assert.AreEqual(first, second);
            Assert.AreNotEqual(first, other);
            Assert.Greater(first.Length, 1000);
        }
    }
}
