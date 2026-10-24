using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Scripting;
using Wick.Economy;

namespace Wick.Services
{
    // Replies from WickModule's economy functions. They must list every field the server sends
    // (ReplyContractTests); the shared Wick.Economy types (Grant, steps) are the same classes.

    [Preserve]
    public sealed class EconomyStateReply
    {
        public long ServerTimeMs;
        public int ConfigVersion;
        public long Gold;
        public long Wax;
        public long Gems;
        public int Crowns;
        public int Stars;
        public int VaultLevel;
        public int Lantern;
        public int Workshops;
        public List<RunningUpgradeReply> Running;
        public VaultLevelStep NextVaultLevel;
        public LanternStep NextLantern;
        public long ClubUntilMs;
        public List<string> Cosmetics;
        public int DailyVaultRetries;
        public string PassSeason;
        public long PassXp;
        public int PassTier;
        public bool PassPremium;
        public List<int> PassClaimedFree;
        public List<int> PassClaimedPremium;
        public List<AchievementProgressReply> Achievements;
        public Dictionary<string, int> AdsToday;
        public int AdsTodayTotal;
    }

    [Preserve]
    public sealed class RunningUpgradeReply
    {
        public string Kind;
        public int ToLevel;
        public long StartedMs;
        public long EndsMs;
    }

    [Preserve]
    public sealed class AchievementProgressReply
    {
        public string Id;
        public long Value;
        public int Claimed;
    }

    [Preserve]
    public sealed class EconomyReply
    {
        public bool Ok;
        public string Reason;
        public Grant Granted;
    }

    [Preserve]
    public sealed class AdRewardReply
    {
        public bool Arrived;
        public bool Granted;
        public string Reason;
        public Grant Grant;
    }

    [Preserve]
    public sealed class AdGrantReply
    {
        public string Nonce;
        public string EventId;
        public string Placement;
        public bool Granted;
        public string Reason;
        public Grant Grant;
        public long AtMs;
    }

    [Preserve]
    public sealed class PurchaseReply
    {
        public bool Ok;
        public string Reason;
        public string ProductId;
        public bool NewlyGranted;
        public Grant Grant;
    }

    /// <summary>The economy's backend calls, and the live config (server's copy, else the bundled default).</summary>
    public sealed class EconomyClient
    {
        private static EconomyConfig _config;
        private readonly CloudCodeClient _cloudCode = new CloudCodeClient();

        /// <summary>The bundled default, for display before (or without) the server's.</summary>
        public static EconomyConfig Bundled => EconomyConfig.Parse(Resources.Load<TextAsset>("Economy/economy-default").text);

        public static EconomyConfig Config => _config ?? (_config = Bundled);

        public async Task<EconomyConfig> RefreshConfigAsync()
        {
            try
            {
                string json = await _cloudCode.CallAsync<string>("GetEconomyConfig", new Dictionary<string, object>());
                _config = EconomyConfig.Parse(json);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Economy config: using the bundled default (" + e.Message + ")");
            }

            return Config;
        }

        /// <summary>The player's Vault Level from the last economy state (1 until one arrives).</summary>
        public static int KnownVaultLevel { get; private set; } = 1;

        public async Task<EconomyStateReply> StateAsync()
        {
            EconomyStateReply state = await Call<EconomyStateReply>("GetEconomyState");
            if (state != null && state.VaultLevel > 0)
            {
                KnownVaultLevel = state.VaultLevel;
            }

            return state;
        }

        public Task<EconomyReply> StartUpgradeAsync(string kind)
        {
            return Call<EconomyReply>("StartUpgrade", ("kind", kind));
        }

        public Task<EconomyReply> FinishUpgradeAsync(string kind, bool useGems)
        {
            return Call<EconomyReply>("FinishUpgrade", ("kind", kind), ("useGems", useGems));
        }

        public Task<EconomyReply> ClaimPassTierAsync(int tier, bool premium)
        {
            return Call<EconomyReply>("ClaimPassTier", ("tier", tier), ("premium", premium));
        }

        public Task<EconomyReply> ClaimAchievementAsync(string id)
        {
            return Call<EconomyReply>("ClaimAchievement", ("id", id));
        }

        public Task<EconomyReply> ClaimClubGemsAsync()
        {
            return Call<EconomyReply>("ClaimClubGems");
        }

        public Task<AdRewardReply> ClaimAdRewardAsync(string nonce)
        {
            return Call<AdRewardReply>("ClaimAdReward", ("nonce", nonce));
        }

        /// <summary>Development: what LevelPlay's callback would do, for testing without real ads.</summary>
        public Task<AdGrantReply> DevAdViewAsync(string placement, string context, string nonce)
        {
            return Call<AdGrantReply>("DevAdView", ("placement", placement), ("adContext", context ?? ""), ("nonce", nonce));
        }

        public Task<PurchaseReply> ValidatePurchaseAsync(string requestId, string productId, string receipt)
        {
            return Call<PurchaseReply>("ValidatePurchase", ("requestId", requestId), ("productId", productId), ("receipt", receipt));
        }

        private Task<T> Call<T>(string function, params (string Key, object Value)[] args)
        {
            var dict = new Dictionary<string, object>();
            foreach ((string k, object v) in args)
            {
                dict[k] = v;
            }

            return _cloudCode.CallAsync<T>(function, dict);
        }
    }
}
