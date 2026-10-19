using System;
using System.Collections;
using UnityEngine;

namespace Wick.Services
{
    /// <summary>A vault code that arrived from outside the game.</summary>
    public sealed class IncomingLink
    {
        public string Code;

        /// <summary>"link" (tapped with the app installed), "referrer" (installed from a link) or "paste".</summary>
        public string Source;

        /// <summary>Ask the player first (a code found on the clipboard).</summary>
        public bool NeedsConfirm;
    }

    /// <summary>
    /// Collects vault codes from share links (Android App Links / iOS Universal Links, at launch
    /// and while running), and on first launch from the Play install referrer (Android) or the
    /// clipboard the web page filled (iOS). The raid flow takes them and starts a friend raid.
    /// </summary>
    public static class LinkRouter
    {
        private const string FirstLaunchKey = "wick.links.firstLaunchChecked";

        private static IncomingLink _pending;
        private static bool _ready;

        /// <summary>A link arrived while the game was already running.</summary>
        public static event Action<IncomingLink> Arrived;

        public static void Init(MonoBehaviour host)
        {
            if (_ready)
            {
                return;
            }

            _ready = true;
            Application.deepLinkActivated += url => Offer(VaultLink.FindCode(url), "link", false, true);
            Offer(VaultLink.FindCode(Application.absoluteURL), "link", false, false);

            if (PlayerPrefs.GetInt(FirstLaunchKey, 0) == 0)
            {
                PlayerPrefs.SetInt(FirstLaunchKey, 1);
                PlayerPrefs.Save();
                if (_pending == null)
                {
                    FirstLaunch(host);
                }
            }
        }

        /// <summary>The waiting link, if any; it's taken only once.</summary>
        public static IncomingLink Take()
        {
            IncomingLink link = _pending;
            _pending = null;
            return link;
        }

        /// <summary>A code the player typed, or one the game found itself.</summary>
        public static void Offer(string code, string source, bool needsConfirm, bool notify)
        {
            if (code == null)
            {
                return;
            }

            _pending = new IncomingLink { Code = code, Source = source, NeedsConfirm = needsConfirm };
            if (notify)
            {
                Arrived?.Invoke(_pending);
            }
        }

        private static void FirstLaunch(MonoBehaviour host)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            host.StartCoroutine(ReadReferrer());
#elif UNITY_IOS && !UNITY_EDITOR
            // The web page copies "Wax Bandits vault code: CODE"; iOS shows its own paste permission prompt.
            Offer(VaultLink.FindCode(GUIUtility.systemCopyBuffer), "paste", true, true);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static IEnumerator ReadReferrer()
        {
            using var referrer = new AndroidJavaClass("com.uprisestudios.wick.WickInstallReferrer");
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            referrer.CallStatic("start", activity);
            for (float waited = 0f; waited < 10f; waited += 0.25f)
            {
                string result = referrer.CallStatic<string>("result");
                if (result != null)
                {
                    Offer(VaultLink.FindCode(result), "referrer", false, true);
                    yield break;
                }

                yield return new WaitForSecondsRealtime(0.25f);
            }
        }
#endif
    }
}
