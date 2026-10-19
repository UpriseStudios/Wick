using UnityEngine;

namespace Wick.Game
{
    public enum HapticStrength : byte
    {
        Light,
        Medium,
        Heavy,
    }

    /// <summary>
    /// Light taps for steps and buttons, medium for a lost flicker, heavy for a snuff, through the
    /// platform's own haptics (no SDK). Android: VibrationEffect's predefined tick, click and heavy
    /// click (API 29+), or short one-shots on older phones. iOS joins with the iOS build
    /// (UIImpactFeedbackGenerator). Off when the player turns haptics off in Settings.
    /// </summary>
    public static class Haptics
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject _vibrator;
        private static AndroidJavaClass _effect;
        private static int _sdk;
#endif

        public static void Play(HapticStrength strength)
        {
            if (!AudioPrefs.Haptics)
            {
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (_vibrator == null)
                {
                    using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    using AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                    _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    using var version = new AndroidJavaClass("android.os.Build$VERSION");
                    _sdk = version.GetStatic<int>("SDK_INT");
                    if (_sdk >= 26)
                    {
                        _effect = new AndroidJavaClass("android.os.VibrationEffect");
                    }
                }

                if (_vibrator == null || !_vibrator.Call<bool>("hasVibrator"))
                {
                    return;
                }

                if (_sdk >= 29)
                {
                    // EFFECT_TICK = 2, EFFECT_CLICK = 0, EFFECT_HEAVY_CLICK = 5
                    int id = strength == HapticStrength.Light ? 2 : strength == HapticStrength.Medium ? 0 : 5;
                    using AndroidJavaObject effect = _effect.CallStatic<AndroidJavaObject>("createPredefined", id);
                    _vibrator.Call("vibrate", effect);
                }
                else if (_sdk >= 26)
                {
                    long ms = strength == HapticStrength.Light ? 10 : strength == HapticStrength.Medium ? 25 : 60;
                    int amplitude = strength == HapticStrength.Light ? 60 : strength == HapticStrength.Medium ? 150 : 255;
                    using AndroidJavaObject effect = _effect.CallStatic<AndroidJavaObject>("createOneShot", ms, amplitude);
                    _vibrator.Call("vibrate", effect);
                }
                else
                {
                    _vibrator.Call("vibrate", strength == HapticStrength.Light ? 10L : strength == HapticStrength.Medium ? 25L : 60L);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Haptics unavailable: " + e.Message);
            }
#endif
        }
    }
}
