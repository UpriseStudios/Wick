using System;
using System.Threading.Tasks;
using Wick.Economy;

namespace Wick.CloudCode
{
    public static class AdPlacements
    {
        public const string DoubleLoot = "raid_double_loot";
        public const string SpeedUp = "upgrade_speedup";
        public const string DailyVaultRetry = "daily_vault_retry";
        public const string ShopChest = "shop_chest";
    }

    /// <summary>What the client learns about one ad view it started (polled after the ad closes).</summary>
    public sealed class AdRewardStatus
    {
        /// <summary>False while LevelPlay's callback hasn't arrived yet.</summary>
        public bool Arrived { get; set; }

        public bool Granted { get; set; }
        public string Reason { get; set; }
        public Grant Grant { get; set; }
    }

    /// <summary>
    /// Rewarded ads, granted only from LevelPlay's server-to-server callback (relayed by the link
    /// site after checking its signature, as a service account), never because the client says
    /// it watched one. Per-placement and daily caps, counted by UTC day; one grant per LevelPlay
    /// event id. The client then asks ClaimAdReward(nonce) what it got.
    /// </summary>
    public sealed class AdService
    {
        private readonly IGameStore _store;
        private readonly IClock _clock;
        private readonly IEconomySource _economy;

        public AdService(IGameStore store, IClock clock, IEconomySource economy)
        {
            _store = store;
            _clock = clock;
            _economy = economy;
        }

        /// <param name="context">raid_double_loot: the raid id; upgrade_speedup: the upgrade kind.</param>
        public async Task<AdGrant> GrantFromCallbackAsync(string playerId, string placementId, string context, string nonce, string eventId)
        {
            EconomyConfig cfg = await _economy.GetAsync();
            RaidRecord raid = null;
            if (placementId == AdPlacements.DoubleLoot && !string.IsNullOrEmpty(context) && context.StartsWith("r_"))
            {
                var found = await _store.GetPrivateCustomAsync(CustomKeys.RaidPrefix + context.Substring(2), new[] { CustomKeys.Record });
                raid = found.TryGetValue(CustomKeys.Record, out StoredItem item) ? Json.Read<RaidRecord>(item.Json) : null;
            }

            return await Locked.UpdateAsync(_store, playerId, new[] { EconomyKeys.Ads, Keys.Wallet, EconomyKeys.Upgrades, EconomyKeys.Inventory }, s =>
            {
                long now = _clock.NowMs;
                AdState ads = Json.Read<AdState>(s.Json(EconomyKeys.Ads));
                AdGrant earlier = ads.Grants.Find(g => g.EventId == eventId);
                if (earlier != null)
                {
                    return new Plan<AdGrant>(earlier); // the network retried its callback
                }

                if (ads.Day != Days.Of(now))
                {
                    ads.Day = Days.Of(now);
                    ads.Counts.Clear();
                    ads.Total = 0;
                }

                var record = new AdGrant { Nonce = nonce, EventId = eventId, Placement = placementId, AtMs = now };
                var plan = new Plan<AdGrant>(record);
                string refusal = Grant(cfg, placementId, context, raid, playerId, now, ads, s, plan, record);
                record.Granted = refusal == null;
                record.Reason = refusal ?? "Granted";
                if (refusal == null)
                {
                    ads.Counts.TryGetValue(placementId, out int n);
                    ads.Counts[placementId] = n + 1;
                    ads.Total++;
                }

                ads.Grants.Add(record);
                while (ads.Grants.Count > AdState.Keep)
                {
                    ads.Grants.RemoveAt(0);
                }

                return plan.Write(EconomyKeys.Ads, Json.Write(ads));
            });
        }

        /// <returns>Null when granted; otherwise why not.</returns>
        private static string Grant(EconomyConfig cfg, string placementId, string context, RaidRecord raid, string playerId, long now,
            AdState ads, Snapshot s, Plan<AdGrant> plan, AdGrant record)
        {
            AdPlacement placement = cfg.Placement(placementId);
            if (placement == null)
            {
                return "Unknown placement";
            }

            ads.Counts.TryGetValue(placementId, out int used);
            if (used >= placement.Daily)
            {
                return "Daily limit for this reward reached";
            }

            if (ads.Total >= cfg.Ads.DailyTotal)
            {
                return "Daily ad limit reached";
            }

            Wallet wallet = Json.Read<Wallet>(s.Json(Keys.Wallet));
            switch (placementId)
            {
                case AdPlacements.DoubleLoot:
                {
                    if (raid == null || raid.RaiderId != playerId || raid.Verified == null || !raid.Verified.Verified)
                    {
                        return "That raid can't be doubled";
                    }

                    if (now - raid.AtMs > placement.WindowMinutes * Rules.MinuteMs)
                    {
                        return "Too long after the raid";
                    }

                    if (ads.DoubledRaids.Contains(raid.RaidId) || raid.Verified.Paid <= 0)
                    {
                        return ads.DoubledRaids.Contains(raid.RaidId) ? "Already doubled" : "Nothing to double";
                    }

                    ads.DoubledRaids.Add(raid.RaidId);
                    while (ads.DoubledRaids.Count > AdState.Keep)
                    {
                        ads.DoubledRaids.RemoveAt(0);
                    }

                    record.Grant = new Grant { Gold = raid.Verified.Paid };
                    wallet.Gold += raid.Verified.Paid;
                    plan.Write(Keys.Wallet, Json.Write(wallet));
                    return null;
                }

                case AdPlacements.SpeedUp:
                {
                    Upgrades upgrades = Json.Read<Upgrades>(s.Json(EconomyKeys.Upgrades));
                    RunningUpgrade running = upgrades.Find(context) ?? (upgrades.Running.Count > 0 ? upgrades.Running[0] : null);
                    if (running == null || running.EndsMs <= now)
                    {
                        return "Nothing to speed up";
                    }

                    running.EndsMs = Math.Max(now, running.EndsMs - placement.Minutes * Rules.MinuteMs);
                    plan.Write(EconomyKeys.Upgrades, Json.Write(upgrades));
                    return null;
                }

                case AdPlacements.DailyVaultRetry:
                {
                    Inventory inventory = Json.Read<Inventory>(s.Json(EconomyKeys.Inventory));
                    inventory.DailyVaultRetries++;
                    plan.Write(EconomyKeys.Inventory, Json.Write(inventory));
                    return null;
                }

                case AdPlacements.ShopChest:
                {
                    Inventory inventory = Json.Read<Inventory>(s.Json(EconomyKeys.Inventory));
                    Grants.Apply(placement.Grant, cfg, wallet, inventory, null);
                    record.Grant = placement.Grant;
                    plan.Write(Keys.Wallet, Json.Write(wallet)).Write(EconomyKeys.Inventory, Json.Write(inventory));
                    return null;
                }

                default:
                    return "Unknown placement";
            }
        }

        public async Task<AdRewardStatus> StatusAsync(string playerId, string nonce)
        {
            var found = await _store.GetProtectedAsync(playerId, new[] { EconomyKeys.Ads });
            AdState ads = Json.Read<AdState>(found.TryGetValue(EconomyKeys.Ads, out StoredItem item) ? item.Json : null);
            AdGrant g = ads.Grants.FindLast(x => x.Nonce == nonce);
            return g == null
                ? new AdRewardStatus { Arrived = false, Reason = "Waiting for the ad network" }
                : new AdRewardStatus { Arrived = true, Granted = g.Granted, Reason = g.Reason, Grant = g.Grant };
        }
    }
}
