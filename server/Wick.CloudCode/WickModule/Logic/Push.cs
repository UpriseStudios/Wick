using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Wick.CloudCode
{
    public interface IPushSender
    {
        Task SendAsync(string deviceToken, string title, string body);
    }

    /// <summary>Used until the FCM secret is configured: pushes are batched as usual but not sent.</summary>
    public sealed class NoPush : IPushSender
    {
        public Task SendAsync(string deviceToken, string title, string body)
        {
            return Task.CompletedTask;
        }
    }

    /// <summary>Firebase Cloud Messaging HTTP v1, authorised as the service account in Secret Manager.</summary>
    public sealed class FcmPushSender : IPushSender
    {
        public const string Scope = "https://www.googleapis.com/auth/firebase.messaging";

        private readonly GoogleAuth _auth;
        private readonly IHttp _http;
        private readonly string _projectId;

        public FcmPushSender(GoogleAuth auth, IHttp http, string projectId)
        {
            _auth = auth;
            _http = http;
            _projectId = projectId;
        }

        public async Task SendAsync(string deviceToken, string title, string body)
        {
            var message = new JObject
            {
                ["message"] = new JObject
                {
                    ["token"] = deviceToken,
                    ["notification"] = new JObject { ["title"] = title, ["body"] = body },
                },
            };
            string url = "https://fcm.googleapis.com/v1/projects/" + _projectId + "/messages:send";
            await _http.PostAsync(url, "application/json", message.ToString(Newtonsoft.Json.Formatting.None), await _auth.AccessTokenAsync(Scope));
        }
    }

    public sealed class PushMessage
    {
        public string Token { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
    }

    /// <summary>
    /// Defence pushes, batched: every raid adds to the pending news, and at most one push goes out
    /// per Rules.PushIntervalMs, summing up everything since the last one.
    /// </summary>
    public static class PushPolicy
    {
        /// <summary>Adds this raid to the pending news; returns the push to send now, or null.</summary>
        public static PushMessage Add(PushState push, DefenceEvent ev, long goldLost, long nowMs)
        {
            push.PendingRaids++;
            push.PendingSnuffs += ev.Outcome == "Snuffed" ? 1 : 0;
            push.PendingGoldLost += goldLost;

            bool newsworthy = push.PendingSnuffs > 0 || push.PendingGoldLost > 0;
            if (string.IsNullOrEmpty(push.Token) || !newsworthy || nowMs - push.LastSentMs < Rules.PushIntervalMs)
            {
                return null;
            }

            PushMessage message = Compose(push);
            message.Token = push.Token;
            push.LastSentMs = nowMs;
            push.PendingRaids = 0;
            push.PendingSnuffs = 0;
            push.PendingGoldLost = 0;
            return message;
        }

        public static PushMessage Compose(PushState p)
        {
            string snuffs = p.PendingSnuffs == 1 ? "1 raider" : p.PendingSnuffs + " raiders";
            if (p.PendingGoldLost > 0)
            {
                return new PushMessage
                {
                    Title = "You were robbed",
                    Body = "Raiders took " + p.PendingGoldLost + " gold from your vault" +
                        (p.PendingSnuffs > 0 ? ", but it snuffed " + snuffs : ""),
                };
            }

            return new PushMessage { Title = "Your vault snuffed " + snuffs, Body = "Watch the replays to see how" };
        }
    }
}
