using System.Threading.Tasks;
using NUnit.Framework;
using Wick.Economy;
using Wick.Sim;

namespace Wick.CloudCode.Tests
{
    /// <summary>
    /// M10: the raid length comes from Remote Config per defender Vault Level, travels on the raid
    /// token, and is what the server re-simulates with. Tokens record the sim version, so a raid
    /// that straddles a deploy is voided rather than judged by rules it wasn't played under.
    /// </summary>
    public class RaidLengthTests
    {
        private const string Owner = "owner01";
        private const string Raider = "raider01";

        private FakeStore _store;
        private FakeClock _clock;
        private EconomyConfig _economy;

        [SetUp]
        public async Task SetUp()
        {
            _store = new FakeStore();
            _clock = new FakeClock();
            _economy = EconomyDefaults.Load();
            PublishResult published = await Publish(TestVaults.PublishDocument(TestVaults.Halls()));
            Assert.IsTrue(published.Ok, published.Reason);
        }

        private RaidService Raids => new RaidService(_store, _clock, new FakeRandom(), economy: new FixedEconomy(_economy));

        private Task<PublishResult> Publish(string document)
        {
            return new PublishService(_store, _clock, new FixedEconomy(_economy)).PublishAsync(Owner, "req-publish-1", document);
        }

        [Test]
        public async Task TheToken_CarriesTheRaidLengthForTheDefendersLevel()
        {
            _economy.Raid.SecondsByLevel[0] = 90; // the published vault is Level 1
            RaidStartResult start = await Raids.StartFriendRaidAsync(Raider, "v_owner01", false);
            Assert.AreEqual(90 * SimTuning.TicksPerSecond, start.RaidTicks);
        }

        [Test]
        public async Task TheServer_JudgesTheRaidWithTheTokensLength()
        {
            _economy.Raid.SecondsByLevel[0] = 90;
            RaidStartResult start = await Raids.StartFriendRaidAsync(Raider, "v_owner01", false);

            // Standing still: under a 90-second clock the raid lasts 2700 ticks, not 1800.
            RaidResult run = RaidRunner.RunToEnd(VaultJson.Parse(start.Vault), start.Seed, InputLog.Empty, new RaidParams(start.RaidTicks));
            _clock.Advance(91_000);
            SubmitResult r = await Raids.SubmitRaidAsync(Raider, "req-submit-1", start.Token, InputLog.Empty.ToBase64(),
                new RaidClaim { Stars = run.Stars, Loot = run.Loot, Hash = run.HashHex });

            Assert.IsTrue(r.Verified, r.Reason);
            Assert.AreEqual(90 * SimTuning.TicksPerSecond, r.Ticks);
        }

        [Test]
        public async Task ARaidStartedOnAnotherSimVersion_IsVoided_AndItsSlotReturned()
        {
            RaidStartResult start = await Raids.StartFriendRaidAsync(Raider, "v_owner01", false);
            TokenBook book = Json.Read<TokenBook>(_store.Read(Raider, Keys.RaidTokens));
            book.Find(start.Token).SimVersion = SimInfo.Version - 1; // as if issued before the deploy
            await _store.SetProtectedBatchAsync(Raider, new[] { new ItemWrite(Keys.RaidTokens, Json.Write(book), null) });

            RaidResult run = RaidRunner.RunToEnd(VaultJson.Parse(start.Vault), start.Seed, TestVaults.Escape);
            _clock.Advance(60_000);
            SubmitResult r = await Raids.SubmitRaidAsync(Raider, "req-submit-1", start.Token, TestVaults.Escape.ToBase64(),
                new RaidClaim { Stars = run.Stars, Loot = run.Loot, Hash = run.HashHex });

            Assert.IsFalse(r.Verified);
            StringAssert.Contains("updated", r.Reason);
            Assert.AreEqual(0, Json.Read<Wallet>(_store.Read(Raider, Keys.Wallet)).Gold, "nothing is paid");
            TokenBook after = Json.Read<TokenBook>(_store.Read(Raider, Keys.RaidTokens));
            Assert.IsNull(after.Find(start.Token), "the token is gone");
            Assert.AreEqual(0, after.IssuedSince(0), "and doesn't count against the raid limit");
        }

        [Test]
        public async Task AClearCheckWithALongerClockThanRaiders_IsRefused()
        {
            VaultDef halls = TestVaults.Halls();
            RaidParams longer = RaidParams.ForSeconds(120);
            RaidResult run = RaidRunner.RunToEnd(halls, TestVaults.Seed, TestVaults.Escape, longer);
            string document = VaultJson.WriteDocument(halls, ClearCheck.FromRun(halls, TestVaults.Seed, TestVaults.Escape, run, longer));

            PublishResult r = await Publish(document);
            Assert.IsFalse(r.Ok);
            StringAssert.Contains("longer clock", r.Reason);

            _economy.Raid.SecondsByLevel[0] = 120;
            Assert.IsTrue((await new PublishService(_store, _clock, new FixedEconomy(_economy))
                .PublishAsync(Owner, "req-publish-2", document)).Ok, "fine once raiders get as long");
        }

        [Test]
        public void RaidLength_FallsBackSensibly()
        {
            var raid = new RaidConfig();
            Assert.AreEqual(SimTuning.RaidTicks, raid.TicksFor(3), "no list: 60 s");
            raid.SecondsByLevel.AddRange(new[] { 60, 75 });
            Assert.AreEqual(75 * SimTuning.TicksPerSecond, raid.TicksFor(10), "past the list: its last entry");
            Assert.AreEqual(60 * SimTuning.TicksPerSecond, raid.TicksFor(0));
        }

        [Test]
        public async Task TheToken_CarriesTheRaidersWickUpgrades()
        {
            await _store.SetProtectedBatchAsync(Raider, new[]
            {
                new ItemWrite(EconomyKeys.Upgrades, Json.Write(new Upgrades { Levels = new Progression { VaultLevel = 6, Lantern = 2, Decoys = 1 } }), null),
            });
            RaidStartResult start = await Raids.StartFriendRaidAsync(Raider, "v_owner01", false);
            Assert.AreEqual(_economy.LanternTo(2).LightBonus, start.LightBonus);
            Assert.AreEqual(2, start.Decoys);
        }

        [Test]
        public async Task AClearCheckPlayedWithWickUpgrades_IsRefused()
        {
            VaultDef halls = TestVaults.Halls();
            RaidParams upgraded = new RaidParams(SimTuning.RaidTicks, 300, 2);
            RaidResult run = RaidRunner.RunToEnd(halls, TestVaults.Seed, TestVaults.Escape, upgraded);
            PublishResult r = await Publish(VaultJson.WriteDocument(halls, ClearCheck.FromRun(halls, TestVaults.Seed, TestVaults.Escape, run, upgraded)));
            Assert.IsFalse(r.Ok);
            StringAssert.Contains("without Wick's upgrades", r.Reason);
        }

    }
}
