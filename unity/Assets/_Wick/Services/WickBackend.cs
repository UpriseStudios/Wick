using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.Scripting;
using Wick.Sim;

namespace Wick.Services
{
    // Replies from WickModule (server/Wick.CloudCode/WickModule/Logic/Results.cs). They must list
    // every field the server sends: the Cloud Code client fails on unknown members
    // (ReplyContractTests checks this). [Preserve] keeps IL2CPP stripping from removing what
    // Newtonsoft builds by reflection.

    [Preserve]
    public sealed class PublishReply
    {
        public bool Ok;
        public string Reason;
        public List<string> Issues;
        public string VaultId;
        public int Version;
        public int Stars;
        public string Code;
    }

    [Preserve]
    public sealed class RaidStartReply
    {
        public bool Ok;
        public string Reason;
        public string Token;
        public string Kind;
        public string Code;
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
    [Preserve]
    public sealed class SubmitReply
    {
        public bool Verified;
        public string Reason;
        public string Outcome;
        public int Stars;
        public int Loot;
        public string Hash;
        public int Ticks;
        public long Paid;
        public long Gold;
        public int CrownsDelta;
        public int Crowns;
        public long WaxEarned;
        public long PassXp;
    }

    [Preserve]
    public sealed class DefenceEntryReply
    {
        public string RaidId;
        public string RaiderId;
        public long AtMs;
        public string Outcome;
        public int Stars;
        public long GoldLost;
        public long Bonus;
        public int CrownsDelta;
        public string RevengeVaultId;
    }

    /// <summary>How the player's vault is doing, and its recent raids (newest first).</summary>
    [Preserve]
    public sealed class DefenceLogReply
    {
        public int Raids;
        public int Snuffs;
        public string DeadliestTrap;
        public long BonusEarned;
        public long GoldLost;
        public long ShieldUntilMs;
        public List<DefenceEntryReply> Entries;
    }

    /// <summary>Everything needed to re-simulate one raid, and the hash the server verified.</summary>
    [Preserve]
    public sealed class ReplayReply
    {
        public bool Ok;
        public string Reason;
        public string RaidId;
        public string OwnerId;
        public string RaiderId;
        public string Code;
        public int SimVersion;
        public string Vault;
        public int Seed;
        public string Log;
        public string Hash;
        public string Outcome;
        public int Stars;
        public int Ticks;
        public long AtMs;
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

        /// <summary>Matchmaking: a vault near the player's Crowns, with a single-use raid token.</summary>
        public async Task<RaidStartReply> GetRaidTargetAsync()
        {
            string integrity = await PlatformIntegrity.TokenAsync();
            return await _cloudCode.CallAsync<RaidStartReply>("GetRaidTarget", new Dictionary<string, object>
            {
                { "platform", PlatformIntegrity.Platform },
                { "integrityToken", integrity },
            });
        }

        /// <param name="kind">"friend" (a vault link or id) or "revenge" (from the defence log).</param>
        public async Task<RaidStartReply> StartFriendRaidAsync(string vaultId, string kind = "friend")
        {
            string integrity = await PlatformIntegrity.TokenAsync();
            return await _cloudCode.CallAsync<RaidStartReply>("StartFriendRaid", new Dictionary<string, object>
            {
                { "vaultId", vaultId },
                { "kind", kind },
                { "platform", PlatformIntegrity.Platform },
                { "integrityToken", integrity },
            });
        }

        public Task<DefenceLogReply> GetDefenceLogAsync()
        {
            return _cloudCode.CallAsync<DefenceLogReply>("GetDefenceLog", new Dictionary<string, object>());
        }

        public Task<ReplayReply> GetReplayAsync(string raidId)
        {
            return _cloudCode.CallAsync<ReplayReply>("GetReplay", new Dictionary<string, object> { { "raidId", raidId } });
        }

        /// <summary>Sends the device's FCM token so the server can push defence news.</summary>
        public Task<bool> RegisterPushTokenAsync(string token)
        {
            return _cloudCode.CallAsync<bool>("RegisterPushToken", new Dictionary<string, object> { { "token", token } });
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
