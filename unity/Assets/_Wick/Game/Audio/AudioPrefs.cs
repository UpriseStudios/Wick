using UnityEngine;

namespace Wick.Game
{
    /// <summary>Volume and haptics settings, kept on the device.</summary>
    public static class AudioPrefs
    {
        public const string Music = "Music";
        public const string Sfx = "Sfx";
        public const string Ui = "Ui";

        public static float Get(string channel)
        {
            return PlayerPrefs.GetFloat("wick.volume." + channel, channel == Music ? 0.7f : 1f);
        }

        public static void Set(string channel, float level)
        {
            PlayerPrefs.SetFloat("wick.volume." + channel, Mathf.Clamp01(level));
            PlayerPrefs.Save();
        }

        public static bool Haptics
        {
            get => PlayerPrefs.GetInt("wick.haptics", 1) == 1;
            set
            {
                PlayerPrefs.SetInt("wick.haptics", value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>A 0-1 slider level as mixer decibels (0 is silent at -80 dB).</summary>
        public static float ToDecibels(float level)
        {
            return level <= 0.0001f ? -80f : Mathf.Max(-80f, 20f * Mathf.Log10(level));
        }
    }
}
