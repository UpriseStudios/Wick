using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Wick.CloudCode
{
    /// <summary>Outcome of a platform integrity check. Unchecked means no verdict either way.</summary>
    public sealed class IntegrityVerdict
    {
        public bool Checked { get; set; }
        public bool Passed { get; set; }
        public string Detail { get; set; }

        public static IntegrityVerdict Unchecked(string why)
        {
            return new IntegrityVerdict { Checked = false, Passed = false, Detail = why };
        }
    }

    /// <summary>
    /// Checks the device and app before a raid token is issued. A failure never blocks a raid or
    /// bans anyone: in production it routes the account to the review pool.
    /// </summary>
    public interface IIntegrityVerifier
    {
        Task<IntegrityVerdict> VerifyAsync(string playerId, string platform, string token);
    }

    /// <summary>Used until the Play Integrity secret is configured.</summary>
    public sealed class NoIntegrity : IIntegrityVerifier
    {
        public Task<IntegrityVerdict> VerifyAsync(string playerId, string platform, string token)
        {
            return Task.FromResult(IntegrityVerdict.Unchecked("integrity checks not configured"));
        }
    }

    /// <summary>
    /// Android: decodes a Play Integrity standard-request token with Google's API. The client must
    /// request it with requestHash = RequestHash(playerId), binding the token to this player.
    /// iOS (App Attest) isn't wired yet and reports Unchecked.
    /// </summary>
    public sealed class PlayIntegrityVerifier : IIntegrityVerifier
    {
        public const string Scope = "https://www.googleapis.com/auth/playintegrity";
        public const long MaxAgeMs = 10 * Rules.MinuteMs;

        private readonly GoogleAuth _auth;
        private readonly IHttp _http;
        private readonly IClock _clock;
        private readonly string _packageName;

        public PlayIntegrityVerifier(GoogleAuth auth, IHttp http, IClock clock, string packageName)
        {
            _auth = auth;
            _http = http;
            _clock = clock;
            _packageName = packageName;
        }

        public static string RequestHash(string playerId)
        {
            return "wick:" + playerId;
        }

        public async Task<IntegrityVerdict> VerifyAsync(string playerId, string platform, string token)
        {
            if (platform == "ios")
            {
                return IntegrityVerdict.Unchecked("App Attest not available yet");
            }

            if (string.IsNullOrEmpty(token))
            {
                return IntegrityVerdict.Unchecked("no integrity token sent");
            }

            string url = "https://playintegrity.googleapis.com/v1/" + _packageName + ":decodeIntegrityToken";
            string body = new JObject { ["integrity_token"] = token }.ToString(Newtonsoft.Json.Formatting.None);
            (int status, string response) = await _http.PostAsync(url, "application/json", body, await _auth.AccessTokenAsync(Scope));
            if (status != 200)
            {
                return IntegrityVerdict.Unchecked("Play Integrity call failed (" + status + ")");
            }

            return Judge(JObject.Parse(response)["tokenPayloadExternal"] as JObject, playerId);
        }

        /// <summary>Pass needs: our package, this player's request hash, fresh, a Play-recognized app on a genuine device.</summary>
        public IntegrityVerdict Judge(JObject payload, string playerId)
        {
            if (payload == null)
            {
                return Fail("no payload");
            }

            JToken request = payload["requestDetails"];
            if ((string)request?["requestPackageName"] != _packageName)
            {
                return Fail("wrong package");
            }

            if ((string)request?["requestHash"] != RequestHash(playerId))
            {
                return Fail("token is for another request");
            }

            long issued = long.TryParse((string)request?["timestampMillis"], out long t) ? t : 0;
            if (Math.Abs(_clock.NowMs - issued) > MaxAgeMs)
            {
                return Fail("token is stale");
            }

            string app = (string)payload["appIntegrity"]?["appRecognitionVerdict"];
            if (app != "PLAY_RECOGNIZED")
            {
                return Fail("app " + (app ?? "unknown"));
            }

            JArray device = payload["deviceIntegrity"]?["deviceRecognitionVerdict"] as JArray;
            bool genuine = device != null && device.ToObject<string[]>() is string[] verdicts &&
                Array.IndexOf(verdicts, "MEETS_DEVICE_INTEGRITY") >= 0;
            return genuine ? new IntegrityVerdict { Checked = true, Passed = true, Detail = "passed" } : Fail("device not genuine");
        }

        private static IntegrityVerdict Fail(string why)
        {
            return new IntegrityVerdict { Checked = true, Passed = false, Detail = why };
        }
    }
}
