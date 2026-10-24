using NUnit.Framework;
using static Wick.Sim.Tests.TrapTests;

namespace Wick.Sim.Tests
{
    /// <summary>
    /// M10 defence upgrade levels (docs/m10-plan.md, "Defence levels") and knockback, which
    /// Bellows level 5 is the first to use.
    /// </summary>
    public class DefenceLevelTests
    {
        // Corridor: floor x = 1..10 on row 1, entrance (1,1).
        private static readonly string[] Corridor =
        {
            "############",
            "#E.........#",
            "############",
        };

        private static VaultDef At(int level, ObjectType type, string[] map, params ObjectSpec[] objects)
        {
            VaultDef vault = Vault(map, objects);
            return level == 1 ? vault : vault.WithDefenceLevels(DefenceLevels.Base.With(type, level));
        }

        private static RaidSim Sim(VaultDef vault, params Command[] commands)
        {
            return new RaidSim(vault, 1, new InputLog(commands));
        }

        // ---- The table ----------------------------------------------------------------

        [Test]
        public void Stats_FollowThePlan()
        {
            CollectionAssert.AreEqual(new[] { 0, 2, 4, 6, 9 }, new[]
            {
                DefenceStats.CycleReduction(1), DefenceStats.CycleReduction(2), DefenceStats.CycleReduction(3),
                DefenceStats.CycleReduction(4), DefenceStats.CycleReduction(5),
            });
            Assert.AreEqual(125, DefenceStats.OwlChaseSpeed(5));
            Assert.AreEqual(110, DefenceStats.OwlChaseSpeed(3));
            Assert.AreEqual(54, DefenceStats.MothSpeed(5));
            Assert.AreEqual(45, DefenceStats.MothDrainTicks(5));
            Assert.AreEqual(SimTuning.MothDrainTicks, DefenceStats.MothDrainTicks(4));
            Assert.AreEqual(DefenceStats.MinTellTicks, DefenceStats.SpikeWarnTicks(5), "tells never go below the floor");
            Assert.AreEqual(1, DefenceStats.BellowsKnockback(5));
            Assert.AreEqual(0, DefenceStats.BellowsKnockback(4));
        }

        [Test]
        public void ACycleNeverDropsBelowItsTypesMinimum()
        {
            VaultDef v = Vault(Corridor, new[] { Obj(1, "drip_pipe", 2, 1, cycle: 8) });
            Assert.AreEqual(ObjectTypes.MinCycle(ObjectType.DripPipe), DefenceStats.Cycle(v.GetObject(0), 5));
        }

        [Test]
        public void TheCap_IsHalfTheVaultLevelRoundedUp()
        {
            CollectionAssert.AreEqual(new[] { 1, 1, 2, 2, 3, 3, 4, 4, 5, 5 }, new[]
            {
                DefenceLevels.Cap(1), DefenceLevels.Cap(2), DefenceLevels.Cap(3), DefenceLevels.Cap(4), DefenceLevels.Cap(5),
                DefenceLevels.Cap(6), DefenceLevels.Cap(7), DefenceLevels.Cap(8), DefenceLevels.Cap(9), DefenceLevels.Cap(10),
            });
        }

        // ---- Data ---------------------------------------------------------------------

        [Test]
        public void Levels_RoundTripThroughJson_AndBaseVaultsWriteNoField()
        {
            VaultDef plain = Vault(Corridor, new[] { Obj(1, "drip_pipe", 2, 1, cycle: 45) });
            StringAssert.DoesNotContain("defenceLevels", VaultJson.Write(plain), "vaults from before M10 are unchanged");

            VaultDef upgraded = plain.WithDefenceLevels(DefenceLevels.Base.With(ObjectType.DripPipe, 3).With(ObjectType.NightOwl, 2));
            string json = VaultJson.Write(upgraded);
            StringAssert.Contains("\"defenceLevels\":{\"drip_pipe\":3,\"night_owl\":2}", json);
            VaultDef back = VaultJson.Parse(json);
            Assert.AreEqual(3, back.LevelOf(ObjectType.DripPipe));
            Assert.AreEqual(2, back.LevelOf(ObjectType.NightOwl));
            Assert.AreEqual(1, back.LevelOf(ObjectType.Bellows));
        }

