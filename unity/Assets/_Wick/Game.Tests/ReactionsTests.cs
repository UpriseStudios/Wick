using NUnit.Framework;
using Wick.Sim;
using static Wick.Sim.Tests.TrapTests;

namespace Wick.Game.Tests
{
    public class ReactionsTests
    {
        // Corridor: floor x = 1..10 on row 1, entrance (1,1).
        private static readonly string[] Corridor =
        {
            "############",
            "#E.........#",
            "############",
        };

        [Test]
        public void Fear_IsZero_WithNoDangerAround()
        {
            Assert.AreEqual(0f, Fear(0, Obj(1, "treasure", 5, 1, value: 10)));
        }

        [Test]
        public void Fear_RisesNearAnActiveTrap_AndIgnoresAnIdleOne()
        {
            // Spikes 2 tiles away, raised on ticks 0-19 of 60.
            Assert.AreEqual(0.5f, Fear(0, Obj(1, "spike_floor", 3, 1, cycle: 60)), 1e-4f, "raised, 2 of 4 tiles");
            Assert.AreEqual(0f, Fear(30, Obj(1, "spike_floor", 3, 1, cycle: 60)), "lowered");
        }

        [Test]
        public void Fear_IsHighNextToAnUntriggeredBell()
        {
            Assert.AreEqual(0.75f, Fear(0, Obj(1, "snuffer_bell", 2, 1)), 1e-4f, "1 tile from a live plate");
        }

        [Test]
        public void Fear_CountsTheWholeGustLine()
        {
            // Bellows at (9,1) facing west: the gust reaches (3,1), 2 tiles from Wick.
            Assert.AreEqual(0.5f, Fear(0, Obj(1, "bellows", 9, 1, cycle: 60, rot: 3)), 1e-4f);
        }

        private static float Fear(int shownTick, params ObjectSpec[] objects)
        {
            VaultDef vault = Vault(Corridor, objects);
            var sim = new RaidSim(vault, 1);
            return Reactions.Fear(sim, vault, shownTick, in sim.State.Wick, sim.State.Guardians, 4f);
        }
    }
}
