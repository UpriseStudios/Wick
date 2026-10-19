using System.Threading.Tasks;
using NUnit.Framework;
using Wick.Economy;
using Wick.Sim;

namespace Wick.CloudCode.Tests
{
    public class AdTests
    {
        private World _w;
        private AdService Ads => new AdService(_w.Store, _w.Clock, new FixedEconomy(TestEconomy.Default));

        [SetUp]
        public void SetUp()
        {
            _w = new World();
            _w.Clock.NowMs = new System.DateTimeOffset(2026, 10, 20, 9, 0, 0, System.TimeSpan.Zero).ToUnixTimeMilliseconds();
        }

        private Task<AdGrant> View(string placement, string context = null, string nonce = null)
        {
            nonce ??= System.Guid.NewGuid().ToString("N");
            return Ads.GrantFromCallbackAsync("p1", placement, context, nonce, "ev-" + nonce);
        }

        [Test]
        public async Task TheShopChest_Grants_TwiceADay()
        {
            Assert.IsTrue((await View(AdPlacements.ShopChest)).Granted);
            Assert.IsTrue((await View(AdPlacements.ShopChest)).Granted);
            AdGrant third = await View(AdPlacements.ShopChest);
            Assert.IsFalse(third.Granted);
            StringAssert.Contains("Daily limit", third.Reason);
            Assert.AreEqual(2 * 120, _w.Get<Wallet>("p1", Keys.Wallet).Gold);

            _w.Clock.Advance(24 * Rules.HourMs);
            Assert.IsTrue((await View(AdPlacements.ShopChest)).Granted, "a new UTC day");
        }

        [Test]
        public async Task NineAdsADay_InTotal()
        {
            int granted = 0;
            foreach (string p in new[] { AdPlacements.ShopChest, AdPlacements.DailyVaultRetry })
            {
                for (int i = 0; i < 5; i++)
                {
                    granted += (await View(p)).Granted ? 1 : 0;
                }
            }

            Assert.AreEqual(3, granted, "2 chests + 1 retry (their own caps)");
            var w = _w.Get<AdState>("p1", EconomyKeys.Ads);
            Assert.AreEqual(3, w.Total);
            Assert.AreEqual(9, TestEconomy.Default.Ads.DailyTotal);
        }

        [Test]
        public async Task ARepeatedCallback_GrantsOnce()
        {
            AdGrant a = await Ads.GrantFromCallbackAsync("p1", AdPlacements.ShopChest, null, "n1", "ev-same");
            AdGrant b = await Ads.GrantFromCallbackAsync("p1", AdPlacements.ShopChest, null, "n1", "ev-same");
            Assert.IsTrue(a.Granted && b.Granted);
            Assert.AreEqual(120, _w.Get<Wallet>("p1", Keys.Wallet).Gold);
            Assert.AreEqual(1, _w.Get<AdState>("p1", EconomyKeys.Ads).Total);
        }

        [Test]
        public async Task DoubleLoot_OnlyForYourOwnFreshVerifiedRaid_Once()
        {
            await _w.Publish("owner");
            RaidStartResult start = await _w.Raids.StartFriendRaidAsync("p1", "v_owner", false);
            SubmitResult raid = await _w.Raid("p1", start, TestVaults.Escape);
            string raidId = "r_" + start.Token;
            long gold = _w.Get<Wallet>("p1", Keys.Wallet).Gold;

            Assert.IsFalse((await Ads.GrantFromCallbackAsync("p2", AdPlacements.DoubleLoot, raidId, "x", "ev-x")).Granted, "someone else's raid");
            AdGrant doubled = await View(AdPlacements.DoubleLoot, raidId);
            Assert.IsTrue(doubled.Granted, doubled.Reason);
            Assert.AreEqual(gold + raid.Paid, _w.Get<Wallet>("p1", Keys.Wallet).Gold);
            Assert.IsFalse((await View(AdPlacements.DoubleLoot, raidId)).Granted, "once per raid");
        }

