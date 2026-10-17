using UnityEngine;

namespace Wick.Game
{
    /// <summary>Presentation tuning for the raid scene. Never affects the sim.</summary>
    [CreateAssetMenu(menuName = "Wick/Raid Tuning", fileName = "RaidTuning")]
    public sealed class RaidTuning : ScriptableObject
    {
        [Header("Visibility")]
        [Tooltip("Brightness of tiles outside Wick's light (0 = pitch black).")]
        [Range(0f, 1f)] public float UnlitBrightness = 0.08f;

        [Header("Camera")]
        [Tooltip("Degrees below horizontal.")]
        public float CameraPitch = 55f;

        [Tooltip("0 keeps the grid upright on screen (swipe up = north).")]
        public float CameraYaw = 0f;

        public float CameraDistance = 11f;
        public float FollowSmoothTime = 0.15f;
        public float PunchZoom = 1.2f;
        public float PunchSeconds = 0.25f;

        [Header("Raid flow")]
        public float ScoutSeconds = 5f;

        [Header("Flame light")]
        public Color FlameColor = new Color(1f, 0.62f, 0.3f);
        public float FlameIntensity = 3f;

        [Tooltip("World units of light range per tile of sim light radius.")]
        public float FlameRangePerTile = 1.25f;

        public float FlameSmoothTime = 0.12f;

        [Header("Reactions")]
        [Tooltip("Fear is 0 at this many tiles from the nearest danger, 1 when on it.")]
        public float FearRangeTiles = 4f;

        public float GreedDecaySeconds = 1f;
    }
}
