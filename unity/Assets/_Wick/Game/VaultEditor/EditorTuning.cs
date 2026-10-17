using UnityEngine;

namespace Wick.Game
{
    /// <summary>Feel of the vault editor: gesture thresholds and camera.</summary>
    [CreateAssetMenu(menuName = "Wick/Editor Tuning", fileName = "EditorTuning")]
    public sealed class EditorTuning : ScriptableObject
    {
        [Header("Gestures")]
        [Tooltip("A one-finger move this far (dp) becomes a drag instead of a tap.")]
        public float DragDp = 12f;

        [Tooltip("Holding still this long is a long-press (delete).")]
        public float LongPressSeconds = 0.5f;

        public float TapMaxSeconds = 0.35f;
        public float FallbackDpi = 320f;

        [Header("Camera")]
        public float Pitch = 65f;

        [Tooltip("Smallest starting distance; Frame backs off further if needed to fit the grid's width.")]
        public float Distance = 17f;

        public float MinDistance = 7f;
        public float MaxDistance = 40f;

        [Tooltip("Frame looks this many tiles south of the grid centre, lifting the vault above the bottom tool bars.")]
        public float FrameShiftTiles = 2.5f;

        [Header("Look")]
        [Tooltip("Flat light added so the whole vault reads clearly while building.")]
        [Range(0f, 1f)] public float LitBoost = 0.55f;
    }
}
