using System;
using System.Threading.Tasks;
using Unity.Services.LevelPlay;
using UnityEngine;

namespace Wick.Services
{
    /// <summary>The result of offering one rewarded ad.</summary>
    public sealed class AdOutcome
    {
        public bool Granted;
        public string Reason;
        public Wick.Economy.Grant Grant;
    }

    /// <summary>
    /// Rewarded ads only: there is no banner or interstitial code anywhere in the game (CI checks).
    /// The reward is decided by the server after LevelPlay's server-to-server callback; the game
    /// passes "nonce~placement~context" as LevelPlay's dynamic user id, shows the ad, then asks the
    /// server what that nonce earned. Without a LevelPlay app key, development builds simulate the
    /// callback with DevAdView, so rewards and caps can be tested.
    /// </summary>
    public sealed class RewardedAds
    {
        private static LevelPlayRewardedAd _ad;
        private static bool _initStarted;
        private static bool _ready;
        private readonly EconomyClient _economy = new EconomyClient();

        public static bool Available => ServicesSettings.Current.AdsConfigured ? _ready && Consent.CanShowAds : Debug.isDebugBuild;

        /// <summary>Starts LevelPlay after consent is settled. Safe to call more than once.</summary>
        public static void Init()
        {
            ServicesSettings s = ServicesSettings.Current;
            if (_initStarted || !s.AdsConfigured || UgsBootstrap.PlayerId == null)
            {
                return;
            }

            _initStarted = true;
            Consent.Apply();
            LevelPlay.OnInitSuccess += _ =>
            {
                _ad = new LevelPlayRewardedAd(s.rewardedAdUnitAndroid);
                _ad.OnAdLoaded += _ => _ready = true;
                _ad.OnAdLoadFailed += e => Debug.LogWarning("Rewarded ad didn't load: " + e);
                _ad.LoadAd();
            };
            LevelPlay.OnInitFailed += e => Debug.LogWarning("LevelPlay init failed: " + e);
            LevelPlay.Init(s.levelPlayAppKeyAndroid, UgsBootstrap.PlayerId);
        }

        /// <param name="placement">raid_double_loot, upgrade_speedup, daily_vault_retry or shop_chest.</param>
        /// <param name="context">raid_double_loot: the raid id; upgrade_speedup: the upgrade kind.</param>
        public async Task<AdOutcome> ShowAsync(string placement, string context)
        {
            string nonce = Guid.NewGuid().ToString("N");
            if (!ServicesSettings.Current.AdsConfigured)
            {
                if (!Debug.isDebugBuild)
                {
                    return new AdOutcome { Granted = false, Reason = "Ads aren't available" };
                }

                AdGrantReply dev = await _economy.DevAdViewAsync(placement, context, nonce);
                return Outcome(placement, dev.Granted, dev.Reason, dev.Grant);
            }

            if (_ad == null || !_ad.IsAdReady())
            {
                return new AdOutcome { Granted = false, Reason = "No ad is ready, try again in a moment" };
            }

            var closed = new TaskCompletionSource<bool>();
            void OnClosed(LevelPlayAdInfo _) => closed.TrySetResult(true);
            void OnFailed(LevelPlayAdInfo _, LevelPlayAdError __) => closed.TrySetResult(false);
            _ad.OnAdClosed += OnClosed;
            _ad.OnAdDisplayFailed += OnFailed;
            try
            {
                LevelPlay.SetDynamicUserId(nonce + "~" + placement + "~" + (context ?? ""));
                _ready = false;
                _ad.ShowAd(placement);
                if (!await closed.Task)
                {
                    return new AdOutcome { Granted = false, Reason = "The ad couldn't be shown" };
                }
            }
            finally
            {
                _ad.OnAdClosed -= OnClosed;
                _ad.OnAdDisplayFailed -= OnFailed;
                _ad.LoadAd(); // ready the next one
            }

            // The reward arrives server to server; give the callback a little time.
            for (int i = 0; i < 20; i++)
            {
                AdRewardReply status = await _economy.ClaimAdRewardAsync(nonce);
                if (status.Arrived)
                {
                    return Outcome(placement, status.Granted, status.Reason, status.Grant);
                }

                await Task.Delay(500);
            }

            return new AdOutcome { Granted = false, Reason = "The reward is on its way; check back shortly" };
        }

        private static AdOutcome Outcome(string placement, bool granted, string reason, Wick.Economy.Grant grant)
        {
            if (granted)
            {
                Telemetry.AdReward(placement);
            }

            return new AdOutcome { Granted = granted, Reason = reason, Grant = grant };
        }
    }
}
