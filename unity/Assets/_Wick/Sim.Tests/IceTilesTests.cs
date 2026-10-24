using NUnit.Framework;
using static Wick.Sim.Tests.TrapTests;

namespace Wick.Sim.Tests
{
    /// <summary>
    /// Ice Tiles (Vault Level 3): Wick slides until something stops him or the ice ends. Swipes
    /// made while sliding wait until he stops.
    /// </summary>
    public class IceTilesTests
    {
        // Entrance (1,1); ice (2,1)-(4,1); floor (5,1); wall (6,1).
        private static readonly string[] Lane = { "#######", "#E~~~.#", "#######" };

        // Entrance (1,1); ice all the way to the east wall.
        private static readonly string[] Rink = { "######", "#E~~~#", "######" };

        private static RaidSim Sim(string[] map, int level, params Command[] commands)
        {
            VaultDef vault = Vault(map, new ObjectSpec[0]);
            if (level > 1)
            {
                vault = vault.WithDefenceLevels(DefenceLevels.Base.With(ObjectType.IceTiles, level));
            }

            return new RaidSim(vault, 1, new InputLog(commands));
        }

        private static int ArrivalTick(RaidSim sim, TilePos tile, int limit = 100)
        {
            for (int i = 0; i < limit; i++)
            {
                int tick = sim.State.Tick;
                sim.Step();
                if (sim.State.Wick.Tile == tile && !sim.State.Wick.Moving)
                {
                    return tick;
                }
            }

            return -1;
        }

        [Test]
        public void OneDash_SlidesAcrossTheIce_AndStopsWhereItEnds()
        {
            RaidSim sim = Sim(Lane, 1, Command.Dash(0, Dir.E));
            Assert.AreEqual(16, ArrivalTick(sim, new TilePos(5, 1)), "dash onto the ice by t4, then 4 ticks a tile");
            Assert.IsFalse(sim.State.Wick.Sliding);
        }

        [Test]
        public void ASlide_StopsAgainstAWall_StillOnTheIce()
        {
            RaidSim sim = Sim(Rink, 1, Command.Dash(0, Dir.E));
            Assert.AreEqual(12, ArrivalTick(sim, new TilePos(4, 1)));
            Assert.IsFalse(sim.State.Wick.Sliding, "stopped");
        }

        [Test]
        public void ASwipeWhileSliding_WaitsUntilHeStops()
        {
            // A swipe west at tick 6 can't turn him mid-slide; it starts the moment he stops at (4,1).
            RaidSim sim = Sim(Rink, 1, Command.Dash(0, Dir.E), Command.Dash(6, Dir.W));
            Steps(sim, 12);
            Assert.AreEqual(Dir.E, sim.State.Wick.MoveDir, "still sliding east on tick 11");
            sim.Step();
            Assert.AreEqual(new TilePos(4, 1), sim.State.Wick.Tile, "reached the wall on tick 12");
            Assert.IsTrue(sim.State.Wick.Moving, "and the queued dash west started straight away");
            Assert.AreEqual(Dir.W, sim.State.Wick.MoveDir);
        }

        [Test]
        public void HigherLevels_SlideFaster()
        {
            Assert.AreEqual(250, DefenceStats.IceSlideSpeed(1));
            Assert.AreEqual(275, DefenceStats.IceSlideSpeed(2));
            Assert.AreEqual(362, DefenceStats.IceSlideSpeed(5));
            int one = ArrivalTick(Sim(Lane, 1, Command.Dash(0, Dir.E)), new TilePos(5, 1));
            int five = ArrivalTick(Sim(Lane, 5, Command.Dash(0, Dir.E)), new TilePos(5, 1));
            Assert.Less(five, one);
        }

        [Test]
        public void Level5_DimsTheFlameWhileSliding()
        {
            RaidSim one = Sim(Lane, 1, Command.Dash(0, Dir.E));
            RaidSim five = Sim(Lane, 5, Command.Dash(0, Dir.E));
            Steps(one, 7);
            Steps(five, 7);
            Assert.AreEqual(0, one.State.Wick.GustDimTicks);
            Assert.Greater(five.State.Wick.GustDimTicks, 0);
        }

        [Test]
        public void IceCostsPerTile_AndUnlocksAtVaultLevel3()
        {
            VaultDef lane = Vault(Lane, new ObjectSpec[0]);
            Assert.AreEqual(3, VaultRules.IceTileCount(lane));
            Assert.AreEqual(9, VaultRules.BuildPoints(lane));
            Assert.That(VaultValidator.Validate(lane).ConvertAll(i => i.Message), Has.Some.Contains("Ice Tiles unlock"));
        }

        [Test]
        public void IceIsNeverAnObject()
        {
            Assert.Throws<System.FormatException>(() => Vault(Lane, new[] { Obj(1, "ice_tiles", 2, 1) }));
        }
    }
}