        [Test]
        public void BadLevels_AreRejected()
        {
            string json = TestVaults.Json(Corridor);
            Assert.Throws<System.FormatException>(() => VaultJson.Parse(json.TrimEnd('}') + ",\"defenceLevels\":{\"drip_pipe\":6}}"));
            Assert.Throws<System.FormatException>(() => VaultJson.Parse(json.TrimEnd('}') + ",\"defenceLevels\":{\"crate\":2}}"));
            Assert.Throws<System.ArgumentException>(() => DefenceLevels.Base.With(ObjectType.Treasure, 2));
        }

        [Test]
        public void Levels_ArePartOfTheLayoutHash()
        {
            VaultDef plain = Vault(Corridor, new[] { Obj(1, "drip_pipe", 2, 1, cycle: 45) });
            VaultDef upgraded = plain.WithDefenceLevels(DefenceLevels.Base.With(ObjectType.DripPipe, 2));
            Assert.AreNotEqual(ClearCheck.LayoutHashOf(plain), ClearCheck.LayoutHashOf(upgraded),
                "an upgrade makes the old clear-check stale");
        }

        // ---- Timed traps --------------------------------------------------------------

        [Test]
        public void ALevelShortensTheCycle()
        {
            // Cycle 45: level 1 drips again at tick 45, level 5 (36) at tick 36.
            VaultDef one = At(1, ObjectType.DripPipe, Corridor, Obj(1, "drip_pipe", 1, 1, cycle: 45));
            VaultDef five = At(5, ObjectType.DripPipe, Corridor, Obj(1, "drip_pipe", 1, 1, cycle: 45));
            CollectionAssert.AreEqual(new[] { 0, 45 }, TicksOf(Sim(one), SimEventType.TrapFired, 60));
            CollectionAssert.AreEqual(new[] { 0, 36 }, TicksOf(Sim(five), SimEventType.TrapFired, 60));
        }

        [Test]
        public void SpikeLevel5_ShortensTheRumbleToTheFloor()
        {
            VaultDef one = At(1, ObjectType.SpikeFloor, Corridor, Obj(1, "spike_floor", 5, 1, cycle: 60));
            VaultDef five = At(5, ObjectType.SpikeFloor, Corridor, Obj(1, "spike_floor", 5, 1, cycle: 60));
            CollectionAssert.AreEqual(new[] { 45 }, TicksOf(Sim(one), SimEventType.TrapWarning, 60), "15 ticks before tick 60");
            CollectionAssert.AreEqual(new[] { 41 }, TicksOf(Sim(five), SimEventType.TrapWarning, 51), "cycle 51: 10 ticks before");
        }

        [Test]
        public void DripLevel5_LeavesAPuddleThatSlowsWick()
        {
            // The drip on (2,1) falls on ticks 0-5; a dash from tick 6 crosses its wet tile.
            int Arrival(int level)
            {
                RaidSim sim = Sim(At(level, ObjectType.DripPipe, Corridor, Obj(1, "drip_pipe", 2, 1, cycle: 45)),
                    Command.Dash(6, Dir.E));
                while (sim.State.Wick.Tile != new TilePos(2, 1))
                {
                    sim.Step();
                }

                Assert.AreEqual(3, sim.State.Wick.Flickers, "dry, just wet");
                return sim.State.Tick - 1;
            }

            Assert.AreEqual(10, Arrival(4), "no puddle: five dash ticks, 6 to 10");
            Assert.AreEqual(12, Arrival(5), "tiptoe speed once his centre is over the puddle");
        }

        // ---- Bellows level 5: knockback -----------------------------------------------

