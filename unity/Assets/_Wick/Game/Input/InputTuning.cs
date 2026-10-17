using UnityEngine;

namespace Wick.Game
{
    /// <summary>Touch thresholds for RaidInput. Moves to Remote Config later.</summary>
    [CreateAssetMenu(menuName = "Wick/Input Tuning", fileName = "InputTuning")]
    public sealed class InputTuning : ScriptableObject
    {
        [Tooltip("A drag this long (dp) becomes a Dash.")]
        public float SwipeDp = 40f;

        [Tooltip("After a swipe, holding still this long starts a run.")]
        public float HoldSeconds = 0.15f;

        [Tooltip("Movement under this (dp) still counts as holding still.")]
        public float HoldStillDp = 10f;

        [Tooltip("A touch shorter than this...")]
        public float TapMaxSeconds = 0.25f;

        [Tooltip("...that moved less than this (dp) is a tap: toggle stealth.")]
        public float TapMaxDp = 10f;

        [Tooltip("Used when the device does not report its screen density.")]
        public float FallbackDpi = 320f;

        [Tooltip("Dev fallback scheme: a stealth button beside the decoy button; taps do nothing.")]
        public bool UseStealthButton;
    }
}
