using NUnit.Framework;
using static Wick.Sim.Tests.TrapTests;

namespace Wick.Sim.Tests
{
    public class GuardianTests
    {
        // Open room, floor x/y = 1..7, entrance (4,7) on the bottom row.
        private static readonly string[] Room =
        {
            "#########",
            "#.......#",
            "#.......#",
            "#.......#",
            "#.......#",
            "#.......#",
            "#.......#",
            "#...E...#",
            "#########",
        };

        private const int S = 2; // rot for facing south
        private const int E = 1; // rot for facing east

        // ---- Night Owl: patrol ----------------------------------------------------------

        [Test]
        public void Owl_WalksItsPatrolLoop()
        {
            // 20 ticks per tile. Tick 0 it notes it is on waypoint 0; moves from tick 1.
            RaidSim sim = Sim(new[] { Obj(1, "night_owl", 1, 1, rot: E, patrol: "[[1,1],[3,1]]") });
            Steps(sim, 21);
            Assert.AreEqual(new TilePos(2, 1), Owl(sim).Tile);
            Steps(sim, 20);
            Assert.AreEqual(new TilePos(3, 1), Owl(sim).Tile, "waypoint 1 at tick 40");
            Steps(sim, 40);
            Assert.AreEqual(new TilePos(1, 1), Owl(sim).Tile, "back to waypoint 0 at tick 80");
            Assert.AreEqual(GuardianMode.Patrol, Owl(sim).Mode);
        }

        // ---- Night Owl: sight -----------------------------------------------------------

        [Test]
        public void Owl_SpotsWick_WhenHisLightReachesIt()
        {
            // Owl 5 tiles north of Wick, facing him. His radius is 4 tiles: one dash closes the gap.
            RaidSim sim = Sim(new[] { Obj(1, "night_owl", 4, 2, rot: S) }, Command.Dash(0, Dir.N));
            Assert.AreEqual(new[] { 4 }, TicksOf(sim, SimEventType.GuardianAlerted, 30), "on arrival at (4,6)");
            Assert.AreEqual(GuardianMode.Chase, Owl(sim).Mode);
        }

        [Test]
        public void Owl_DoesNotSpotWick_WhileTiptoeKeepsTheLightOffIt()
        {
            // Tiptoe radius is 2 tiles: (4,5) is 3 tiles from the owl, (4,4) is 2.
            RaidSim sim = Sim(new[] { Obj(1, "night_owl", 4, 2, rot: S) },
                Command.TiptoeOn(0), Command.Dash(0, Dir.N), Command.Dash(10, Dir.N), Command.Dash(20, Dir.N));
            Assert.AreEqual(new int[0], TicksOf(sim, SimEventType.GuardianAlerted, 29), "up to (4,5)");
            Assert.AreEqual(new[] { 29 }, TicksOf(sim, SimEventType.GuardianAlerted, 10), "on arrival at (4,4)");
        }

        [Test]
        public void Owl_OnlySeesInFront()
        {
            RaidSim facingAway = Sim(new[] { Obj(1, "night_owl", 4, 2, rot: 0) }, Command.Dash(0, Dir.N));
            Assert.AreEqual(new int[0], TicksOf(facingAway, SimEventType.GuardianAlerted, 30));
        }

        [Test]
        public void Owl_CannotSeeThroughWalls()
        {
            string[] walled =
            {
                "#########",
                "#.......#",
                "#.......#",
                "#.......#",
                "#..###..#",
                "#.......#",
                "#.......#",
                "#...E...#",
                "#########",
            };
            var vault = Vault(walled, new[] { Obj(1, "night_owl", 4, 2, rot: S) });
            var sim = new RaidSim(vault, 1, new InputLog(new[] { Command.Dash(0, Dir.N) }));
            Assert.AreEqual(new int[0], TicksOf(sim, SimEventType.GuardianAlerted, 30));
        }

        // ---- Night Owl: chase -----------------------------------------------------------

