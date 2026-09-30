using System;
using Esnaf.Core;
using NUnit.Framework;

namespace Esnaf.Tests.Core
{
    public class ResultTests
    {
        [Test]
        public void Ok_IsSuccess()
        {
            Result result = Result.Ok();
            Assert.IsTrue(result.IsSuccess);
            Assert.IsFalse(result.IsFailure);
            Assert.IsNull(result.ErrorCode);
        }

        [Test]
        public void Fail_CarriesCodeAndMessage()
        {
            Result result = Result.Fail("cash.insufficient", "Need 27.000 TL");
            Assert.IsTrue(result.IsFailure);
            Assert.AreEqual("cash.insufficient", result.ErrorCode);
            Assert.AreEqual("Need 27.000 TL", result.Message);
        }

        [Test]
        public void Fail_RequiresErrorCode()
        {
            Assert.Throws<ArgumentException>(() => Result.Fail(null));
            Assert.Throws<ArgumentException>(() => Result.Fail(""));
        }

        [Test]
        public void DefaultResult_IsSuccess()
        {
            Assert.IsTrue(default(Result).IsSuccess);
        }

        [Test]
        public void GenericOk_ExposesValue()
        {
            Result<int> result = Result<int>.Ok(7);
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(7, result.Value);
        }

        [Test]
        public void GenericFail_ThrowsWhenReadingValue()
        {
            Result<int> result = Result<int>.Fail("shelf.full");
            Assert.IsTrue(result.IsFailure);
            Assert.AreEqual("shelf.full", result.ErrorCode);
            Assert.Throws<InvalidOperationException>(() => { int _ = result.Value; });
        }

        [Test]
        public void Generic_ConvertsToNonGeneric()
        {
            Result ok = Result<string>.Ok("x");
            Result fail = Result<string>.Fail("a.b", "msg");

            Assert.IsTrue(ok.IsSuccess);
            Assert.IsTrue(fail.IsFailure);
            Assert.AreEqual("a.b", fail.ErrorCode);
            Assert.AreEqual("msg", fail.Message);
        }
    }

    public class IdGeneratorTests
    {
        [Test]
        public void Next_StartsAtOne_AndIncrements()
        {
            var ids = new IdGenerator();
            Assert.AreEqual(1L, ids.Next());
            Assert.AreEqual(2L, ids.Next());
            Assert.AreEqual(3L, ids.Next());
            Assert.AreEqual(3L, ids.LastIssued);
        }

        [Test]
        public void Restore_ContinuesFromSavedCounter()
        {
            var ids = new IdGenerator();
            ids.Next();
            ids.Next();
            long saved = ids.LastIssued;

            var loaded = new IdGenerator();
            loaded.Restore(saved);
            Assert.AreEqual(3L, loaded.Next());

            Assert.AreEqual(1043L, new IdGenerator(1042).Next());
        }

        [Test]
        public void NegativeCounter_Rejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new IdGenerator(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new IdGenerator().Restore(-5));
        }

        [Test]
        public void Overflow_Throws()
        {
            var ids = new IdGenerator(long.MaxValue);
            Assert.Throws<OverflowException>(() => ids.Next());
        }
    }
}
