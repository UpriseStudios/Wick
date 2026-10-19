using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Wick.Economy;

namespace Wick.CloudCode
{
    /// <summary>Where economy numbers come from: Remote Config in Cloud Code, the bundled default in tests.</summary>
    public interface IEconomySource
    {
        Task<EconomyConfig> GetAsync();
    }

    public static class EconomyDefaults
    {
        private static readonly Lazy<string> Json = new Lazy<string>(() =>
        {
            using Stream s = typeof(EconomyDefaults).Assembly.GetManifestResourceStream("economy-default.json");
            using var reader = new StreamReader(s);
            return reader.ReadToEnd();
        });

        /// <summary>The bundled default (tools/economy/make_economy.py), parsed fresh so callers can't share edits.</summary>
        public static EconomyConfig Load()
        {
            return EconomyConfig.Parse(Json.Value);
        }

        public static string Text => Json.Value;
    }

    public sealed class FixedEconomy : IEconomySource
    {
        private readonly EconomyConfig _config;

        public FixedEconomy(EconomyConfig config = null)
        {
            _config = config ?? EconomyDefaults.Load();
        }

        public Task<EconomyConfig> GetAsync()
        {
            return Task.FromResult(_config);
        }
    }

    /// <summary>
    /// Remote Config's "economy" setting (deploy server/remote-config/economy.rc), cached for a
    /// minute per worker. Falls back to the bundled default if it's missing, unreadable or a
    /// version this module doesn't understand.
    /// </summary>
    public sealed class RemoteEconomy : IEconomySource
    {
        private static readonly object Gate = new object();
        private static (EconomyConfig Config, long AtMs) _cache;

        private readonly IGameApiClient _api;
        private readonly IExecutionContext _ctx;
        private readonly IClock _clock;

        public RemoteEconomy(IGameApiClient api, IExecutionContext ctx, IClock clock)
        {
            _api = api;
            _ctx = ctx;
            _clock = clock;
        }

        public async Task<EconomyConfig> GetAsync()
        {
            long now = _clock.NowMs;
            lock (Gate)
            {
                if (_cache.Config != null && now - _cache.AtMs < Rules.MinuteMs)
                {
                    return _cache.Config;
                }
            }

            EconomyConfig config;
            try
            {
                var response = await _api.RemoteConfigSettings.AssignSettingsGetAsync(_ctx, _ctx.ServiceToken, _ctx.ProjectId,
                    _ctx.EnvironmentId, "settings", new List<string> { "economy" }, null);
                object value = null;
                response.Data?.Configs?.Settings?.TryGetValue("economy", out value);
                config = value == null ? EconomyDefaults.Load() : EconomyConfig.Parse(JsonConvert.SerializeObject(value));
            }
            catch (Exception)
            {
                config = EconomyDefaults.Load();
            }

            lock (Gate)
            {
                _cache = (config, now);
            }

            return config;
        }
    }
}
