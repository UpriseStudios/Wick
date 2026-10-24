using NUnit.Framework;
using static Wick.Sim.Tests.TrapTests;

namespace Wick.Sim.Tests
{
    /// <summary>
    /// Sleepy Troll (Vault Level 6): a bright flame within 2 tiles wakes it; awake, it stomps its
    /// tile on a rhythm and falls asleep again after 3 s undisturbed. Tiptoeing gets past.
    /// </summary>
    public class SleepyTrollTests
    {
        // Floor x = 1..10 on row 1, entrance (1,1).
        private static readonly string[] Corridor =
        {
            "############",
            "#E.........#",
            "############",
        };

        private static VaultDef Troll(int level, int x)
        {
            VaultDef vault = Vault(Corridor, new[] { Obj(1, "sleepy_troll", x, 1) });
            return level == 1 ? vault : vault.WithDefenceLevels(DefenceLevels.Base.With(ObjectType.SleepyTroll, level));
        }

        [Test]
        public void ABrightFlameNearby_WakesIt_AndItStompsOnARhythm()
        {
            // On the troll's own tile at full flame: awake on tick 0, stomps on 15, 45, ...
            var sim = new RaidSim(Troll(1, 1), 1, InputLog.Empty);
            int[] stomps = TicksOf(sim, SimEventType.TrapFired, 50);
            CollectionAssert.AreEqual(new[] { 15, 45 }, stomps);
            Assert.AreEqual(2, sim.State.Wick.Flickers, "the stomp on 45 lands inside the invulnerability from 15 (to 60)");
        }

        [Test]
        public void TiptoeingPast_NeverWakesIt()
        {
            var sim = new RaidSim(Troll(1, 2), 1, new InputLog(new[] { Command.TiptoeOn(0) }));
            Steps(sim, 120);
            Assert.AreEqual(GuardianMode.Patrol, sim.State.Guardians[0].Mode);
        }

        [Test]
        public void FarEnoughAway_ItSleepsOn()
        {
            var sim = new RaidSim(Troll(1, 4), 1, InputLog.Empty);
            Steps(sim, 60);
            Assert.AreEqual(GuardianMode.Patrol, sim.State.Guardians[0].Mode, "3 tiles away is out of range");
        }

        [Test]
        public void LeftAlone_ItFallsAsleepAgain()
        {
            // Woken from the entrance two tiles away, then Wick tiptoes: undisturbed, it sleeps after 90 ticks.
            var sim = new RaidSim(Troll(1, 3), 1, new InputLog(new[] { Command.TiptoeOn(1) }));
            sim.Step();
            Assert.AreEqual(GuardianMode.Chase, sim.State.Guardians[0].Mode, "woken on tick 0");
            Steps(sim, 89);
            Assert.AreEqual(GuardianMode.Chase, sim.State.Guardians[0].Mode);
            sim.Step();
            Assert.AreEqual(GuardianMode.Patrol, sim.State.Guardians[0].Mode, "asleep after 90 quiet ticks");
        }

        [Test]
        public void Level5_StompsTheTilesAroundIt()
        {
            // The troll on (2,1), Wick beside it on the entrance: only level 5 reaches him.
            var five = new RaidSim(Troll(5, 2), 1, InputLog.Empty);
            CollectionAssert.AreEqual(new[] { 15 }, TicksOf(five, SimEventType.FlickerLost, 20));
            var four = new RaidSim(Troll(4, 2), 1, InputLog.Empty);
            CollectionAssert.IsEmpty(TicksOf(four, SimEventType.FlickerLost, 20));
        }

        [Test]
        public void Levels_WakeAtADimmerLight_ButNeverOnATiptoeAtFullFlame()
        {
            Assert.AreEqual(3000, DefenceStats.TrollWakeLight(1));
            Assert.AreEqual(2800, DefenceStats.TrollWakeLight(2));
            Assert.AreEqual(2300, DefenceStats.TrollWakeLight(5));
            int tiptoeFull = SimTuning.LightRadius3Flickers / SimTuning.TiptoeLightDivisor;
            Assert.Less(tiptoeFull, DefenceStats.TrollWakeLight(5));
        }
    }
}
