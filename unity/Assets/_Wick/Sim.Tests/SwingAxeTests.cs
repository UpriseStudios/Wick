using NUnit.Framework;
using static Wick.Sim.Tests.TrapTests;

namespace Wick.Sim.Tests
{
    /// <summary>
    /// Swing Axe (Vault Level 4): its blade passes over its tile twice a cycle, live for 6 ticks
    /// each time; level 5 also catches the tile in front at the top of the arc.
    /// </summary>
    public class SwingAxeTests
    {
        // Floor x = 1..10 on row 1, entrance (1,1).
        private static readonly string[] Corridor =
        {
            "############",
            "#E.........#",
            "############",
        };

        private static VaultDef Axe(int level, int x = 1, int cycle = 60, int rot = 1)
        {
            VaultDef vault = Vault(Corridor, new[] { Obj(1, "swing_axe", x, 1, cycle: cycle, rot: rot) });
            return level == 1 ? vault : vault.WithDefenceLevels(DefenceLevels.Base.With(ObjectType.SwingAxe, level));
        }

        [Test]
        public void TheBladeIsLive_TwiceACycle()
        {
            ObjectDef axe = Axe(1).GetObject(0);
            var live = new System.Collections.Generic.List<int>();
            for (int t = 0; t < 60; t++)
            {
                if (RaidSim.AxeHits(t, axe, 1, axe.Pos))
                {
                    live.Add(t);
                }
            }

            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5, 30, 31, 32, 33, 34, 35 }, live);
            Assert.IsFalse(RaidSim.AxeHits(15, axe, 1, axe.Pos.Step(axe.Facing)), "level 1 never reaches the next tile");
        }

        [Test]
        public void StandingUnderIt_CostsAFlickerEachPass()
        {
            // Over the entrance, cycle 60: hits at 0, then the return at 30 lands inside invulnerability,
            // the downswing at 60 lands.
            var sim = new RaidSim(Axe(1), 1, InputLog.Empty);
            CollectionAssert.AreEqual(new[] { 0, 60 }, TicksOf(sim, SimEventType.FlickerLost, 61));
        }

        [Test]
        public void ItFires_OnEachSwing()
        {
            var sim = new RaidSim(Axe(1, x: 5), 1, InputLog.Empty);
            CollectionAssert.AreEqual(new[] { 0, 30, 60 }, TicksOf(sim, SimEventType.TrapFired, 61));
        }

        [Test]
        public void Level5_CatchesTheTileInFront_AtTheTopOfTheArc()
        {
            // Level 5: cycle 51, quarter 12. Facing east from (1,1), so (2,1) is caught on ticks 12-17.
            ObjectDef axe = Axe(5).GetObject(0);
            TilePos front = axe.Pos.Step(axe.Facing);
            Assert.IsTrue(RaidSim.AxeHits(12, axe, 5, front));
            Assert.IsTrue(RaidSim.AxeHits(17, axe, 5, front));
            Assert.IsFalse(RaidSim.AxeHits(18, axe, 5, front));
            Assert.IsFalse(RaidSim.AxeHits(12, axe, 4, front));
        }

        [Test]
        public void ItsPassesNeverOverlap_EvenAtTheShortestCycle()
        {
            Assert.AreEqual(28, ObjectTypes.MinCycle(ObjectType.SwingAxe));
            Assert.AreEqual(28, DefenceStats.Cycle(Axe(5, cycle: 30).GetObject(0), 5), "level 5 can't go below the minimum");
        }

        [Test]
        public void ItUnlocksAtVaultLevel4_AndCosts22()
        {
            Assert.AreEqual(4, VaultRules.RequiredLevel(ObjectType.SwingAxe));
            Assert.AreEqual(22, VaultRules.Cost(ObjectType.SwingAxe));
            Assert.IsTrue(ObjectTypes.IsTimedTrap(ObjectType.SwingAxe));
            Assert.IsTrue(ObjectTypes.IsUpgradable(ObjectType.SwingAxe));
        }
    }
}
