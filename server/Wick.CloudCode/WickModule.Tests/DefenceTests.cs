using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Wick.Sim;

namespace Wick.CloudCode.Tests
{
    using static TestEconomy;

    public class DefenceTests
    {
        private World _w;

        [SetUp]
        public async Task SetUp()
        {
            _w = new World();
            await _w.Publish("def");
            await _w.Set("def", Keys.Wallet, new Wallet { Gold = 1000 });
            await _w.Set("def", Keys.Profile, new Profile { Crowns = 500 });
            await _w.Set("raider", Keys.Profile, new Profile { Crowns = 500 });
        }

        [Test]
        public async Task FailedRaid_PaysTheDefenceBonus_AndMovesCrownsToTheDefender()
        {
            SubmitResult r = await _w.RaidVault("raider", "v_def", InputLog.Empty);

            Assert.IsTrue(r.Verified, r.Reason);
            Assert.AreEqual(-14, r.CrownsDelta);
            Assert.AreEqual(1000 + Rules.DefenceBonus(Loot, 1), _w.Get<Wallet>("def", Keys.Wallet).Gold);
            Assert.AreEqual(514, _w.Get<Profile>("def", Keys.Profile).Crowns);
            DefenceState d = _w.Get<DefenceState>("def", Keys.Defence);
            Assert.AreEqual(1, d.Raids);
            Assert.AreEqual(0, d.ShieldUntilMs, "no shield after a failed raid");
            Assert.AreEqual(Rules.DefenceBonus(Loot, 1), d.Log[0].Bonus);
            Assert.AreEqual(14, d.Log[0].CrownsDelta);
        }

        [Test]
        public async Task SuccessfulRaid_CostsTheDefenderTheLoot_AndShieldsTheVault()
        {
            long before = _w.Clock.NowMs;
            SubmitResult r = await _w.RaidVault("raider", "v_def", TestVaults.Escape);

            Assert.IsTrue(r.Verified, r.Reason);
            Assert.AreEqual(100, r.Paid);
            Assert.AreEqual(24, r.CrownsDelta);
            Assert.AreEqual(900, _w.Get<Wallet>("def", Keys.Wallet).Gold);
            Assert.AreEqual(476, _w.Get<Profile>("def", Keys.Profile).Crowns);
            DefenceState d = _w.Get<DefenceState>("def", Keys.Defence);
            Assert.Greater(d.ShieldUntilMs, before + Rules.ShieldMs(Loot) - 1);
            Assert.AreEqual(100, d.Log[0].GoldLost);
            Assert.AreEqual("v_raider", d.Log[0].RevengeVaultId);
        }

        [Test]
        public async Task TheDefenderNeverLosesMoreThanAFifthOfTheirGold()
        {
            await _w.Set("def", Keys.Wallet, new Wallet { Gold = 100 });
            SubmitResult r = await _w.RaidVault("raider", "v_def", TestVaults.Escape);

            Assert.AreEqual(100, r.Paid, "the raider is still paid in full");
            Assert.AreEqual(80, _w.Get<Wallet>("def", Keys.Wallet).Gold, "lost 20%, not 100");
        }

        [Test]
        public async Task LootIsCappedByTheDefendersVaultLevel()
        {
            // Three 100-gold piles on the run up the middle: 300 loot against a level 1 cap of 200.
            VaultDef rich = TestVaults.WithObjects(TestVaults.Halls(),
                new ObjectDef(30, ObjectType.Treasure, new TilePos(4, 1), 0, 0, 0, 100, new TilePos[0]),
                new ObjectDef(31, ObjectType.Treasure, new TilePos(4, 3), 0, 0, 0, 100, new TilePos[0]));
            Assume.That(RaidRunner.RunToEnd(rich, TestVaults.Seed, TestVaults.Escape).Loot, Is.EqualTo(300));
            await _w.Publish("rich", rich);

            SubmitResult r = await _w.RaidVault("raider", "v_rich", TestVaults.Escape);

            Assert.AreEqual(300, r.Loot);
            Assert.AreEqual(Rules.LootCap(Loot, 1), r.Paid);
        }

        [Test]
        public async Task ARetriedSubmit_AppliesTheDefenceOnce()
        {
            RaidStartResult start = await _w.Raids.StartFriendRaidAsync("raider", "v_def", false);
            await _w.Raid("raider", start, InputLog.Empty, "req-same-request");
            RaidResult run = RaidRunner.RunToEnd(VaultJson.Parse(start.Vault), start.Seed, InputLog.Empty);
            await _w.Raids.SubmitRaidAsync("raider", "req-same-request", start.Token, InputLog.Empty.ToBase64(),
                new RaidClaim { Stars = run.Stars, Loot = run.Loot, Hash = run.HashHex });

            DefenceState d = _w.Get<DefenceState>("def", Keys.Defence);
            Assert.AreEqual(1, d.Raids);
            Assert.AreEqual(1, d.Log.Count);
            Assert.AreEqual(1000 + Rules.DefenceBonus(Loot, 1), _w.Get<Wallet>("def", Keys.Wallet).Gold);
        }

