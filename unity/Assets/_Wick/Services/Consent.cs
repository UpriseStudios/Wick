using System;
using System.Threading.Tasks;
using Unity.Services.LevelPlay;
using UnityEngine;

namespace Wick.Services
{
    /// <summary>
    /// Ad and analytics consent, settled in Boot before any ad loads: an age question (stored), then
    /// Google's UMP form on Android where the region needs it (iOS's ATT prompt joins with the iOS
    /// build). Anyone who declines, or is under 18, gets non-personalised ads only. Until UMP is
    /// configured, ads stay non-personalised for everyone.
    /// </summary>
    public static class Consent
    {
        private const string AdultKey = "wick.consent.adult";

        /// <summary>Null until the player has answered the age question.</summary>
        public static bool? Adult
        {
            get
            {
                int v = PlayerPrefs.GetInt(AdultKey, -1);
                return v < 0 ? (bool?)null : v == 1;
            }
        }

        public static bool Personalised { get; private set; }
        public static bool CanShowAds { get; private set; } = true;
        public static bool Settled { get; private set; }

        public static void SetAdult(bool adult)
        {
            PlayerPrefs.SetInt(AdultKey, adult ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>Runs the consent form if needed and applies the result to ads and analytics.</summary>
        public static async Task SettleAsync()
        {
            bool adult = Adult == true;
            bool personalised = false;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (ServicesSettings.Current.umpEnabled)
            {
                (bool canRequest, bool consented) = await RunUmpAsync(!adult);
                CanShowAds = canRequest;
                personalised = consented;
            }
#else
            await Task.CompletedTask;
#endif
            Use(adult, personalised);
            Settled = true;
        }

        /// <summary>True when the player's region lets them change their ad choices (the Privacy screen's button).</summary>
        public static bool PrivacyOptionsAvailable
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                if (ServicesSettings.Current.umpEnabled)
                {
                    try
                    {
                        using var consent = new AndroidJavaClass("com.uprisestudios.wick.WickConsent");
                        using AndroidJavaObject activity = CurrentActivity();
                        return consent.CallStatic<bool>("privacyOptionsRequired", activity);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("Privacy options unavailable: " + e.Message);
                    }
                }
#endif
                return false;
            }
        }

        /// <summary>Shows Google's privacy options form, then applies the player's new choices.</summary>
        public static async Task ShowPrivacyOptionsAsync()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var consent = new AndroidJavaClass("com.uprisestudios.wick.WickConsent");
                using AndroidJavaObject activity = CurrentActivity();
                consent.CallStatic("showPrivacyOptions", activity);
                await WaitForForm(consent);
                Use(Adult == true, consent.CallStatic<bool>("personalised", activity));
            }
            catch (Exception e)
            {
                Debug.LogWarning("Privacy options unavailable: " + e.Message);
            }
#else
            await Task.CompletedTask;
#endif
        }

        private static void Use(bool adult, bool personalised)
        {
            Personalised = adult && personalised;
            // Analytics: adults only; with UMP, the form's answer decides; without it, development builds only (as in M7).
            Telemetry.Consented = adult && (ServicesSettings.Current.umpEnabled ? personalised : Debug.isDebugBuild);
            Apply();
        }

        /// <summary>Tells LevelPlay what it may do: personalised or not, and child-directed for under-18s.</summary>
        public static void Apply()
        {
            LevelPlay.SetConsent(Personalised);
            LevelPlay.SetMetaData("is_child_directed", Adult == true ? "false" : "true");
            LevelPlay.SetMetaData("do_not_sell", Personalised ? "false" : "true");
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static async Task<(bool, bool)> RunUmpAsync(bool underAge)
        {
            try
            {
                using var consent = new AndroidJavaClass("com.uprisestudios.wick.WickConsent");
                using AndroidJavaObject activity = CurrentActivity();
                consent.CallStatic("run", activity, underAge, ServicesSettings.Current.umpTestDeviceHash ?? "");
                await WaitForForm(consent);
                return (consent.CallStatic<bool>("canRequestAds", activity), consent.CallStatic<bool>("personalised", activity));
            }
            catch (Exception e)
            {
                Debug.LogWarning("Consent unavailable: " + e.Message);
                return (true, false); // non-personalised ads only
            }
        }

        private static async Task WaitForForm(AndroidJavaClass consent)
        {
            for (int i = 0; i < 600 && consent.CallStatic<int>("state") == 0; i++)
            {
                await Task.Delay(100); // the form waits on the player; up to a minute
            }

            if (consent.CallStatic<int>("state") != 1)
            {
                Debug.LogWarning("Consent form: " + consent.CallStatic<string>("error"));
            }
        }

        private static AndroidJavaObject CurrentActivity()
        {
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            return player.GetStatic<AndroidJavaObject>("currentActivity");
        }
#endif
    }
}
