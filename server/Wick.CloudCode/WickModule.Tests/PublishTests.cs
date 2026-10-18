using System.Threading.Tasks;
using NUnit.Framework;
using Wick.Sim;

namespace Wick.CloudCode.Tests
{
    public class PublishTests
    {
        private const string Owner = "owner01";

        private FakeStore _store;
        private PublishService _publish;

        [SetUp]
        public void SetUp()
        {
            _store = new FakeStore();
            _publish = new PublishService(_store, new FakeClock());
        }

        [Test]
        public async Task ValidVault_IsWrittenToVaultLive_AfterTheClearCheckReplays()
        {
            PublishResult r = await _publish.PublishAsync(Owner, "req-publish-1", TestVaults.PublishDocument(TestVaults.Halls()));

            Assert.IsTrue(r.Ok, r.Reason);
            Assert.AreEqual("v_owner01", r.VaultId);
            Assert.AreEqual(1, r.Version);
            Assert.AreEqual(3, r.Stars);
            LiveVault live = Json.Read<LiveVault>(_store.Read(Owner, Keys.VaultLive));
            Assert.AreEqual(Owner, live.OwnerId);
            VaultDef stored = VaultJson.Parse(live.Document);
            Assert.AreEqual("v_owner01", stored.VaultId, "the server sets the id and version");
            Assert.AreEqual(1, stored.Version);
        }

        [Test]
        public async Task Republishing_BumpsTheVersion_ButARetriedRequestDoesNot()
        {
            string doc = TestVaults.PublishDocument(TestVaults.Halls());
            await _publish.PublishAsync(Owner, "req-publish-1", doc);
            PublishResult retry = await _publish.PublishAsync(Owner, "req-publish-1", doc);
            Assert.AreEqual(1, retry.Version, "same request id: the stored result, not a new version");

            PublishResult second = await _publish.PublishAsync(Owner, "req-publish-2", doc);
            Assert.AreEqual(2, second.Version);
            Assert.AreEqual(2, Json.Read<LiveVault>(_store.Read(Owner, Keys.VaultLive)).Version);
        }

        [Test]
        public async Task OverBudgetVault_IsRejected_AndNothingIsWritten()
        {
            // Seven 10-point drips on a level 1 vault (budget 60).
            VaultDef halls = TestVaults.Halls();
            var drips = new ObjectDef[7];
            for (int i = 0; i < drips.Length; i++)
            {
                drips[i] = TestVaults.Drip(10 + i, 1 + i, 6);
            }

            PublishResult r = await _publish.PublishAsync(Owner, "req-publish-1", TestVaults.PublishDocument(TestVaults.WithObjects(halls, drips)));

            Assert.IsFalse(r.Ok);
            Assert.That(r.Issues, Has.Some.Contains("build points"));
            Assert.AreEqual(0, _store.Count, "no vault_live, no idempotency record");
        }

        [Test]
        public async Task ClearCheckFromADifferentLayout_IsRejected()
        {
            // The clear-check was recorded on Three Halls, then a crate was added before publishing.
            VaultDef halls = TestVaults.Halls();
            RaidResult run = RaidRunner.RunToEnd(halls, TestVaults.Seed, TestVaults.Escape);
            ClearCheck cc = ClearCheck.FromRun(halls, TestVaults.Seed, TestVaults.Escape, run);
            VaultDef edited = TestVaults.WithObjects(halls,
                new ObjectDef(9, ObjectType.Crate, new TilePos(2, 6), 0, 0, 0, 0, new TilePos[0]));

            PublishResult r = await _publish.PublishAsync(Owner, "req-publish-1", VaultJson.WriteDocument(edited, cc));

            Assert.IsFalse(r.Ok);
            StringAssert.Contains("clear-check", r.Reason);
            Assert.AreEqual(0, _store.Count);
        }

        [Test]
        public async Task ForgedClearCheckHash_IsRejected()
        {
            VaultDef halls = TestVaults.Halls();
            var forged = new ClearCheck(TestVaults.Seed, TestVaults.Escape, "0000000000000000", 3, SimInfo.Version,
                ClearCheck.LayoutHashOf(halls));

            PublishResult r = await _publish.PublishAsync(Owner, "req-publish-1", VaultJson.WriteDocument(halls, forged));

            Assert.IsFalse(r.Ok);
            Assert.AreEqual(0, _store.Count);
        }

        [Test]
        public async Task GarbageDocument_IsRejected()
        {
            PublishResult r = await _publish.PublishAsync(Owner, "req-publish-1", "{\"schema\": 1, \"grid\": ");
            Assert.IsFalse(r.Ok);
            Assert.AreEqual(0, _store.Count);
        }
    }
}
