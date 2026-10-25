using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Wick.Economy;
using Wick.Sim;

namespace Wick.CloudCode.Tests
{
    public class EconomyTests
    {
        private World _w;
        private EconomyService Economy => new EconomyService(_w.Store, _w.Clock, new FixedEconomy(TestEconomy.Default));

        [SetUp]
        public void SetUp()
        {
            _w = new World();
            _w.Clock.NowMs = new System.DateTimeOffset(2026, 10, 20, 12, 0, 0, System.TimeSpan.Zero).ToUnixTimeMilliseconds();
        }

        private Task Rich(string player, int stars = 0)
        {
            return Task.WhenAll(
                _w.Set(player, Keys.Wallet, new Wallet { Gold = 1_000_000, Wax = 1_000_000, Gems = 1_000_000 }),
                _w.Set(player, Keys.Stats, new RaidStats { Stars = stars }));
        }

        [Test]
        public void TheDefaultConfig_Loads_AndOtherVersionsAreRefused()
        {
            EconomyConfig c = TestEconomy.Default;
            Assert.AreEqual(9, c.VaultLevels.Count);
            Assert.AreEqual(40, c.Pass.Rewards.Count);
            Assert.AreEqual(4, c.Ads.Placements.Count);
            Assert.Throws<System.FormatException>(() => EconomyConfig.Parse("{\"version\": 2}"));
        }

        // ---- Star gates (acceptance) -------------------------------------------------------

        /// <summary>No amount of gold or gems raises the Vault Level without the stars from raids.</summary>
        [Test]
        public async Task VaultLevel_NeedsRaidStars_WhateverThePlayerSpends()
        {
            await Rich("p1", stars: 4);

            EconomyResult start = await Economy.StartUpgradeAsync("p1", UpgradeKinds.VaultLevel);
            Assert.IsFalse(start.Ok);
            StringAssert.Contains("Needs 5 stars", start.Reason);
            Assert.IsFalse((await Economy.FinishUpgradeAsync("p1", UpgradeKinds.VaultLevel, true)).Ok, "gems can't finish what can't start");
            Assert.AreEqual(1, (await Economy.GetStateAsync("p1")).VaultLevel);
            Assert.AreEqual(1_000_000, _w.Get<Wallet>("p1", Keys.Wallet).Gold, "nothing spent on a refusal");

            await _w.Set("p1", Keys.Stats, new RaidStats { Stars = 5 });
            Assert.IsTrue((await Economy.StartUpgradeAsync("p1", UpgradeKinds.VaultLevel)).Ok);
            Assert.IsTrue((await Economy.FinishUpgradeAsync("p1", UpgradeKinds.VaultLevel, true)).Ok);
            Assert.AreEqual(2, (await Economy.GetStateAsync("p1")).VaultLevel);

            EconomyResult next = await Economy.StartUpgradeAsync("p1", UpgradeKinds.VaultLevel);
            Assert.IsFalse(next.Ok, "level 3 needs 15 stars");
            StringAssert.Contains("15 stars", next.Reason);
        }

        [Test]
        public async Task StarsOnlyComeFromVerifiedRaids()
        {
            await _w.Publish("owner");
            for (int i = 0; i < 2; i++)
            {
                await _w.RaidVault("p1", "v_owner", TestVaults.Escape); // 3 stars each
                _w.Clock.Advance(Rules.ShieldMs(TestEconomy.Loot) + 1);
            }

            Assert.AreEqual(6, _w.Get<RaidStats>("p1", Keys.Stats).Stars);
            await _w.Set("p1", Keys.Wallet, new Wallet { Gold = 10_000 });
            Assert.IsTrue((await Economy.StartUpgradeAsync("p1", UpgradeKinds.VaultLevel)).Ok);
        }

        [Test]
        public async Task UpgradesRunOnServerTime()
        {
            await Rich("p1", stars: 5);
            await Economy.StartUpgradeAsync("p1", UpgradeKinds.VaultLevel); // 2 minutes
            EconomyResult early = await Economy.FinishUpgradeAsync("p1", UpgradeKinds.VaultLevel, false);
            Assert.IsFalse(early.Ok);
            StringAssert.Contains("2 minutes left", early.Reason);

            _w.Clock.Advance(2 * Rules.MinuteMs);
            Assert.IsTrue((await Economy.FinishUpgradeAsync("p1", UpgradeKinds.VaultLevel, false)).Ok);
            Assert.AreEqual(1_000_000 - 400, _w.Get<Wallet>("p1", Keys.Wallet).Gold);
            Assert.AreEqual(1_000_000, _w.Get<Wallet>("p1", Keys.Wallet).Gems, "no gems when it finished on time");
        }

