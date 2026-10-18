using UnityEngine;
using UnityEngine.SceneManagement;
using Wick.Services;

namespace Wick.Game
{
    /// <summary>
    /// The Boot scene: starts listening for vault links (and, on first launch, the install
    /// referrer or clipboard), starts signing in, then opens Home. The raid flow picks up any link.
    /// Consent, Remote Config and the rest of the boot sequence join here in later milestones.
    /// </summary>
    public sealed class Boot : MonoBehaviour
    {
        public const string HomeScene = "Raid";

        private void Start()
        {
            DontDestroyOnLoad(gameObject); // keeps the referrer check running across the scene change
            LinkRouter.Init(this);
            _ = UgsBootstrap.EnsureSignedInAsync();
            SceneManager.LoadScene(HomeScene);
        }
    }
}
