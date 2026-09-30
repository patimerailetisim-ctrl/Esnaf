using System;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Content;
using Esnaf.Domain.Game;
using Esnaf.Domain.Market;
using Esnaf.Persistence;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Persistence
{
    public class AutoSaverTests
    {
        private sealed class Rig
        {
            public readonly InMemorySaveStorage Storage = new InMemorySaveStorage();
            public readonly SaveService Service;
            public readonly GameSession Session;
            public readonly AutoSaver Saver;
            public long Playtime = 100;

            public Rig(ulong seed = 1UL)
            {
                Service = new SaveService(
                    Storage, new SaveSerializer(), new FakeSaveClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)), "0.1.0");
                Session = GameSession.NewGame(MarketHarness.RealContent(), seed);
                Saver = new AutoSaver(Session, Service, () => Playtime);
            }

            public GameSession Reload()
            {
                LoadOutcome o = Service.Load(MarketHarness.RealContent());
                Assert.IsNotNull(o.Session, o.Detail);
                return o.Session;
            }
        }

        [Test]
        public void NothingIsSaved_UntilATriggerFires()
        {
            var rig = new Rig();

            Assert.AreEqual(0, rig.Saver.SaveCount);
            Assert.IsTrue(rig.Saver.LastResult.IsSuccess, "hiç kayıt yapılmadıysa başarılı sayılır");
            Assert.AreEqual(0, rig.Storage.List().Count);
        }

        [Test]
        public void EndingTheDay_SavesTheNewDay()
        {
            var rig = new Rig();

            Assert.IsTrue(rig.Session.Api.EndDay().IsSuccess);

            Assert.AreEqual(1, rig.Saver.SaveCount);
            GameSession back = rig.Reload();
            Assert.AreEqual(2, back.Api.GetDay(), "gün sonu adım 8: yeni gün açıldıktan sonra kaydedilir");
            Assert.AreEqual(rig.Session.Api.GetStateDigest(), back.Api.GetStateDigest());
        }

        [Test]
        public void ADeal_SavesTheShelfAndNoOpenNegotiation()
        {
            var rig = new Rig();
            MarketListing g = rig.Session.Market.Listings.Single(l => l.IsGuided);
            rig.Session.Api.StartNegotiation(g.ListingId);

            rig.Session.Api.MakeOffer(Money.FromTl(5800));

            Assert.AreEqual(1, rig.Saver.SaveCount);
            GameSession back = rig.Reload();
            Assert.AreEqual(1, back.Api.GetInventory().Count);
            Assert.IsNull(back.Api.GetNegotiation());
            Assert.AreEqual(rig.Session.Api.GetStateDigest(), back.Api.GetStateDigest());
        }

        [Test]
        public void ASale_IsSaved()
        {
            var rig = new Rig();
            MarketListing g = rig.Session.Market.Listings.Single(l => l.IsGuided);
            rig.Session.Api.StartNegotiation(g.ListingId);
            rig.Session.Api.MakeOffer(Money.FromTl(5800));
            rig.Session.Api.SetPrice(g.InstanceId, Money.FromTl(5900));
            int before = rig.Saver.SaveCount;
            rig.Session.Api.StartSale(rig.Session.Api.GetCustomers()[0].CustomerId);

            rig.Session.Api.AskPrice(Money.FromTl(1000));

            Assert.AreEqual(before + 1, rig.Saver.SaveCount);
            GameSession back = rig.Reload();
            Assert.AreEqual(0, back.Api.GetInventory().Count);
            Assert.AreEqual(rig.Session.Api.GetStateDigest(), back.Api.GetStateDigest());
        }

        [Test]
        public void AnAppraisal_IsSaved_WithItsLockedResult()
        {
            var rig = new Rig();
            for (int i = 0; i < 2; i++)
            {
                rig.Session.Api.EndDay();
            }

            int before = rig.Saver.SaveCount;
            long listing = rig.Session.Market.Listings.First(l => l.DayListed == 3).ListingId;

            Assert.IsTrue(rig.Session.Api.StartAppraisal(listing, "s1").IsSuccess);

            Assert.AreEqual(before + 1, rig.Saver.SaveCount);
            GameSession back = rig.Reload();
            Assert.AreEqual(1, back.Api.GetAppraisals(listing).Count);
            Assert.AreEqual(rig.Session.Api.GetStateDigest(), back.Api.GetStateDigest());
        }

        [Test]
        public void StartingOrAskingWithoutAResult_DoesNotSave()
        {
            var rig = new Rig();
            MarketListing g = rig.Session.Market.Listings.Single(l => l.IsGuided);

            rig.Session.Api.StartNegotiation(g.ListingId);
            rig.Session.Api.MakeOffer(Money.FromTl(4000));
            rig.Session.Api.WalkAway();

            Assert.AreEqual(0, rig.Saver.SaveCount);
        }

        [Test]
        public void ThePlayTimeProvider_IsAskedAtSaveTime()
        {
            var rig = new Rig();
            rig.Playtime = 4242;

            rig.Session.Api.EndDay();

            var header = new SaveSerializer().Parse(rig.Storage.Files["slot0.json"]).Value.Header;
            Assert.AreEqual(4242L, header.PlayTimeSeconds);
        }

        [Test]
        public void AFailingSave_DoesNotBreakTheGame_AndIsReported()
        {
            var rig = new Rig();
            rig.Storage.FailOn("write", "slot0.tmp");

            Result r = rig.Session.Api.EndDay();

            Assert.IsTrue(r.IsSuccess, "kayıt hatası gün sonunu bozmaz");
            Assert.AreEqual(2, rig.Session.Api.GetDay());
            Assert.IsTrue(rig.Saver.LastResult.IsFailure);
            Assert.AreEqual(0, rig.Saver.SaveCount);
            rig.Storage.ClearFaults();
            rig.Session.Api.EndDay();
            Assert.IsTrue(rig.Saver.LastResult.IsSuccess);
            Assert.AreEqual(1, rig.Saver.SaveCount);
        }

        [Test]
        public void Dispose_StopsTheAutoSaves()
        {
            var rig = new Rig();
            rig.Saver.Dispose();

            rig.Session.Api.EndDay();

            Assert.AreEqual(0, rig.Saver.SaveCount);
            Assert.AreEqual(0, rig.Storage.List().Count);
            rig.Saver.Dispose();
        }

        [Test]
        public void ASaveInsideATrigger_DoesNotRecurse()
        {
            var rig = new Rig();
            int saved = 0;
            rig.Session.Bus.Subscribe<GameSaved>(e => saved++);

            rig.Session.Api.EndDay();

            Assert.AreEqual(1, saved);
        }

        [Test]
        public void ATriggerRaisedWhileSaving_IsIgnored()
        {
            var rig = new Rig();
            bool raised = false;
            rig.Session.Bus.Subscribe<GameSaved>(e =>
            {
                if (!raised)
                {
                    raised = true;
                    rig.Session.Bus.Publish(new Esnaf.Domain.Time.AutoSaveRequested(99));
                }
            });

            rig.Session.Api.EndDay();

            Assert.IsTrue(raised);
            Assert.AreEqual(1, rig.Saver.SaveCount, "kayıt sürerken gelen tetik yok sayılır");
        }

        [Test]
        public void Constructor_ChecksItsArguments()
        {
            var rig = new Rig();
            Assert.Throws<ArgumentNullException>(() => new AutoSaver(null, rig.Service, () => 0));
            Assert.Throws<ArgumentNullException>(() => new AutoSaver(rig.Session, null, () => 0));
            Assert.Throws<ArgumentNullException>(() => new AutoSaver(rig.Session, rig.Service, null));
        }
    }
}
