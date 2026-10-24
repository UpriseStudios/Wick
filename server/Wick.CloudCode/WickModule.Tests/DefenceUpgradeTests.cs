using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Wick.Economy;
using Wick.Sim;

namespace Wick.CloudCode.Tests
{
    /// <summary>
    /// M10 defence upgrades: bought with gold and wax, capped at half the Vault Level, stamped on
    /// published vaults by the server, and picked up by the live vault when they finish if its
    /// stored clear-check still escapes (decision C in docs/m10-plan.md).
    /// </summary>
    public class DefenceUpgradeTests
    {
        private World _w;
        private EconomyService Economy => new EconomyService(_w.Store, _w.Clock, new FixedEconomy(TestEconomy.Default));
        private static readonly string Drip = UpgradeKinds.Defence(ObjectType.DripPipe);
        private static readonly string Bell = UpgradeKinds.Defence(ObjectType.SnufferBell);

        [SetUp]
        public void SetUp()
        {
            _w = new World();
        }

        private Task Player(string id, int vaultLevel, Dictionary<string, int> defences = null)
        {
            return Task.WhenAll(
                _w.Set(id, Keys.Wallet, new Wallet { Gold = 1_000_000, Wax = 1_000_000, Gems = 1_000_000 }),
                _w.Set(id, EconomyKeys.Upgrades, new Upgrades
                {
                    Levels = new Progression { VaultLevel = vaultLevel, Defences = defences ?? new Dictionary<string, int>() },
                }));
        }

        /// <summary>Three Halls at a Vault Level, with its treasure revalued and extra objects.</summary>
        private static VaultDef Halls(int level, DefenceLevels levels, params ObjectDef[] extra)
        {
            VaultDef halls = TestVaults.Halls();
            var tiles = new TileType[halls.Width * halls.Height];
            for (int i = 0; i < tiles.Length; i++)
            {
                tiles[i] = halls.TileAtIndex(i);
            }

            var objects = new List<ObjectDef>();
            ObjectDef pile = halls.GetObject(0);
            objects.Add(new ObjectDef(pile.Id, pile.Type, pile.Pos, pile.Rot, 0, 0, VaultRules.TreasureValue(level), pile.Patrol));
            objects.AddRange(extra);
            return new VaultDef(halls.Schema, halls.VaultId, halls.Version, level, halls.Width, halls.Height, tiles, halls.Entrance,
                objects.ToArray(), halls.HasHoard, halls.Hoard, halls.Theme, levels);
        }

        private Task<PublishResult> Publish(string player, VaultDef vault)
        {
            return new PublishService(_w.Store, _w.Clock).PublishAsync(player, _w.NextRequest(), TestVaults.PublishDocument(vault));
        }

        private VaultDef Live(string player)
        {
            return VaultJson.Parse(_w.Get<LiveVault>(player, Keys.VaultLive).Document);
        }

        // ---- Buying ----------------------------------------------------------------------------

        [Test]
        public async Task ADefenceUpgrade_CostsGoldAndWax_AndRaisesTheLevel()
        {
            await Player("p1", 3);
            Assert.IsTrue((await Economy.StartUpgradeAsync("p1", Drip)).Ok);
            Wallet after = _w.Get<Wallet>("p1", Keys.Wallet);
            Assert.AreEqual(1_000_000 - 300, after.Gold);
            Assert.AreEqual(1_000_000 - 10, after.Wax);

            Assert.IsTrue((await Economy.FinishUpgradeAsync("p1", Drip, true)).Ok, "gems finish it early");
            EconomyState state = await Economy.GetStateAsync("p1");
            Assert.AreEqual(2, state.Defences["drip_pipe"]);
        }

        [Test]
        public async Task TheCap_IsHalfTheVaultLevel_SoMoneyCantBuyAhead()
        {
            await Player("p1", 2); // cap 1
            EconomyResult refused = await Economy.StartUpgradeAsync("p1", Drip);
            Assert.IsFalse(refused.Ok);
            StringAssert.Contains("needs Vault Level 3", refused.Reason);

            await Player("p2", 3, new Dictionary<string, int> { ["drip_pipe"] = 2 }); // cap 2
            Assert.IsFalse((await Economy.StartUpgradeAsync("p2", Drip)).Ok, "level 3 needs Vault Level 5");
        }

        [Test]
        public async Task ADefenceUnlocksWithItsTrap()
        {
            await Player("p1", 1);
            EconomyResult refused = await Economy.StartUpgradeAsync("p1", UpgradeKinds.Defence(ObjectType.WaxMoths));
            Assert.IsFalse(refused.Ok);
            StringAssert.Contains("unlocks at Vault Level 3", refused.Reason);
            Assert.IsFalse((await Economy.StartUpgradeAsync("p1", "defence:treasure")).Ok, "treasure has no levels");
        }

