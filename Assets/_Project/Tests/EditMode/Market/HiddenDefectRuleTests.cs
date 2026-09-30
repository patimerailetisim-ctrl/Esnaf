using System;
using Esnaf.Domain.Market;
using NUnit.Framework;

namespace Esnaf.Tests.Market
{
    public class HiddenDefectRuleTests
    {
        [Test]
        public void IsHidden_MatchesExactValuesOnly_CaseSensitive()
        {
            var rule = new HiddenDefectRule("camera", new[] { "spotted", "faulty" }, "ok");

            Assert.IsTrue(rule.IsHidden("spotted"));
            Assert.IsTrue(rule.IsHidden("faulty"));
            Assert.IsFalse(rule.IsHidden("ok"));
            Assert.IsFalse(rule.IsHidden("Spotted"), "kimlikler büyük/küçük harfe duyarlıdır");
            Assert.IsFalse(rule.IsHidden("FAULTY"));
            Assert.IsFalse(rule.IsHidden(null));
        }

        [Test]
        public void HiddenValues_AreACopy()
        {
            var values = new System.Collections.Generic.List<string> { "spotted" };
            var rule = new HiddenDefectRule("camera", values, "ok");

            values.Add("faulty");

            Assert.AreEqual(1, rule.HiddenValues.Count);
            Assert.Throws<ArgumentNullException>(() => new HiddenDefectRule("camera", null, "ok"));
        }
    }
}