        [Test]
        public async Task AnUpgradeCompletesOnItsOwn_WhenItsTimeIsUp()
        {
            await Rich("p1", stars: 5);
            await Economy.StartUpgradeAsync("p1", UpgradeKinds.VaultLevel); // 2 minutes
            Assert.AreEqual(1, (await Economy.GetStateAsync("p1")).VaultLevel);

            _w.Clock.Advance(2 * Rules.MinuteMs);
            EconomyState state = await Economy.GetStateAsync("p1");
            Assert.AreEqual(2, state.VaultLevel, "nobody pressed Finish");
            Assert.AreEqual(0, state.Running.Count, "the workshop is free again");
            Assert.AreEqual(2, await Economy.VaultLevelAsync("p1"));
        }

        [Test]
        public async Task AFinishedUpgradeIsStoredByTheNextWrite_AndItsWorkshopReused()
        {
            await Rich("p1", stars: 15);
            await Economy.StartUpgradeAsync("p1", UpgradeKinds.VaultLevel);
            _w.Clock.Advance(2 * Rules.MinuteMs);

            EconomyResult next = await Economy.StartUpgradeAsync("p1", UpgradeKinds.VaultLevel); // Vault Level 3, same workshop
            Assert.IsTrue(next.Ok, next.Reason);
            Upgrades stored = _w.Get<Upgrades>("p1", EconomyKeys.Upgrades);
            Assert.AreEqual(2, stored.Levels.VaultLevel);
            Assert.AreEqual(3, stored.Running[0].ToLevel);
        }

        [Test]
        public async Task FinishingEarly_CostsGems_ByTimeLeft()
        {
            await Rich("p1", stars: 15);
            await _w.Set("p1", EconomyKeys.Upgrades, new Upgrades { Levels = new Progression { VaultLevel = 2 } });
            await Economy.StartUpgradeAsync("p1", UpgradeKinds.VaultLevel); // 15 minutes: 20 gems an hour, rounded up
            await Economy.FinishUpgradeAsync("p1", UpgradeKinds.VaultLevel, true);
            Assert.AreEqual(1_000_000 - 5, _w.Get<Wallet>("p1", Keys.Wallet).Gems);
        }

        [Test]
        public async Task OneUpgradePerWorkshop_AndTheStarterPackAddsOne()
        {
            await Rich("p1", stars: 5);
            await _w.Set("p1", EconomyKeys.Upgrades, new Upgrades { Levels = new Progression { VaultLevel = 2 } });
            await _w.Set("p1", Keys.Stats, new RaidStats { Stars = 15 });
            Assert.IsTrue((await Economy.StartUpgradeAsync("p1", UpgradeKinds.VaultLevel)).Ok);
            EconomyResult second = await Economy.StartUpgradeAsync("p1", UpgradeKinds.Lantern);
            Assert.IsFalse(second.Ok);
            StringAssert.Contains("workshops are busy", second.Reason);

            await _w.Set("p1", EconomyKeys.Inventory, new Inventory { Workshops = 1 });
            Assert.IsTrue((await Economy.StartUpgradeAsync("p1", UpgradeKinds.Lantern)).Ok);
        }

        [Test]
        public async Task TwoStartsAtOnce_SpendOnce()
        {
            await _w.Set("p1", Keys.Wallet, new Wallet { Gold = 400 });
            await _w.Set("p1", Keys.Stats, new RaidStats { Stars = 5 });
            EconomyResult[] r = await Task.WhenAll(
                Task.Run(() => Economy.StartUpgradeAsync("p1", UpgradeKinds.VaultLevel)),
                Task.Run(() => Economy.StartUpgradeAsync("p1", UpgradeKinds.VaultLevel)));

            Assert.AreEqual(1, r.Count(x => x.Ok));
            Assert.AreEqual(0, _w.Get<Wallet>("p1", Keys.Wallet).Gold);
        }

        // ---- Lantern (the one upgrade that will touch raids) ----------------------------------