        [Test]
        public void BellowsLevel5_KnocksWickOneTileDownTheGust()
        {
            // In the west wall facing east: the gust covers (1,1) .. (6,1) on ticks 0-14.
            ObjectSpec bellows = Obj(1, "bellows", 0, 1, cycle: 60, rot: 1);
            RaidSim sim = Sim(At(5, ObjectType.Bellows, Corridor, bellows));
            sim.Step();
            Assert.AreEqual(2, sim.State.Wick.Flickers);
            Assert.AreEqual(1, sim.State.Wick.KnockTiles);
            Assert.IsTrue(sim.State.Wick.Moving);

            Steps(sim, 4);
            Assert.AreEqual(new TilePos(2, 1), sim.State.Wick.Tile, "four ticks a tile at knockback speed");
            Assert.IsFalse(sim.State.Wick.Moving);
            Assert.AreEqual(0, sim.State.Wick.KnockTiles);
            Assert.AreEqual(SimTuning.InvulnTicks - 4, sim.State.Wick.InvulnTicks, "the usual invulnerability window");

            RaidSim four = Sim(At(4, ObjectType.Bellows, Corridor, bellows));
            Steps(four, 5);
            Assert.AreEqual(new TilePos(1, 1), four.State.Wick.Tile, "no knockback below level 5");
        }

        [Test]
        public void Knockback_IntoAWall_GoesNowhere()
        {
            // In the east wall facing west: the push is west, and (0,1) is wall.
            RaidSim sim = Sim(At(5, ObjectType.Bellows, new[] { "#####", "#E..#", "#####" }, Obj(1, "bellows", 4, 1, cycle: 60, rot: 3)));
            sim.Step();
            Assert.AreEqual(2, sim.State.Wick.Flickers);
            Assert.AreEqual(0, sim.State.Wick.KnockTiles);
            Assert.IsFalse(sim.State.Wick.Moving);
            Assert.AreEqual(new TilePos(1, 1), sim.State.Wick.Tile);
        }

        [Test]
        public void Knockback_NeverSetsOffASnufferBell()
        {
            VaultDef vault = Vault(Corridor, new[] { Obj(1, "bellows", 0, 1, cycle: 60, rot: 1), Obj(2, "snuffer_bell", 2, 1) })
                .WithDefenceLevels(DefenceLevels.Base.With(ObjectType.Bellows, 5));
            RaidSim sim = Sim(vault);
            Steps(sim, 10);
            Assert.AreEqual(new TilePos(1, 1), sim.State.Wick.Tile, "the plate counts as in the way");
            Assert.AreEqual(2, sim.State.Wick.Flickers, "lost one flicker to the gust, not snuffed");
            Assert.AreEqual(Outcome.Running, sim.State.Outcome);
        }

        [Test]
        public void Knockback_OntoTheEntrance_IsNotAnEscape()
        {
            // Wick steps to (2,1) by tick 4. The gust from the east wall knocks him home: at level 5
            // the 60-tick cycle is 51, so offset 41 puts phase 0 on tick 10.
            VaultDef vault = At(5, ObjectType.Bellows, new[] { "#####", "#E..#", "#####" },
                Obj(1, "bellows", 4, 1, cycle: 60, offset: 41, rot: 3));
            RaidSim sim = Sim(vault, Command.Dash(0, Dir.E));
            Steps(sim, 20);
            Assert.AreEqual(new TilePos(1, 1), sim.State.Wick.Tile);
            Assert.IsTrue(sim.State.Wick.LeftEntrance);
            Assert.AreEqual(Outcome.Running, sim.State.Outcome, "only walking out escapes");
        }

        [Test]
        public void ASwipeDuringKnockback_RunsWhenItEnds()
        {
            ObjectSpec bellows = Obj(1, "bellows", 0, 1, cycle: 60, rot: 1);
            RaidSim sim = Sim(At(5, ObjectType.Bellows, Corridor, bellows), Command.Dash(2, Dir.E));
            Steps(sim, 5);
            Assert.AreEqual(new TilePos(2, 1), sim.State.Wick.Tile, "the slide finished first");
            Assert.IsTrue(sim.State.Wick.Moving, "then the queued dash started");
            Assert.AreEqual(Dir.E, sim.State.Wick.MoveDir);
        }

