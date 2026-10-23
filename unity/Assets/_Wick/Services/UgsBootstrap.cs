using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;

namespace Wick.Services
{
    /// <summary>
    /// Starts Unity Gaming Services and signs the player in anonymously (the session is cached, so
    /// it's the same player on the next launch). Every backend call awaits EnsureSignedInAsync first.
    /// </summary>
    public static class UgsBootstrap
    {
        private static Task _signIn;

        /// <summary>Development builds talk to the development environment; release builds to the one services.json names.</summary>
        public static string EnvironmentName => Debug.isDebugBuild ? "development" : ServicesSettings.Current.ReleaseEnvironment;

        public static bool IsSignedIn =>
            UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsSignedIn;

        public static string PlayerId => IsSignedIn ? AuthenticationService.Instance.PlayerId : null;

        /// <summary>Safe to call from anywhere, any number of times. A failure is retried on the next call.</summary>
        public static Task EnsureSignedInAsync()
        {
            if (_signIn == null || _signIn.IsFaulted || _signIn.IsCanceled)
            {
                _signIn = SignInAsync();
            }

            return _signIn;
        }

        private static async Task SignInAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                await UnityServices.InitializeAsync(new InitializationOptions().SetEnvironmentName(EnvironmentName));
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }

        /// <summary>
        /// Links the anonymous account to Apple, Google Play Games or Google sign-in so it survives
        /// a reinstall. A stub until the store builds add those sign-in SDKs.
        /// </summary>
        public static Task<bool> LinkAccountAsync(string provider)
        {
            Debug.Log("Account linking (" + provider + ") isn't available yet");
            return Task.FromResult(false);
        }
    }
}
