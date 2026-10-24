using NUnit.Framework;
using static Wick.Sim.Tests.TrapTests;

namespace Wick.Sim.Tests
{
    /// <summary>
    /// Mimic Chest (Vault Level 5): a fake treasure pile that winds up while Wick is on or beside
    /// it and bites for 2 flickers; stepping away resets it.
    /// </summary>
    public class MimicChestTests
    {
        // Floor x = 1..10 on row 1, entrance (1,1).
        private static readonly string[] Corridor =
        {
            "############",
            "#E.........#",
            "############",
        };

        private static VaultDef Mimic(int level, int x, params ObjectSpec[] more)
        {
            var objects = new System.Collections.Generic.List<ObjectSpec> { Obj(1, "mimic_chest", x, 1) };
            objects.AddRange(more);
            VaultDef vault = Vault(Corridor, objects.ToArray());
            return level == 1 ? vault : vault.WithDefenceLevels(DefenceLevels.Base.With(ObjectType.MimicChest, level));
        }

        [Test]
        public void BesideIt_ItBitesForTwoFlickers_AfterItsWindUp()
        {
            // On the entrance beside a mimic at (2,1): it winds up from tick 0 and bites on tick 17.
            var sim = new RaidSim(Mimic(1, 2), 1, InputLog.Empty);
            int[] bites = TicksOf(sim, SimEventType.FlickerLost, 30);
            CollectionAssert.AreEqual(new[] { 17 }, bites);
            Assert.AreEqual(1, sim.State.Wick.Flickers);
        }

        [Test]
        public void SteppingAway_ResetsTheWindUp()
        {
            // Beside the mimic on (3,1) from tick 2 to tick 9, then back out: 8 ticks of wind-up, no bite.
            var sim = new RaidSim(Mimic(1, 3), 1, new InputLog(new[] { Command.Dash(0, Dir.E), Command.Dash(8, Dir.W) }));
            Steps(sim, 11);
            Assert.AreEqual(0, sim.State.Guardians[0].ContactTicks, "reset once he stepped away");
            Steps(sim, 40);
            Assert.AreEqual(3, sim.State.Wick.Flickers);
            Assert.AreEqual(Outcome.Escaped, sim.State.Outcome);
        }

        [Test]
        public void Diagonally_ItCantReach()
        {
            string[] room = { "#####", "#...#", "#E..#", "#####" };
            VaultDef vault = Vault(room, new[] { Obj(1, "mimic_chest", 2, 1) });
            var sim = new RaidSim(vault, 1, InputLog.Empty);
            Steps(sim, 40);
            Assert.AreEqual(3, sim.State.Wick.Flickers);
            Assert.AreEqual(GuardianMode.Patrol, sim.State.Guardians[0].Mode);
        }

        [Test]
        public void Levels_ShortenTheWindUp_DownToTheTellFloor()
        {
            Assert.AreEqual(18, DefenceStats.MimicLungeTicks(1));
            Assert.AreEqual(16, DefenceStats.MimicLungeTicks(2));
            Assert.AreEqual(12, DefenceStats.MimicLungeTicks(4));
            Assert.AreEqual(DefenceStats.MinTellTicks, DefenceStats.MimicLungeTicks(5));
            var sim = new RaidSim(Mimic(5, 2), 1, InputLog.Empty);
            CollectionAssert.AreEqual(new[] { 9 }, TicksOf(sim, SimEventType.FlickerLost, 20));
        }

        [Test]
        public void Level5_StealsACarriedPile()
        {
            // Wick takes the 100 pile on (2,1) on the way, then lingers beside the mimic on (3,1).
            ObjectSpec pile = Obj(2, "treasure", 2, 1, value: 100);
            VaultDef five = Mimic(5, 3, pile);
            var sim = new RaidSim(five, 1, new InputLog(new[] { Command.Dash(0, Dir.E) }));
            Steps(sim, 30);
            Assert.AreEqual(1, sim.State.Wick.Flickers);
            Assert.AreEqual(0, sim.State.Wick.CarriedPiles, "snatched back");
            Assert.AreEqual(0, sim.State.Wick.CarriedTreasure);

            var four = new RaidSim(Mimic(4, 3, pile), 1, new InputLog(new[] { Command.Dash(0, Dir.E) }));
            Steps(four, 30);
            Assert.AreEqual(1, four.State.Wick.CarriedPiles, "below level 5 it only bites");
        }

        [Test]
        public void ItIsAGuardian_NotTreasure()
        {
            Assert.IsTrue(ObjectTypes.IsGuardian(ObjectType.MimicChest));
            Assert.AreEqual(0, Scoring.TotalTreasure(Mimic(1, 3)));
            Assert.AreEqual(5, VaultRules.RequiredLevel(ObjectType.MimicChest));
            Assert.AreEqual(28, VaultRules.Cost(ObjectType.MimicChest));
        }
    }
}