        // ---- Snuffer Bell level 5: wide plate ------------------------------------------

        [Test]
        public void BellLevel5_ThePlateCoversTheTileInFront()
        {
            // The bell on (3,1) faces west, so its level 5 plate also covers (2,1).
            ObjectSpec bell = Obj(1, "snuffer_bell", 3, 1, rot: 3);
            RaidSim five = Sim(At(5, ObjectType.SnufferBell, Corridor, bell), Command.Dash(0, Dir.E));
            Steps(five, 5);
            Assert.AreEqual(Outcome.Snuffed, five.State.Outcome);

            RaidSim four = Sim(At(4, ObjectType.SnufferBell, Corridor, bell), Command.Dash(0, Dir.E));
            Steps(four, 5);
            Assert.AreEqual(3, four.State.Wick.Flickers, "(2,1) is plain floor below level 5");
        }

        // ---- Guardians -----------------------------------------------------------------

        [Test]
        public void OwlLevels_ChaseFaster()
        {
            // Facing west from (4,1), the owl sees Wick (3 tiles, lit) on tick 0 and starts chasing.
            ObjectSpec owl = Obj(1, "night_owl", 4, 1, rot: 3);
            RaidSim one = Sim(At(1, ObjectType.NightOwl, Corridor, owl));
            RaidSim five = Sim(At(5, ObjectType.NightOwl, Corridor, owl));
            one.Step();
            five.Step();
            Assert.AreEqual(GuardianMode.Chase, one.State.Guardians[0].Mode);
            Assert.AreEqual(SimTuning.OwlChaseSpeed, one.State.Guardians[0].Progress);
            Assert.AreEqual(DefenceStats.OwlChaseSpeed(5), five.State.Guardians[0].Progress);
        }

        [Test]
        public void OwlLevel5_HasAWiderSightCone()
        {
            // The owl at (5,1) faces west. Wick at the entrance (3,4) is 2 tiles ahead of it and 3
            // to the side: outside the 90-degree cone, exactly on the edge of level 5's wider one.
            string[] room =
            {
                "#######",
                "#.....#",
                "#.....#",
                "#.....#",
                "#..E..#",
                "#######",
            };

            ObjectSpec owl = Obj(1, "night_owl", 5, 1, rot: 3);
            RaidSim one = Sim(At(1, ObjectType.NightOwl, room, owl));
            RaidSim five = Sim(At(5, ObjectType.NightOwl, room, owl));
            one.Step();
            five.Step();
            Assert.AreEqual(GuardianMode.Patrol, one.State.Guardians[0].Mode, "level 1 doesn't see him");
            Assert.AreEqual(GuardianMode.Chase, five.State.Guardians[0].Mode, "level 5 does");
        }

        [Test]
        public void MothLevel5_DrainsSooner()
        {
            ObjectSpec moths = Obj(1, "wax_moths", 1, 1);
            int[] one = TicksOf(Sim(At(1, ObjectType.WaxMoths, Corridor, moths)), SimEventType.FlickerLost, 70);
            int[] five = TicksOf(Sim(At(5, ObjectType.WaxMoths, Corridor, moths)), SimEventType.FlickerLost, 70);
            Assert.AreEqual(SimTuning.MothDrainTicks - 1, one[0]);
            Assert.AreEqual(44, five[0]);
        }

        // ---- Validation ----------------------------------------------------------------

        [Test]
        public void LevelsAboveTheVaultLevelsCap_AreReported()
        {
            VaultDef v = Vault(Corridor, new ObjectSpec[0]).WithDefenceLevels(DefenceLevels.Base.With(ObjectType.Bellows, 2));
            Assert.That(VaultValidator.Validate(v).ConvertAll(i => i.Code), Has.Member(VaultIssueCode.DefenceLevelTooHigh),
                "Vault Level 1 caps defences at level 1");
        }
    }
}
