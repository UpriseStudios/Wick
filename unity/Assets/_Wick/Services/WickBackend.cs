using System.Collections.Generic;
using System.Threading.Tasks;
using Wick.Sim;

namespace Wick.Services
{
    // Replies from WickModule (server/Wick.CloudCode/WickModule/Logic/Results.cs).

    public sealed class PublishReply
    {
        public bool Ok;
        public string Reason;
        public List<string> Issues;
        public string VaultId;
        public int Version;
        public int Stars;
    }

    public sealed class RaidStartReply
    {
        public bool Ok;
        public string Reason;
        public string Token;
        public int Seed;
        public string VaultId;
        public int VaultVersion;
        public string Vault;
        public long ExpiresAtMs;
    }

    /// <summary>
    /// The server's verdict. Verified means its re-simulation reproduced the raid under the rules;
    /// Paid is what it granted from that re-sim, whatever the client claimed.
    /// </summary>
    public sealed class SubmitReply
    {
        public bool Verified;
        public string Reason;
        public string Outcome;
        public int Stars;
        public int Loot;
        public string Hash;
        public long Paid;
        public long Gold;
    }

    /// <summary>The game's backend calls. Each player action gets one request id, reused across retries.</summary>
    public sealed class WickBackend
    {
        private readonly CloudCodeClient _cloudCode = new CloudCodeClient();

        /// <summary>The signed-in player's own vault id (one live vault per player).</summary>
        public static string MyVaultId => UgsBootstrap.PlayerId == null ? null : "v_" + UgsBootstrap.PlayerId;

        public Task SignInAsync()
        {
            return UgsBootstrap.EnsureSignedInAsync();
        }

        /// <param name="document">VaultJson.WriteDocument of the vault and its clear-check.</param>
        public Task<PublishReply> PublishVaultAsync(string document)
        {
            return _cloudCode.CallAsync<PublishReply>("PublishVault", new Dictionary<string, object>
            {
                { "requestId", Backoff.NewRequestId() },
                { "vault", document },
            });
        }

        public Task<RaidStartReply> StartFriendRaidAsync(string vaultId)
        {
            return _cloudCode.CallAsync<RaidStartReply>("StartFriendRaid", new Dictionary<string, object>
            {
                { "vaultId", vaultId },
            });
        }

        public Task<SubmitReply> SubmitRaidAsync(string token, InputLog log, RaidResult result)
        {
            return _cloudCode.CallAsync<SubmitReply>("SubmitRaid", new Dictionary<string, object>
            {
                { "requestId", Backoff.NewRequestId() },
                { "token", token },
                { "log", log.ToBase64() },
                { "claimed", new Dictionary<string, object> { { "stars", result.Stars }, { "loot", result.Loot }, { "hash", result.HashHex } } },
            });
        }
    }
}
