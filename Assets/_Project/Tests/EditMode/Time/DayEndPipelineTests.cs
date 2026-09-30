using System;
using System.Collections.Generic;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Time;
using NUnit.Framework;

namespace Esnaf.Tests.Time
{
    public class DayEndPipelineTests
    {
        private sealed class FakeStep : IDayEndStep
        {
            private readonly List<string> _log;
            private readonly Result _result;

            public FakeStep(string id, int order, List<string> log, Result? result = null)
            {
                Id = id;
                Order = order;
                _log = log;
                _result = result ?? Result.Ok();
            }

            public string Id { get; }
            public int Order { get; }

            public Result Execute(DayEndContext context)
            {
                _log.Add(Id + "@" + context.Day);
                return _result;
            }
        }

        [Test]
        public void Run_ExecutesStepsInAscendingOrder_AndRecordsThem()
        {
            var log = new List<string>();
            var pipeline = new DayEndPipeline(new IDayEndStep[]
            {
                new FakeStep("a", 2, log), new FakeStep("b", 4, log), new FakeStep("c", 7, log)
            });
            var ctx = new DayEndContext(3);

            Result result = pipeline.Run(ctx);

            Assert.IsTrue(result.IsSuccess);
            CollectionAssert.AreEqual(new[] { "a@3", "b@3", "c@3" }, log);
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, ctx.ExecutedStepIds.ToArray());
        }

        [Test]
        public void Steps_ExposeTheConfiguredSteps()
        {
            var log = new List<string>();
            var a = new FakeStep("a", 2, log);
            var b = new FakeStep("b", 8, log);

            var pipeline = new DayEndPipeline(new IDayEndStep[] { a, b });

            Assert.AreSame(a, pipeline.Steps[0]);
            Assert.AreSame(b, pipeline.Steps[1]);
            Assert.AreEqual(2, pipeline.Steps.Count);
            Assert.IsFalse(pipeline.Steps is List<IDayEndStep>);
        }

        [Test]
        public void Run_StopsAtTheFirstFailure_AndReportsTheStep()
        {
            var log = new List<string>();
            var pipeline = new DayEndPipeline(new IDayEndStep[]
            {
                new FakeStep("a", 2, log),
                new FakeStep("boom", 4, log, Result.Fail("x.failed", "it broke")),
                new FakeStep("c", 7, log)
            });
            var ctx = new DayEndContext(5);

            Result result = pipeline.Run(ctx);

            Assert.IsTrue(result.IsFailure);
            Assert.AreEqual("x.failed", result.ErrorCode);
            StringAssert.Contains("boom", result.Message);
            StringAssert.Contains("it broke", result.Message);
            CollectionAssert.AreEqual(new[] { "a@5", "boom@5" }, log, "sonraki adım çalışmamalı");
            CollectionAssert.AreEqual(new[] { "a" }, ctx.ExecutedStepIds.ToArray(), "başarısız adım 'çalıştı' sayılmaz");
        }

        [Test]
        public void EmptyPipeline_IsAllowedAndDoesNothing()
        {
            var pipeline = new DayEndPipeline(new IDayEndStep[0]);

            Assert.IsTrue(pipeline.Run(new DayEndContext(1)).IsSuccess);
        }

        [Test]
        public void Constructor_RejectsInvalidStepLists()
        {
            var log = new List<string>();

            Assert.Throws<ArgumentNullException>(() => new DayEndPipeline(null));
            Assert.Throws<ArgumentNullException>(() => new DayEndPipeline(new IDayEndStep[] { null }));
            Assert.Throws<ArgumentException>(() => new DayEndPipeline(new IDayEndStep[] { new FakeStep("a", 0, log) }), "sıra 1'den küçük");
            Assert.Throws<ArgumentException>(() => new DayEndPipeline(new IDayEndStep[] { new FakeStep("a", 9, log) }), "sıra 8'den büyük");
            Assert.Throws<ArgumentException>(
                () => new DayEndPipeline(new IDayEndStep[] { new FakeStep("a", 4, log), new FakeStep("b", 2, log) }), "azalan sıra");
            Assert.Throws<ArgumentException>(
                () => new DayEndPipeline(new IDayEndStep[] { new FakeStep("a", 4, log), new FakeStep("b", 4, log) }), "aynı sıra");
            Assert.Throws<ArgumentException>(
                () => new DayEndPipeline(new IDayEndStep[] { new FakeStep("a", 2, log), new FakeStep("a", 4, log) }), "aynı kimlik");
            Assert.Throws<ArgumentException>(
                () => new DayEndPipeline(new IDayEndStep[] { new FakeStep("", 2, log) }), "boş kimlik");
        }

        [Test]
        public void Constructor_AcceptsTheExtremeOrders()
        {
            var log = new List<string>();

            Assert.DoesNotThrow(() => new DayEndPipeline(new IDayEndStep[] { new FakeStep("a", 1, log), new FakeStep("b", 8, log) }));
        }

        [Test]
        public void Run_RejectsANullContext()
        {
            Assert.Throws<ArgumentNullException>(() => new DayEndPipeline(new IDayEndStep[0]).Run(null));
        }
    }
}
