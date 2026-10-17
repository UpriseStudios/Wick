using NUnit.Framework;

namespace Wick.Sim.Tests
{
    public class MovementTests
    {
        // Entrance at (2,4). Open column x = 1..3, rows y = 1..4; wall above row 1.
        private static readonly string[] Room =
        {
            "#####",
            "#...#",
            "#...#",
            "#...#",
            "#.E.#",
            "#####",
        };

        private static readonly TilePos Start = new TilePos(2, 4);

        [Test]
        public void StartsAtEntranceWithFullFlame()
        {
            RaidSim sim = Sim();
            Assert.AreEqual(Start, sim.State.Wick.Tile);
            Assert.AreEqual(SimTuning.StartFlickers, sim.State.Wick.Flickers);
            Assert.AreEqual(Outcome.Running, sim.State.Outcome);
        }

        [Test]
        public void Dash_MovesOneTileInFiveTicks()
        {
            RaidSim sim = Sim(Command.Dash(0, Dir.N));
            StepN(sim, 4);
            Assert.AreEqual(Start, sim.State.Wick.Tile);
            Assert.AreEqual(800, sim.State.Wick.Progress);

            StepN(sim, 1);
            Assert.AreEqual(new TilePos(2, 3), sim.State.Wick.Tile);
            Assert.AreEqual(0, sim.State.Wick.Progress);
            Assert.IsFalse(sim.State.Wick.Moving);

            StepN(sim, 20);
            Assert.AreEqual(new TilePos(2, 3), sim.State.Wick.Tile, "a dash moves exactly one tile");
        }

        [Test]
        public void TiptoeDash_TakesTenTicks()
        {
            RaidSim sim = Sim(Command.TiptoeOn(0), Command.Dash(0, Dir.N));
            StepN(sim, 9);
            Assert.AreEqual(Start, sim.State.Wick.Tile);
            StepN(sim, 1);
            Assert.AreEqual(new TilePos(2, 3), sim.State.Wick.Tile);
        }

        [Test]
        public void Tiptoe_ChangesSpeedMidMove()
        {
            // 2 ticks at 200, then 6 ticks at 100 = 1000.
            RaidSim sim = Sim(Command.Dash(0, Dir.N), Command.TiptoeOn(2));
            StepN(sim, 7);
            Assert.AreEqual(Start, sim.State.Wick.Tile);
            StepN(sim, 1);
            Assert.AreEqual(new TilePos(2, 3), sim.State.Wick.Tile);
        }

        [Test]
        public void Run_ContinuesUntilWall()
        {
            RaidSim sim = Sim(Command.RunStart(0, Dir.N));
            StepN(sim, 10);
            Assert.AreEqual(new TilePos(2, 3), sim.State.Wick.Tile);
            StepN(sim, 20);
            Assert.AreEqual(new TilePos(2, 1), sim.State.Wick.Tile);
            Assert.IsFalse(sim.State.Wick.Moving, "stopped by the wall");
            Assert.IsTrue(sim.State.Wick.Running, "still holding");

            StepN(sim, 30);
            Assert.AreEqual(new TilePos(2, 1), sim.State.Wick.Tile);
        }

        [Test]
        public void RunStop_FinishesCurrentTileAtRunSpeed()
        {
            RaidSim sim = Sim(Command.RunStart(0, Dir.N), Command.RunStop(3));
            StepN(sim, 9);
            Assert.AreEqual(Start, sim.State.Wick.Tile);
            Assert.AreEqual(900, sim.State.Wick.Progress);
            StepN(sim, 1);
            Assert.AreEqual(new TilePos(2, 3), sim.State.Wick.Tile);
            StepN(sim, 30);
            Assert.AreEqual(new TilePos(2, 3), sim.State.Wick.Tile, "stops after that tile");
        }

        [Test]
        public void RunStart_ChangesDirectionAtNextTileCentre()
        {
            RaidSim sim = Sim(Command.RunStart(0, Dir.N), Command.RunStart(4, Dir.E));
            StepN(sim, 10);
            Assert.AreEqual(new TilePos(2, 3), sim.State.Wick.Tile);
            StepN(sim, 10);
            Assert.AreEqual(new TilePos(3, 3), sim.State.Wick.Tile);
            Assert.IsFalse(sim.State.Wick.Moving, "x = 4 is a wall");
        }

        [Test]
        public void DashIntoWall_OnlyTurns()
        {
            RaidSim sim = Sim(Command.Dash(0, Dir.S));
            StepN(sim, 10);
            Assert.AreEqual(Start, sim.State.Wick.Tile);
            Assert.AreEqual(Dir.S, sim.State.Wick.Facing);
            Assert.IsFalse(sim.State.Wick.Moving);
        }

        [Test]
        public void DashDuringMove_IsQueued()
        {
            RaidSim sim = Sim(Command.Dash(0, Dir.N), Command.Dash(2, Dir.E));
            StepN(sim, 5);
            Assert.AreEqual(new TilePos(2, 3), sim.State.Wick.Tile);
            Assert.IsTrue(sim.State.Wick.Moving, "queued dash starts on arrival");
            Assert.AreEqual(Dir.E, sim.State.Wick.MoveDir);
            StepN(sim, 5);
            Assert.AreEqual(new TilePos(3, 3), sim.State.Wick.Tile);
        }

