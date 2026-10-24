using System.Collections.Generic;
using NUnit.Framework;
using static Wick.Sim.Tests.TrapTests;

namespace Wick.Sim.Tests
{
    /// <summary>
    /// Lever Doors (Vault Level 8): a gate in a doorway blocks Wick and guardians while closed;
    /// standing on its lever opens it, and it stays open a while after he leaves.
    /// </summary>
    public class LeverDoorTests
    {
        // Entrance (1,1); lever (2,1); a door at (4,1) with the gate; floor beyond.
        private static readonly string[] Hall = { "#########", "#E..D...#", "#########" };

        private static ObjectSpec Gate(int id, int x, int y = 1) => Obj(id, "gate", x, y);

        private static ObjectSpec Lever(int id, int x, int y, params int[] gates)
        {
            ObjectSpec spec = Obj(id, "lever", x, y);
            spec.Json = spec.Json.TrimEnd('}') + ",\"links\":[" + string.Join(",", gates) + "]}";
            return spec;
        }

        private static VaultDef Doors(int level, string[] map, params ObjectSpec[] objects)
        {
            VaultDef vault = Vault(map, objects);
            return level == 1 ? vault : vault.WithDefenceLevels(DefenceLevels.Base.With(ObjectType.Gate, level));
        }

        [Test]
        public void AClosedGate_StopsWick()
        {
            // No lever pressed: running east he stops in front of the gate at (3,1).
            var sim = new RaidSim(Doors(1, Hall, Gate(1, 4), Lever(2, 7, 1, 1)), 1,
                new InputLog(new[] { Command.RunStart(0, Dir.E) }));
            TrapTests.Steps(sim, 60);
            Assert.AreEqual(new TilePos(3, 1), sim.State.Wick.Tile);
        }

        [Test]
        public void ItsLever_OpensIt_AndItStaysOpenAWhileAfter()
        {
            // The lever on (2,1): running east he is on it from tick 5, the gate opens, and he runs through.
            var sim = new RaidSim(Doors(1, Hall, Gate(1, 4), Lever(2, 2, 1, 1)), 1,
                new InputLog(new[] { Command.RunStart(0, Dir.E) }));
            TrapTests.Steps(sim, 70);
            Assert.AreEqual(new TilePos(7, 1), sim.State.Wick.Tile, "through the gate to the far wall");
            Assert.Greater(sim.State.GateOpen[0], 0, "still open well after he left the lever");
        }

        [Test]
        public void ItCloses_WhenItsTimeIsUp_ButNeverOnWick()
        {
            // Over the lever and on to (3,1): on it ticks 2-6, so the gate opens on tick 2 and, open for
            // the 240 ticks after he leaves (7-246), shuts on tick 246 (he is not in the doorway).
            var sim = new RaidSim(Doors(1, Hall, Gate(1, 4), Lever(2, 2, 1, 1)), 1,
                new InputLog(new[] { Command.Dash(0, Dir.E), Command.Dash(5, Dir.E) }));
            CollectionAssert.AreEqual(new[] { 2, 246 }, TicksOf(sim, SimEventType.TrapFired, 400), "opened, then closed");
        }

        [Test]
        public void AGateHeldOpenByWickStandingInIt_WaitsForHimToMove()
        {
            var doors = Doors(1, Hall, Gate(1, 4), Lever(2, 2, 1, 1)).WithDefenceLevels(DefenceLevels.Base);
            // Onto the lever, then into the doorway, and wait there long past the timer.
            var sim = new RaidSim(doors, 1, new InputLog(new[] { Command.Dash(0, Dir.E), Command.Dash(5, Dir.E), Command.Dash(10, Dir.E) }));
            TrapTests.Steps(sim, 400);
            Assert.AreEqual(new TilePos(4, 1), sim.State.Wick.Tile);
            Assert.AreEqual(1, sim.State.GateOpen[0], "held open while he stands in it");
        }

        [Test]
        public void Guardians_CantPassAClosedGate()
        {
            // An owl beyond the gate patrols west toward the entrance; the closed gate stops it at (5,1).
            ObjectSpec owl = Obj(3, "night_owl", 7, 1, patrol: "[[7,1],[1,1]]");
            var sim = new RaidSim(Doors(1, Hall, Gate(1, 4), Lever(2, 6, 1, 1), owl), 1, new InputLog(new[] { Command.TiptoeOn(0) }));
            TrapTests.Steps(sim, 200);
            Assert.GreaterOrEqual(sim.State.Guardians[0].Tile.X, 5);
        }

        [Test]
        public void Levels_ShortenTheOpenTime_AndLevel5WorksTwoGates()
        {
            Assert.AreEqual(240, DefenceStats.GateOpenTicks(1));
            Assert.AreEqual(225, DefenceStats.GateOpenTicks(2));
            Assert.AreEqual(165, DefenceStats.GateOpenTicks(5));
            Assert.AreEqual(1, DefenceStats.LeverLinks(4));
            Assert.AreEqual(2, DefenceStats.LeverLinks(5));
        }

        // ---- Validation --------------------------------------------------------------------

        private static List<VaultIssueCode> Codes(VaultDef v)
        {
            return VaultValidator.Validate(v).ConvertAll(i => i.Code);
        }

        [Test]
        public void GatesGoInDoorways_AndNeedALever()
        {
            Assert.That(Codes(Doors(1, Hall, Gate(1, 3), Lever(2, 2, 1, 1))), Has.Member(VaultIssueCode.GateNotInDoorway));
            Assert.That(Codes(Doors(1, Hall, Gate(1, 4))), Has.Member(VaultIssueCode.GateWithoutLever));
        }

        [Test]
        public void ALeverWorksOneGate_TwoAtLevel5()
        {
            string[] twoDoors = { "##########", "#E.D..D..#", "##########" };
            VaultDef one = Doors(1, twoDoors, Gate(1, 3), Gate(2, 6), Lever(3, 2, 1, 1, 2));
            Assert.That(Codes(one), Has.Member(VaultIssueCode.LeverLinks));
            VaultDef five = Doors(5, twoDoors, Gate(1, 3), Gate(2, 6), Lever(3, 2, 1, 1, 2));
            Assert.That(Codes(five), Has.No.Member(VaultIssueCode.LeverLinks));
        }

        [Test]
        public void ALinkMustBeToAGate()
        {
            Assert.Throws<System.FormatException>(() => Vault(Hall, new[] { Obj(1, "crate", 6, 1), Lever(2, 2, 1, 1) }));
            Assert.Throws<System.FormatException>(() => Vault(Hall, new[] { Gate(1, 4), Lever(2, 2, 1, 99) }));
        }

        [Test]
        public void Reachability_OpensGatesOnlyWhenTheirLeverCanBeReached()
        {
            // Treasure behind the gate; the lever in front: reachable. Lever behind its own gate: not.
            string[] map = { "#########", "#E..D...#", "#########" };
            ObjectSpec pile = Obj(5, "treasure", 6, 1, value: 100);
            bool[] reachable = VaultValidator.ReachableWithGates(Vault(map, new[] { Gate(1, 4), Lever(2, 2, 1, 1), pile }));
            Assert.IsTrue(reachable[1 * 9 + 6]);
            bool[] locked = VaultValidator.ReachableWithGates(Vault(map, new[] { Gate(1, 4), Lever(2, 7, 1, 1), pile }));
            Assert.IsFalse(locked[1 * 9 + 6], "its lever is behind it");
        }
    }
}
