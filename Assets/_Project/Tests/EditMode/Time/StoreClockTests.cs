using System.Collections.Generic;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Game;
using Esnaf.Domain.Time;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Time
{
    /// <summary>
    /// Günlük mağaza saati (Gün 12.1): açılış 09:00, kapanış 21:00; gerçek zamanlı değil, yalnızca oyun aksiyonlarıyla (IGameApi.AdvanceTime) ilerler. Gün sonu
    /// saati sıfırlar; kayıt isteğe bağlı ve geriye uyumludur; RNG'ye dokunulmaz; mevcut müşteri/satış davranışı saatten etkilenmez.
    /// </summary>
    public class StoreClockTests
    {
        private static GameSession New(ulong seed = 1UL, IEventBus bus = null)
        {
            return GameSession.NewGame(MarketHarness.RealContent(), seed, bus);
        }

        // ---------- saat ve sabitler ----------

        [Test]
        public void TheStoreHours_AreNineToTwentyOne_AndFormatAsHHmm()
        {
            Assert.AreEqual(540, StoreHours.OpenMinute);
            Assert.AreEqual(1260, StoreHours.CloseMinute);
            Assert.AreEqual(720, StoreHours.OpenMinutes);
            Assert.AreEqual("09:00", StoreHours.Format(StoreHours.OpenMinute));
            Assert.AreEqual("21:00", StoreHours.Format(StoreHours.CloseMinute));
            Assert.AreEqual("12:05", StoreHours.Format(12 * 60 + 5));
            Assert.IsTrue(StoreHours.IsValidMinute(540) && StoreHours.IsValidMinute(1260));
            Assert.IsFalse(StoreHours.IsValidMinute(539) || StoreHours.IsValidMinute(1261));
        }

        [Test]
        public void ANewGame_OpensTheStoreAtNine()
        {
            GameSession s = New();

            ClockView clock = s.Api.GetClock();

            Assert.AreEqual(1, clock.Day);
            Assert.AreEqual(540, clock.MinuteOfDay);
            Assert.AreEqual("09:00", clock.Text);
            Assert.AreEqual(9, clock.Hour);
            Assert.AreEqual(0, clock.Minute);
            Assert.IsTrue(clock.IsOpen);
            Assert.AreEqual(720, clock.MinutesLeft);
        }

        // ---------- ilerleme ----------

        [Test]
        public void TheClock_AdvancesOnlyByGameActions_NotByRealTime()
        {
            GameSession s = New();
            string first = s.Api.GetClock().Text;

            System.Threading.Thread.Sleep(30);

            Assert.AreEqual(first, s.Api.GetClock().Text, "gerçek zaman saati ilerletmez");
            Assert.IsTrue(s.Api.AdvanceTime(45).IsSuccess);
            Assert.AreEqual("09:45", s.Api.GetClock().Text);
        }

        [Test]
        public void AdvancingTime_IsDeterministic_TheSameActionsGiveTheSameClock()
        {
            GameSession a = New(7UL);
            GameSession b = New(7UL);

            foreach (int m in new[] { 15, 30, 5, 120, 45 })
            {
                a.Api.AdvanceTime(m);
                b.Api.AdvanceTime(m);
            }

            Assert.AreEqual(a.Api.GetClock().MinuteOfDay, b.Api.GetClock().MinuteOfDay);
            Assert.AreEqual(540 + 15 + 30 + 5 + 120 + 45, a.Api.GetClock().MinuteOfDay);
            Assert.AreEqual(a.Api.GetStateDigest(), b.Api.GetStateDigest());
        }

        [Test]
        public void TheClockStopsAtClosingTime_AndNeverPassesIt()
        {
            GameSession s = New();

            Result<ClockView> r = s.Api.AdvanceTime(5000);

            Assert.IsTrue(r.IsSuccess, r.ErrorCode);
            Assert.AreEqual(1260, r.Value.MinuteOfDay, "21:00'de durur");
            Assert.AreEqual("21:00", r.Value.Text);
            Assert.IsFalse(r.Value.IsOpen);
            Assert.AreEqual(0, r.Value.MinutesLeft);
        }

        [Test]
        public void OnceClosed_FurtherAdvancingIsRefused_AndNothingChanges()
        {
            GameSession s = New();
            s.Api.AdvanceTime(720);
            string digest = s.Api.GetStateDigest();

            Result<ClockView> r = s.Api.AdvanceTime(10);

            Assert.AreEqual("time.store_closed", r.ErrorCode);
            Assert.AreEqual(1260, s.Api.GetClock().MinuteOfDay);
            Assert.AreEqual(digest, s.Api.GetStateDigest());
        }

        [Test]
        public void InvalidMinutes_AreRefused_AndNothingChanges()
        {
            GameSession s = New();
            string digest = s.Api.GetStateDigest();

            Assert.AreEqual("time.invalid", s.Api.AdvanceTime(0).ErrorCode);
            Assert.AreEqual("time.invalid", s.Api.AdvanceTime(-30).ErrorCode);
            Assert.AreEqual(digest, s.Api.GetStateDigest());
            Assert.AreEqual(540, s.Api.GetClock().MinuteOfDay);
        }

        [Test]
        public void StoreClosed_IsPublishedExactlyOnce_WhenTheClockReachesClosing()
        {
            var bus = new EventBus();
            var closed = new List<StoreClosed>();
            bus.Subscribe<StoreClosed>(closed.Add);
            GameSession s = New(1UL, bus);

            s.Api.AdvanceTime(600);
            Assert.AreEqual(0, closed.Count, "henüz 19:00");
            s.Api.AdvanceTime(120);
            s.Api.AdvanceTime(10);

            Assert.AreEqual(1, closed.Count);
            Assert.AreEqual(1, closed[0].Day);
        }

        [Test]
        public void ReachingClosing_DoesNotEndTheDay()
        {
            GameSession s = New();

            s.Api.AdvanceTime(720);

            Assert.AreEqual(1, s.Api.GetDay(), "gün bitmez; Günü Bitir oyuncunun kararı");
        }

        // ---------- gün sonu ----------

        [Test]
        public void EndingTheDay_ResetsTheClockToNine_WhateverTheTime()
        {
            foreach (int minutes in new[] { 0, 90, 5000 })
            {
                GameSession s = New();
                if (minutes > 0)
                {
                    s.Api.AdvanceTime(minutes);
                }

                Assert.IsTrue(s.Api.EndDay().IsSuccess, "gün " + minutes + " dakika sonra da bitebilir");

                Assert.AreEqual(2, s.Api.GetDay());
                Assert.AreEqual("09:00", s.Api.GetClock().Text);
                Assert.IsTrue(s.Api.GetClock().IsOpen);
            }
        }

        // ---------- kayıt ----------

        [Test]
        public void TheClock_SurvivesSaveAndLoad()
        {
            GameSession s = New();
            s.Api.AdvanceTime(150);

            Result<GameSession> back = GameSession.Restore(MarketHarness.RealContent(), s.Capture());

            Assert.IsTrue(back.IsSuccess, back.ErrorCode + ": " + back.Message);
            Assert.AreEqual(690, back.Value.Api.GetClock().MinuteOfDay);
            Assert.AreEqual("11:30", back.Value.Api.GetClock().Text);
            Assert.AreEqual(s.Api.GetStateDigest(), back.Value.Api.GetStateDigest());
        }

        [Test]
        public void AClosedStore_SurvivesSaveAndLoad()
        {
            GameSession s = New();
            s.Api.AdvanceTime(5000);

            GameSession back = GameSession.Restore(MarketHarness.RealContent(), s.Capture()).Value;

            Assert.AreEqual(1260, back.Api.GetClock().MinuteOfDay);
            Assert.IsFalse(back.Api.GetClock().IsOpen);
        }

        [Test]
        public void AtOpeningTime_TheSaveHasNoClockField_SoOldSavesAndDigestsStayTheSame()
        {
            GameSession s = New();

            GameSnapshot snap = s.Capture();

            Assert.IsNull(snap.Time.Minute, "açılışta alan yazılmaz");
            Assert.IsFalse(Esnaf.Domain.Game.GameStateDigest.Describe(s).Contains("minute="), "açılışta sağlama metni eskisiyle aynı");
            Assert.AreEqual(540, GameSession.Restore(MarketHarness.RealContent(), snap).Value.Api.GetClock().MinuteOfDay, "alan yoksa gün açılışta başlar");
        }

        [Test]
        public void AnAdvancedClock_ShowsUpInTheDigest_AndInTheSnapshotOnly()
        {
            GameSession s = New();
            string before = s.Api.GetStateDigest();

            s.Api.AdvanceTime(30);

            Assert.AreNotEqual(before, s.Api.GetStateDigest());
            Assert.AreEqual(570, s.Capture().Time.Minute);
        }

        [Test]
        public void AnInvalidSavedMinute_IsRefused()
        {
            GameSession s = New();
            foreach (int bad in new[] { 0, 100, 539, 1261, 2000, -5 })
            {
                GameSnapshot snap = s.Capture();
                snap.Time.Minute = bad;

                Result<GameSession> back = GameSession.Restore(MarketHarness.RealContent(), snap);

                Assert.IsTrue(back.IsFailure, "dakika " + bad);
                Assert.AreEqual("save.invalid", back.ErrorCode);
            }
        }

        [Test]
        public void TheSavedClock_RoundTripsThroughJson()
        {
            GameSession s = New();
            s.Api.AdvanceTime(75);
            GameSnapshot snap = s.Capture();

            string json = Newtonsoft.Json.JsonConvert.SerializeObject(snap);
            GameSnapshot read = Newtonsoft.Json.JsonConvert.DeserializeObject<GameSnapshot>(json);

            StringAssert.Contains("\"Minute\":615", json);
            Assert.AreEqual(615, read.Time.Minute);
            string open = Newtonsoft.Json.JsonConvert.SerializeObject(New().Capture());
            Assert.IsFalse(open.Contains("\"Minute\""), "açılışta JSON'da alan yok");
        }

        // ---------- mevcut sistemler ----------

        [Test]
        public void TheClock_DoesNotTouchTheRng()
        {
            GameSession s = New();
            var rng = s.Capture().Rng;

            s.Api.AdvanceTime(30);
            s.Api.AdvanceTime(5000);
            s.Api.AdvanceTime(10);
            s.Api.GetClock();

            Assert.IsNull(DeepCompare.FirstDifference(rng, s.Capture().Rng));
        }

        [Test]
        public void BuySideActions_DoNotMoveTheClock_AndTheClockDoesNotChangeThem()
        {
            // Alış tarafı (ilan satın alma, pazarlık, ekspertiz, toptan) saat harcamaz ve saatten bağımsızdır; müşteri/satış aksiyonlarının süreleri Gün 12.4'tedir (InteractionTimeTests).
            GameSession a = New(5UL);
            GameSession b = New(5UL);
            b.Api.AdvanceTime(5000); // b: mağaza kapalı

            long listingA = a.Api.GetListings()[0].ListingId;
            long listingB = b.Api.GetListings()[0].ListingId;
            Result<Money> boughtA = a.Api.BuyListing(listingA);
            Result<Money> boughtB = b.Api.BuyListing(listingB);

            Assert.IsTrue(boughtA.IsSuccess);
            Assert.IsTrue(boughtB.IsSuccess, "kapalıyken de mevcut davranış aynı");
            Assert.AreEqual(boughtA.Value, boughtB.Value);
            Assert.AreEqual(540, a.Api.GetClock().MinuteOfDay, "satın alma saati ilerletmedi");
            Assert.AreEqual(a.Api.GetCash(), b.Api.GetCash());
        }
    }
}