        [Test]
        public void Dash_CancelsHeldRun()
        {
            RaidSim sim = Sim(Command.RunStart(0, Dir.N), Command.Dash(1, Dir.N));
            StepN(sim, 10);
            Assert.AreEqual(new TilePos(2, 3), sim.State.Wick.Tile);
            Assert.IsFalse(sim.State.Wick.Running);
            StepN(sim, 5);
            Assert.AreEqual(new TilePos(2, 2), sim.State.Wick.Tile, "queued dash moves exactly one more tile");
            StepN(sim, 20);
            Assert.AreEqual(new TilePos(2, 2), sim.State.Wick.Tile);
        }

        [Test]
        public void Decoy_OnlyOncePerRaid_AndCountsDown()
        {
            RaidSim sim = Sim(Command.Decoy(0), Command.Dash(1, Dir.N), Command.Decoy(10));
            StepN(sim, 1);
            Assert.IsTrue(sim.State.Wick.DecoyUsed);
            Assert.AreEqual(Start, sim.State.Wick.DecoyTile);
            Assert.AreEqual(SimTuning.DecoyTicks - 1, sim.State.Wick.DecoyTicksLeft);

            StepN(sim, 10);
            Assert.AreEqual(Start, sim.State.Wick.DecoyTile, "second decoy ignored");
            Assert.AreEqual(SimTuning.DecoyTicks - 11, sim.State.Wick.DecoyTicksLeft);

            StepN(sim, 200);
            Assert.AreEqual(0, sim.State.Wick.DecoyTicksLeft);
        }

        [Test]
        public void ReturningToEntrance_Escapes()
        {
            RaidResult r = RaidRunner.RunToEnd(Vault(), 1, Log(Command.Dash(0, Dir.N), Command.Dash(5, Dir.S)));
            Assert.AreEqual(Outcome.Escaped, r.Outcome);
            Assert.AreEqual(10, r.Ticks);
        }

        [Test]
        public void RunningThroughEntrance_Escapes()
        {
            var vault = TestVaults.Parse(
                "#####",
                "#...#",
                "#.E.#",
                "#...#",
                "#####");
            // Turns south at (2,1) on tick 9 and would run on past the entrance, but arriving there ends the raid.
            RaidResult r = RaidRunner.RunToEnd(vault, 1, Log(Command.RunStart(0, Dir.N), Command.RunStart(5, Dir.S)));
            Assert.AreEqual(Outcome.Escaped, r.Outcome);
            Assert.AreEqual(20, r.Ticks);
        }

        [Test]
        public void StayingAtEntrance_DoesNotEscape()
        {
            RaidResult r = RaidRunner.RunToEnd(Vault(), 1, Log(Command.Dash(0, Dir.S), Command.Dash(1, Dir.W)));
            Assert.AreEqual(Outcome.TimedOut, r.Outcome);
        }

        [Test]
        public void TimesOutAfter1800Ticks_With60Checkpoints()
        {
            RaidResult r = RaidRunner.RunToEnd(Vault(), 1, InputLog.Empty);
            Assert.AreEqual(Outcome.TimedOut, r.Outcome);
            Assert.AreEqual(SimTuning.RaidTicks, r.Ticks);
            Assert.AreEqual(60, r.Checkpoints.Length);
            Assert.AreEqual(r.FinalHash, r.Checkpoints[59]);
        }

        [Test]
        public void EndBetweenIntervals_AddsFinalCheckpoint()
        {
            RaidResult r = RaidRunner.RunToEnd(Vault(), 1, Log(Command.Dash(40, Dir.N), Command.Dash(45, Dir.S)));
            Assert.AreEqual(Outcome.Escaped, r.Outcome);
            Assert.AreEqual(50, r.Ticks);
            Assert.AreEqual(2, r.Checkpoints.Length, "tick 29 interval + end at tick 49");
        }

        [Test]
        public void StepAfterEnd_IsNoOp()
        {
            RaidSim sim = Sim(Command.Dash(0, Dir.N), Command.Dash(5, Dir.S));
            StepN(sim, 10);
            Assert.AreEqual(Outcome.Escaped, sim.State.Outcome);
            ulong hash = sim.State.Hash.Value;
            StepN(sim, 50);
            Assert.AreEqual(10, sim.State.Tick);
            Assert.AreEqual(hash, sim.State.Hash.Value);
        }

        [Test]
        public void Hash_DependsOnInputsAndSeed()
        {
            // Note: the hash samples state every 30 ticks, so inputs whose effects converge before
            // the next checkpoint (e.g. the same dash one tick later) give the same hash by design.
            InputLog a = Log(Command.Dash(0, Dir.N));
            InputLog b = Log(Command.Dash(0, Dir.E));
            Assert.AreEqual(RaidRunner.RunToEnd(Vault(), 1, a).FinalHash, RaidRunner.RunToEnd(Vault(), 1, a).FinalHash);
            Assert.AreNotEqual(RaidRunner.RunToEnd(Vault(), 1, a).FinalHash, RaidRunner.RunToEnd(Vault(), 1, b).FinalHash);
            Assert.AreNotEqual(RaidRunner.RunToEnd(Vault(), 1, a).FinalHash, RaidRunner.RunToEnd(Vault(), 2, a).FinalHash);
        }

        private static VaultDef Vault()
        {
            return TestVaults.Parse(Room);
        }

        private static InputLog Log(params Command[] commands)
        {
            return new InputLog(commands);
        }

        private static RaidSim Sim(params Command[] commands)
        {
            return new RaidSim(Vault(), 1, Log(commands));
        }

        private static void StepN(RaidSim sim, int n)
        {
            for (int i = 0; i < n; i++)
            {
                sim.Step();
            }
        }
    }
}
