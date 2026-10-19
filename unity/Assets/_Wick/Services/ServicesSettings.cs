using System;
using UnityEngine;

namespace Wick.Services
{
    /// <summary>
    /// Account-specific switches, in Resources/Wick/services.json. Empty values keep a feature off:
    /// development builds then use DevAdView and the fake store so every flow can still be tested.
    /// </summary>
    [Serializable]
    public sealed class ServicesSettings
    {
        /// <summary>LevelPlay app key and rewarded ad unit (LevelPlay dashboard, Android app).</summary>
        public string levelPlayAppKeyAndroid = "";

        public string rewardedAdUnitAndroid = "";

        /// <summary>
        /// True once AdMob's Privacy &amp; messaging has a consent message for the app and the
        /// manifest carries the AdMob app id (com.google.android.gms.ads.APPLICATION_ID).
        /// </summary>
        public bool umpEnabled;

        /// <summary>Optional: a test device's hashed id, to see the EEA consent form while testing.</summary>
        public string umpTestDeviceHash = "";

        private static ServicesSettings _loaded;

        public static ServicesSettings Current
        {
            get
            {
                if (_loaded == null)
                {
                    var text = Resources.Load<TextAsset>("Wick/services");
                    _loaded = text != null ? JsonUtility.FromJson<ServicesSettings>(text.text) : new ServicesSettings();
                }

                return _loaded;
            }
        }

        public bool AdsConfigured => !string.IsNullOrEmpty(levelPlayAppKeyAndroid) && !string.IsNullOrEmpty(rewardedAdUnitAndroid);
    }
}