        [Test]
        public async Task DoubleLoot_ExpiresAfterItsWindow()
        {
            await _w.Publish("owner");
            RaidStartResult start = await _w.Raids.StartFriendRaidAsync("p1", "v_owner", false);
            await _w.Raid("p1", start, TestVaults.Escape);
            _w.Clock.Advance(11 * Rules.MinuteMs);
            Assert.IsFalse((await View(AdPlacements.DoubleLoot, "r_" + start.Token)).Granted);
        }

        [Test]
        public async Task SpeedUp_TakesThirtyMinutesOffARunningUpgrade()
        {
            long now = _w.Clock.NowMs;
            await _w.Set("p1", EconomyKeys.Upgrades, new Upgrades
            {
                Running = { new RunningUpgrade { Kind = UpgradeKinds.VaultLevel, ToLevel = 4, StartedMs = now, EndsMs = now + 60 * Rules.MinuteMs } },
            });
            Assert.IsTrue((await View(AdPlacements.SpeedUp, UpgradeKinds.VaultLevel)).Granted);
            Assert.AreEqual(now + 30 * Rules.MinuteMs, _w.Get<Upgrades>("p1", EconomyKeys.Upgrades).Running[0].EndsMs);
        }

        [Test]
        public async Task ASpeedUpPastTheEnd_FinishesTheUpgrade()
        {
            long now = _w.Clock.NowMs;
            await _w.Set("p1", EconomyKeys.Upgrades, new Upgrades
            {
                Running = { new RunningUpgrade { Kind = UpgradeKinds.VaultLevel, ToLevel = 2, StartedMs = now, EndsMs = now + 10 * Rules.MinuteMs } },
            });
            Assert.IsTrue((await View(AdPlacements.SpeedUp, UpgradeKinds.VaultLevel)).Granted);
            Upgrades stored = _w.Get<Upgrades>("p1", EconomyKeys.Upgrades);
            Assert.AreEqual(2, stored.Levels.VaultLevel);
            Assert.AreEqual(0, stored.Running.Count);
        }

        [Test]
        public async Task TheClientLearnsWhatItsAdEarned_OnlyFromTheServer()
        {
            AdRewardStatus before = await Ads.StatusAsync("p1", "n-42");
            Assert.IsFalse(before.Arrived, "nothing until LevelPlay's callback");
            await Ads.GrantFromCallbackAsync("p1", AdPlacements.ShopChest, null, "n-42", "ev-42");
            AdRewardStatus after = await Ads.StatusAsync("p1", "n-42");
            Assert.IsTrue(after.Arrived && after.Granted);
            Assert.AreEqual(120, after.Grant.Gold);
        }
    }

    public class PurchaseTests
    {
        private World _w;

        [SetUp]
        public void SetUp()
        {
            _w = new World();
        }

        private PurchaseService Purchases(IReceiptVerifier v = null)
        {
            return new PurchaseService(_w.Store, new FixedEconomy(TestEconomy.Default), v ?? new FakeStoreVerifier());
        }

        [Test]
        public async Task AGemPack_GrantsOncePerOrder()
        {
            PurchaseResult a = await Purchases().ValidateAsync("p1", "req-buy-0001", "gems_500", EconomyTests.FakeReceipt("t1"));
            PurchaseResult retry = await Purchases().ValidateAsync("p1", "req-buy-0001", "gems_500", EconomyTests.FakeReceipt("t1"));
            PurchaseResult resent = await Purchases().ValidateAsync("p1", "req-buy-0002", "gems_500", EconomyTests.FakeReceipt("t1"));
            PurchaseResult another = await Purchases().ValidateAsync("p1", "req-buy-0003", "gems_500", EconomyTests.FakeReceipt("t2"));

            Assert.IsTrue(a.NewlyGranted);
            Assert.IsTrue(retry.NewlyGranted, "same request: same answer");
            Assert.IsFalse(resent.NewlyGranted, "same order: nothing new");
            Assert.IsTrue(another.NewlyGranted, "a new order of a consumable");
            Assert.AreEqual(1000, _w.Get<Wallet>("p1", Keys.Wallet).Gems);
        }