        [Test]
        public async Task TheLantern_IsCappedByVaultLevel_SoPayingOnlyGetsThereSooner()
        {
            await Rich("p1");
            EconomyResult r = await Economy.StartUpgradeAsync("p1", UpgradeKinds.Lantern);
            Assert.IsFalse(r.Ok);
            StringAssert.Contains("Vault Level 2", r.Reason);

            foreach (LanternStep step in TestEconomy.Default.Lantern)
            {
                Assert.LessOrEqual(step.LightBonus, 1000, "no Lantern level adds more than a tile of light");
                Assert.GreaterOrEqual(step.MinVaultLevel, 2 * step.Level, "each level waits for a higher Vault Level");
            }
        }

        // ---- Publishing follows Vault Level ------------------------------------------------------

        [Test]
        public async Task APlayerPublishesUpToTheirVaultLevel()
        {
            VaultDef halls = TestVaults.Halls();
            VaultDef level2 = TestVaults.Padded(new VaultDef(halls.Schema, halls.VaultId, halls.Version, 2, halls.Width, halls.Height,
                Tiles(halls), halls.Entrance, new[] { Revalued(halls.GetObject(0), 2) }, halls.HasHoard, halls.Hoard));

            PublishResult refused = await new PublishService(_w.Store, _w.Clock).PublishAsync("p1", "req-pub-0001", TestVaults.PublishDocument(level2));
            Assert.IsFalse(refused.Ok);

            await _w.Set("p1", EconomyKeys.Upgrades, new Upgrades { Levels = new Progression { VaultLevel = 2 } });
            PublishResult ok = await new PublishService(_w.Store, _w.Clock).PublishAsync("p1", "req-pub-0002", TestVaults.PublishDocument(level2));
            Assert.IsTrue(ok.Ok, ok.Reason);
        }

        [Test]
        public async Task PublishingUsesAVaultLevelWhoseBuildHasEnded()
        {
            VaultDef halls = TestVaults.Halls();
            VaultDef level2 = TestVaults.Padded(new VaultDef(halls.Schema, halls.VaultId, halls.Version, 2, halls.Width, halls.Height,
                Tiles(halls), halls.Entrance, new[] { Revalued(halls.GetObject(0), 2) }, halls.HasHoard, halls.Hoard));
            long now = _w.Clock.NowMs;
            await _w.Set("p1", EconomyKeys.Upgrades, new Upgrades
            {
                Running = { new RunningUpgrade { Kind = UpgradeKinds.VaultLevel, ToLevel = 2, StartedMs = now - 3 * Rules.MinuteMs, EndsMs = now - 1 } },
            });

            PublishResult ok = await new PublishService(_w.Store, _w.Clock).PublishAsync("p1", "req-pub-0003", TestVaults.PublishDocument(level2));
            Assert.IsTrue(ok.Ok, ok.Reason);
        }

        // ---- Raid economics -------------------------------------------------------------------

        [Test]
        public async Task Raids_PayWaxPerStar_AndFailedRaidsAConsolation()
        {
            await _w.Publish("owner");
            SubmitResult won = await _w.RaidVault("p1", "v_owner", TestVaults.Escape);
            Assert.AreEqual(3 * TestEconomy.Loot.WaxPerStar, won.WaxEarned);

            await _w.Publish("owner2");
            SubmitResult lost = await _w.RaidVault("p2", "v_owner2", InputLog.Empty);
            Assert.AreEqual(Rules.Consolation(TestEconomy.Loot, 1), lost.Paid);
            Assert.AreEqual(Rules.Consolation(TestEconomy.Loot, 1), _w.Get<Wallet>("p2", Keys.Wallet).Gold);
        }

        // ---- Heist Pass ----------------------------------------------------------------------

        [Test]
        public async Task PassXp_FromRaids_WithOneDailyFirstWin()
        {
            await _w.Publish("owner");
            await _w.Publish("owner2");
            SubmitResult first = await _w.RaidVault("p1", "v_owner", TestVaults.Escape);
            SubmitResult second = await _w.RaidVault("p1", "v_owner2", TestVaults.Escape);
            PassXp xp = TestEconomy.Default.Pass.Xp;
            Assert.AreEqual(xp.Raid + xp.ThreeStarBonus + xp.DailyFirstWin, first.PassXp);
            Assert.AreEqual(xp.Raid + xp.ThreeStarBonus, second.PassXp, "the first-win bonus is once a day");
        }

