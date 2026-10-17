using NUnit.Framework;
using static Wick.Sim.Tests.TrapTests;

namespace Wick.Sim.Tests
{
    public class PickupScoringTests
    {
        // Corridor: floor x = 1..10 on row 1, entrance (1,1); rows 0 and 2 are wall.
        private static readonly string[] Corridor =
        {
            "############",
            "#E.........#",
            "############",
        };

        // ---- Treasure -------------------------------------------------------------------

        [TestCase(0, false, false, 200)]
        [TestCase(1, false, false, 180)]
        [TestCase(4, false, false, 120)]
        [TestCase(6, false, false, 120)]
        [TestCase(0, true, false, 100)]
        [TestCase(2, true, false, 80)]
        [TestCase(5, true, false, 60)]
        [TestCase(1, false, true, 90)]
        public void Speed_SlowsTenPercentPerPile_CappedAtForty(int piles, bool run, bool tiptoe, int expected)
        {
            var w = new WickState { CarriedPiles = piles, MoveIsRun = run, Tiptoe = tiptoe };
            Assert.AreEqual(expected, RaidSim.WickSpeed(in w));
        }

        [Test]
        public void Treasure_TakenWhenOccupied()
        {
            RaidSim sim = Sim(new[] { Obj(1, "treasure", 2, 1, value: 100) }, Command.Dash(0, Dir.E));
            Assert.AreEqual(new[] { 2 }, TicksOf(sim, SimEventType.TreasureTaken, 10));
            Assert.AreEqual(100, sim.State.Wick.CarriedTreasure);
            Assert.AreEqual(1, sim.State.Wick.CarriedPiles);
            Assert.IsTrue(sim.State.Spent[0]);
        }

        // ---- Braziers -------------------------------------------------------------------

        [Test]
        public void Brazier_RelightsFromAnAdjacentTile_OnceOnly()
        {
            // Drip on the entrance hits at tick 0; the brazier on the wall above (2,1) relights
            // when (2,1) becomes occupied on tick 3.
            RaidSim sim = Sim(new[] { Obj(1, "drip_pipe", 1, 1, cycle: 200), Obj(2, "brazier", 2, 0) },
                Command.Dash(1, Dir.E), Command.Dash(10, Dir.W), Command.Dash(20, Dir.E));
            Assert.AreEqual(new[] { 3 }, TicksOf(sim, SimEventType.BrazierUsed, 400));
            Assert.IsTrue(sim.State.Spent[1]);
        }

        [Test]
        public void Brazier_NotWastedAtFullFlame()
        {
            RaidSim sim = Sim(new[] { Obj(1, "brazier", 2, 0) }, Command.Dash(0, Dir.E));
            Steps(sim, 10);
            Assert.IsFalse(sim.State.Spent[0]);
            Assert.AreEqual(3, sim.State.Wick.Flickers);
        }

        [Test]
        public void Brazier_NeverAboveThree()
        {
            RaidSim sim = Sim(new[] { Obj(1, "drip_pipe", 1, 1, cycle: 200), Obj(2, "brazier", 1, 0) });
            Steps(sim, 1);
            Assert.AreEqual(3, sim.State.Wick.Flickers, "hit and relit on the same tick");
            Assert.IsTrue(sim.State.Spent[1]);
        }

        // ---- Stars and loot -------------------------------------------------------------

        // Treasure 100 at (3,1), 300 at (5,1). Run east, stop at (3,1), run home:
        // pile taken on tick 14, home on tick 52 (worked through in the comments of the test).
        private static readonly Command[] GrabAndGo =
        {
            Command.RunStart(0, Dir.E), Command.RunStop(15), Command.RunStart(30, Dir.W),
        };

        [Test]
        public void GrabAndGo_EscapesWithLoot()
        {
            // (2,1) at tick 9; (3,1) occupied at 14 (pile taken, speed 90); arrives tick 20 and
            // stops. Back west at 90/tick: (2,1) at 41 (80 carried over), (1,1) at 52.
            RaidResult r = Run(Treasures(), GrabAndGo);
            Assert.AreEqual(Outcome.Escaped, r.Outcome);
            Assert.AreEqual(53, r.Ticks);
            Assert.AreEqual(100, r.Loot);
            Assert.AreEqual(2, r.Stars, "took a pile (no hoard) + escaped with 3 flickers; 100 of 400 is under half");
        }

        [Test]
        public void Loot_OnlyCountsOnEscape()
        {
            RaidResult r = Run(Treasures(), Command.RunStart(0, Dir.E), Command.RunStop(15));
            Assert.AreEqual(Outcome.TimedOut, r.Outcome);
            Assert.AreEqual(0, r.Loot);
            Assert.AreEqual(1, r.Stars, "took a pile");
        }

        [Test]
        public void StarTwo_NeedsHalfTheTreasureByValue()
        {
            RaidResult both = Run(Treasures(), Command.RunStart(0, Dir.E), Command.RunStop(35));
            Assert.AreEqual(2, both.Stars, "pile + 400 of 400; timed out so no star 3");

            RaidResult half = Run(new[] { Obj(1, "treasure", 3, 1, value: 200), Obj(2, "treasure", 9, 1, value: 200) },
                Command.RunStart(0, Dir.E), Command.RunStop(15));
            Assert.AreEqual(2, half.Stars, "exactly half counts");
        }

        [Test]
        public void StarOne_UsesTheHoardWhenTheVaultHasOne()
        {
            string json = TestVaults.Json(Corridor, "[" + Obj(1, "treasure", 3, 1, value: 100).Json + "]");
            json = json.Substring(0, json.Length - 1) + ",\"hoard\":[6,1,10,1]}";
            VaultDef vault = VaultJson.Parse(json);

            RaidResult pileOnly = RaidRunner.RunToEnd(vault, 1,
                new InputLog(new[] { Command.RunStart(0, Dir.E), Command.RunStop(15) }));
            Assert.AreEqual(1, pileOnly.Stars, "star 2 only: the pile is outside the hoard");

            RaidResult hoard = RaidRunner.RunToEnd(vault, 1, new InputLog(new[] { Command.RunStart(0, Dir.E) }));
            Assert.AreEqual(2, hoard.Stars, "reached the hoard + all the treasure");
        }

        [Test]
        public void StarThree_NeedsEveryFlicker()
        {
            // Same grab-and-go, but a drip on (2,1) catches him on the way out (window 0-5 of 200).
            RaidResult r = Run(new[] { Obj(1, "treasure", 3, 1, value: 100), Obj(2, "treasure", 5, 1, value: 300),
                Obj(3, "drip_pipe", 2, 1, cycle: 200, offset: 160) }, GrabAndGo);
            Assert.AreEqual(Outcome.Escaped, r.Outcome);
            Assert.AreEqual(2, r.Flickers);
            Assert.AreEqual(1, r.Stars, "pile only: no full-flame star");
        }

        // ---- Helpers --------------------------------------------------------------------

        private static ObjectSpec[] Treasures()
        {
            return new[] { Obj(1, "treasure", 3, 1, value: 100), Obj(2, "treasure", 5, 1, value: 300) };
        }

        private static RaidSim Sim(ObjectSpec[] objects, params Command[] commands)
        {
            return new RaidSim(Vault(Corridor, objects), 1, new InputLog(commands));
        }

        private static RaidResult Run(ObjectSpec[] objects, params Command[] commands)
        {
            return RaidRunner.RunToEnd(Vault(Corridor, objects), 1, new InputLog(commands));
        }
    }
}
