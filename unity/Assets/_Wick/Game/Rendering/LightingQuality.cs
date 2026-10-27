using System;
using UnityEngine;

namespace Wick.Game
{
    /// <summary>The three lighting quality tiers (docs/m10-2-lighting.md, Mobile performance budget).</summary>
    public enum QualityTier
    {
        High,
        Medium,
        Low,
    }

    /// <summary>
    /// The device's lighting quality tier: picked once from a short benchmark on first launch, then
    /// changeable in Settings. Tiers change only how things look and cost, never gameplay: the lit
    /// tiles, Wick's light radius and every always-readable glow are the same on all three.
    /// </summary>
    public static class LightingQuality
    {
        private const string Key = "wick.quality";

        // Average frame times (ms) the benchmark accepts for each tier; slower than Medium's is Low.
        public const float HighMaxMs = 17.5f;
        public const float MediumMaxMs = 22f;

        private static readonly int EmissiveExtraId = Shader.PropertyToID("_WickEmissiveExtra");

        /// <summary>Raised after the tier changes.</summary>
        public static event Action<QualityTier> Changed;

        /// <summary>Whether a tier has been chosen (by the benchmark or the player).</summary>
        public static bool Chosen => PlayerPrefs.GetInt(Key, -1) >= 0;

        public static QualityTier Tier
        {
            get => (QualityTier)Mathf.Clamp(PlayerPrefs.GetInt(Key, 0), 0, 2);
            set
            {
                PlayerPrefs.SetInt(Key, (int)value);
                PlayerPrefs.Save();
                Changed?.Invoke(value);
            }
        }

        /// <summary>The tier a benchmark's average frame time (ms, rendering at High) earns.</summary>
        public static QualityTier FromFrameMs(float averageMs)
        {
            return averageMs <= HighMaxMs ? QualityTier.High : averageMs <= MediumMaxMs ? QualityTier.Medium : QualityTier.Low;
        }

        public static string Name(QualityTier tier)
        {
            return tier.ToString();
        }

        // ---- What each tier allows (the budget table) ----------------------------------------

        /// <summary>Soft per-pixel mask edge (High, Medium) or a hard per-tile one (Low).</summary>
        public static bool SoftMaskEdge(QualityTier t) => t != QualityTier.Low;

        public static bool Bloom(QualityTier t) => t != QualityTier.Low;

        /// <summary>Medium blooms at a quarter of the resolution, High at half.</summary>
        public static bool BloomQuarterRes(QualityTier t) => t == QualityTier.Medium;

        public static bool Fog(QualityTier t) => t != QualityTier.Low;

        /// <summary>Light shafts per room as a share of the profile's count.</summary>
        public static float ShaftShare(QualityTier t) => t == QualityTier.High ? 1f : t == QualityTier.Medium ? 0.5f : 0f;

        public static float DustShare(QualityTier t) => t == QualityTier.High ? 1f : t == QualityTier.Medium ? 0.5f : 0f;

        /// <summary>Extra emission on Low, standing in for the bloom it doesn't have.</summary>
        public static void ApplyEmissive(QualityTier t, LightingProfile profile)
        {
            Shader.SetGlobalFloat(EmissiveExtraId, t == QualityTier.Low && profile != null ? profile.LowEmissiveBoost : 0f);
        }
    }
}
