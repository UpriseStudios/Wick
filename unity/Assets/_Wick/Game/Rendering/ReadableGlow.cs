using UnityEngine;

namespace Wick.Game
{
    /// <summary>
    /// A part that stays readable in the dark (docs/m10-2-lighting.md): pit edges, trap warnings,
    /// guardian eyes, lit braziers and their blobs, the entrance glow, treasure twinkle. Its renderers
    /// use materials that ignore the visibility mask, and its owning view keeps it out of SetVisible:
    /// it shows while Active and either its tile is lit or the profile keeps this kind readable.
    /// Glows only show what the sim already decided (a warning, an awake guardian); they never reveal
    /// a body, a sleeping guardian or an inactive trap.
    /// </summary>
    public sealed class ReadableGlow : MonoBehaviour
    {
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        public ReadableKind Kind;

        [Tooltip("Emission strength over the profile's colour for this kind (1 = as is).")]
        public float Strength = 1f;

        private Renderer[] _renderers;
        private MaterialPropertyBlock _block;
        private bool _shown = true;

        /// <summary>The lab's silhouette check hides every glow (VisibilityMask.Silhouette sets it).</summary>
        public static bool Silhouette { get; set; }

        /// <summary>Set by the owning view: whether the thing it shows is happening (warning on, eyes open).</summary>
        public bool Active { get; set; } = true;

        public Renderer[] Renderers => _renderers ??= GetComponentsInChildren<Renderer>(true);

        /// <summary>Shows or hides the glow for this frame.</summary>
        public void Apply(bool tileLit, LightingProfile profile)
        {
            bool show = Active && (tileLit || profile.IsReadable(Kind)) && !Silhouette;
            if (show == _shown)
            {
                return;
            }

            _shown = show;
            foreach (Renderer r in Renderers)
            {
                r.enabled = show;
            }
        }

        /// <summary>Tints the glow with the profile's colour for its kind, times Strength and a pulse.</summary>
        public void SetColor(Color color, float multiplier = 1f)
        {
            _block ??= new MaterialPropertyBlock();
            foreach (Renderer r in Renderers)
            {
                r.GetPropertyBlock(_block);
                _block.SetColor(EmissionId, color * (Strength * multiplier));
                r.SetPropertyBlock(_block);
            }
        }

        public static Color ColorOf(ReadableKind kind, LightingProfile p)
        {
            switch (kind)
            {
                case ReadableKind.PitEdge: return p.PitEdgeColor;
                case ReadableKind.TrapWarning: return p.WarningRed;
                case ReadableKind.Treasure: return p.TreasureGold;
                case ReadableKind.Entrance: return p.EntranceColor;
                case ReadableKind.Brazier: return p.BlobColor;
                default: return Color.white;
            }
        }
    }
}
