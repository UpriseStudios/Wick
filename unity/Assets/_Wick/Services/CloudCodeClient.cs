using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode;

namespace Wick.Services
{
    /// <summary>
    /// Calls WickModule functions with retry. Retries reuse the same arguments, including the
    /// request id the caller put in them, so the server applies the action at most once.
    /// </summary>
    public sealed class CloudCodeClient
    {
        public const string Module = "WickModule";

        public async Task<T> CallAsync<T>(string function, Dictionary<string, object> args)
        {
            await UgsBootstrap.EnsureSignedInAsync();
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    return await CloudCodeService.Instance.CallModuleEndpointAsync<T>(Module, function, args);
                }
                catch (CloudCodeRateLimitedException e) when (attempt < Backoff.MaxAttempts)
                {
                    await Task.Delay(Backoff.DelayMs(attempt, e.RetryAfter));
                }
                catch (CloudCodeException e) when (attempt < Backoff.MaxAttempts && IsTransient(e.Reason))
                {
                    await Task.Delay(Backoff.DelayMs(attempt));
                }
            }
        }

        /// <summary>
        /// Worth retrying: no connection, overloaded, or the function failed (it gives up on
        /// repeated write conflicts without writing anything, so a retry is safe).
        /// </summary>
        private static bool IsTransient(CloudCodeExceptionReason reason)
        {
            switch (reason)
            {
                case CloudCodeExceptionReason.NoInternetConnection:
                case CloudCodeExceptionReason.TooManyRequests:
                case CloudCodeExceptionReason.ServiceUnavailable:
                case CloudCodeExceptionReason.ScriptError:
                case CloudCodeExceptionReason.Unknown:
                    return true;
                default:
                    return false;
            }
        }
    }
}
