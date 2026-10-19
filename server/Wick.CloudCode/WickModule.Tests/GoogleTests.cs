using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Wick.Sim;

namespace Wick.CloudCode.Tests
{
    using static TestEconomy;

    /// <summary>Push batching, Play Integrity verdicts and Google service-account auth, with HTTP faked.</summary>
    public class GoogleTests
    {
        // ---- Push batching -------------------------------------------------------------------

        [Test]
        public void AtMostOnePushPerHalfHour_TheNextOneSumsUpTheGap()
        {
            var push = new PushState { Token = "device-1" };
            var robbed = new DefenceEvent { Outcome = "Escaped" };
            var snuffed = new DefenceEvent { Outcome = "Snuffed" };
            long t = 10 * Rules.HourMs;

            PushMessage first = PushPolicy.Add(push, robbed, 120, t);
            Assert.AreEqual("You were robbed", first.Title);
            StringAssert.Contains("120 gold", first.Body);

            Assert.IsNull(PushPolicy.Add(push, snuffed, 0, t + 5 * Rules.MinuteMs), "too soon: batched");
            Assert.IsNull(PushPolicy.Add(push, robbed, 30, t + 20 * Rules.MinuteMs));

            PushMessage later = PushPolicy.Add(push, snuffed, 0, t + 31 * Rules.MinuteMs);
            Assert.AreEqual("You were robbed", later.Title);
            StringAssert.Contains("30 gold", later.Body);
            StringAssert.Contains("snuffed 2 raiders", later.Body);
        }

        [Test]
        public void OnlySnuffs_ReadAsAWin()
        {
            var push = new PushState { Token = "device-1" };
            PushMessage m = PushPolicy.Add(push, new DefenceEvent { Outcome = "Snuffed" }, 0, Rules.HourMs);
            Assert.AreEqual("Your vault snuffed 1 raider", m.Title);
        }

        [Test]
        public void NoToken_NoPush_ButTheNewsWaits()
        {
            var push = new PushState();
            Assert.IsNull(PushPolicy.Add(push, new DefenceEvent { Outcome = "Snuffed" }, 0, Rules.HourMs));
            Assert.AreEqual(1, push.PendingSnuffs);
        }

        [Test]
        public async Task SubmitRaid_PushesTheDefender()
        {
            var w = new World();
            await w.Publish("def");
            await w.Defence.RegisterPushTokenAsync("def", "device-def");
            await w.RaidVault("raider", "v_def", InputLog.Empty);
            Assert.AreEqual(0, w.Push.Sent.Count, "a timed-out raid is neither a snuff nor a robbery");

            w.Clock.Advance(Rules.ShieldMs(Loot));
            await w.RaidVault("raider2", "v_def", TestVaults.Escape);
            Assert.AreEqual(1, w.Push.Sent.Count);
            Assert.AreEqual("device-def", w.Push.Sent[0].Token);
        }

        // ---- Play Integrity --------------------------------------------------------------------

        private static JObject Payload(string package = GameFunctions.AndroidPackage, string hash = "wick:p1", long ts = 1_000_000,
            string app = "PLAY_RECOGNIZED", string device = "MEETS_DEVICE_INTEGRITY")
        {
            return JObject.Parse(@"{
                'requestDetails': { 'requestPackageName': '" + package + "', 'requestHash': '" + hash + "', 'timestampMillis': '" + ts + @"' },
                'appIntegrity': { 'appRecognitionVerdict': '" + app + @"' },
                'deviceIntegrity': { 'deviceRecognitionVerdict': ['" + device + @"'] } }");
        }

        private static PlayIntegrityVerifier Verifier(FakeHttp http = null)
        {
            return new PlayIntegrityVerifier(null, http ?? new FakeHttp(), new FakeClock(), GameFunctions.AndroidPackage);
        }

        [Test]
        public void PlayIntegrity_PassesOnlyAPlayRecognisedAppOnAGenuineDevice_ForThisPlayer()
        {
            PlayIntegrityVerifier v = Verifier();
            Assert.IsTrue(v.Judge(Payload(), "p1").Passed);
            Assert.AreEqual("app UNRECOGNIZED_VERSION", v.Judge(Payload(app: "UNRECOGNIZED_VERSION"), "p1").Detail, "a sideloaded build");
            Assert.IsFalse(v.Judge(Payload(device: "MEETS_BASIC_INTEGRITY"), "p1").Passed);
            Assert.IsFalse(v.Judge(Payload(hash: "wick:p2"), "p1").Passed, "someone else's token");
            Assert.IsFalse(v.Judge(Payload(package: "com.other.app"), "p1").Passed);
            Assert.IsFalse(v.Judge(Payload(ts: 1_000_000 - PlayIntegrityVerifier.MaxAgeMs - 1), "p1").Passed, "stale");
        }

        [Test]
        public async Task AppAttest_IsReportedAsNotAvailableYet()
        {
            IntegrityVerdict v = await Verifier().VerifyAsync("p1", "ios", "anything");
            Assert.IsFalse(v.Checked);
        }

        // ---- Service-account auth ------------------------------------------------------------

        private static (GoogleServiceAccount Account, RSA Key) Account()
        {
            RSA rsa = RSA.Create(2048);
            string pem = rsa.ExportPkcs8PrivateKeyPem();
            var json = new JObject
            {
                ["client_email"] = "wick@test.iam.gserviceaccount.com",
                ["private_key"] = pem,
                ["token_uri"] = "https://oauth2.example/token",
                ["project_id"] = "wick-test",
            };
            return (GoogleServiceAccount.Parse(json.ToString()), rsa);
        }

        [Test]
        public void TheJwt_IsSignedWithTheServiceAccountKey()
        {
            (GoogleServiceAccount account, RSA key) = Account();
            string jwt = new GoogleAuth(account, new FakeHttp(), new FakeClock()).SignedJwt(FcmPushSender.Scope, 1000);
            string[] parts = jwt.Split('.');

            byte[] signature = FromBase64Url(parts[2]);
            Assert.IsTrue(key.VerifyData(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]), signature, HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1));
            JObject claims = JObject.Parse(Encoding.UTF8.GetString(FromBase64Url(parts[1])));
            Assert.AreEqual("wick@test.iam.gserviceaccount.com", (string)claims["iss"]);
            Assert.AreEqual(4600, (long)claims["exp"]);
        }

        [Test]
        public async Task FcmSend_GetsATokenOnce_ThenReusesIt()
        {
            (GoogleServiceAccount account, _) = Account();
            var http = new FakeHttp
            {
                Respond = url => url.Contains("oauth2") ? (200, "{\"access_token\":\"at-1\",\"expires_in\":3600}") : (200, "{}"),
            };
            var sender = new FcmPushSender(new GoogleAuth(account, http, new FakeClock()), http, account.ProjectId);

            await sender.SendAsync("device-1", "t", "b");
            await sender.SendAsync("device-2", "t", "b");

            Assert.AreEqual(3, http.Calls.Count, "one token exchange, two sends");
            Assert.AreEqual("https://fcm.googleapis.com/v1/projects/wick-test/messages:send", http.Calls[1].Url);
            Assert.AreEqual("at-1", http.Calls[2].Bearer);
            StringAssert.Contains("device-2", http.Calls[2].Body);
        }

        private static byte[] FromBase64Url(string s)
        {
            s = s.Replace('-', '+').Replace('_', '/');
            return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
        }
    }
}
