using System.Threading.Tasks;
using NUnit.Framework;
using Wick.Sim;

namespace Wick.CloudCode.Tests
{
    using static TestEconomy;

    public class AbuseTests
    {
        private World _w;

        [SetUp]
        public async Task SetUp()
        {
            _w = new World();
            await _w.Seed("crypt", 0);
        }

        // ---- Hourly limit ------------------------------------------------------------------

        [Test]
        public async Task TwentyRaidsAnHour_ThenWait()
        {
            for (int i = 0; i < Rules.RaidsPerHour; i++)
            {
                Assert.IsTrue((await _w.Raids.StartFriendRaidAsync("p1", "v_bot_crypt", false)).Ok, "raid " + (i + 1));
                _w.Clock.Advance(Rules.MinuteMs);
            }

            RaidStartResult over = await _w.Raids.GetRaidTargetAsync("p1");
            Assert.IsFalse(over.Ok);
            StringAssert.Contains("Raid limit", over.Reason);
            Assert.IsFalse((await _w.Raids.StartFriendRaidAsync("p1", "v_bot_crypt", false)).Ok, "friend and revenge raids count too");

            _w.Clock.Advance(Rules.HourMs - 19 * Rules.MinuteMs);
            Assert.IsTrue((await _w.Raids.GetRaidTargetAsync("p1")).Ok, "the oldest token has aged out");
        }

        // ---- Anomaly score -------------------------------------------------------------------

        [Test]
        public void ABotLikeLog_IsFlagged_AHumanLikeOneIsNot()
        {
            VaultDef vault = Players.DripHalls();
            InputLog bot = Players.UpAndBack(vault, TestVaults.Seed, true);
            InputLog human = Players.UpAndBack(vault, TestVaults.Seed, false);
            var token = new RaidToken { Difficulty = 0, ClearTicks = 0 };

            RaidAnalysis botRun = RaidAnalysis.Run(vault, TestVaults.Seed, bot, RaidParams.Default);
            RaidAnalysis humanRun = RaidAnalysis.Run(vault, TestVaults.Seed, human, RaidParams.Default);
            int botScore = AnomalyScore.Score(botRun, token);
            int humanScore = AnomalyScore.Score(humanRun, token);

            TestContext.WriteLine("bot " + botScore + " (" + botRun.InputsOnSafeFrame + "/" + botRun.InputsIntoTraps +
                " on the safe frame), human " + humanScore + " (" + humanRun.InputsOnSafeFrame + "/" + humanRun.InputsIntoTraps + ")");
            Assert.GreaterOrEqual(botScore, Rules.FlagRaidScore, "bot");
            Assert.Less(humanScore, 30, "human");
        }

        [Test]
        public async Task RepeatedBotLikeRaids_MoveTheAccountToReview_WithoutBlockingIt()
        {
            VaultDef vault = Players.DripHalls();
            await _w.Publish("owner", vault);
            for (int i = 0; i < Rules.ScoreMinimumRaids; i++)
            {
                RaidStartResult start = await _w.Raids.StartFriendRaidAsync("bot", "v_owner", false);
                SubmitResult r = await _w.Raid("bot", start, Players.UpAndBack(VaultJson.Parse(start.Vault), start.Seed, true));
                Assert.IsTrue(start.Ok, start.Reason);
                Assert.IsTrue(r.Verified, "flagging never refuses a verified raid: " + r.Reason);
                _w.Clock.Advance(Rules.ShieldMs(Loot) + 1); // each robbery shields the vault
            }

            Profile p = _w.Get<Profile>("bot", Keys.Profile);
            Assert.AreEqual(Pools.Review, p.Pool);
            Assert.IsTrue((await _w.Raids.GetRaidTargetAsync("bot")).Ok, "still gets raids: bots, or the review pool");
        }

        [Test]
        public async Task AMismatchedClaim_CountsAsFullySuspicious()
        {
            RaidStartResult start = await _w.Raids.StartFriendRaidAsync("p1", "v_bot_crypt", false);
            _w.Clock.Advance(Rules.MinuteMs);
            await _w.Raids.SubmitRaidAsync("p1", "req-cheat-001", start.Token, InputLog.Empty.ToBase64(),
                new RaidClaim { Stars = 3, Loot = 500, Hash = "0000000000000000" });

            Assert.AreEqual(100, _w.Get<Profile>("p1", Keys.Profile).RecentScores[0]);
        }

        // ---- Integrity -------------------------------------------------------------------------

        [Test]
        public async Task FailedIntegrity_InProduction_RoutesToReview_ButStillIssuesTheToken()
        {
            _w.Production = true;
            _w.Integrity.Verdict = new IntegrityVerdict { Checked = true, Passed = false, Detail = "device not genuine" };

            RaidStartResult r = await _w.Raids.GetRaidTargetAsync("p1", "android", "token");

            Assert.IsTrue(r.Ok, "no automatic block");
            Profile p = _w.Get<Profile>("p1", Keys.Profile);
            Assert.AreEqual(Pools.Review, p.Pool);
            Assert.AreEqual("device not genuine", p.Integrity);
        }

        [Test]
        public async Task FailedIntegrity_InDevelopment_IsOnlyRecorded()
        {
            _w.Integrity.Verdict = new IntegrityVerdict { Checked = true, Passed = false, Detail = "app UNRECOGNIZED_VERSION" };
            await _w.Raids.GetRaidTargetAsync("p1", "android", "token");
            Assert.AreEqual(Pools.Main, _w.Get<Profile>("p1", Keys.Profile).Pool);
        }
    }
}
