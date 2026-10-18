using NUnit.Framework;

namespace Wick.Sim.Tests
{
    public class CrownRulesTests
    {
        [TestCase(-500, 0)]
        [TestCase(-200, 0)]
        [TestCase(-199, 1)]
        [TestCase(-50, 1)]
        [TestCase(-49, 2)]
        [TestCase(0, 2)]
        [TestCase(49, 2)]
        [TestCase(50, 3)]
        [TestCase(199, 3)]
        [TestCase(200, 4)]
        [TestCase(900, 4)]
        public void GapBands(int gap, int band)
        {
            Assert.AreEqual(band, CrownRules.Band(gap));
        }

        [Test]
        public void EvenMatch_ThreeStars_Gains24_AndTheDefenderLosesThem()
        {
            CrownChange c = CrownRules.Apply(500, 500, 3, false);
            Assert.AreEqual(24, c.RaiderDelta);
            Assert.AreEqual(-24, c.DefenderDelta);
            Assert.AreEqual(524, c.Raider);
            Assert.AreEqual(476, c.Defender);
        }

        [Test]
        public void EvenMatch_Failure_Costs14_AndTheDefenderGainsThem()
        {
            CrownChange c = CrownRules.Apply(500, 500, 0, false);
            Assert.AreEqual(-14, c.RaiderDelta);
            Assert.AreEqual(14, c.DefenderDelta);
        }

        [Test]
        public void BeatingAStrongerVault_EarnsMore_FailingAgainstItCostsLess()
        {
            Assert.AreEqual(36, CrownRules.Apply(100, 400, 3, false).RaiderDelta, "300 above");
            Assert.AreEqual(12, CrownRules.Apply(400, 100, 3, false).RaiderDelta, "300 below");
            Assert.AreEqual(-8, CrownRules.Apply(100, 400, 0, false).RaiderDelta);
            Assert.AreEqual(-20, CrownRules.Apply(400, 100, 0, false).RaiderDelta);
        }

        [Test]
        public void MoreStars_NeverEarnLess()
        {
            for (int band = 0; band <= 4; band++)
            {
                for (int stars = 1; stars <= 3; stars++)
                {
                    Assert.Greater(CrownRules.RaiderDelta(stars, band), CrownRules.RaiderDelta(stars - 1, band));
                }
            }
        }

        [Test]
        public void NobodyDropsBelowZero()
        {
            CrownChange fail = CrownRules.Apply(5, 0, 0, false);
            Assert.AreEqual(0, fail.Raider);
            Assert.AreEqual(-5, fail.RaiderDelta, "only what they had");

            CrownChange win = CrownRules.Apply(0, 10, 3, false);
            Assert.AreEqual(0, win.Defender);
            Assert.AreEqual(-10, win.DefenderDelta);
        }

        [Test]
        public void BotDefenders_NeverChange()
        {
            CrownChange c = CrownRules.Apply(300, 300, 3, true);
            Assert.AreEqual(24, c.RaiderDelta);
            Assert.AreEqual(300, c.Defender);
            Assert.AreEqual(0, c.DefenderDelta);
        }
    }
}
