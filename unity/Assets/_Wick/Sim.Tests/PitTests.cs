using System;
using System.Collections.Generic;
using NUnit.Framework;
using static Wick.Sim.Tests.TrapTests;

namespace Wick.Sim.Tests
{
    /// <summary>
    /// Pits (M10.1, docs/m10-1-pits.md): stepping onto one costs a flicker and drops Wick back at
    /// the entrance after a fall; the exit then stays shut for 8 s; a level 5 pit also puts his loot
    /// back. One test per row of the pack's rules and edge-case tables.
    /// </summary>
    public class PitTests
    {
        // Entrance (1,1); floor (2,1); pit (3,1); floor beyond. A dash is 5 ticks a tile.
        private static readonly string[] Lane = { "########", "#E.O...#", "########" };

        // The same lane with plain floor where the pit was, to show what the pit changes.
        private static readonly string[] NoPit = { "########", "#E.....#", "########" };

        private static readonly TilePos Entrance = new TilePos(1, 1);
        private static readonly TilePos Pit = new TilePos(3, 1);

        private static RaidSim Sim(string[] map, int pitLevel, ObjectSpec[] objects, params Command[] commands)
        {
            VaultDef vault = Vault(map, objects);
            if (pitLevel > 1)
            {
                vault = vault.WithDefenceLevels(DefenceLevels.Base.With(ObjectType.Pit, pitLevel));
            }

            return new RaidSim(vault, 1, new InputLog(commands));
        }

        private static RaidSim Sim(int pitLevel, params Command[] commands)
        {
            return Sim(Lane, pitLevel, new ObjectSpec[0], commands);
        }

        /// <summary>Dashes from the entrance into the pit: (2,1) on tick 4, the pit on tick 9.</summary>
        private static Command[] IntoThePit(int from)
        {
            return new[] { Command.Dash(from, Dir.E), Command.Dash(from + 5, Dir.E) };
        }

        private static Command[] Concat(params Command[][] parts)
        {
            var all = new List<Command>();
            foreach (Command[] p in parts)
            {
                all.AddRange(p);
            }

            return all.ToArray();
        }

        /// <summary>Steps to the end (or `ticks`), collecting every event with the tick it fired on.</summary>
        private static List<(int Tick, SimEvent Event)> Run(RaidSim sim, int ticks)
        {
            var events = new List<(int, SimEvent)>();
            for (int i = 0; i < ticks && !sim.IsOver; i++)
            {
                int tick = sim.State.Tick;
                sim.Step();
                foreach (SimEvent e in sim.Events)
                {
                    events.Add((tick, e));
                }
            }

            return events;
        }

        private static int[] Ticks(List<(int Tick, SimEvent Event)> events, SimEventType type)
        {
            return events.FindAll(e => e.Event.Type == type).ConvertAll(e => e.Tick).ToArray();
        }

        [Test]
        public void AFall_CostsAFlicker_AndPopsWickOutAtTheEntrance()
        {
            RaidSim sim = Sim(1, IntoThePit(0));
            List<(int Tick, SimEvent Event)> events = Run(sim, 10);
            CollectionAssert.AreEqual(new[] { 9 }, Ticks(events, SimEventType.PitFall));
            Assert.AreEqual(2, events.Find(e => e.Event.Type == SimEventType.PitFall).Event.Value, "flickers left");
            Assert.IsTrue(sim.State.Wick.Falling);
            Assert.AreEqual(Pit, sim.State.Wick.Tile, "in the pit while he falls");
            CollectionAssert.IsEmpty(Ticks(events, SimEventType.FlickerLost), "a fall isn't a trap hit");

            events = Run(sim, 30);
            CollectionAssert.AreEqual(new[] { 33 }, Ticks(events, SimEventType.PitRespawn), "24 ticks in a level 1 pit");
            WickState w = sim.State.Wick;
            Assert.AreEqual(Entrance, w.Tile);
            Assert.AreEqual(Dir.E, w.Facing, "facing into the vault");
            Assert.IsFalse(w.Falling);
            Assert.AreEqual(2, w.Flickers);
            Assert.AreEqual(Outcome.Running, sim.State.Outcome, "a respawn on the entrance isn't an escape");
        }

