using UnityEngine;
using UnityEngine.SceneManagement;
using Wick.Services;

namespace Wick.Game
{
    /// <summary>
    /// The Boot scene: starts listening for vault links (and, on first launch, the install
    /// referrer or clipboard), starts signing in, settles ad consent, then opens Home. The raid
    /// flow picks up any link.
    /// </summary>
    public sealed class Boot : MonoBehaviour
    {
        public const string HomeScene = "Raid";

        private async void Start()
        {
            DontDestroyOnLoad(gameObject); // keeps the referrer check running across the scene change
            Screen.sleepTimeout = SleepTimeout.NeverSleep; // the screen stays on while the game is open
            LinkRouter.Init(this);
            _ = UgsBootstrap.EnsureSignedInAsync();

            // Returning players settle consent here (the UMP form, if their region needs it); a first
            // launch asks the age question on Home first.
            if (Consent.Adult != null)
            {
                await Consent.SettleAsync();
            }

            SceneManager.LoadScene(HomeScene);
        }
    }
}
