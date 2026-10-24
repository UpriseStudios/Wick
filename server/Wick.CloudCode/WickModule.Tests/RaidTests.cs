using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Wick.Sim;

namespace Wick.CloudCode.Tests
{
    public class RaidTests
    {
        private const string Owner = "owner01";
        private const string Raider = "raider01";
        private const long RaidTime = 60_000;

        private FakeStore _store;
        private FakeClock _clock;
        private RaidService _raids;

        [SetUp]
        public async Task SetUp()
        {
            _store = new FakeStore();
            _clock = new FakeClock();
            _raids = new RaidService(_store, _clock, new FakeRandom());
            PublishResult published = await new PublishService(_store, _clock)
                .PublishAsync(Owner, "req-publish-1", TestVaults.PublishDocument(TestVaults.Halls()));
            Assert.IsTrue(published.Ok, published.Reason);
        }

        private async Task<RaidStartResult> Start()
        {
            RaidStartResult start = await _raids.StartFriendRaidAsync(Raider, "v_owner01", false);
            Assert.IsTrue(start.Ok, start.Reason);
            return start;
        }

        /// <summary>What an unmodified client would claim after playing this log.</summary>
        private static RaidClaim Honest(RaidStartResult start, InputLog log)
        {
            RaidResult r = RaidRunner.RunToEnd(VaultJson.Parse(start.Vault), start.Seed, log, new RaidParams(start.RaidTicks));
            return new RaidClaim { Stars = r.Stars, Loot = r.Loot, Hash = r.HashHex };
        }

        private long Gold => Json.Read<Wallet>(_store.Read(Raider, Keys.Wallet)).Gold;

        // ---- Tokens ---------------------------------------------------------------------

        [Test]
        public async Task StartFriendRaid_NeverHandsOutTheOwnersClearCheck()
        {
            RaidStartResult start = await Start();
            StringAssert.DoesNotContain("clearCheck", start.Vault);
            Assert.AreEqual(1, start.VaultVersion);
            Assert.AreEqual(0, Gold, "the wallet is created before any payout");
        }

        [Test]
        public async Task OwnVault_OnlyInDevelopment()
        {
            Assert.IsFalse((await _raids.StartFriendRaidAsync(Owner, "v_owner01", false)).Ok);
            Assert.IsTrue((await _raids.StartFriendRaidAsync(Owner, "v_owner01", true)).Ok);
        }

        // ---- Honest raids ---------------------------------------------------------------

        [Test]
        public async Task HonestRaid_PaysTheServersOwnLoot()
        {
            RaidStartResult start = await Start();
            RaidClaim claim = Honest(start, TestVaults.Escape);
            _clock.Advance(RaidTime);

            SubmitResult r = await _raids.SubmitRaidAsync(Raider, "req-submit-1", start.Token, TestVaults.Escape.ToBase64(), claim);

            Assert.IsTrue(r.Verified, r.Reason);
            Assert.AreEqual(3, r.Stars);
            Assert.Greater(r.Paid, 0);
            Assert.AreEqual(r.Paid, Gold);
            Assert.AreEqual(r.Paid, r.Gold);
            Assert.AreEqual(1, Json.Read<RaidStats>(_store.Read(Raider, Keys.Stats)).Escapes);
            Assert.IsTrue(_store.Custom.ContainsKey("raid_" + start.Token + "/record"));
        }

        [Test]
        public async Task RetriedRequest_ReturnsTheSameResult_AndPaysOnce()
        {
            RaidStartResult start = await Start();
            RaidClaim claim = Honest(start, TestVaults.Escape);
            _clock.Advance(RaidTime);
            SubmitResult first = await _raids.SubmitRaidAsync(Raider, "req-submit-1", start.Token, TestVaults.Escape.ToBase64(), claim);

            SubmitResult retry = await _raids.SubmitRaidAsync(Raider, "req-submit-1", start.Token, TestVaults.Escape.ToBase64(), claim);

            Assert.IsTrue(retry.Verified);
            Assert.AreEqual(first.Paid, retry.Paid);
            Assert.AreEqual(first.Paid, Gold, "paid once");
        }