        [Test]
        public async Task PremiumTiers_UnlockRetroactively_WhenThePassIsBought()
        {
            await _w.Set("p1", EconomyKeys.Pass, new PassState { Season = "s1", Xp = 3_500 }); // tier 3
            Assert.IsTrue((await Economy.ClaimPassTierAsync("p1", 3, false)).Ok);
            Assert.IsFalse((await Economy.ClaimPassTierAsync("p1", 3, false)).Ok, "once");
            Assert.IsFalse((await Economy.ClaimPassTierAsync("p1", 4, false)).Ok, "not reached");
            Assert.IsFalse((await Economy.ClaimPassTierAsync("p1", 1, true)).Ok, "premium row without the pass");

            PurchaseResult bought = await Purchases.ValidateAsync("p1", "req-buy-0001", "heist_pass_s1", FakeReceipt("t1"));
            Assert.IsTrue(bought.Ok, bought.Reason);
            for (int tier = 1; tier <= 3; tier++)
            {
                Assert.IsTrue((await Economy.ClaimPassTierAsync("p1", tier, true)).Ok, "tier " + tier + " premium, bought late");
            }
        }

        [Test]
        public async Task ANewSeason_StartsXpAgain()
        {
            await _w.Set("p1", EconomyKeys.Pass, new PassState { Season = "s0", Xp = 9_000, ClaimedFree = { 3 } });
            EconomyState s = await Economy.GetStateAsync("p1");
            Assert.AreEqual("s1", s.PassSeason);
            Assert.AreEqual(0, s.PassXp);
        }

        // ---- Achievements ------------------------------------------------------------------------

        [Test]
        public async Task Achievements_TrackOnTheServer_AndClaimTierByTier()
        {
            await _w.Set("p1", EconomyKeys.Progress, new Progress { Raids = 49 });
            Assert.IsFalse((await Economy.ClaimAchievementAsync("p1", "raider")).Ok, "49 of 50");

            await _w.Publish("owner");
            await _w.RaidVault("p1", "v_owner", InputLog.Empty);
            EconomyResult bronze = await Economy.ClaimAchievementAsync("p1", "raider");
            Assert.IsTrue(bronze.Ok, bronze.Reason);
            StringAssert.Contains("bronze", bronze.Reason);
            Assert.IsFalse((await Economy.ClaimAchievementAsync("p1", "raider")).Ok, "silver needs 200");
        }

        [Test]
        public async Task DefenceSnuffs_CountForTheOwner()
        {
            await _w.Defence.ApplyAsync(new DefenceEvent
            {
                RaidId = "r_s1", RaiderId = "x", OwnerId = "owner", Outcome = "Snuffed", DefenderLevel = 1, AtMs = _w.Clock.NowMs,
            });
            Assert.AreEqual(1, _w.Get<Progress>("owner", EconomyKeys.Progress).Snuffs);
            Assert.AreEqual(TestEconomy.Default.Pass.Xp.Defence, _w.Get<PassState>("owner", EconomyKeys.Pass).Xp);
        }

        [Test]
        public async Task LinkOpens_CountEachFriendOnce()
        {
            PublishResult p = await new PublishService(_w.Store, _w.Clock).PublishAsync("owner", "req-pub-0009",
                TestVaults.PublishDocument(TestVaults.Halls()));
            await _w.Raids.StartFriendRaidAsync("f1", p.Code, false);
            await _w.Raids.StartFriendRaidAsync("f1", p.Code, false);
            await _w.Raids.StartFriendRaidAsync("f2", p.Code, false);
            await _w.Raids.StartFriendRaidAsync("f3", "v_owner", false); // by id, not a link
            Assert.AreEqual(2, _w.Get<Progress>("owner", EconomyKeys.Progress).LinkOpeners.Count);
        }

        // ---- Helpers --------------------------------------------------------------------------

        private PurchaseService Purchases => new PurchaseService(_w.Store, new FixedEconomy(TestEconomy.Default), new FakeStoreVerifier());

        public static string FakeReceipt(string transaction)
        {
            return "{\"Store\":\"fake\",\"TransactionID\":\"" + transaction + "\",\"Payload\":\"ThisIsFakeReceiptData\"}";
        }

        private static TileType[] Tiles(VaultDef v)
        {
            var t = new TileType[v.Width * v.Height];
            for (int i = 0; i < t.Length; i++)
            {
                t[i] = v.TileAtIndex(i);
            }

            return t;
        }

        private static ObjectDef Revalued(ObjectDef p, int level)
        {
            return new ObjectDef(p.Id, p.Type, p.Pos, p.Rot, p.Cycle, p.Offset, VaultRules.TreasureValue(level), p.Patrol);
        }
    }
}