        [Test]
        public void HigherLevels_KeepWickInThePitLonger()
        {
            CollectionAssert.AreEqual(new[] { 24, 30, 36, 42, 42 },
                new[] { DefenceStats.PitFallTicks(1), DefenceStats.PitFallTicks(2), DefenceStats.PitFallTicks(3),
                    DefenceStats.PitFallTicks(4), DefenceStats.PitFallTicks(5) });
            List<(int Tick, SimEvent Event)> events = Run(Sim(3, IntoThePit(0)), 60);
            CollectionAssert.AreEqual(new[] { 9 + 36 }, Ticks(events, SimEventType.PitRespawn));
        }

        [Test]
        public void TheTimer_KeepsRunningThroughTheFall()
        {
            RaidSim sim = Sim(1, IntoThePit(0));
            Run(sim, 40);
            Assert.AreEqual(40, sim.State.Tick);
        }

        [Test]
        public void AFallOnTheLastFlicker_SnuffsHim()
        {
            // Three falls, each started once the invulnerability from the last respawn has worn off.
            RaidSim sim = Sim(1, Concat(IntoThePit(0), IntoThePit(80), IntoThePit(160)));
            List<(int Tick, SimEvent Event)> events = Run(sim, 400);
            CollectionAssert.AreEqual(new[] { 9, 89, 169 }, Ticks(events, SimEventType.PitFall));
            CollectionAssert.AreEqual(new[] { 169 }, Ticks(events, SimEventType.Snuffed));
            CollectionAssert.AreEqual(new[] { 33, 113 }, Ticks(events, SimEventType.PitRespawn), "no respawn from the last");
            Assert.AreEqual(Outcome.Snuffed, sim.State.Outcome);
            Assert.AreEqual(170, sim.GetResult().Ticks);
        }

        [Test]
        public void AFallWhileInvulnerable_CostsNoFlicker_ButStillSendsHimBack()
        {
            // Back in straight after the respawn on tick 33, inside its 45-tick invulnerability.
            RaidSim sim = Sim(1, Concat(IntoThePit(0), IntoThePit(34)));
            List<(int Tick, SimEvent Event)> events = Run(sim, 100);
            CollectionAssert.AreEqual(new[] { 9, 43 }, Ticks(events, SimEventType.PitFall));
            CollectionAssert.AreEqual(new[] { 33, 67 }, Ticks(events, SimEventType.PitRespawn));
            Assert.AreEqual(2, sim.State.Wick.Flickers);
        }

        [Test]
        public void AfterARespawn_HeIsInvulnerableForTheUsualWindow()
        {
            RaidSim sim = Sim(1, IntoThePit(0));
            Run(sim, 34);
            Assert.AreEqual(SimTuning.InvulnTicks, sim.State.Wick.InvulnTicks, "as after a hit, counted from the respawn tick");
        }

        [Test]
        public void RunningIntoAPit_StopsTheRun()
        {
            // Run speed is 10 ticks a tile: (2,1) on tick 9, the pit on tick 19.
            RaidSim sim = Sim(1, Command.RunStart(0, Dir.E));
            List<(int Tick, SimEvent Event)> events = Run(sim, 80);
            CollectionAssert.AreEqual(new[] { 19 }, Ticks(events, SimEventType.PitFall));
            Assert.AreEqual(Entrance, sim.State.Wick.Tile, "out at the entrance, and the held run didn't carry on");
            Assert.IsFalse(sim.State.Wick.Moving);
            Assert.IsFalse(sim.State.Wick.Running);
        }

