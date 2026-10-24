using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Apis.Extensions;
using Unity.Services.CloudCode.Core;

namespace Wick.CloudCode
{
    public class ModuleConfig : ICloudCodeSetup
    {
        public void Setup(ICloudCodeConfig config)
        {
            config.AddGameApiClient();
            config.Dependencies.AddSingleton<IClock, SystemClock>();
            config.Dependencies.AddSingleton<IRandomSource, CryptoRandom>();
            config.Dependencies.AddSingleton<IHttp, HttpClientHttp>();
        }
    }

    /// <summary>
    /// The client-callable functions. Each one is a thin shell: the caller is always the signed-in
    /// player (context.PlayerId), never an id from the request.
    /// </summary>
    public class GameFunctions
    {
        public const string AndroidPackage = "com.uprisestudios.wick";

        /// <summary>Secret Manager keys: Google service account key files (JSON). Unset = feature off.</summary>
        public const string FcmSecret = "FCM_SERVICE_ACCOUNT";

        public const string PlayIntegritySecret = "PLAY_INTEGRITY_SERVICE_ACCOUNT";

        /// <summary>Google Play Developer API service account (receipt verification). Unset = only the dev fake store.</summary>
        public const string PlayPublisherSecret = "PLAY_PUBLISHER_SERVICE_ACCOUNT";

        private const string DevelopmentEnvironment = "development";
        private const string ProductionEnvironment = "production";
        private static readonly Dictionary<string, (string Value, long FetchedAtMs)> Secrets = new Dictionary<string, (string, long)>();

        private readonly IGameApiClient _api;
        private readonly IClock _clock;
        private readonly IRandomSource _random;
        private readonly IHttp _http;

        public GameFunctions(IGameApiClient api, IClock clock, IRandomSource random, IHttp http)
        {
            _api = api;
            _clock = clock;
            _random = random;
            _http = http;
        }

        [CloudCodeFunction("PublishVault")]
        public Task<PublishResult> PublishVault(IExecutionContext context, string requestId, string vault)
        {
            return new PublishService(Store(context), _clock, Economy(context)).PublishAsync(context.PlayerId, requestId, vault);
        }

        // ---- Economy (M8) -------------------------------------------------------------------

        /// <summary>The live economy config (Remote Config, or the bundled default), as JSON.</summary>
        [CloudCodeFunction("GetEconomyConfig")]
        public async Task<string> GetEconomyConfig(IExecutionContext context)
        {
            return Json.Write(await Economy(context).GetAsync());
        }

        [CloudCodeFunction("GetEconomyState")]
        public Task<EconomyState> GetEconomyState(IExecutionContext context)
        {
            return EconomyFor(context).GetStateAsync(context.PlayerId);
        }

        /// <summary>kind: "vaultLevel" or "lantern".</summary>
        [CloudCodeFunction("StartUpgrade")]
        public Task<EconomyResult> StartUpgrade(IExecutionContext context, string kind)
        {
            return EconomyFor(context).StartUpgradeAsync(context.PlayerId, kind);
        }

        [CloudCodeFunction("FinishUpgrade")]
        public Task<EconomyResult> FinishUpgrade(IExecutionContext context, string kind, bool useGems)
        {
            return EconomyFor(context).FinishUpgradeAsync(context.PlayerId, kind, useGems);
        }

        [CloudCodeFunction("ClaimPassTier")]
        public Task<EconomyResult> ClaimPassTier(IExecutionContext context, int tier, bool premium)
        {
            return EconomyFor(context).ClaimPassTierAsync(context.PlayerId, tier, premium);
        }

        [CloudCodeFunction("ClaimAchievement")]
        public Task<EconomyResult> ClaimAchievement(IExecutionContext context, string id)
        {
            return EconomyFor(context).ClaimAchievementAsync(context.PlayerId, id);
        }

