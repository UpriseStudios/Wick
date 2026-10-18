using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models;
using PlayerData = Unity.Services.CloudSave.Models.Data.Player;

namespace Wick.Services
{
    /// <summary>
    /// Reads the player's server-written data (Protected: wallet, stats, vault_live) and saves the
    /// few keys the client owns (Default class), with retry on rate limits. The client never
    /// writes wallet, inventory or vault_live; Cloud Save itself refuses player writes to them.
    /// </summary>
    public sealed class CloudSaveClient
    {
        /// <summary>Key to JSON for each key that exists.</summary>
        public async Task<Dictionary<string, string>> LoadProtectedAsync(params string[] keys)
        {
            await UgsBootstrap.EnsureSignedInAsync();
            var options = new PlayerData.LoadOptions(new PlayerData.ProtectedReadAccessClassOptions());
            Dictionary<string, Item> items =
                await WithRetry(() => CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string>(keys), options));
            var result = new Dictionary<string, string>();
            foreach (KeyValuePair<string, Item> pair in items)
            {
                result[pair.Key] = pair.Value.Value.GetAsString();
            }

            return result;
        }

        /// <summary>
        /// Saves a client-owned key under its write lock (null only when creating it) and returns
        /// the new lock. Throws CloudSaveConflictException if someone else saved it first.
        /// </summary>
        public async Task<string> SaveAsync(string key, object value, string writeLock)
        {
            await UgsBootstrap.EnsureSignedInAsync();
            var data = new Dictionary<string, SaveItem> { { key, new SaveItem(value, writeLock) } };
            Dictionary<string, string> locks =
                await WithRetry(() => CloudSaveService.Instance.Data.Player.SaveAsync(data, new PlayerData.SaveOptions()));
            return locks[key];
        }

        private static async Task<T> WithRetry<T>(System.Func<Task<T>> call)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    return await call();
                }
                catch (CloudSaveRateLimitedException e) when (attempt < Backoff.MaxAttempts)
                {
                    await Task.Delay(Backoff.DelayMs(attempt, e.RetryAfter));
                }
            }
        }
    }
}