        [Test]
        public void TiptoeingOverAPit_StillFalls()
        {
            RaidSim sim = Sim(1, Concat(new[] { Command.TiptoeOn(0) }, IntoThePit(0)));
            List<(int Tick, SimEvent Event)> events = Run(sim, 60);
            Assert.AreEqual(1, Ticks(events, SimEventType.PitFall).Length);
        }

        [Test]
        public void SlidingOnIceIntoAPit_Falls()
        {
            string[] rink = { "########", "#E~~O..#", "########" };
            RaidSim sim = Sim(rink, 1, new ObjectSpec[0], Command.Dash(0, Dir.E));
            List<(int Tick, SimEvent Event)> events = Run(sim, 40);
            Assert.AreEqual(1, Ticks(events, SimEventType.PitFall).Length, "the slide carries him in");
            Assert.AreEqual(new TilePos(4, 1), events.Find(e => e.Event.Type == SimEventType.PitFall).Event.Tile);
        }

        [Test]
        public void InputsDuringTheFall_AreIgnored()
        {
            // A dash and a decoy while he's in the pit (ticks 9 to 32) do nothing.
            RaidSim sim = new RaidSim(Vault(Lane, new ObjectSpec[0]), 1,
                new InputLog(Concat(IntoThePit(0), new[] { Command.Dash(15, Dir.W), Command.Decoy(20) })), RaidParams.Default);
            int decoys = sim.State.Wick.DecoysLeft;
            Run(sim, 40);
            Assert.AreEqual(Entrance, sim.State.Wick.Tile);
            Assert.IsFalse(sim.State.Wick.Moving, "the dash wasn't queued for after the respawn");
            Assert.AreEqual(decoys, sim.State.Wick.DecoysLeft);
        }

        [Test]
        public void WhileFalling_HisLightIsOut()
        {
            RaidSim sim = Sim(1, IntoThePit(0));
            Run(sim, 12);
            Assert.IsTrue(sim.State.Wick.Falling);
            Assert.IsFalse(Array.Exists(sim.State.Lit, lit => lit), "nothing lit, so nothing sees him");
        }

        [Test]
        public void TheExitStaysShut_For8SecondsAfterARespawn()
        {
            // Respawn on tick 33; out and straight back in arrives on the entrance on tick 43: no escape.
            // The lock lifts on tick 273; out and back again then escapes.
            Command[] commands = Concat(IntoThePit(0),
                new[] { Command.Dash(34, Dir.E), Command.Dash(39, Dir.W), Command.Dash(280, Dir.E), Command.Dash(285, Dir.W) });
            RaidSim sim = Sim(1, commands);
            List<(int Tick, SimEvent Event)> events = Run(sim, 100);
            CollectionAssert.AreEqual(new[] { 33 }, Ticks(events, SimEventType.ExitLocked));
            Assert.AreEqual(SimTuning.PitRespawnExitLockTicks, events.Find(e => e.Event.Type == SimEventType.ExitLocked).Event.Value);
            Assert.AreEqual(Entrance, sim.State.Wick.Tile, "back on the entrance");
            Assert.AreEqual(Outcome.Running, sim.State.Outcome, "but the exit is shut");

            events = Run(sim, 400);
            CollectionAssert.AreEqual(new[] { 33 + 240 }, Ticks(events, SimEventType.ExitUnlocked));
            Assert.AreEqual(Outcome.Escaped, sim.State.Outcome);
            Assert.AreEqual(290, sim.GetResult().Ticks);
            Assert.AreEqual(240, SimTuning.PitRespawnExitLockTicks, "decided: 8 seconds");
        }

