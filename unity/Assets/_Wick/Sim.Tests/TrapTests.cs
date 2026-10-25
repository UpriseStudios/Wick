using System.Collections.Generic;
using NUnit.Framework;

namespace Wick.Sim.Tests
{
    public class TrapTests
    {
        // Corridor: floor x = 1..10 on row 1, entrance (1,1).
        private static readonly string[] Corridor =
        {
            "############",
            "#E.........#",
            "############",
        };

        // ---- Drip Pipe --------------------------------------------------------------

        [Test]
        public void Drip_FirstDamagingTick_FollowsOffset()
        {
            // phase = (t + 25) % 30 < 6  =>  t in [5, 10]
            RaidSim sim = Sim(Obj(1, "drip_pipe", 1, 1, cycle: 30, offset: 25));
            Steps(sim, 5);
            Assert.AreEqual(3, sim.State.Wick.Flickers, "tick 4: phase 29, dry");
            Steps(sim, 1);
            Assert.AreEqual(2, sim.State.Wick.Flickers, "tick 5: phase 0, drip");
        }

        [Test]
        public void Drip_LastDamagingTick()
        {
            // Window ticks 0-5. A dash makes (2,1) Wick's occupied tile 2 ticks after it starts.
            RaidSim hitOnFive = Sim(Obj(1, "drip_pipe", 2, 1, cycle: 30), Command.Dash(3, Dir.E));
            Steps(hitOnFive, 6);
            Assert.AreEqual(2, hitOnFive.State.Wick.Flickers, "occupied on tick 5 (phase 5): hit");

            RaidSim dryOnSix = Sim(Obj(1, "drip_pipe", 2, 1, cycle: 30), Command.Dash(4, Dir.E));
            Steps(dryOnSix, 30);
            Assert.AreEqual(3, dryOnSix.State.Wick.Flickers, "occupied from tick 6 (phase 6): dry until tick 30");
            Steps(dryOnSix, 1);
            Assert.AreEqual(2, dryOnSix.State.Wick.Flickers, "tick 30: next drip");
        }

        [Test]
        public void Invulnerability_BlocksHitsFor45Ticks_ThenSnuffs()
        {
            // Drip on the entrance every 30 ticks (window 0-5). Hits at 0, 60 and 120; the drips at
            // 30 and 90 land inside invulnerability windows (1-45 and 61-105).
            RaidSim sim = Sim(Obj(1, "drip_pipe", 1, 1, cycle: 30));
            var lost = new List<int>();
            while (!sim.IsOver)
            {
                int tick = sim.State.Tick;
                sim.Step();
                foreach (SimEvent e in sim.Events)
                {
                    if (e.Type == SimEventType.FlickerLost)
                    {
                        lost.Add(tick);
                    }
                }
            }

            CollectionAssert.AreEqual(new[] { 0, 60, 120 }, lost);
            Assert.AreEqual(Outcome.Snuffed, sim.State.Outcome);
            Assert.AreEqual(121, sim.State.Tick);
        }

        [Test]
        public void Invulnerability_LastProtectedTickIs45AfterTheHit()
        {
            // Hit at tick 0 by drip A (protected 1-45); drip B on the same tile is live on ticks 45-50.
            RaidSim on45 = Sim(Obj(1, "drip_pipe", 1, 1, cycle: 200),
                Obj(2, "drip_pipe", 1, 1, cycle: 200, offset: 155));
            Steps(on45, 46);
            Assert.AreEqual(2, on45.State.Wick.Flickers, "tick 45 is still protected");
            Steps(on45, 1);
            Assert.AreEqual(1, on45.State.Wick.Flickers, "tick 46 lands");
        }

        // ---- Bellows ----------------------------------------------------------------

        [Test]
        public void Bellows_GustHitsAlongItsRow_UpToRange()
        {
            // Facing W from (7,1): the gust covers (6,1) .. (1,1).
            RaidSim inRange = Sim(Obj(1, "bellows", 7, 1, cycle: 60, rot: 3));
            Steps(inRange, 1);
            Assert.AreEqual(2, inRange.State.Wick.Flickers);
            Assert.AreEqual(SimTuning.GustDimTicks, inRange.State.Wick.GustDimTicks);

            RaidSim outOfRange = Sim(Obj(1, "bellows", 8, 1, cycle: 60, rot: 3));
            Steps(outOfRange, 15);
            Assert.AreEqual(3, outOfRange.State.Wick.Flickers, "7 tiles away");
        }

