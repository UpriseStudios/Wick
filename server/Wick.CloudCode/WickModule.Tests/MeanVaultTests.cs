using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Wick.Economy;
using Wick.Sim;

namespace Wick.CloudCode.Tests
{
    /// <summary>
    /// M10 mean vault protection: vaults that snuff nearly everyone are served less often, and
    /// raiders can skip a served vault for free a few times an hour.
    /// </summary>
    public class MeanVaultTests
    {
        private static readonly MatchmakingConfig Mean = new MatchmakingConfig();

        private static MatchEntry Entry(string id, int raids, int snuffs)
        {
            var recent = new List<bool>();
            for (int i = 0; i < raids; i++)
            {
                recent.Add(i < snuffs);
            }

            return new MatchEntry { VaultId = id, Recent = recent };
        }

        [Test]
        public void AVaultIsMean_OnlyOverAFullWindow_AtTheThreshold()
        {
            Assert.IsFalse(Entry("a", 19, 19).IsMean(Mean), "not enough raids to say");
            Assert.IsTrue(Entry("b", 20, 17).IsMean(Mean), "85% of 20");
            Assert.IsFalse(Entry("c", 20, 16).IsMean(Mean), "80%");
            // Newest first: only the newest 20 outcomes count.
            var reformed = new MatchEntry { Recent = new List<bool>() };
            for (int i = 0; i < 30; i++)
            {
                reformed.Recent.Add(i >= 10); // 10 recent fair raids, then 20 old snuffs
            }

            Assert.IsFalse(reformed.IsMean(Mean), "it was mean, but its newest 20 raids are only half snuffs");
            Assert.IsTrue(Entry("d", 30, 20).IsMean(Mean), "its newest 20 are all snuffs; the older 10 don't matter");
        }

        [Test]
        public void AMeanVault_IsServedAQuarterAsOften()
        {
            // Weights: mean 25, normal 100 (total 125). Rolls 0-24 pick the mean vault, 25-124 the other.
            var eligible = new List<MatchEntry> { Entry("mean", 20, 20), Entry("fair", 20, 2) };
            var random = new FakeRandom { Index = 24 };
            Assert.AreEqual("mean", RaidService.PickWeighted(eligible, Mean, random).VaultId);
            random.Index = 25;
            Assert.AreEqual("fair", RaidService.PickWeighted(eligible, Mean, random).VaultId);
        }

        [Test]
        public async Task Raids_AreRecordedInTheVaultsRecentWindow()
        {
            var w = new World();
            await w.Publish("owner");
            await w.RaidVault("raider", "v_owner", InputLog.Empty); // stands still: times out
            DefenceState d = w.Get<DefenceState>("owner", Keys.Defence);
            CollectionAssert.AreEqual(new[] { false }, d.Recent);
        }

        [Test]
        public async Task ASkip_IsFree_UpToTheHourlyLimit()
        {
            var w = new World();
            await w.Seed("cellar", 0);
            for (int i = 0; i < Mean.FreeSkipsPerHour; i++)
            {
                await w.Seed("cellar" + i, 0);
            }

            for (int i = 0; i < Mean.FreeSkipsPerHour; i++)
            {
                RaidStartResult start = await w.Raids.GetRaidTargetAsync("raider");
                Assert.IsTrue(start.Ok, start.Reason);
                RaidStartResult skip = await w.Raids.SkipRaidAsync("raider", start.Token);
                Assert.IsTrue(skip.Ok, skip.Reason);
            }

            TokenBook book = w.Get<TokenBook>("raider", Keys.RaidTokens);
            Assert.AreEqual(0, book.IssuedSince(0), "skipped raids don't count against the raid limit");
            RaidStartResult another = await w.Raids.GetRaidTargetAsync("raider");
            Assert.IsFalse((await w.Raids.SkipRaidAsync("raider", another.Token)).Ok, "no free skips left this hour");
        }

        [Test]
        public async Task OnlyMatchmakingRaids_CanBeSkipped()
        {
            var w = new World();
            await w.Publish("owner");
            RaidStartResult friend = await w.Raids.StartFriendRaidAsync("raider", "v_owner", false);
            Assert.IsFalse((await w.Raids.SkipRaidAsync("raider", friend.Token)).Ok);
        }
    }
}