        [Test]
        public void BelowLevel5_HeKeepsHisLoot()
        {
            // The 100 pile on (2,1) is taken on tick 2, as he crosses into its tile; 10% slower, he reaches
            // (2,1) on tick 5, and the dash queued on 5 starts then and drops him in on tick 11.
            ObjectSpec[] pile = { Obj(1, "treasure", 2, 1, value: 100) };
            RaidSim sim = Sim(Lane, 4, pile, IntoThePit(0));
            List<(int Tick, SimEvent Event)> events = Run(sim, 60);
            CollectionAssert.AreEqual(new[] { 11 }, Ticks(events, SimEventType.PitFall));
            CollectionAssert.IsEmpty(Ticks(events, SimEventType.LootDropped));
            Assert.AreEqual(1, sim.State.Wick.CarriedPiles);
            Assert.AreEqual(100, sim.State.Wick.CarriedTreasure);
        }

        [Test]
        public void AtLevel5_HisLootGoesBackToItsPiles_AndCanBeTakenAgain()
        {
            // The 100 pile on (2,1) is taken on tick 2 and he drops in on tick 11 (as below). A level 5
            // pit holds him 42 ticks: out on tick 53, and a dash on 54 takes the pile again on 56.
            ObjectSpec[] pile = { Obj(7, "treasure", 2, 1, value: 100) };
            RaidSim sim = Sim(Lane, 5, pile, Concat(IntoThePit(0), new[] { Command.Dash(54, Dir.E) }));
            List<(int Tick, SimEvent Event)> events = Run(sim, 49);
            Assert.AreEqual(1, Ticks(events, SimEventType.LootDropped).Length);
            SimEvent dropped = events.Find(e => e.Event.Type == SimEventType.LootDropped).Event;
            Assert.AreEqual(7, dropped.EntityId);
            Assert.AreEqual(100, dropped.Value);
            Assert.AreEqual(new TilePos(2, 1), dropped.Tile);
            Assert.AreEqual(0, sim.State.Wick.CarriedPiles);
            Assert.AreEqual(0, sim.State.Wick.CarriedTreasure);
            Assert.IsFalse(sim.State.Spent[0], "the pile is back");

            events = Run(sim, 10);
            CollectionAssert.AreEqual(new[] { 56 }, Ticks(events, SimEventType.TreasureTaken), "and grabbed again");
            Assert.AreEqual(100, sim.State.Wick.CarriedTreasure);
        }

        [Test]
        public void AtLevel5_EveryCarriedPile_GoesBackToTheOneItCameFrom()
        {
            // Piles on (2,1) and (2,2); the pit on (3,1).
            string[] room = { "#####", "#E.O#", "#..##", "#####" };
            ObjectSpec[] piles = { Obj(3, "treasure", 2, 1, value: 50), Obj(4, "treasure", 2, 2, value: 50) };
            Command[] commands =
            {
                Command.Dash(0, Dir.E), Command.Dash(5, Dir.S), Command.Dash(12, Dir.N), Command.Dash(19, Dir.E),
            };
            RaidSim sim = Sim(room, 5, piles, commands);
            List<(int Tick, SimEvent Event)> events = Run(sim, 40);
            List<(int Tick, SimEvent Event)> drops = events.FindAll(e => e.Event.Type == SimEventType.LootDropped);
            Assert.AreEqual(2, drops.Count);
            Assert.AreEqual(3, drops[0].Event.EntityId);
            Assert.AreEqual(new TilePos(2, 1), drops[0].Event.Tile);
            Assert.AreEqual(4, drops[1].Event.EntityId);
            Assert.AreEqual(new TilePos(2, 2), drops[1].Event.Tile);
            Assert.IsFalse(sim.State.Spent[0] || sim.State.Spent[1]);
        }

        [Test]
        public void AFallOnTheSameTickAsATrapHit_CountsAsTheFallOnly()
        {
            // A drip over the pit tile drips from tick 9, the very tick he drops in.
            ObjectSpec[] drip = { Obj(1, "drip_pipe", 3, 1, cycle: 45, offset: 36) };
            RaidSim sim = Sim(Lane, 1, drip, IntoThePit(0));
            List<(int Tick, SimEvent Event)> events = Run(sim, 12);
            CollectionAssert.AreEqual(new[] { 9 }, Ticks(events, SimEventType.PitFall));
            CollectionAssert.IsEmpty(Ticks(events, SimEventType.FlickerLost));
            Assert.AreEqual(2, sim.State.Wick.Flickers, "one flicker, not two");

            // Without the pit the same drip catches him there.
            RaidSim floor = Sim(NoPit, 1, drip, IntoThePit(0));
            CollectionAssert.AreEqual(new[] { 9 }, Ticks(Run(floor, 12), SimEventType.FlickerLost));
        }