        // ---- Publishing --------------------------------------------------------------------------

        [Test]
        public async Task PublishedLevels_MustMatchTheOwnersUpgrades()
        {
            await Player("p1", 3, new Dictionary<string, int> { ["drip_pipe"] = 2 });
            PublishResult stale = await Publish("p1", Halls(3, DefenceLevels.Base));
            Assert.IsFalse(stale.Ok);
            StringAssert.Contains("defence levels changed", stale.Reason);

            PublishResult ok = await Publish("p1", Halls(3, DefenceLevels.Base.With(ObjectType.DripPipe, 2)));
            Assert.IsTrue(ok.Ok, ok.Reason);
            Assert.AreEqual(2, Live("p1").LevelOf(ObjectType.DripPipe), "the stamped vault keeps its levels");
        }

        [Test]
        public async Task AVaultBuiltBelowTheOwnersLevel_GetsLevelsCappedForIt()
        {
            await Player("p1", 5, new Dictionary<string, int> { ["drip_pipe"] = 3 });
            PublishResult ok = await Publish("p1", Halls(3, DefenceLevels.Base.With(ObjectType.DripPipe, 2)));
            Assert.IsTrue(ok.Ok, ok.Reason);
        }

        // ---- Decision C: the live vault after an upgrade -------------------------------------------

        [Test]
        public async Task AFinishedUpgrade_ReachesTheLiveVault_WhenItsClearCheckStillEscapes()
        {
            // A drip off the escape route: a faster drip changes nothing about the run.
            ObjectDef drip = new ObjectDef(9, ObjectType.DripPipe, new TilePos(8, 6), 0, 45, 0, 0, new TilePos[0]);
            await Player("p1", 3);
            Assert.IsTrue((await Publish("p1", Halls(3, DefenceLevels.Base, drip))).Ok);
            int before = _w.Get<LiveVault>("p1", Keys.VaultLive).Version;

            Assert.IsTrue((await Economy.StartUpgradeAsync("p1", Drip)).Ok);
            Assert.IsTrue((await Economy.FinishUpgradeAsync("p1", Drip, true)).Ok);

            LiveVault live = _w.Get<LiveVault>("p1", Keys.VaultLive);
            Assert.AreEqual(2, Live("p1").LevelOf(ObjectType.DripPipe));
            Assert.AreEqual(before + 1, live.Version);
            Assert.IsFalse(live.NeedsRetest);
            JsonNode root = JsonReader.Parse(live.Document);
            Assert.IsTrue(ClearCheck.Verify(VaultJson.FromNode(root), VaultJson.ReadClearCheck(root)).Ok,
                "the re-recorded clear-check verifies on the upgraded vault");
        }

        [Test]
        public async Task AnUpgradeThatBreaksTheClearCheck_WaitsForARetest()
        {
            // A Snuffer Bell beside the route facing it: at level 5 its plate covers the route.
            ObjectDef bell = new ObjectDef(9, ObjectType.SnufferBell, new TilePos(5, 7), 3, 0, 0, 0, new TilePos[0]);
            await Player("p1", 9, new Dictionary<string, int> { ["snuffer_bell"] = 4 });
            PublishResult published = await Publish("p1", Halls(9, DefenceLevels.Base.With(ObjectType.SnufferBell, 4), bell));
            Assert.IsTrue(published.Ok, published.Reason);

            Assert.IsTrue((await Economy.StartUpgradeAsync("p1", Bell)).Ok);
            Assert.IsTrue((await Economy.FinishUpgradeAsync("p1", Bell, true)).Ok);

            Assert.AreEqual(4, Live("p1").LevelOf(ObjectType.SnufferBell), "the live vault keeps its old level");
            Assert.IsTrue(_w.Get<LiveVault>("p1", Keys.VaultLive).NeedsRetest);
            Assert.IsTrue((await Economy.GetStateAsync("p1")).VaultNeedsRetest, "the game asks the owner to test again");
        }

        [Test]
        public async Task AnUpgradeThatEndsWhileAway_IsPickedUpOnTheNextEconomyRead()
        {
            ObjectDef drip = new ObjectDef(9, ObjectType.DripPipe, new TilePos(8, 6), 0, 45, 0, 0, new TilePos[0]);
            await Player("p1", 3);
            Assert.IsTrue((await Publish("p1", Halls(3, DefenceLevels.Base, drip))).Ok);
            Assert.IsTrue((await Economy.StartUpgradeAsync("p1", Drip)).Ok);

            _w.Clock.Advance(21 * Rules.MinuteMs);
            EconomyState state = await Economy.GetStateAsync("p1");
            Assert.AreEqual(2, state.Defences["drip_pipe"]);
            Assert.AreEqual(2, Live("p1").LevelOf(ObjectType.DripPipe));
        }
    }
}