        [Test]
        public void Bellows_CrateAndWallGiveCover()
        {
            RaidSim crate = Sim(Obj(1, "bellows", 7, 1, cycle: 60, rot: 3), Obj(2, "crate", 4, 1));
            Steps(crate, 15);
            Assert.AreEqual(3, crate.State.Wick.Flickers, "behind the crate");

            RaidSim facingAway = Sim(Obj(1, "bellows", 7, 1, cycle: 60, rot: 1));
            Steps(facingAway, 15);
            Assert.AreEqual(3, facingAway.State.Wick.Flickers, "gust goes east into the wall");
        }

        [Test]
        public void Bellows_DimsLightEvenWhenInvulnerable()
        {
            // Drip hits at tick 0; the gust (ticks 10-24) arrives during invulnerability.
            RaidSim sim = Sim(Obj(1, "drip_pipe", 1, 1, cycle: 200),
                Obj(2, "bellows", 7, 1, cycle: 60, offset: 50, rot: 3));
            Steps(sim, 11);
            Assert.AreEqual(2, sim.State.Wick.Flickers, "no second flicker lost");
            Assert.AreEqual(SimTuning.GustDimTicks, sim.State.Wick.GustDimTicks);

            Steps(sim, 1);
            // 2 flickers = 3000, dimmed by 1000.
            Assert.AreEqual(2000, Lighting.RadiusUnits(in sim.State.Wick));
        }

        [Test]
        public void GustDim_NeverPutsALitFlameOut()
        {
            Assert.AreEqual(SimTuning.MinLightRadius, Lighting.RadiusUnits(1, true, true));
            Assert.AreEqual(0, Lighting.RadiusUnits(0, false, true));
        }

        // ---- Spike Floor ------------------------------------------------------------

        [Test]
        public void Spikes_RaisedFrom_OffsetTick()
        {
            // phase = (t + 50) % 60 < 20  =>  raised on ticks 10-29.
            RaidSim sim = Sim(Obj(1, "spike_floor", 1, 1, cycle: 60, offset: 50));
            Steps(sim, 10);
            Assert.AreEqual(3, sim.State.Wick.Flickers);
            Steps(sim, 1);
            Assert.AreEqual(2, sim.State.Wick.Flickers);
        }

        [Test]
        public void Spikes_LastRaisedTick()
        {
            // Raised ticks 0-19 and 60-79; occupied tile switches 2 ticks after a dash starts.
            RaidSim hit = Sim(Obj(1, "spike_floor", 2, 1, cycle: 60), Command.Dash(17, Dir.E));
            Steps(hit, 20);
            Assert.AreEqual(2, hit.State.Wick.Flickers, "occupied on tick 19: hit");

            RaidSim miss = Sim(Obj(1, "spike_floor", 2, 1, cycle: 60), Command.Dash(18, Dir.E));
            Steps(miss, 60);
            Assert.AreEqual(3, miss.State.Wick.Flickers, "occupied from tick 20: safe until 60");
        }

        [Test]
        public void Spikes_WarnFifteenTicksBeforeRising()
        {
            RaidSim sim = Sim(Obj(1, "spike_floor", 5, 1, cycle: 60));
            Assert.AreEqual(new[] { 45, 105 }, TicksOf(sim, SimEventType.TrapWarning, 120));
        }

        // ---- Snuffer Bell -----------------------------------------------------------

        [Test]
        public void Bell_LandsAfterItsDrop_AndSnuffsEvenWhenInvulnerable()
        {
            RaidSim sim = Sim(Obj(1, "drip_pipe", 1, 1, cycle: 200), Obj(2, "snuffer_bell", 2, 1),
                Command.Dash(1, Dir.E));
            // Dash from tick 1: progress 200, 400, 600, so (2,1) is occupied on tick 3: the bell starts to
            // fall and lands 8 ticks later, on tick 11. Wick stopped on the plate (arriving tick 5).
            Steps(sim, 3);
            Assert.AreEqual(2, sim.State.Wick.Flickers, "drip hit at tick 0");
            Steps(sim, 1);
            Assert.AreEqual(12, sim.State.BellFall[1], "falling from tick 3, lands on tick 11");
            Steps(sim, 7);
            Assert.AreEqual(2, sim.State.Wick.Flickers, "still falling on tick 10");
            Steps(sim, 1);
            Assert.AreEqual(0, sim.State.Wick.Flickers, "landed on tick 11, through invulnerability");
            Assert.AreEqual(Outcome.Snuffed, sim.State.Outcome);
            Assert.IsTrue(sim.State.Spent[1]);
        }

        [Test]
        public void Bell_ADashAcrossTheLevel1Plate_GetsAway()
        {
            // Two dashes: over the plate at (2,1) (on it ticks 2-6) and on to (3,1). It lands on tick 10.
            RaidSim sim = Sim(Obj(1, "snuffer_bell", 2, 1), Command.Dash(0, Dir.E), Command.Dash(5, Dir.E));
            Steps(sim, 20);
            Assert.AreEqual(3, sim.State.Wick.Flickers);
            Assert.IsTrue(sim.State.Spent[0], "it dropped behind him");
        }

