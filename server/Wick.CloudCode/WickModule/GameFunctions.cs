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
            return new PublishService(Store(context), _clock).PublishAsync(context.PlayerId, requestId, vault);
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

            return new RaidService(Store(context), _clock, _random, integrity, push, context.EnvironmentName == ProductionEnvironment);
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