        [Test]
        public void Owl_LosesWickAfter60UnseenTicks()
        {
            // A crate wall on row 4 keeps the owl from reaching Wick but not from seeing him.
            var objects = new[]
            {
                Obj(1, "night_owl", 4, 2, rot: S),
                Obj(2, "crate", 1, 4), Obj(3, "crate", 2, 4), Obj(4, "crate", 3, 4), Obj(5, "crate", 4, 4),
                Obj(6, "crate", 5, 4), Obj(7, "crate", 6, 4), Obj(8, "crate", 7, 4),
            };
            // Spotted on arrival at (4,6) on tick 4; the dash west from tick 5 takes him out of
            // reach of the light at once, so ticks 5..64 are unseen.
            RaidSim sim = Sim(objects, Command.Dash(0, Dir.N), Command.Dash(5, Dir.W));
            Steps(sim, 64);
            Assert.AreEqual(GuardianMode.Chase, Owl(sim).Mode, "tick 63: 59 unseen ticks");
            Steps(sim, 1);
            Assert.AreEqual(GuardianMode.Patrol, Owl(sim).Mode, "tick 64: 60 unseen ticks");
        }

        [Test]
        public void Owl_ContactCostsAFlicker_ThenInvulnerabilityApplies()
        {
            // Owl two tiles north of Wick spots him at tick 0, reaches his tile (occupied) at
            // tick 14: 10 ticks per tile, halfway into the second tile.
            RaidSim sim = Sim(new[] { Obj(1, "night_owl", 4, 5, rot: S) });
            Assert.AreEqual(new[] { 14, 60 }, TicksOf(sim, SimEventType.FlickerLost, 61));
        }

        [Test]
        public void Decoy_DistractsTheOwl_UntilItBurnsOut()
        {
            // Decoy dropped on the entrance; Wick runs east out of the owl's cone.
            RaidSim sim = Sim(new[] { Obj(1, "night_owl", 4, 5, rot: S) },
                Command.Decoy(0), Command.RunStart(0, Dir.E));
            Steps(sim, 50);
            Assert.AreEqual(GuardianMode.Distracted, Owl(sim).Mode);
            Assert.AreEqual(new TilePos(4, 7), Owl(sim).Target);
            Steps(sim, 39);
            Assert.AreEqual(GuardianMode.Distracted, Owl(sim).Mode, "decoy still burning on tick 88");
            Assert.AreEqual(3, sim.State.Wick.Flickers);
            Steps(sim, 1);
            Assert.AreEqual(GuardianMode.Patrol, Owl(sim).Mode, "decoy out on tick 89");
        }

        // ---- Wax Moths ------------------------------------------------------------------

        [Test]
        public void Moths_DrawnByWicksLight_ButNotWhenHeTiptoes()
        {
            // 6 tiles away: full flame reaches 4 + 2 tiles, tiptoe only 2 + 2.
            RaidSim lit = Sim(new[] { Obj(1, "wax_moths", 4, 1) });
            Steps(lit, 1);
            Assert.AreEqual(GuardianMode.Chase, Owl(lit).Mode);

            RaidSim dim = Sim(new[] { Obj(1, "wax_moths", 4, 1) }, Command.TiptoeOn(0));
            Steps(dim, 1);
            Assert.AreEqual(GuardianMode.Patrol, Owl(dim).Mode);
        }

        [Test]
        public void Moths_PreferTheDecoy()
        {
            RaidSim sim = Sim(new[] { Obj(1, "wax_moths", 4, 3) }, Command.Decoy(0), Command.Dash(0, Dir.E));
            Steps(sim, 10);
            Assert.AreEqual(GuardianMode.Distracted, Owl(sim).Mode);
            Assert.AreEqual(new TilePos(4, 7), Owl(sim).Target);
        }

        [Test]
        public void Moths_DrainAfter60TicksOfContact()
        {
            // Moths one tile north drift onto Wick's tile (occupied at tick 12) and sit there.
            RaidSim sim = Sim(new[] { Obj(1, "wax_moths", 4, 6) });
            Assert.AreEqual(new[] { 71, 131 }, TicksOf(sim, SimEventType.FlickerLost, 140));
        }

        // ---- Helpers --------------------------------------------------------------------

        private static RaidSim Sim(ObjectSpec[] objects, params Command[] commands)
        {
            return new RaidSim(Vault(Room, objects), 1, new InputLog(commands));
        }

        private static GuardianState Owl(RaidSim sim)
        {
            return sim.State.Guardians[0];
        }
    }
}
