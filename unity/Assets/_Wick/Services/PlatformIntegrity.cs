using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Wick.Services
{
    /// <summary>
    /// Integrity tokens for raid requests (Play Integrity on Android; App Attest on iOS later). The
    /// server verifies them before issuing a raid token. Off until a provider is installed: the
    /// Play Integrity plugin sets Provider when it's added, requesting a token with the request hash
    /// "wick:" + player id. The server then records "no integrity token sent".
    /// </summary>
    public static class PlatformIntegrity
    {
        /// <summary>Takes the request hash, returns a token (or null).</summary>
        public static Func<string, Task<string>> Provider;

        public static string Platform =>
            Application.platform == RuntimePlatform.Android ? "android" :
            Application.platform == RuntimePlatform.IPhonePlayer ? "ios" : "editor";

        public static async Task<string> TokenAsync()
        {
            if (Provider == null || UgsBootstrap.PlayerId == null)
            {
                return null;
            }

            try
            {
                return await Provider("wick:" + UgsBootstrap.PlayerId);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Integrity token unavailable: " + e.Message);
                return null; // the server records this; it never blocks a raid
            }
        }
    }
}
