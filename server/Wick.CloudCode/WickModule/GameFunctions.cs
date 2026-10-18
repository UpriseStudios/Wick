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
        }
    }

    /// <summary>
    /// The client-callable functions. Each one is a thin shell: the caller is always the signed-in
    /// player (context.PlayerId), never an id from the request.
    /// </summary>
    public class GameFunctions
    {
        private const string DevelopmentEnvironment = "development";

        private readonly IGameApiClient _api;
        private readonly IClock _clock;
        private readonly IRandomSource _random;

        public GameFunctions(IGameApiClient api, IClock clock, IRandomSource random)
        {
            _api = api;
            _clock = clock;
            _random = random;
        }

        [CloudCodeFunction("PublishVault")]
        public Task<PublishResult> PublishVault(IExecutionContext context, string requestId, string vault)
        {
            return new PublishService(Store(context), _clock).PublishAsync(context.PlayerId, requestId, vault);
        }

        [CloudCodeFunction("StartFriendRaid")]
        public Task<RaidStartResult> StartFriendRaid(IExecutionContext context, string vaultId)
        {
            bool allowOwnVault = context.EnvironmentName == DevelopmentEnvironment;
            return new RaidService(Store(context), _clock, _random).StartFriendRaidAsync(context.PlayerId, vaultId, allowOwnVault);
        }

        [CloudCodeFunction("SubmitRaid")]
        public Task<SubmitResult> SubmitRaid(IExecutionContext context, string requestId, string token, string log, RaidClaim claimed)
        {
            return new RaidService(Store(context), _clock, _random).SubmitRaidAsync(context.PlayerId, requestId, token, log, claimed);
        }

        private IGameStore Store(IExecutionContext context)
        {
            return new CloudSaveStore(_api, context);
        }
    }
}