        [Test]
        public void Knockback_StopsAtAPitEdge()
        {
            Assert.IsFalse(SimTuning.KnockbackIntoPits, "off until the balance simulator decides");

            // A level 5 bellows in the west wall (cycle 60, 51 at level 5) gusts along the lane from
            // tick 10, knocking him 1 tile east from (2,1): into the pit, so the knockback goes nowhere.
            ObjectSpec[] bellows = { Obj(1, "bellows", 0, 1, cycle: 60, offset: 41, rot: 1) };
            Command[] commands = { Command.Dash(0, Dir.E) };
            VaultDef vault = Vault(Lane, bellows).WithDefenceLevels(DefenceLevels.Base.With(ObjectType.Bellows, 5));
            var sim = new RaidSim(vault, 1, new InputLog(commands));
            List<(int Tick, SimEvent Event)> events = Run(sim, 20);
            CollectionAssert.AreEqual(new[] { 10 }, Ticks(events, SimEventType.KnockedBack));
            Assert.AreEqual(0, events.Find(e => e.Event.Type == SimEventType.KnockedBack).Event.Value, "no free tile before the pit");
            Assert.AreEqual(new TilePos(2, 1), sim.State.Wick.Tile);
            CollectionAssert.IsEmpty(Ticks(events, SimEventType.PitFall));

            // On plain floor the same gust pushes him a tile.
            VaultDef floor = Vault(NoPit, bellows).WithDefenceLevels(DefenceLevels.Base.With(ObjectType.Bellows, 5));
            var pushed = new RaidSim(floor, 1, new InputLog(commands));
            Run(pushed, 20);
            Assert.AreEqual(new TilePos(3, 1), pushed.State.Wick.Tile);
        }

        [Test]
        public void Guardians_WalkAroundPits()
        {
            // An owl patrols from (1,2) to (5,2) with a pit at (3,2) in the straight line between.
            string[] room = { "#######", "#.....#", "#..O..#", "#.....#", "###E###" };
            ObjectSpec[] owl = { Obj(1, "night_owl", 1, 2, rot: 1, patrol: "[[1,2],[5,2]]") };
            var sim = new RaidSim(Vault(room, owl), 1, InputLog.Empty);
            bool reached = false;
            for (int i = 0; i < 300; i++)
            {
                sim.Step();
                TilePos at = sim.State.Guardians[0].Tile;
                Assert.AreNotEqual(new TilePos(3, 2), at, "tick " + i + ": the owl stepped into the pit");
                reached |= at == new TilePos(5, 2);
            }

            Assert.IsTrue(reached, "it went round");
        }

        [Test]
        public void Pits_CostBuildPointsPerTile_AndArePaintedNotPlaced()
        {
            Assert.AreEqual(5, VaultRules.Cost(ObjectType.Pit));
            Assert.AreEqual(1, VaultRules.RequiredLevel(ObjectType.Pit), "from Vault Level 1");
            Assert.IsTrue(ObjectTypes.IsUpgradable(ObjectType.Pit));
            VaultDef twoPits = Vault(new[] { "#######", "#E.OO.#", "#######" }, new ObjectSpec[0]);
            Assert.AreEqual(2, VaultRules.PitTileCount(twoPits));
            Assert.AreEqual(10, VaultRules.BuildPoints(twoPits));
            Assert.Throws<FormatException>(() => Vault(NoPit, new[] { Obj(1, "pit", 3, 1) }));
        }
    }
}
