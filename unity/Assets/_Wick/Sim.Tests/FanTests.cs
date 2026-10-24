using NUnit.Framework;
using static Wick.Sim.Tests.TrapTests;

namespace Wick.Sim.Tests
{
    /// <summary>
    /// Fan Maze (Vault Level 7): a fan turns a quarter every quarter cycle and, for the first 10
    /// ticks of each quarter, pushes Wick along its wind (a knockback, no flicker lost).
    /// </summary>
    public class FanTests
    {
        // Floor x = 1..10 on row 1, entrance (1,1).
        private static readonly string[] Corridor =
        {
            "############",
            "#E.........#",
            "############",
        };

        private static VaultDef Fans(int level, params ObjectSpec[] objects)
        {
            VaultDef vault = Vault(Corridor, objects);
            return level == 1 ? vault : vault.WithDefenceLevels(DefenceLevels.Base.With(ObjectType.Fan, level));
        }

        [Test]
        public void ItTurnsAQuarter_EveryQuarterCycle()
        {
            ObjectDef fan = Fans(1, Obj(1, "fan", 4, 1, cycle: 120, rot: 0)).GetObject(0);
            Assert.AreEqual(Dir.N, RaidSim.FanFacing(0, fan, 1));
            Assert.AreEqual(Dir.E, RaidSim.FanFacing(30, fan, 1));
            Assert.AreEqual(Dir.S, RaidSim.FanFacing(60, fan, 1));
            Assert.AreEqual(Dir.W, RaidSim.FanFacing(119, fan, 1));
            Assert.IsTrue(RaidSim.FanBlowing(30, fan, 1));
            Assert.IsTrue(RaidSim.FanBlowing(39, fan, 1));
            Assert.IsFalse(RaidSim.FanBlowing(40, fan, 1));
        }

        [Test]
        public void ItsWind_PushesWickWithoutCostingAFlicker()
        {
            // A fan on (4,1) facing west blows over the entrance on ticks 0-9, but (0,1) is wall.
            var wall = new RaidSim(Fans(1, Obj(1, "fan", 4, 1, cycle: 120, rot: 3)), 1, InputLog.Empty);
            wall.Step();
            Assert.AreEqual(0, wall.State.Wick.KnockTiles, "pushed toward a wall: nowhere to go");
            Assert.AreEqual(3, wall.State.Wick.Flickers);

            // A fan on (1,1) facing east. Wick dashes off it; on (2,1) from tick 2 he is in its wind,
            // pushed to (3,1) (arriving t6), and at rest in the wind again that tick, on to (4,1) (t10).
            var east = new RaidSim(Fans(1, Obj(1, "fan", 1, 1, cycle: 120, rot: 1)), 1,
                new InputLog(new[] { Command.Dash(0, Dir.E) }));
            CollectionAssert.AreEqual(new[] { 2, 6 }, TicksOf(east, SimEventType.KnockedBack, 12));
            Assert.AreEqual(new TilePos(4, 1), east.State.Wick.Tile);
            Assert.AreEqual(3, east.State.Wick.Flickers);
        }

        [Test]
        public void TheWind_StopsAtACrate()
        {
            var sim = new RaidSim(Fans(1, Obj(1, "fan", 4, 1, cycle: 120, rot: 3), Obj(2, "crate", 3, 1)), 1,
                new InputLog(new[] { Command.Dash(0, Dir.E) }));
            Steps(sim, 9);
            Assert.AreEqual(new TilePos(2, 1), sim.State.Wick.Tile, "sheltered behind the crate");
            Assert.AreEqual(0, sim.State.Wick.KnockTiles);
        }

        [Test]
        public void Level5_PushesTwoTiles()
        {
            Assert.AreEqual(1, DefenceStats.FanPush(4));
            Assert.AreEqual(2, DefenceStats.FanPush(5));
            // A fan on (1,1) facing east, Wick beside it on (2,1) after a dash: level 5 sends him two tiles.
            var five = new RaidSim(Fans(5, Obj(1, "fan", 1, 1, cycle: 120, rot: 1)), 1, new InputLog(new[] { Command.Dash(0, Dir.E) }));
            int knocked = -1;
            for (int i = 0; i < 12 && knocked < 0; i++)
            {
                five.Step();
                foreach (SimEvent e in five.Events)
                {
                    if (e.Type == SimEventType.KnockedBack)
                    {
                        knocked = e.Value;
                    }
                }
            }

            Assert.AreEqual(2, knocked);
        }

        [Test]
        public void ItUnlocksAtVaultLevel7_AndCosts30()
        {
            Assert.AreEqual(7, VaultRules.RequiredLevel(ObjectType.Fan));
            Assert.AreEqual(30, VaultRules.Cost(ObjectType.Fan));
            Assert.AreEqual(48, ObjectTypes.MinCycle(ObjectType.Fan));
        }
    }
}