        // ---- Cheats and mistakes ----------------------------------------------------------

        /// <summary>
        /// Acceptance: a modified client that failed (stood still until time ran out) but claims a
        /// 3-star heist gets nothing, whether it forges the hash or sends the real one.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public async Task ModifiedClient_Claiming3StarsOnAFailedRaid_ReceivesNothing(bool forgeHash)
        {
            RaidStartResult start = await Start();
            RaidClaim failed = Honest(start, InputLog.Empty);
            Assert.AreEqual(0, failed.Stars, "standing still earns nothing");
            var claim = new RaidClaim { Stars = 3, Loot = 500, Hash = forgeHash ? Honest(start, TestVaults.Escape).Hash : failed.Hash };
            _clock.Advance(RaidTime);

            SubmitResult r = await _raids.SubmitRaidAsync(Raider, "req-submit-1", start.Token, InputLog.Empty.ToBase64(), claim);

            Assert.IsFalse(r.Verified);
            Assert.AreEqual(0, r.Paid);
            Assert.AreEqual("TimedOut", r.Outcome, "judged by re-simulating the submitted log");
            Assert.AreEqual(0, r.Stars, "the verdict reports the server's re-sim, not the claim");
            StringAssert.Contains("re-simulation", r.Reason);
            Assert.AreEqual(0, Gold);
            Assert.IsNull(_store.Read(Raider, Keys.Stats));
        }

        [Test]
        public async Task TamperedLog_IsNotPaid_AndBurnsTheToken()
        {
            RaidStartResult start = await Start();
            RaidClaim claim = Honest(start, TestVaults.Escape);
            var tampered = new InputLog(new[] { Command.RunStart(0, Dir.N), Command.RunStart(20, Dir.S) });
            _clock.Advance(RaidTime);

            SubmitResult r = await _raids.SubmitRaidAsync(Raider, "req-submit-1", start.Token, tampered.ToBase64(), claim);
            Assert.IsFalse(r.Verified);
            Assert.AreEqual(0, Gold);

            SubmitResult again = await _raids.SubmitRaidAsync(Raider, "req-submit-2", start.Token, TestVaults.Escape.ToBase64(), claim);
            Assert.IsFalse(again.Verified, "no second try with a corrected log");
            StringAssert.Contains("already submitted", again.Reason);
        }

        [Test]
        public async Task UnreadableLog_IsRejected()
        {
            RaidStartResult start = await Start();
            _clock.Advance(RaidTime);
            SubmitResult r = await _raids.SubmitRaidAsync(Raider, "req-submit-1", start.Token, "not base64!!", new RaidClaim());
            Assert.IsFalse(r.Verified);
            Assert.AreEqual(0, Gold);
        }

        [Test]
        public async Task ReplayedToken_PaysOnlyOnce()
        {
            RaidStartResult start = await Start();
            RaidClaim claim = Honest(start, TestVaults.Escape);
            _clock.Advance(RaidTime);
            SubmitResult first = await _raids.SubmitRaidAsync(Raider, "req-submit-1", start.Token, TestVaults.Escape.ToBase64(), claim);

            SubmitResult replay = await _raids.SubmitRaidAsync(Raider, "req-submit-2", start.Token, TestVaults.Escape.ToBase64(), claim);

            Assert.IsTrue(first.Verified);
            Assert.IsFalse(replay.Verified);
            StringAssert.Contains("already submitted", replay.Reason);
            Assert.AreEqual(first.Paid, Gold);
        }

        [Test]
        public async Task ExpiredToken_IsRejected()
        {
            RaidStartResult start = await Start();
            RaidClaim claim = Honest(start, TestVaults.Escape);
            _clock.Advance(Rules.TokenLifetimeMs + 1);

            SubmitResult r = await _raids.SubmitRaidAsync(Raider, "req-submit-1", start.Token, TestVaults.Escape.ToBase64(), claim);

            Assert.IsFalse(r.Verified);
            StringAssert.Contains("expired", r.Reason);
            Assert.AreEqual(0, Gold);
        }