        [Test]
        public async Task TheStarterPack_GrantsGemsAWorkshopAndASkin_Once()
        {
            await Purchases().ValidateAsync("p1", "req-buy-0001", "starter_pack", EconomyTests.FakeReceipt("t1"));
            await Purchases().ValidateAsync("p1", "req-buy-0002", "starter_pack", EconomyTests.FakeReceipt("t9"));
            Assert.AreEqual(500, _w.Get<Wallet>("p1", Keys.Wallet).Gems);
            Inventory inv = _w.Get<Inventory>("p1", EconomyKeys.Inventory);
            Assert.AreEqual(1, inv.Workshops);
            CollectionAssert.Contains(inv.Cosmetics, "skin_pip");
        }

        [Test]
        public async Task UnknownProductsAndBadReceipts_GrantNothing()
        {
            Assert.IsFalse((await Purchases().ValidateAsync("p1", "req-buy-0001", "gems_9999", EconomyTests.FakeReceipt("t1"))).Ok);
            Assert.IsFalse((await Purchases().ValidateAsync("p1", "req-buy-0002", "gems_80", "{\"Store\":\"GooglePlay\"}")).Ok);
            Assert.IsNull(_w.Store.Read("p1", Keys.Wallet));
        }

        private static string PlayReceipt(string productId, string token)
        {
            string purchase = "{\"orderId\":\"GPA.1\",\"packageName\":\"com.uprisestudios.wick\",\"productId\":\"" + productId +
                "\",\"purchaseToken\":\"" + token + "\"}";
            string payload = Newtonsoft.Json.JsonConvert.SerializeObject(new { json = purchase, signature = "sig" });
            return Newtonsoft.Json.JsonConvert.SerializeObject(new { Store = "GooglePlay", TransactionID = "GPA.1", Payload = payload });
        }

        private static GooglePlayVerifier Play(FakeHttp http)
        {
            RSA_Key(out GoogleServiceAccount account);
            return new GooglePlayVerifier(new GoogleAuth(account, http, new FakeClock()), http, GameFunctions.AndroidPackage);
        }

        private static void RSA_Key(out GoogleServiceAccount account)
        {
            var rsa = System.Security.Cryptography.RSA.Create(2048);
            account = GoogleServiceAccount.Parse(new Newtonsoft.Json.Linq.JObject
            {
                ["client_email"] = "play@test", ["private_key"] = rsa.ExportPkcs8PrivateKeyPem(), ["token_uri"] = "https://oauth2.example/token",
            }.ToString());
        }

        [TestCase(0, true)]
        [TestCase(1, false)]
        public async Task GooglePlay_GrantsOnlyCompletedPurchases(int state, bool ok)
        {
            var http = new FakeHttp
            {
                Respond = url => url.Contains("oauth2")
                    ? (200, "{\"access_token\":\"at\",\"expires_in\":3600}")
                    : (200, "{\"purchaseState\":" + state + ",\"orderId\":\"GPA.1\"}"),
            };

            PurchaseVerdict v = await Play(http).VerifyAsync("gems_80", "consumable", PlayReceipt("gems_80", "tok"));

            Assert.AreEqual(ok, v.Ok, v.Reason);
            Assert.IsTrue(http.Calls.Exists(c => c.Url.Contains("/purchases/products/gems_80/tokens/tok")), "asked Google about this token");
        }

        [Test]
        public async Task GooglePlay_RefusesAReceiptForAnotherProduct()
        {
            PurchaseVerdict v = await Play(new FakeHttp()).VerifyAsync("gems_7000", "consumable", PlayReceipt("gems_80", "tok"));
            Assert.IsFalse(v.Ok);
        }
    }
}
