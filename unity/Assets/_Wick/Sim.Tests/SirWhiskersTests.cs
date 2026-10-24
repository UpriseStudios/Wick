using System.Collections.Generic;
using NUnit.Framework;
using static Wick.Sim.Tests.TrapTests;

namespace Wick.Sim.Tests
{
    /// <summary>
    /// Sir Whiskers (game spec, Vault Level 10): wakes when a moving flame lights him from within 3
    /// tiles, stalks only while Wick moves, gives up after 1.5 s of stillness, wiggles 30 ticks before
    /// every pounce, swats for 1 flicker and a 2-tile knockback, always prefers a decoy, one per vault.
    /// The goldens whiskers-wall, whiskers-trap and whiskers-freeze cover whole encounters.
    /// </summary>
    public class SirWhiskersTests
    {
        private static readonly string[] Corridor = { "##########", "#E.......#", "##########" };

        private static RaidSim Sim(int catX, params Command[] commands)
        {
            return new RaidSim(Vault(Corridor, new[] { Obj(1, "sir_whiskers", catX, 1) }), 1, new InputLog(commands));
        }

        private static GuardianState Cat(RaidSim sim) => sim.State.Guardians[0];

        [Test]
        public void AStillFlame_NeverWakesHim()
        {
            RaidSim sim = Sim(3);
            Steps(sim, 120);
            Assert.AreEqual(GuardianMode.Patrol, Cat(sim).Mode, "two tiles away and lit, but Wick never moves");
        }

        [Test]
        public void AMovingFlameWithin3Tiles_WakesHim_ButNotFurther()
        {
            RaidSim near = Sim(4, Command.Dash(0, Dir.E));
            near.Step();
            Assert.AreEqual(GuardianMode.Chase, Cat(near).Mode, "3 tiles");

            // One dash to (2,1) leaves him 4 tiles from a cat on (6,1).
            RaidSim far = Sim(6, Command.Dash(0, Dir.E));
            Steps(far, 20);
            Assert.AreEqual(GuardianMode.Patrol, Cat(far).Mode, "4 tiles");
        }

        [Test]
        public void TiptoeingPast_AtADistance_KeepsHimAsleep()
        {
            // Tiptoe halves the light to 2 tiles, so 3 tiles away his tile stays dark.
            RaidSim sim = Sim(4, Command.TiptoeOn(0), Command.Dash(0, Dir.N));
            Steps(sim, 30);
            Assert.AreEqual(GuardianMode.Patrol, Cat(sim).Mode);
        }

        [Test]
        public void HeStalks_OnlyWhileWickMoves()
        {
            RaidSim sim = Sim(4, Command.Dash(0, Dir.E));
            Steps(sim, 5); // Wick arrives at (2,1) on tick 4 and stops
            TilePos stoppedAt = Cat(sim).Moving ? Cat(sim).Tile.Step(Cat(sim).MoveDir) : Cat(sim).Tile;
            Steps(sim, 20);
            Assert.AreEqual(stoppedAt, Cat(sim).Tile, "he finishes his step, then waits");
            Assert.IsFalse(Cat(sim).Moving);
        }

        [Test]
        public void HoldingStill_For45Ticks_MakesHimLoseInterest()
        {
            RaidSim sim = Sim(5, Command.Dash(0, Dir.E));
            Steps(sim, 10);
            Assert.AreEqual(GuardianMode.Chase, Cat(sim).Mode);
            Steps(sim, 60);
            Assert.AreEqual(GuardianMode.Patrol, Cat(sim).Mode, "bored: off home to his cushion");
        }

        [Test]
        public void TheWiggle_LastsExactly30Ticks_BeforeThePounce()
        {
            // Beside him from the start (cat on (2,1), Wick moving off the entrance): he wakes, then wiggles.
            RaidSim sim = Sim(3, Command.Dash(0, Dir.E));
            int wiggleStart = -1;
            int pounce = -1;
            for (int t = 0; t < 80 && pounce < 0; t++)
            {
                sim.Step();
                if (wiggleStart < 0 && Cat(sim).Waypoint == RaidSim.WhiskersWiggling)
                {
                    wiggleStart = t;
                }

                foreach (SimEvent e in sim.Events)
                {
                    if (e.Type == SimEventType.TrapFired)
                    {
                        pounce = t;
                    }
                }
            }

            Assert.AreEqual(SimTuning.WhiskersWiggleTicks, pounce - wiggleStart);
        }

        [Test]
        public void AnyKnockback_StopsShortOfASnufferBell()
        {
            // Wick on (3,1) between the cat on (4,1) and a bell on (1,1)... the bell is two tiles west:
            // the swat's 2 tiles stop on (2,1), short of the plate.
            var vault = Vault(new[] { "#######", "#.E...#", "#######" }, new[]
            {
                Obj(1, "sir_whiskers", 5, 1), Obj(2, "snuffer_bell", 1, 1),
            });
            var sim = new RaidSim(vault, 1, new InputLog(new[]
            {
                Command.Dash(0, Dir.E), Command.Dash(6, Dir.W), Command.Dash(12, Dir.E), Command.Dash(18, Dir.W),
                Command.Dash(24, Dir.E),
            }));
            Steps(sim, 200);
            Assert.AreNotEqual(Outcome.Snuffed, sim.State.Outcome);
            Assert.IsTrue(sim.State.Spent[1] == false, "the bell never dropped");
        }

        [Test]
        public void ADecoy_AlwaysWins()
        {
            RaidSim sim = Sim(4, Command.Dash(0, Dir.E), Command.Decoy(5));
            Steps(sim, 8);
            Assert.AreEqual(GuardianMode.Distracted, Cat(sim).Mode);
            Assert.AreEqual(sim.State.Wick.DecoyTile, Cat(sim).Target);
        }

        [Test]
        public void OnlyOnePerVault_AtVaultLevel10_WithNoLevels()
        {
            VaultDef two = Vault(Corridor, new[] { Obj(1, "sir_whiskers", 4, 1), Obj(2, "sir_whiskers", 6, 1) });
            List<VaultIssueCode> codes = VaultValidator.Validate(two).ConvertAll(i => i.Code);
            Assert.That(codes, Has.Member(VaultIssueCode.TooManySirWhiskers));
            Assert.AreEqual(10, VaultRules.RequiredLevel(ObjectType.SirWhiskers));
            Assert.AreEqual(60, VaultRules.Cost(ObjectType.SirWhiskers));
            Assert.IsFalse(ObjectTypes.IsUpgradable(ObjectType.SirWhiskers), "his upgrade path comes later");
        }

        [Test]
        public void TheSwat_PushesAwayFromTheCat()
        {
            Assert.AreEqual(Dir.E, RaidSim.DirToward(new TilePos(2, 1), new TilePos(3, 1), Dir.N));
            Assert.AreEqual(Dir.N, RaidSim.DirToward(new TilePos(2, 2), new TilePos(2, 1), Dir.E));
            Assert.AreEqual(Dir.W, RaidSim.DirToward(new TilePos(2, 1), new TilePos(2, 1), Dir.W), "same tile: his facing");
        }
    }
}