        [Test]
        public async Task MismatchedHash_IsNotPaid()
        {
            RaidStartResult start = await Start();
            RaidClaim claim = Honest(start, TestVaults.Escape);
            claim.Hash = "0123456789abcdef";
            _clock.Advance(RaidTime);

            SubmitResult r = await _raids.SubmitRaidAsync(Raider, "req-submit-1", start.Token, TestVaults.Escape.ToBase64(), claim);

            Assert.IsFalse(r.Verified);
            Assert.AreEqual(0, Gold);
        }

        [Test]
        public async Task RaidFinishedFasterThanRealTime_IsNotPaid()
        {
            RaidStartResult start = await Start();
            RaidClaim claim = Honest(start, TestVaults.Escape);
            _clock.Advance(1000); // the escape takes several seconds of sim time

            SubmitResult r = await _raids.SubmitRaidAsync(Raider, "req-submit-1", start.Token, TestVaults.Escape.ToBase64(), claim);

            Assert.IsFalse(r.Verified);
            StringAssert.Contains("faster", r.Reason);
            Assert.AreEqual(0, Gold);
        }

        [Test]
        public async Task UnknownToken_IsRejected()
        {
            await Start();
            SubmitResult r = await _raids.SubmitRaidAsync(Raider, "req-submit-1", "tok-made-up", TestVaults.Escape.ToBase64(), new RaidClaim());
            Assert.IsFalse(r.Verified);
            StringAssert.Contains("Unknown", r.Reason);
        }

        // ---- Concurrency ------------------------------------------------------------------

        /// <summary>
        /// Acceptance: two SubmitRaid calls paying into the same wallet at the same time. Both read
        /// the wallet before either writes, so one write conflicts, re-reads and retries. Both land.
        /// </summary>
        [Test]
        public async Task ConcurrentSubmits_BothPay_NeitherOverwritesTheOther()
        {
            RaidStartResult a = await Start();
            RaidStartResult b = await Start();
            RaidClaim claim = Honest(a, TestVaults.Escape);
            _clock.Advance(RaidTime);

            int reads = 0;
            var bothRead = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _store.AfterRead = () =>
            {
                int n = Interlocked.Increment(ref reads);
                if (n == 2)
                {
                    bothRead.SetResult(true);
                }

                return n <= 2 ? bothRead.Task : Task.CompletedTask; // hold the first two reads together
            };

            string log = TestVaults.Escape.ToBase64();
            Task<SubmitResult> first = Task.Run(() => _raids.SubmitRaidAsync(Raider, "req-submit-a", a.Token, log, claim));
            Task<SubmitResult> second = Task.Run(() => _raids.SubmitRaidAsync(Raider, "req-submit-b", b.Token, log, claim));
            SubmitResult[] results = await Task.WhenAll(first, second);

            Assert.IsTrue(results[0].Verified, results[0].Reason);
            Assert.IsTrue(results[1].Verified, results[1].Reason);
            Assert.AreEqual(results[0].Paid + results[1].Paid, Gold, "both payouts landed");
            Assert.GreaterOrEqual(_store.Conflicts, 1, "the two writes really did collide");
            Assert.AreEqual(2, Json.Read<RaidStats>(_store.Read(Raider, Keys.Stats)).Raids);
        }

        [Test]
        public async Task EndlessConflicts_GiveUp_WithoutWritingAnything()
        {
            RaidStartResult start = await Start();
            RaidClaim claim = Honest(start, TestVaults.Escape);
            _clock.Advance(RaidTime);
            _store.AlwaysConflict = true;

            Assert.ThrowsAsync<StoreBusyException>(() =>
                _raids.SubmitRaidAsync(Raider, "req-submit-1", start.Token, TestVaults.Escape.ToBase64(), claim));
            Assert.AreEqual(Locked.MaxAttempts, _store.Conflicts);

            _store.AlwaysConflict = false;
            Assert.AreEqual(0, Gold);
            SubmitResult retry = await _raids.SubmitRaidAsync(Raider, "req-submit-1", start.Token, TestVaults.Escape.ToBase64(), claim);
            Assert.IsTrue(retry.Verified, "the token is still unused, so the client's retry goes through");
        }
    }
}