        // ---- End and events ---------------------------------------------------------

        [Test]
        public void SnuffedOnTheEntrance_BeatsEscaped()
        {
            // Drip on the entrance live on ticks 1-6, 61-66, 121-126: hits at 1 and 61 leave one
            // flicker. Wick steps out and dashes back so he arrives on tick 121, as it drips.
            RaidSim sim = Sim(Obj(1, "drip_pipe", 1, 1, cycle: 60, offset: 59),
                Command.Dash(107, Dir.E), Command.Dash(117, Dir.W));
            while (!sim.IsOver)
            {
                sim.Step();
            }

            Assert.AreEqual(Outcome.Snuffed, sim.State.Outcome);
            Assert.AreEqual(122, sim.State.Tick);
            Assert.AreEqual(new TilePos(1, 1), sim.State.Wick.Tile);
        }

        [Test]
        public void Events_FireOnTheirTicks()
        {
            RaidSim sim = Sim(Obj(1, "drip_pipe", 2, 1, cycle: 30));
            Assert.AreEqual(new[] { 0, 30 }, TicksOf(sim, SimEventType.TrapFired, 60));

            RaidSim near = Sim(Obj(1, "drip_pipe", 2, 1, cycle: 30));
            Assert.AreEqual(new[] { 0, 30 }, TicksOf(near, SimEventType.NearMiss, 60), "dripping next to Wick");
        }

        [Test]
        public void ReadingEvents_DoesNotChangeTheHash()
        {
            ObjectSpec[] objects =
            {
                Obj(1, "drip_pipe", 2, 1, cycle: 30), Obj(2, "spike_floor", 4, 1, cycle: 60),
                Obj(3, "bellows", 9, 1, cycle: 90, rot: 3),
            };
            Command[] log = { Command.RunStart(0, Dir.E), Command.RunStop(40), Command.RunStart(90, Dir.W) };

            ulong headless = RaidRunner.RunToEnd(Vault(objects), 1, new InputLog(log)).FinalHash;

            var sim = new RaidSim(Vault(objects), 1, new InputLog(log));
            int seen = 0;
            while (!sim.IsOver)
            {
                sim.Step();
                seen += sim.Events.Count;
            }

            Assert.Greater(seen, 0);
            Assert.AreEqual(headless, sim.GetResult().FinalHash);
        }

        // ---- Helpers ----------------------------------------------------------------

        public struct ObjectSpec
        {
            public string Json;
        }

        public static ObjectSpec Obj(int id, string type, int x, int y, int cycle = 0, int offset = 0, int rot = 0,
            int value = 0, string patrol = null)
        {
            string json = "{\"id\":" + id + ",\"type\":\"" + type + "\",\"x\":" + x + ",\"y\":" + y +
                ",\"cycle\":" + cycle + ",\"offset\":" + offset + ",\"rot\":" + rot + ",\"value\":" + value +
                (patrol != null ? ",\"patrol\":" + patrol : "") + "}";
            return new ObjectSpec { Json = json };
        }

        private static VaultDef Vault(ObjectSpec[] objects)
        {
            return Vault(Corridor, objects);
        }

        public static VaultDef Vault(string[] map, ObjectSpec[] objects)
        {
            var parts = new string[objects.Length];
            for (int i = 0; i < objects.Length; i++)
            {
                parts[i] = objects[i].Json;
            }

            return VaultJson.Parse(TestVaults.Json(map, "[" + string.Join(",", parts) + "]"));
        }

        private static RaidSim Sim(params ObjectSpec[] objects)
        {
            return new RaidSim(Vault(objects), 1, InputLog.Empty);
        }

        private static RaidSim Sim(ObjectSpec a, params Command[] commands)
        {
            return new RaidSim(Vault(new[] { a }), 1, new InputLog(commands));
        }

        private static RaidSim Sim(ObjectSpec a, ObjectSpec b, params Command[] commands)
        {
            return new RaidSim(Vault(new[] { a, b }), 1, new InputLog(commands));
        }

        public static void Steps(RaidSim sim, int n)
        {
            for (int i = 0; i < n; i++)
            {
                sim.Step();
            }
        }

        public static int[] TicksOf(RaidSim sim, SimEventType type, int ticks)
        {
            var found = new List<int>();
            for (int i = 0; i < ticks && !sim.IsOver; i++)
            {
                int tick = sim.State.Tick;
                sim.Step();
                foreach (SimEvent e in sim.Events)
                {
                    if (e.Type == type)
                    {
                        found.Add(tick);
                    }
                }
            }

            return found.ToArray();
        }
    }
}
