using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Wick.CloudCode
{
    /// <summary>
    /// Player data keys. All Protected: the player can read them, only Cloud Code writes. Cloud Save
    /// keys allow only letters, digits, _ and -.
    /// </summary>
    public static class Keys
    {
        public const string Wallet = "wallet";
        public const string Stats = "stats";
        public const string Profile = "profile";
        public const string Defence = "defence";
        public const string Push = "push";
        public const string VaultLive = "vault_live";
        public const string RaidTokens = "raid_tokens";
        public const string Idempotency = "idempotency";
    }

    /// <summary>Server-only custom items (Private access class).</summary>
    public static class CustomKeys
    {
        /// <summary>Matchmaking entry per live vault: custom id mm_&lt;vaultId&gt;.</summary>
        public const string MatchPrefix = "mm_";

        /// <summary>Indexed (compound index mmPool_1_mmCrowns_1), so both must stay small values.</summary>
        public const string Pool = "mmPool";

        public const string Crowns = "mmCrowns";
        public const string Entry = "entry";

        /// <summary>One raid record per submitted raid: custom id raid_&lt;tokenId&gt;.</summary>
        public const string RaidPrefix = "raid_";

        public const string Record = "record";
    }

    public static class Json
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
        };

        public static string Write(object value)
        {
            return JsonConvert.SerializeObject(value, Settings);
        }

        /// <summary>Null JSON (a missing key) reads as a fresh T.</summary>
        public static T Read<T>(string json) where T : new()
        {
            return json == null ? new T() : JsonConvert.DeserializeObject<T>(json, Settings) ?? new T();
        }
    }

    public sealed class Wallet
    {
        public long Gold { get; set; }
    }

    /// <summary>The player's raids as the raider.</summary>
    public sealed class RaidStats
    {
        public int Raids { get; set; }
        public int Escapes { get; set; }
        public int Stars { get; set; }
        public long Loot { get; set; }
    }

    /// <summary>Rating, matchmaking pool and the abuse signals that decide it.</summary>
    public sealed class Profile
    {
        public int Crowns { get; set; }

        /// <summary>Matchmaking pool: "main", or "review" for flagged accounts. Never a ban.</summary>
        public string Pool { get; set; } = Pools.Main;

        /// <summary>Anomaly scores of the latest raids, oldest first.</summary>
        public List<int> RecentScores { get; set; } = new List<int>();

        /// <summary>Last Play Integrity / App Attest outcome, for review.</summary>
        public string Integrity { get; set; }
    }

    public static class Pools
    {
        public const string Main = "main";
        public const string Review = "review";
        public const string Bot = "bot";
    }

    /// <summary>The player's vault as the defender: stats, shield, the defence log.</summary>
    public sealed class DefenceState
    {
        public const int LogSize = 30;
        public const int AppliedSize = 64;

        public int Raids { get; set; }
        public int Snuffs { get; set; }

        /// <summary>Snuffs per object type ("DripPipe": 3, ...), for the deadliest trap.</summary>
        public Dictionary<string, int> SnuffsByType { get; set; } = new Dictionary<string, int>();

        public string DeadliestTrap { get; set; }
        public long BonusEarned { get; set; }
        public long GoldLost { get; set; }
        public long ShieldUntilMs { get; set; }

        /// <summary>Newest first.</summary>
        public List<DefenceEntry> Log { get; set; } = new List<DefenceEntry>();

        /// <summary>Raid ids already applied, so a retried SubmitRaid can't apply one twice.</summary>
        public List<string> Applied { get; set; } = new List<string>();
    }

    public sealed class DefenceEntry
    {
        public string RaidId { get; set; }
        public string RaiderId { get; set; }
        public long AtMs { get; set; }
        public string Outcome { get; set; }
        public int Stars { get; set; }
        public long GoldLost { get; set; }
        public long Bonus { get; set; }
        public int CrownsDelta { get; set; }

        /// <summary>The raider's vault, for Revenge.</summary>
        public string RevengeVaultId { get; set; }
    }

    /// <summary>Push token and the defence news not yet sent (at most one push per 30 minutes).</summary>
    public sealed class PushState
    {
        public string Token { get; set; }
        public long LastSentMs { get; set; }
        public int PendingRaids { get; set; }
        public int PendingSnuffs { get; set; }
        public long PendingGoldLost { get; set; }
    }

    /// <summary>vault_live: the published vault document (with its clear-check) and who owns it.</summary>
    public sealed class LiveVault
    {
        public string OwnerId { get; set; }
        public string VaultId { get; set; }
        public int Version { get; set; }
        public long PublishedAtMs { get; set; }

        /// <summary>Short code for share links.</summary>
        public string Code { get; set; }

        /// <summary>Ticks the owner's clear-check run took, a yardstick for raid durations.</summary>
        public int ClearTicks { get; set; }

        /// <summary>VaultJson document including the owner's clear-check. Never sent to raiders.</summary>
        public string Document { get; set; }
    }

    /// <summary>
    /// The matchmaking directory entry for a live vault (player or seed bot): enough to pick it and
    /// issue a raid on it without reading the owner's data.
    /// </summary>
    public sealed class MatchEntry
    {
        public string VaultId { get; set; }
        public string Code { get; set; }
        public string OwnerId { get; set; }
        public int Version { get; set; }
        public bool Bot { get; set; }
        public int VaultLevel { get; set; }
        public int Crowns { get; set; }
        public string Pool { get; set; }
        public long ShieldUntilMs { get; set; }
        public int ClearTicks { get; set; }
        public int Raids { get; set; }
        public int Snuffs { get; set; }

        /// <summary>The vault to play (VaultJson, no clear-check).</summary>
        public string Vault { get; set; }

        /// <summary>Snuff rate in percent, once there are enough raids to say.</summary>
        public int Difficulty => Raids >= 5 ? Snuffs * 100 / Raids : 0;
    }

    /// <summary>A single-use permission to raid one vault version with one seed.</summary>
    public sealed class RaidToken
    {
        public string Id { get; set; }
        public string Kind { get; set; }
        public string VaultId { get; set; }
        public int VaultVersion { get; set; }
        public string OwnerId { get; set; }
        public bool DefenderBot { get; set; }
        public int DefenderCrowns { get; set; }
        public int DefenderLevel { get; set; }
        public int Difficulty { get; set; }
        public int ClearTicks { get; set; }
        public int Seed { get; set; }
        public long IssuedAtMs { get; set; }
        public long ExpiresAtMs { get; set; }
        public bool Used { get; set; }

        /// <summary>The vault as issued (no clear-check), so a republish mid-raid can't change it.</summary>
        public string Vault { get; set; }

        /// <summary>Set when the raid is verified: what the defender's side still has to apply.</summary>
        public DefenceEvent Defence { get; set; }
    }

    /// <summary>A verified raid, as it lands on the defender. Applied once per raid id.</summary>
    public sealed class DefenceEvent
    {
        public string RaidId { get; set; }
        public string RaiderId { get; set; }
        public string OwnerId { get; set; }
        public string Outcome { get; set; }
        public int Stars { get; set; }
        public bool Robbed { get; set; }
        public long Paid { get; set; }
        public int RaiderCrownsDelta { get; set; }
        public string SnuffedBy { get; set; }
        public int DefenderLevel { get; set; }
        public long AtMs { get; set; }
    }

    public sealed class RecentTarget
    {
        public string VaultId { get; set; }
        public long AtMs { get; set; }
    }

    /// <summary>
    /// raid_tokens: the player's recent tokens, when they were issued (for the hourly limit) and
    /// which vaults they raided in the last day. Old entries are pruned on every write.
    /// </summary>
    public sealed class TokenBook
    {
        public const int MaxTokens = 8;

        public List<RaidToken> Tokens { get; set; } = new List<RaidToken>();
        public List<long> IssuedAtMs { get; set; } = new List<long>();
        public List<RecentTarget> Recent { get; set; } = new List<RecentTarget>();

        public RaidToken Find(string id)
        {
            return Tokens.Find(t => t.Id == id);
        }

        public int IssuedSince(long sinceMs)
        {
            return IssuedAtMs.FindAll(t => t >= sinceMs).Count;
        }

        public bool RaidedRecently(string vaultId, long sinceMs)
        {
            return Recent.Exists(r => r.VaultId == vaultId && r.AtMs >= sinceMs);
        }

        public void Add(RaidToken token, long nowMs)
        {
            Tokens.RemoveAll(t => t.ExpiresAtMs < nowMs);
            Tokens.Add(token);
            while (Tokens.Count > MaxTokens)
            {
                Tokens.RemoveAt(0); // oldest first
            }

            IssuedAtMs.RemoveAll(t => t < nowMs - Rules.HourMs);
            IssuedAtMs.Add(nowMs);
        }

        public void AddRecent(string vaultId, long nowMs)
        {
            Recent.RemoveAll(r => r.AtMs < nowMs - Rules.RecentTargetMs || r.VaultId == vaultId);
            Recent.Add(new RecentTarget { VaultId = vaultId, AtMs = nowMs });
        }
    }
}
