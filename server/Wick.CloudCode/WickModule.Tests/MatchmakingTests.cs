using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using Wick.Sim;

namespace Wick.CloudCode.Tests
{
    using static TestEconomy;

    public class MatchmakingTests
    {
        private World _w;

        [SetUp]
        public void SetUp()
        {
            _w = new World();
        }

        [Test]
        public async Task WithOnlySeedVaults_ANewPlayerGetsTheNearestBot()
        {
            await _w.Seed("crypt", 50);
            await _w.Seed("tower", 700);

            RaidStartResult r = await _w.Raids.GetRaidTargetAsync("p1");

            Assert.IsTrue(r.Ok, r.Reason);
            Assert.AreEqual("v_bot_crypt", r.VaultId);
            Assert.AreEqual(RaidService.KindMatch, r.Kind);
            StringAssert.DoesNotContain("clearCheck", r.Vault);
        }

        [Test]
        public async Task TheBandWidens_UntilSomethingFits()
        {
            await _w.Seed("far", 0);
            await _w.Set("p1", Keys.Profile, new Profile { Crowns = 1500 });

            RaidStartResult r = await _w.Raids.GetRaidTargetAsync("p1");

            Assert.IsTrue(r.Ok, r.Reason);
            Assert.AreEqual("v_bot_far", r.VaultId, "1500 Crowns away: found once the band reaches 1600");
        }

        [Test]
        public async Task Players_ComeBeforeBots_InTheSameBand()
        {
            await _w.Seed("crypt", 0);
            await _w.Publish("p2");

            RaidStartResult r = await _w.Raids.GetRaidTargetAsync("p1");

            Assert.AreEqual("v_p2", r.VaultId);
        }

        [Test]
        public async Task NeverYourOwnVault()
        {
            await _w.Publish("p1");
            RaidStartResult r = await _w.Raids.GetRaidTargetAsync("p1");
            Assert.IsFalse(r.Ok, "the only live vault is p1's own");
        }

        [Test]
        public async Task ARobbedVault_IsShielded_AndSkippedForADay_ByItsRaider()
        {
            await _w.Publish("p2");
            await _w.Set("p2", Keys.Wallet, new Wallet { Gold = 1000 });
            SubmitResult robbed = await _w.Raid("p1", await _w.Raids.GetRaidTargetAsync("p1"), TestVaults.Escape);
            Assert.IsTrue(robbed.Verified, robbed.Reason);

            Assert.IsFalse((await _w.Raids.GetRaidTargetAsync("p3")).Ok, "shielded for everyone");
            RaidStartResult friend = await _w.Raids.StartFriendRaidAsync("p3", "v_p2", false);
            Assert.IsFalse(friend.Ok);
            StringAssert.Contains("shielded", friend.Reason);

            _w.Clock.Advance(Rules.ShieldMs(Loot) + 1);
            Assert.IsTrue((await _w.Raids.GetRaidTargetAsync("p3")).Ok, "shield over");
            Assert.IsFalse((await _w.Raids.GetRaidTargetAsync("p1")).Ok, "p1 raided it in the last 24 h");

            _w.Clock.Advance(Rules.RecentTargetMs);
            Assert.IsTrue((await _w.Raids.GetRaidTargetAsync("p1")).Ok);
        }

        [Test]
        public async Task FlaggedAccounts_AreMatchedInTheReviewPool_OrWithBots()
        {
            await _w.Publish("p2");
            await _w.Seed("crypt", 0);
            await _w.Set("p1", Keys.Profile, new Profile { Pool = Pools.Review });

            RaidStartResult r = await _w.Raids.GetRaidTargetAsync("p1");

            Assert.AreEqual("v_bot_crypt", r.VaultId, "not main-pool players");
        }

        /// <summary>The committed seed vaults (Data/SeedVaults) all pass the server's checks.</summary>
        [Test]
        public async Task TheTwentySeedVaults_AllSeed_AndServeMatchmaking()
        {
            string dir = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "SeedVaults");
            Newtonsoft.Json.Linq.JArray manifest = Newtonsoft.Json.Linq.JArray.Parse(File.ReadAllText(Path.Combine(dir, "seeds.json")));
            Assert.AreEqual(20, manifest.Count);
            foreach (var seed in manifest)
            {
                string name = (string)seed["name"];
                SeedResult r = await _w.Defence.SeedVaultAsync(name, File.ReadAllText(Path.Combine(dir, name + ".json")), (int)seed["crowns"]);
                Assert.IsTrue(r.Ok, name + ": " + r.Reason + " " + string.Join("; ", r.Issues ?? new System.Collections.Generic.List<string>()));
            }

            for (int crowns = 0; crowns <= 2000; crowns += 250)
            {
                await _w.Set("p" + crowns, Keys.Profile, new Profile { Crowns = crowns });
                Assert.IsTrue((await _w.Raids.GetRaidTargetAsync("p" + crowns)).Ok, "a player at " + crowns + " Crowns finds a vault");
            }
        }

        [Test]
        public async Task SeedVaults_PassTheSameChecksAsPlayerVaults()
        {
            VaultDef broken = TestVaults.WithObjects(TestVaults.Halls(), TestVaults.Drip(9, 0, 6)); // on a wall
            SeedResult r = await _w.Defence.SeedVaultAsync("bad", TestVaults.PublishDocument(broken), 0);
            Assert.IsFalse(r.Ok);
            Assert.AreEqual(0, _w.Store.CustomWrites);
        }
    }
}