        [Test]
        public async Task SeedVaultDefenders_HaveNoSide()
        {
            await _w.Seed("crypt", 500);
            SubmitResult r = await _w.RaidVault("raider", "v_bot_crypt", TestVaults.Escape);
            Assert.IsTrue(r.Verified);
            Assert.AreEqual(24, r.CrownsDelta);
            Assert.IsNull(_w.Store.Read("bot_crypt", Keys.Defence));
        }

        [Test]
        public void TheDeadliestTrap_IsTheOneThatSnuffedMost()
        {
            var counts = new Dictionary<string, int> { { "DripPipe", 2 }, { "NightOwl", 5 }, { "Bellows", 5 } };
            Assert.AreEqual("Bellows", DefenceService.Deadliest(counts), "ties go to the alphabetically first");
        }

        [Test]
        public void TheAnalysis_NamesWhatSnuffedWick()
        {
            // Level 2 Three Halls with a Snuffer Bell in the middle of the run.
            VaultDef halls = TestVaults.Halls();
            VaultDef belled = TestVaults.WithObjects(new VaultDef(halls.Schema, halls.VaultId, halls.Version, 2, halls.Width,
                    halls.Height, Tiles(halls), halls.Entrance, new[] { Pile(halls) }, halls.HasHoard, halls.Hoard),
                new ObjectDef(40, ObjectType.SnufferBell, new TilePos(4, 7), 0, 0, 0, 0, new TilePos[0]));

            RaidAnalysis a = RaidAnalysis.Run(belled, TestVaults.Seed, TestVaults.Escape);

            Assert.AreEqual(Outcome.Snuffed, a.Result.Outcome);
            Assert.AreEqual("SnufferBell", a.SnuffedBy);
            Assert.AreEqual(RaidRunner.RunToEnd(belled, TestVaults.Seed, TestVaults.Escape).HashHex, a.Result.HashHex,
                "same sim, same hash as RaidRunner");
        }

        private static TileType[] Tiles(VaultDef v)
        {
            var tiles = new TileType[v.Width * v.Height];
            for (int i = 0; i < tiles.Length; i++)
            {
                tiles[i] = v.TileAtIndex(i);
            }

            return tiles;
        }

        /// <summary>The template's treasure pile, revalued for Vault Level 2.</summary>
        private static ObjectDef Pile(VaultDef v)
        {
            ObjectDef p = v.GetObject(0);
            return new ObjectDef(p.Id, p.Type, p.Pos, p.Rot, p.Cycle, p.Offset, VaultRules.TreasureValue(2), p.Patrol);
        }

        [Test]
        public async Task SnuffsCountTowardsTheDeadliestTrap()
        {
            await _w.Defence.ApplyAsync(new DefenceEvent
            {
                RaidId = "r_x1", RaiderId = "raider", OwnerId = "def", Outcome = "Snuffed", SnuffedBy = "DripPipe",
                DefenderLevel = 1, AtMs = _w.Clock.NowMs,
            });
            DefenceLogResult log = await _w.Defence.GetDefenceLogAsync("def");
            Assert.AreEqual(1, log.Snuffs);
            Assert.AreEqual("DripPipe", log.DeadliestTrap);
        }

        // ---- Replays and revenge ---------------------------------------------------------

        [Test]
        public async Task ReplaysAreForTheDefenderAndTheRaider_AndReplayToTheVerifiedHash()
        {
            RaidStartResult start = await _w.Raids.StartFriendRaidAsync("raider", "v_def", false);
            SubmitResult r = await _w.Raid("raider", start, TestVaults.Escape);
            string raidId = _w.Get<DefenceState>("def", Keys.Defence).Log[0].RaidId;

            ReplayResult forDefender = await _w.Defence.GetReplayAsync("def", raidId);
            ReplayResult forRaider = await _w.Defence.GetReplayAsync("raider", raidId);
            ReplayResult forStranger = await _w.Defence.GetReplayAsync("stranger", raidId);

            Assert.IsTrue(forDefender.Ok, forDefender.Reason);
            Assert.IsTrue(forRaider.Ok);
            Assert.IsFalse(forStranger.Ok);
            RaidResult replay = RaidRunner.RunToEnd(VaultJson.Parse(forDefender.Vault), forDefender.Seed, InputLog.FromBase64(forDefender.Log));
            Assert.AreEqual(r.Hash, replay.HashHex);
            Assert.AreEqual(r.Hash, forDefender.Hash);
        }

        [Test]
        public async Task Revenge_RaidsTheRaidersVault()
        {
            await _w.Publish("raider");
            await _w.RaidVault("raider", "v_def", InputLog.Empty);
            string revengeOn = _w.Get<DefenceState>("def", Keys.Defence).Log[0].RevengeVaultId;

            RaidStartResult r = await _w.Raids.StartFriendRaidAsync("def", revengeOn, false, RaidService.KindRevenge);

            Assert.IsTrue(r.Ok, r.Reason);
            Assert.AreEqual("v_raider", r.VaultId);
            Assert.AreEqual(RaidService.KindRevenge, r.Kind);
        }
    }
}
