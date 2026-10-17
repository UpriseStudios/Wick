using UnityEngine;

namespace Wick.Game
{
    /// <summary>Prefabs and materials the vault editor draws with, on top of the ViewRegistry.</summary>
    [CreateAssetMenu(menuName = "Wick/Editor Assets", fileName = "EditorAssets")]
    public sealed class EditorAssets : ScriptableObject
    {
        public ViewRegistry Registry;
        public EditorTuning Tuning;

        [Tooltip("Marks the entrance tile.")]
        public GameObject EntranceMarker;

        [Tooltip("A unit cube used for the hoard frame's four edges.")]
        public GameObject HoardEdge;

        [Tooltip("A night owl patrol point.")]
        public GameObject Waypoint;

        [Tooltip("Marks the first hoard corner while setting the hoard.")]
        public GameObject CornerMarker;

        [Tooltip("Material for owl patrol lines.")]
        public Material PatrolLine;
    }
}
