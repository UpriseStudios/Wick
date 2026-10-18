using System;

namespace Wick.Services
{
    /// <summary>Retry timing shared by the Cloud Code and Cloud Save clients.</summary>
    public static class Backoff
    {
        /// <summary>Tries per call, including the first. Cold starts can take ~15 s, so keep this modest.</summary>
        public const int MaxAttempts = 4;

        /// <summary>1 s, 2 s, 4 s... or the service's Retry-After when it sends one.</summary>
        public static int DelayMs(int failedAttempt, float retryAfterSeconds = 0f)
        {
            if (retryAfterSeconds > 0f)
            {
                return (int)Math.Ceiling(retryAfterSeconds * 1000f);
            }

            return 1000 << Math.Min(Math.Max(failedAttempt - 1, 0), 4);
        }

        /// <summary>A fresh id for one player action; reuse it for every retry of that action.</summary>
        public static string NewRequestId()
        {
            return Guid.NewGuid().ToString("N");
        }
    }
}
