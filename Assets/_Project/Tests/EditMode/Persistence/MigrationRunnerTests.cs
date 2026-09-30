using System;
using System.Collections.Generic;
using Esnaf.Persistence;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Esnaf.Tests.Persistence
{
    public class MigrationRunnerTests
    {
        private sealed class Step : IMigration
        {
            private readonly Action<JObject> _apply;

            public int From { get; }
            public int To { get; }

            public Step(int from, int to, Action<JObject> apply = null)
            {
                From = from;
                To = to;
                _apply = apply ?? (p => { });
            }

            public void Apply(JObject payload)
            {
                _apply(payload);
            }
        }

        [Test]
        public void TheCurrentVersion_NeedsNoMigration()
        {
            var runner = new MigrationRunner(1, new IMigration[0]);

            Assert.IsTrue(runner.Migrate(new JObject(), 1).IsSuccess);
            Assert.AreEqual(1, runner.CurrentVersion);
        }

        [Test]
        public void TheChain_RunsInOrder_AndTransformsThePayload()
        {
            var runner = new MigrationRunner(3, new IMigration[]
            {
                new Step(2, 3, p => p["trail"] = p["trail"] + "C"),
                new Step(0, 1, p => p["trail"] = "A"),
                new Step(1, 2, p => p["trail"] = p["trail"] + "B")
            });
            var payload = new JObject();

            Assert.IsTrue(runner.Migrate(payload, 0).IsSuccess);

            Assert.AreEqual("ABC", (string)payload["trail"]);
        }

        [Test]
        public void StartingMidChain_SkipsTheEarlierSteps()
        {
            var runner = new MigrationRunner(3, new IMigration[]
            {
                new Step(0, 1, p => p["a"] = true),
                new Step(1, 2, p => p["b"] = true),
                new Step(2, 3, p => p["c"] = true)
            });
            var payload = new JObject();

            Assert.IsTrue(runner.Migrate(payload, 2).IsSuccess);

            Assert.IsNull(payload["a"]);
            Assert.IsNull(payload["b"]);
            Assert.IsTrue((bool)payload["c"]);
        }

        [Test]
        public void ANewerVersion_IsRefused()
        {
            var runner = new MigrationRunner(1, new IMigration[0]);

            Assert.AreEqual("save.version_newer", runner.Migrate(new JObject(), 2).ErrorCode);
        }

        [Test]
        public void AnOldVersionWithoutAPath_IsUnsupported()
        {
            var runner = new MigrationRunner(2, new IMigration[] { new Step(1, 2) });

            Assert.AreEqual("save.version_unsupported", runner.Migrate(new JObject(), 0).ErrorCode);
        }

        [Test]
        public void ANegativeVersion_IsUnsupported()
        {
            var runner = new MigrationRunner(1, new IMigration[0]);

            Assert.AreEqual("save.version_unsupported", runner.Migrate(new JObject(), -1).ErrorCode);
        }

        [Test]
        public void AFailingStep_ReportsAMigrationError_AndStopsTheChain()
        {
            bool secondRan = false;
            var runner = new MigrationRunner(2, new IMigration[]
            {
                new Step(0, 1, p => { throw new InvalidOperationException("boom"); }),
                new Step(1, 2, p => secondRan = true)
            });

            var r = runner.Migrate(new JObject(), 0);

            Assert.AreEqual("save.migration", r.ErrorCode);
            StringAssert.Contains("boom", r.Message);
            Assert.IsFalse(secondRan);
        }

        [Test]
        public void TheChainIsValidatedWhenBuilt()
        {
            Assert.Throws<ArgumentException>(() => new MigrationRunner(2, new IMigration[] { new Step(0, 2) }), "adım atlayan");
            Assert.Throws<ArgumentException>(() => new MigrationRunner(2, new IMigration[] { new Step(1, 1) }), "yerinde sayan");
            Assert.Throws<ArgumentException>(() => new MigrationRunner(2, new IMigration[] { new Step(1, 2), new Step(1, 2) }), "yinelenen");
            Assert.Throws<ArgumentException>(() => new MigrationRunner(1, new IMigration[] { new Step(1, 2) }), "güncel sürümün ötesine");
            Assert.Throws<ArgumentException>(() => new MigrationRunner(2, new IMigration[] { new Step(-1, 0) }), "negatif");
            Assert.Throws<ArgumentException>(() => new MigrationRunner(0, new IMigration[0]), "sürüm en az 1");
            Assert.Throws<ArgumentNullException>(() => new MigrationRunner(1, null));
            Assert.Throws<ArgumentNullException>(() => new MigrationRunner(1, new IMigration[] { null }));
        }

        [Test]
        public void Migrate_RejectsANullPayload()
        {
            Assert.Throws<ArgumentNullException>(() => new MigrationRunner(1, new IMigration[0]).Migrate(null, 1));
        }
    }
}
