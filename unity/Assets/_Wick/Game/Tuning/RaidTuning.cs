using UnityEngine;

namespace Wick.Game
{
    /// <summary>
    /// Presentation tuning for the raid scene. Never affects the sim. Lighting (darkness levels,
    /// Wick's flame) lives in the vault theme's LightingProfile.
    /// </summary>
    [CreateAssetMenu(menuName = "Wick/Raid Tuning", fileName = "RaidTuning")]
    public sealed class RaidTuning : ScriptableObject
    {
        [Header("Camera")]
        [Tooltip("Degrees below horizontal.")]
        public float CameraPitch = 55f;

        [Tooltip("0 keeps the grid upright on screen (swipe up = north).")]
        public float CameraYaw = 0f;

        public float CameraDistance = 11f;

        [Tooltip("Tiles ahead (north) of Wick the camera centres on, so he sits low in portrait.")]
        public float CameraLookAhead = 2f;
        public float FollowSmoothTime = 0.15f;
        public float PunchZoom = 1.2f;
        public float PunchSeconds = 0.25f;

        [Header("Raid flow")]
        public float ScoutSeconds = 5f;

        [Header("Reactions")]
        [Tooltip("Fear is 0 at this many tiles from the nearest danger, 1 when on it.")]
        public float FearRangeTiles = 4f;

        public float GreedDecaySeconds = 1f;
    }
}