        [CloudCodeFunction("ClaimClubGems")]
        public Task<EconomyResult> ClaimClubGems(IExecutionContext context)
        {
            return EconomyFor(context).ClaimClubGemsAsync(context.PlayerId);
        }

        /// <summary>What an ad view (by the client's nonce) earned, once LevelPlay's callback has arrived.</summary>
        [CloudCodeFunction("ClaimAdReward")]
        public Task<AdRewardStatus> ClaimAdReward(IExecutionContext context, string nonce)
        {
            return new AdService(Store(context), _clock, Economy(context)).StatusAsync(context.PlayerId, nonce);
        }

        /// <summary>
        /// LevelPlay's server-to-server callback, relayed by the link site after it checked the
        /// signature. Service accounts only: players can't grant themselves ad rewards.
        /// </summary>
        [CloudCodeFunction("AdCallback")]
        public Task<AdGrant> AdCallback(IExecutionContext context, string playerId, string placement, string adContext, string nonce,
            string eventId)
        {
            if (!string.IsNullOrEmpty(context.PlayerId) || string.IsNullOrEmpty(playerId) || string.IsNullOrEmpty(eventId))
            {
                return Task.FromResult(new AdGrant { Granted = false, Reason = "Not allowed" });
            }

            return new AdService(Store(context), _clock, Economy(context)).GrantFromCallbackAsync(playerId, placement, adContext, nonce, eventId);
        }

        /// <summary>Development only: what LevelPlay's callback would do for an ad view, to test rewards and caps.</summary>
        [CloudCodeFunction("DevAdView")]
        public Task<AdGrant> DevAdView(IExecutionContext context, string placement, string adContext, string nonce)
        {
            if (context.EnvironmentName != DevelopmentEnvironment)
            {
                return Task.FromResult(new AdGrant { Granted = false, Reason = "Not allowed" });
            }

            return new AdService(Store(context), _clock, Economy(context)).GrantFromCallbackAsync(context.PlayerId, placement, adContext, nonce,
                "dev-" + nonce);
        }

        [CloudCodeFunction("ValidatePurchase")]
        public async Task<PurchaseResult> ValidatePurchase(IExecutionContext context, string requestId, string productId, string receipt)
        {
            var verifiers = new System.Collections.Generic.List<IReceiptVerifier>();
            string publisherKey = await SecretAsync(context, PlayPublisherSecret);
            if (publisherKey != null)
            {
                verifiers.Add(new GooglePlayVerifier(new GoogleAuth(GoogleServiceAccount.Parse(publisherKey), _http, _clock), _http, AndroidPackage));
            }

            if (context.EnvironmentName == DevelopmentEnvironment)
            {
                verifiers.Add(new FakeStoreVerifier());
            }

            return await new PurchaseService(Store(context), Economy(context), new ReceiptVerifiers(verifiers.ToArray()))
                .ValidateAsync(context.PlayerId, requestId, productId, receipt);
        }

        [CloudCodeFunction("GetRaidTarget")]
        public async Task<RaidStartResult> GetRaidTarget(IExecutionContext context, string platform, string integrityToken)
        {
            return await (await Raids(context)).GetRaidTargetAsync(context.PlayerId, platform, integrityToken);
        }

        /// <summary>kind: "friend" (a shared link) or "revenge" (from the defence log).</summary>
        [CloudCodeFunction("StartFriendRaid")]
        public async Task<RaidStartResult> StartFriendRaid(IExecutionContext context, string vaultId, string kind, string platform,
            string integrityToken)
        {
            bool allowOwnVault = context.EnvironmentName == DevelopmentEnvironment;
            return await (await Raids(context)).StartFriendRaidAsync(context.PlayerId, vaultId, allowOwnVault,
                kind ?? RaidService.KindFriend, platform, integrityToken);
        }

        [CloudCodeFunction("SubmitRaid")]
        public async Task<SubmitResult> SubmitRaid(IExecutionContext context, string requestId, string token, string log, RaidClaim claimed)
        {
            return await (await Raids(context)).SubmitRaidAsync(context.PlayerId, requestId, token, log, claimed);
        }

        [CloudCodeFunction("GetDefenceLog")]
        public Task<DefenceLogResult> GetDefenceLog(IExecutionContext context)
        {
            return new DefenceService(Store(context), _clock).GetDefenceLogAsync(context.PlayerId);
        }

        [CloudCodeFunction("GetReplay")]
        public Task<ReplayResult> GetReplay(IExecutionContext context, string raidId)
        {
            return new DefenceService(Store(context), _clock).GetReplayAsync(context.PlayerId, raidId);
        }

        /// <summary>Public vault card for a share link (the web page calls it through its proxy).</summary>
        [CloudCodeFunction("GetVaultCard")]
        public Task<VaultCard> GetVaultCard(IExecutionContext context, string code)
        {
            return new DefenceService(Store(context), _clock).GetVaultCardAsync(code);
        }

        [CloudCodeFunction("RegisterPushToken")]
        public Task<bool> RegisterPushToken(IExecutionContext context, string token)
        {
            return new DefenceService(Store(context), _clock).RegisterPushTokenAsync(context.PlayerId, token);
        }

        /// <summary>Uploads a designer vault as a bot-owned live vault. Service accounts only (the seed tool).</summary>
        [CloudCodeFunction("SeedVault")]
        public Task<SeedResult> SeedVault(IExecutionContext context, string name, string vault, int crowns)
        {
            if (!string.IsNullOrEmpty(context.PlayerId))
            {
                return Task.FromResult(new SeedResult { Ok = false, Reason = "Not allowed" });
            }

            return new DefenceService(Store(context), _clock).SeedVaultAsync(name, vault, crowns);
        }

        private IGameStore Store(IExecutionContext context)
        {
            return new CloudSaveStore(_api, context);
        }

        private IEconomySource Economy(IExecutionContext context)
        {
            return new RemoteEconomy(_api, context, _clock);
        }

        private EconomyService EconomyFor(IExecutionContext context)
        {
            return new EconomyService(Store(context), _clock, Economy(context));
        }

        private async Task<RaidService> Raids(IExecutionContext context)
        {
            IIntegrityVerifier integrity = new NoIntegrity();
            string integrityKey = await SecretAsync(context, PlayIntegritySecret);
            if (integrityKey != null)
            {
                integrity = new PlayIntegrityVerifier(new GoogleAuth(GoogleServiceAccount.Parse(integrityKey), _http, _clock), _http, _clock,
                    AndroidPackage);
            }

            IPushSender push = new NoPush();
            string fcmKey = await SecretAsync(context, FcmSecret);
            if (fcmKey != null)
            {
                GoogleServiceAccount account = GoogleServiceAccount.Parse(fcmKey);
                push = new FcmPushSender(new GoogleAuth(account, _http, _clock), _http, account.ProjectId);
            }

            return new RaidService(Store(context), _clock, _random, integrity, push, context.EnvironmentName == ProductionEnvironment,
                Economy(context));
        }

        /// <summary>A Secret Manager value, cached for five minutes; null when it isn't set.</summary>
        private async Task<string> SecretAsync(IExecutionContext context, string key)
        {
            long now = _clock.NowMs;
            lock (Secrets)
            {
                if (Secrets.TryGetValue(key, out var cached) && now - cached.FetchedAtMs < 5 * Rules.MinuteMs)
                {
                    return cached.Value;
                }
            }

            string value;
            try
            {
                value = (await _api.SecretManager.GetSecret(context, key))?.Value;
            }
            catch (Exception)
            {
                value = null; // not configured
            }

            lock (Secrets)
            {
                Secrets[key] = (string.IsNullOrEmpty(value) ? null : value, now);
            }

            return string.IsNullOrEmpty(value) ? null : value;
        }
    }
}
